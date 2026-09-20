using System;
using System.Collections;
using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// Draws the battle from table state and forwards clicks to reducers.
/// Holds no rules: it never computes damage, legality or turn order.
public class BattleHud : MonoBehaviour
{
    static readonly Vector2[] PlayerSlots =
    {
        new Vector2(360f, 50f),
        new Vector2(530f, 300f),
        new Vector2(810f, 550f),
    };

    static readonly Vector2[] EnemySlots =
    {
        new Vector2(1680f, 30f),
        new Vector2(1420f, 220f),
        new Vector2(1680f, 410f),
        new Vector2(1420f, 600f),
    };

    static readonly Vector2 PlayerCardSize = new Vector2(250f, 190f);
    static readonly Vector2 EnemyCardSize = new Vector2(220f, 155f);
    static readonly Vector2 BossCardSize = new Vector2(320f, 250f);
    static readonly Vector2 BossSlot = new Vector2(1540f, 210f);

    RectTransform _field;
    BattleLogView _log;
    ActionMenuView _menu;
    EquipmentPanelView _equipment;
    RectTransform _overlay;
    Text _overlayText;
    Text _overlaySubtext;
    GameObject _resetButton;
    Text _connectionLabel;
    Text _stageLabel;
    StatPopupView _popup;
    InventoryPopupView _inventory;
    TurnOrderListView _turnList;
    LevelUpBannerView _banner;
    EscapeMenuView _escape;
    BiomeBackdropView _backdrop;
    ItemTooltipView _itemTooltip;

    readonly Dictionary<ulong, EntityView> _views = new Dictionary<ulong, EntityView>();
    readonly Dictionary<ulong, Vector2> _lastHomes = new Dictionary<ulong, Vector2>();
    readonly List<ulong> _stale = new List<ulong>();

    /// Hits waiting to be animated, oldest first.
    readonly Queue<BattleLog> _pendingHits = new Queue<BattleLog>();
    bool _animating;
    ulong _aoeLungeActor;
    bool _magicBulletViiHold;
    bool _magicBulletViiBurstPlayed;

    const float EndScreenDelaySeconds = 0.85f;
    BattlePhase _endScreenPhase;
    float _endScreenAt;

    bool _targeting;

    /// Which attack the player picked before choosing a target. 0 is the free swing.
    uint _pendingSkillId;

    public void Init(
        RectTransform field,
        BattleLogView log,
        ActionMenuView menu,
        EquipmentPanelView equipment,
        RectTransform overlay,
        Text overlayText,
        Text overlaySubtext,
        GameObject resetButton,
        Text connectionLabel,
        Text stageLabel,
        StatPopupView popup,
        InventoryPopupView inventory,
        TurnOrderListView turnList,
        LevelUpBannerView banner,
        EscapeMenuView escape,
        BiomeBackdropView backdrop,
        ItemTooltipView itemTooltip
    )
    {
        _field = field;
        _log = log;
        _menu = menu;
        _equipment = equipment;
        _overlay = overlay;
        _overlayText = overlayText;
        _overlaySubtext = overlaySubtext;
        _resetButton = resetButton;
        _connectionLabel = connectionLabel;
        _stageLabel = stageLabel;
        _popup = popup;
        _inventory = inventory;
        _turnList = turnList;
        _banner = banner;
        _escape = escape;
        _backdrop = backdrop;
        _itemTooltip = itemTooltip;

        bool TooltipBlocked() =>
            (_popup != null && _popup.IsOpen)
            || (_escape != null && _escape.IsOpen)
            || (_inventory != null && _inventory.ContextOpen);

        _equipment?.BindTooltip(_itemTooltip, TooltipBlocked);
        _inventory?.BindTooltip(_itemTooltip, TooltipBlocked);

        _menu.OnJoin = GameManager.JoinGame;
        _menu.OnReady = HandleReadyClicked;
        _menu.OnFocus = () =>
        {
            ClearTargeting();
            GameManager.Focus();
        };
        _menu.OnUseItem = itemId =>
        {
            ClearTargeting();
            GameManager.UseItem(itemId);
        };
        _menu.OnAttackSelected = HandleAttackSelected;

        _equipment.OnBagClicked = HandleBagClicked;

        Refresh();
    }

    void OnEnable()
    {
        GameManager.StateChanged += Refresh;
        GameManager.LogAppended += HandleLogAppended;
    }

