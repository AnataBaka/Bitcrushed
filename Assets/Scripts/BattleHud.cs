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

    readonly Dictionary<ulong, EntityView> _views = new Dictionary<ulong, EntityView>();
    readonly List<ulong> _stale = new List<ulong>();

    /// Hits waiting to be animated, oldest first.
    readonly Queue<BattleLog> _pendingHits = new Queue<BattleLog>();
    bool _animating;
    ulong _aoeLungeActor;

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
        BiomeBackdropView backdrop
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
        _overlay.gameObject.SetActive(finished || transitioning);
        if (_resetButton != null)
        {
            _resetButton.SetActive(finished);
        }

        if (finished || transitioning)
        {
            _popup?.Close();
            _inventory?.Close();
            _overlay.SetAsLastSibling();
        }

        _banner?.Raise();
        _escape?.transform.SetAsLastSibling();

        if (finished || transitioning)
        {
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
            if (!entity.Alive && !hasView && entity.Faction == Team.Enemies)
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
            var pending = GameManager.Conn?.Db.SkillDef.Id.Find(_pendingSkillId);
            var revive = GameManager.SkillTargetsFallenAlly(pending);
            var targetable =
                _targeting
                && myTurn
                && (
                    revive
                        ? entity.Faction == Team.Players && !entity.Alive && !isLocal
                        : entity.Faction == Team.Enemies && entity.Alive
                );
            view.Bind(entity, activeId == entity.EntityId, isLocal, targetable, HandleEntityClicked);

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
            else if (!entity.Alive && !AnimationsPending() && entity.Faction == Team.Enemies)
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

    bool AnimationsPending() => _animating || _pendingHits.Count > 0;

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
            var skill = GameManager.Conn?.Db.SkillDef.Id.Find(skillDefId);
            if (GameManager.SkillTargetsFallenAlly(skill))
            {
                if (entity.Faction != Team.Players || entity.Alive)
                {
                    return;
                }

                ClearTargeting();
                GameManager.CastSkill(skillDefId, entityId);
                return;
            }

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

        if (row.Kind != LogKind.Attack && row.Kind != LogKind.Aoe)
        {
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

        if (row.ActorEntityId == row.TargetEntityId)
        {
            return;
        }

        _pendingHits.Enqueue(row);
    }

    IEnumerator PlayHit(BattleLog row)
    {
        _animating = true;

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

        if (
            _views.TryGetValue(row.ActorEntityId, out var actor)
            && _views.TryGetValue(row.TargetEntityId, out var target)
            && actor != null
            && target != null
        )
        {
            var skipLunge = _aoeLungeActor != 0 && row.ActorEntityId == _aoeLungeActor;
            if (!skipLunge)
            {
                _aoeLungeActor = 0;
                actor.PlayLunge(target.Home);
                yield return new WaitForSeconds(0.14f);
            }

            var targetEntity = GameManager.FindEntity(row.TargetEntityId);
            if (targetEntity != null)
            {
                target.PlayHit();
            }

            yield return new WaitForSeconds(
                skipLunge ? 0.16f : EntityView.LungeSeconds - 0.14f
            );
        }

        _animating = false;

        // A defeated combatant only leaves once its last hit has played out.
        if (_pendingHits.Count == 0)
        {
            _aoeLungeActor = 0;
            Refresh();
        }
    }

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