    void OnDisable()
    {
        GameManager.StateChanged -= Refresh;
        GameManager.LogAppended -= HandleLogAppended;
    }

    void Start() => Refresh();

    void Update()
    {
        if (!_animating && _pendingHits.Count > 0)
        {
            StartCoroutine(PlayHit(_pendingHits.Dequeue()));
        }

        if (
            !_overlay.gameObject.activeSelf
            && (_endScreenPhase == BattlePhase.StageTransition || _endScreenPhase == BattlePhase.Defeat)
            && Time.unscaledTime >= _endScreenAt
            && !AnimationsPending()
        )
        {
            Refresh();
        }

        HandleInspectDismiss();
        HandleTargetingCancel();
        HandleEscapeMenu();
    }

    void HandleEscapeMenu()
    {
        if (!EscapePressedThisFrame())
        {
            return;
        }

        if (_inventory != null && _inventory.HandleEscape())
        {
            return;
        }

        if (_escape == null)
        {
            return;
        }

        _escape.HandleEscape();
        if (_escape.IsOpen)
        {
            _itemTooltip?.Hide();
        }
    }

    void HandleBagClicked(Vector2 screenPoint)
    {
        if (_inventory == null)
        {
            return;
        }

        if (_inventory.IsOpen)
        {
            _inventory.Close();
            return;
        }

        _popup?.Close();
        _itemTooltip?.Hide();
        _inventory.Open(screenPoint);
    }

    void HandleTargetingCancel()
    {
        if (!_targeting || (_escape != null && _escape.IsOpen))
        {
            return;
        }

        if (!RightPressedThisFrame())
        {
            return;
        }

        if (_inventory != null && _inventory.ContainsInteractivePoint(PointerScreenPoint()))
        {
            return;
        }

        ClearTargeting();
        _menu.ShowRoot();
        Refresh();
    }

    void Refresh()
    {
        if (_field == null)
        {
            return;
        }

        if (_connectionLabel != null && GameManager.Instance != null)
        {
            _connectionLabel.text =
                $"{GameManager.Instance.DatabaseName} @ {GameManager.Instance.ServerUrl}   |   {GameManager.Instance.Status}";
        }

        var session = GameManager.Session();
        var me = GameManager.LocalEntity();
        var myTurn = GameManager.IsLocalTurn();

        if (_stageLabel != null)
        {
            if (session != null && session.StageNumber > 0 && session.Phase != BattlePhase.Waiting)
            {
                var stageText = $"Stage {session.StageNumber}";
                if (session.IsBossStage && session.Phase == BattlePhase.InBattle)
                {
                    stageText = $"{stageText} - BOSS";
                }
                _stageLabel.text =
                    session.Phase == BattlePhase.RestStop
                        ? $"{stageText}  —  Rest Stop"
                        : stageText;
            }
            else
            {
                _stageLabel.text = "";
            }
        }

        if (!myTurn)
        {
            ClearTargeting();
        }

        SyncTeam(GameManager.TeamMembers(Team.Players), PlayerSlots, PlayerCardSize, true, me);
        SyncTeam(GameManager.TeamMembers(Team.Enemies), EnemySlots, EnemyCardSize, false, me);
        PruneMissing();
        if (
            _itemTooltip != null
            && (
                (_popup != null && _popup.IsOpen)
                || (_escape != null && _escape.IsOpen)
                || (_inventory != null && _inventory.ContextOpen)
            )
        )
        {
            _itemTooltip.Hide();
        }

        _popup?.Refresh();
        _inventory?.Refresh();
        _turnList?.Render(session);

        _log.SetLines(GameManager.LogLines(60));
        _menu.Render(session, me, myTurn, _targeting);
        _equipment.Render();

        _backdrop?.Render(session);

        var transitioning =
            session != null && session.Phase == BattlePhase.StageTransition;
        var finished = session != null && session.Phase == BattlePhase.Defeat;
        var showEndScreen = EndScreenReady(session, transitioning, finished);
        _overlay.gameObject.SetActive(showEndScreen);
        if (_resetButton != null)
        {
            _resetButton.SetActive(showEndScreen && finished);
        }

        if (finished || transitioning)
        {
            _popup?.Close();
            _inventory?.Close();
        }

        _banner?.Raise();
        _escape?.transform.SetAsLastSibling();

        if (showEndScreen)
        {
            _overlay.SetAsLastSibling();
            if (transitioning)
            {
                _overlayText.fontSize = 64;
                _overlayText.text = $"STAGE {session.StageNumber} CLEARED";
                _overlayText.color = new Color(0.95f, 0.86f, 0.45f);
                if (_overlaySubtext != null)
                {
                    var next = NextLine(session);
                    _overlaySubtext.text = string.IsNullOrEmpty(session.StageClearNote)
                        ? next
                        : $"{session.StageClearNote}\n{next}";
                }
            }
            else
            {
                _overlayText.fontSize = 96;
                _overlayText.text = "DEFEAT";
                _overlayText.color = new Color(0.92f, 0.45f, 0.45f);
                if (_overlaySubtext != null)
                {
                    _overlaySubtext.text = $"Reached Stage {session.StageNumber}";
                }
            }
        }
    }

    bool EndScreenReady(GameSession session, bool transitioning, bool finished)
    {
        if (session == null || (!transitioning && !finished))
        {
            _endScreenPhase = BattlePhase.Waiting;
            return false;
        }

        if (_endScreenPhase != session.Phase)
        {
            _endScreenPhase = session.Phase;
            _endScreenAt = Time.unscaledTime + EndScreenDelaySeconds;
        }

        return !AnimationsPending() && Time.unscaledTime >= _endScreenAt;
    }

    static string NextLine(GameSession session)
    {
        var nextName = GameManager.BiomeTheName(session.NextBiome);
        var biomeChanges = session.NextBiome != session.CurrentBiome;
        if (session.UpcomingRestStop && biomeChanges)
        {
            return $"Next: Rest Stop — then {nextName}";
        }

        if (session.UpcomingRestStop)
        {
            return "Next: Rest Stop";
        }

        if (biomeChanges)
        {
            return $"Next: {nextName}";
        }

        return $"Next: Stage {session.StageNumber + 1}";
    }

    void SyncTeam(
        List<Entity> entities,
        Vector2[] slots,
        Vector2 cardSize,
        bool showMana,
        Entity me
    )
    {
        var myTurn = GameManager.IsLocalTurn();
        var session = GameManager.Session();
        var activeId = session?.ActiveEntityId ?? 0;

        foreach (var entity in entities)
        {
            var hasView = _views.TryGetValue(entity.EntityId, out var view) && view != null;

            // Defeated combatants keep an existing card so the killing blow can
            // finish, but a later attack must never spawn a new one. AnimationsPending
            // used to force a recreate, which is what flickered dead entities back in.
            if (!entity.Alive && !hasView)
            {
                continue;
            }

            if (!hasView)
            {
                var size = entity.IsBoss ? BossCardSize : cardSize;
                view = EntityView.Create(_field, entity.Name, size, showMana);
                _views[entity.EntityId] = view;
            }

            if (view == null)
            {
                continue;
            }

            var index = (int)Mathf.Min(entity.Slot, slots.Length - 1);
            view.SetPosition(entity.IsBoss ? BossSlot : slots[index]);

            var isLocal = me != null && me.EntityId == entity.EntityId;
            var targetable =
                _targeting && myTurn && entity.Faction == Team.Enemies && entity.Alive;
            view.Bind(entity, activeId == entity.EntityId, isLocal, targetable, HandleEntityClicked);
            _lastHomes[entity.EntityId] = view.Home;

            var occupant = GameManager.FindPlayer(entity.EntityId);
            view.SetReadyBanner(
                session != null
                    && (
                        session.Phase == BattlePhase.Waiting
                        || session.Phase == BattlePhase.RestStop
                    )
                    && occupant != null
                    && occupant.Ready
            );
        }
    }

    void PruneMissing()
    {
        _stale.Clear();

        foreach (var pair in _views)
        {
            var entity = GameManager.FindEntity(pair.Key);
            if (entity == null)
            {
                _stale.Add(pair.Key);
            }
            else if (
                !entity.Alive
                && !AnimationsPending()
                && (pair.Value == null || !pair.Value.Busy)
            )
            {
                _stale.Add(pair.Key);
            }
        }

        foreach (var id in _stale)
        {
            if (_views.TryGetValue(id, out var view) && view != null)
            {
                Destroy(view.gameObject);
            }

            _views.Remove(id);
        }
    }

    bool AnimationsPending()
    {
        if (_animating || _pendingHits.Count > 0 || _magicBulletViiHold)
        {
            return true;
        }

        foreach (var pair in _views)
        {
            if (pair.Value != null && pair.Value.Busy)
            {
                return true;
            }
        }

        return false;
    }

    void HandleReadyClicked()
    {
        var player = GameManager.LocalPlayer();
        if (player == null)
        {
            return;
        }

        GameManager.SetReady(!player.Ready);
    }

    void HandleAttackSelected(uint skillDefId)
    {
        _popup?.Close();
        if (skillDefId == 0)
        {
            _pendingSkillId = 0;
            _targeting = true;
            Refresh();
            return;
        }

        var skill = GameManager.Conn?.Db.SkillDef.Id.Find(skillDefId);

        // Buffs have nobody to point at, so they resolve on the spot.
        if (skill != null && skill.TargetCount == 0)
        {
            ClearTargeting();
            GameManager.CastSkill(skillDefId, 0);
            return;
        }

        _pendingSkillId = skillDefId;
        _targeting = true;
        Refresh();
    }

    void HandleEntityClicked(ulong entityId, Vector2 screenPoint)
    {
        var entity = GameManager.FindEntity(entityId);
        if (entity == null)
        {
            return;
        }

        if (_targeting)
        {
            if (!GameManager.IsLocalTurn())
            {
                return;
            }

            var skillDefId = _pendingSkillId;
            if (!entity.Alive || entity.Faction != Team.Enemies)
            {
                return;
            }

            ClearTargeting();

            if (skillDefId == 0)
            {
                GameManager.Attack(entityId);
                return;
            }

            GameManager.CastSkill(skillDefId, entityId);
            return;
        }

        if (!entity.Alive)
        {
            return;
        }

        _inventory?.Close();
        _itemTooltip?.Hide();
        _popup?.Open(entityId, screenPoint);
    }

    void HandleInspectDismiss()
    {
        if (_escape != null && _escape.IsOpen)
        {
            return;
        }

        if (_inventory != null && _inventory.IsOpen)
        {
            return;
        }

        if (_popup == null || !_popup.IsOpen || !PointerPressedThisFrame())
        {
            return;
        }

        var screen = PointerScreenPoint();
        if (_popup.ContainsScreenPoint(screen) || PointerOverEntity(screen))
        {
            return;
        }

        _popup.Close();
    }

    bool PointerOverEntity(Vector2 screen)
    {
        foreach (var pair in _views)
        {
            if (pair.Value != null && pair.Value.Rect != null
                && RectTransformUtility.RectangleContainsScreenPoint(pair.Value.Rect, screen, null))
            {
                return true;
            }
        }

        return false;
    }

    static Vector2 PointerScreenPoint()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current == null ? (Vector2)Input.mousePosition : Mouse.current.position.ReadValue();
#else
        return Input.mousePosition;
#endif
    }

    static bool PointerPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    static bool RightPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(1);
#endif
    }

    static bool EscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    void ClearTargeting()
    {
        _targeting = false;
        _pendingSkillId = 0;
    }

    /// The module stamps every strike with its actor and target, so the log the
    /// player reads is also the animation script.
    void HandleLogAppended(BattleLog row)
    {
        // The initial subscription replays the whole log; only animate live rows.
        if (GameManager.Instance == null || !GameManager.Instance.SubscriptionReady)
        {
            return;
        }

        if (row.Kind == LogKind.Focus || row.Kind == LogKind.Heal)
        {
            if (
                ClassSpriteArt.IsPlayerBuffMessage(
                    row.Message,
                    row.Kind == LogKind.Heal,
                    row.Kind == LogKind.Focus
                )
                && row.TargetEntityId != 0
            )
            {
                _pendingHits.Enqueue(row);
            }

            return;
        }

        if (row.Kind != LogKind.Attack && row.Kind != LogKind.Aoe)
        {
            return;
        }

        if (IsMagicBulletViiLog(row))
        {
            SetAllBarsFrozen(true);
            _magicBulletViiHold = true;
            _pendingHits.Enqueue(row);
            return;
        }

        if (row.Kind == LogKind.Attack && (row.ActorEntityId == 0 || row.TargetEntityId == 0))
        {
            return;
        }

        if (row.Kind == LogKind.Aoe && row.ActorEntityId == 0)
        {
            return;
        }

        var isBurn = IsBurnLog(row);
        if (row.ActorEntityId == row.TargetEntityId && !isBurn)
        {
            return;
        }

        // "CRITICAL HIT!" reuses LogKind.Attack but is not a strike of its own.
        if (!isBurn && ClassSpriteArt.ActionNameFromLog(row.Message) == null)
        {
            return;
        }

        _pendingHits.Enqueue(row);
    }

    IEnumerator PlayHit(BattleLog row)
    {
        _animating = true;

        if (row.Kind == LogKind.Focus || row.Kind == LogKind.Heal)
        {
            if (_views.TryGetValue(row.TargetEntityId, out var buffTarget) && buffTarget != null)
            {
                var buffEntity = GameManager.FindEntity(row.TargetEntityId);
                if (buffEntity != null && buffEntity.Faction == Team.Players)
                {
                    buffTarget.PlayBuffGleam();
                }
            }

            yield return new WaitForSeconds(0.18f);
            _animating = false;
            if (_pendingHits.Count == 0)
            {
                Refresh();
            }

            yield break;
        }

        if (IsMagicBulletViiLog(row))
        {
            yield return PlayMagicBulletVii(row);
            yield break;
        }

        if (row.Kind == LogKind.Aoe)
        {
            if (_views.TryGetValue(row.ActorEntityId, out var aoeActor) && aoeActor != null)
            {
                var midpoint = PartyMidpoint(aoeActor.Home);
                aoeActor.PlayLunge(midpoint);
                _aoeLungeActor = row.ActorEntityId;
                yield return new WaitForSeconds(EntityView.LungeSeconds);
            }

            _animating = false;
            if (_pendingHits.Count == 0)
            {
                _aoeLungeActor = 0;
                Refresh();
            }

            yield break;
        }

        _views.TryGetValue(row.ActorEntityId, out var actor);
        _views.TryGetValue(row.TargetEntityId, out var target);
        if (actor == null)
        {
            _animating = false;
            if (_pendingHits.Count == 0)
            {
                _aoeLungeActor = 0;
                Refresh();
            }

            yield break;
        }

        if (!TryLastHome(row.TargetEntityId, out var targetHome))
        {
            targetHome = actor.Home;
        }

        var skipLunge =
            IsBurnLog(row)
            || (_aoeLungeActor != 0 && row.ActorEntityId == _aoeLungeActor);
        var actionName = ClassSpriteArt.ActionNameFromLog(row.Message);
        var actorEntity = GameManager.FindEntity(row.ActorEntityId);
        var className = actorEntity != null ? actorEntity.ClassName : null;
        var targetEntity = GameManager.FindEntity(row.TargetEntityId);
        var magicBulletStage = actorEntity != null ? actorEntity.MagicBulletStage : 0;
        var casterAlive = actorEntity == null || actorEntity.Alive;

        void Impact()
        {
            if (target != null)
            {
                target.PlayHit();
                if (row.Damage > 0)
                {
                    if (
                        className != null
                        && ClassSpriteArt.TryHitEffect(
                            className,
                            actionName,
                            out var effect,
                            magicBulletStage,
                            casterAlive
                        )
                    )
                    {
                        CombatVfx.Spawn(_field, target.ShapeRect, effect);
                    }
                    else if (
                        actorEntity != null
                        && actorEntity.Faction == Team.Enemies
                        && targetEntity != null
                        && targetEntity.Faction == Team.Players
                    )
                    {
                        CombatVfx.Spawn(_field, target.ShapeRect, HitEffectKind.Impact);
                    }
                }
            }

            var victim = GameManager.FindEntity(row.TargetEntityId);
            if (victim != null && !victim.Alive && target != null)
            {
                target.PlayDeath();
            }
        }

        if (skipLunge)
        {
            Impact();
            yield return new WaitForSeconds(0.16f);
        }
        else if (actor.UsesClassSprites)
        {
            _aoeLungeActor = 0;
            yield return actor.PlayStrike(
                targetHome,
                Impact,
                ClassSpriteArt.AttackClipFor(className, actionName, magicBulletStage, casterAlive),
                ClassSpriteArt.AttackFpsFor(className),
                actionName,
                returnHome: !SameVolleyContinues(row, actionName),
                target?.ShapeRect,
                magicBulletStage,
                casterAlive
            );
        }
        else
        {
            _aoeLungeActor = 0;
            actor.PlayLunge(targetHome);
            yield return new WaitForSeconds(0.14f);
            Impact();
            yield return new WaitForSeconds(EntityView.LungeSeconds - 0.14f);
        }

        var wait = 0f;
        while (target != null && target.Busy && wait < 2f)
        {
            wait += Time.deltaTime;
            yield return null;
        }

        _animating = false;

        // A defeated combatant only leaves once its last hit has played out.
        if (_pendingHits.Count == 0)
        {
            _aoeLungeActor = 0;
            _magicBulletViiBurstPlayed = false;
            Refresh();
        }
    }

    IEnumerator PlayMagicBulletVii(BattleLog row)
    {
        SetAllBarsFrozen(true);
        _magicBulletViiHold = true;
        if (!_magicBulletViiBurstPlayed)
        {
            _magicBulletViiBurstPlayed = true;
            _views.TryGetValue(row.ActorEntityId, out var mage);
            if (mage != null && mage.UsesClassSprites)
            {
                yield return mage.PlayCastHold(1f, restoreIdle: false);
            }
            else
            {
                yield return new WaitForSeconds(1f);
            }

            yield return CombatVfx.PlayFullscreen(_field, HitEffectKind.Explosion2);
            _magicBulletViiHold = false;
            SetAllBarsFrozen(false);
            Refresh();
            var caster = GameManager.FindEntity(row.ActorEntityId);
            if (mage != null && caster != null && !caster.Alive)
            {
                mage.PlayDeath();
            }
        }

        _views.TryGetValue(row.TargetEntityId, out var target);
        if (target != null)
        {
            target.PlayHit();
            var victim = GameManager.FindEntity(row.TargetEntityId);
            if (victim != null && !victim.Alive)
            {
                target.PlayDeath();
            }
        }

        yield return new WaitForSeconds(0.12f);

        var wait = 0f;
        while (target != null && target.Busy && wait < 2f)
        {
            wait += Time.deltaTime;
            yield return null;
        }

        _animating = false;
        if (_pendingHits.Count == 0)
        {
            _aoeLungeActor = 0;
            _magicBulletViiBurstPlayed = false;
            Refresh();
        }
    }

    void SetAllBarsFrozen(bool frozen)
    {
        foreach (var pair in _views)
        {
            if (pair.Value != null)
            {
                pair.Value.SetBarsFrozen(frozen);
            }
        }
    }

    bool SameVolleyContinues(BattleLog current, string actionName)
    {
        if (_pendingHits.Count == 0 || string.IsNullOrEmpty(actionName))
        {
            return false;
        }

        var next = _pendingHits.Peek();
        if (next.Kind != LogKind.Attack || next.ActorEntityId != current.ActorEntityId)
        {
            return false;
        }

        return ClassSpriteArt.ActionNameFromLog(next.Message) == actionName;
    }

    bool IsMagicBulletViiLog(BattleLog row)
    {
        var caster = row.ActorEntityId != 0 ? GameManager.FindEntity(row.ActorEntityId) : null;
        return ClassSpriteArt.IsMagicBulletVii(
            row.Message,
            row.Damage,
            caster != null,
            caster == null || caster.Alive,
            caster != null ? caster.MagicBulletStage : 0
        );
    }

    bool TryLastHome(ulong entityId, out Vector2 home)
    {
        if (_views.TryGetValue(entityId, out var view) && view != null)
        {
            home = view.Home;
            return true;
        }

        return _lastHomes.TryGetValue(entityId, out home);
    }

    static bool IsBurnLog(BattleLog row) =>
        row.Kind == LogKind.Attack
        && row.ActorEntityId == row.TargetEntityId
        && !string.IsNullOrEmpty(row.Message)
        && row.Message.IndexOf("burn", StringComparison.OrdinalIgnoreCase) >= 0;

    Vector2 PartyMidpoint(Vector2 fallback)
    {
        var sum = Vector2.zero;
        var count = 0;
        foreach (var pair in _views)
        {
            var entity = GameManager.FindEntity(pair.Key);
            if (
                entity == null
                || !entity.Alive
                || entity.Faction != Team.Players
                || pair.Value == null
            )
            {
                continue;
            }

            sum += pair.Value.Home;
            count++;
        }

        return count == 0 ? fallback : sum / count;
    }
}
