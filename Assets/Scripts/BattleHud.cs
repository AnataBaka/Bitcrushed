using System.Collections;
using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;

/// Draws the battle from table state and forwards clicks to reducers.
/// Holds no rules: it never computes damage, legality or turn order.
public class BattleHud : MonoBehaviour
{
    static readonly Vector2[] PlayerSlots =
    {
        new Vector2(330f, 120f),
        new Vector2(510f, 290f),
        new Vector2(690f, 460f),
    };

    static readonly Vector2[] EnemySlots =
    {
        new Vector2(1580f, 130f),
        new Vector2(1460f, 390f),
    };

    static readonly Vector2 PlayerCardSize = new Vector2(300f, 215f);
    static readonly Vector2 EnemyCardSize = new Vector2(340f, 240f);

    RectTransform _field;
    BattleLogView _log;
    ActionMenuView _menu;
    EquipmentPanelView _equipment;
    RectTransform _overlay;
    Text _overlayText;
    Text _connectionLabel;

    readonly Dictionary<ulong, EntityView> _views = new Dictionary<ulong, EntityView>();
    readonly List<ulong> _stale = new List<ulong>();

    /// Hits waiting to be animated, oldest first.
    readonly Queue<BattleLog> _pendingHits = new Queue<BattleLog>();
    bool _animating;

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
        Text connectionLabel
    )
    {
        _field = field;
        _log = log;
        _menu = menu;
        _equipment = equipment;
        _overlay = overlay;
        _overlayText = overlayText;
        _connectionLabel = connectionLabel;

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

        _equipment.OnEquip = GameManager.EquipItem;
        _equipment.OnUnequip = GameManager.UnequipItem;

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

        if (!myTurn)
        {
            ClearTargeting();
        }

        SyncTeam(GameManager.TeamMembers(Team.Players), PlayerSlots, PlayerCardSize, true, me);
        SyncTeam(GameManager.TeamMembers(Team.Enemies), EnemySlots, EnemyCardSize, false, me);
        PruneMissing();

        _log.SetLines(GameManager.LogLines(60));
        _menu.Render(session, me, myTurn, _targeting);
        _equipment.Render(
            me,
            session == null || session.Phase != BattlePhase.InBattle
        );

        var finished =
            session != null
            && (session.Phase == BattlePhase.Victory || session.Phase == BattlePhase.Defeat);
        _overlay.gameObject.SetActive(finished);
        if (finished)
        {
            _overlayText.text =
                session.Phase == BattlePhase.Victory ? "LEVEL COMPLETE" : "DEFEAT";
            _overlayText.color =
                session.Phase == BattlePhase.Victory
                    ? new Color(0.55f, 0.90f, 0.55f)
                    : new Color(0.92f, 0.45f, 0.45f);
        }
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
                view = EntityView.Create(_field, entity.Name, cardSize, showMana);
                _views[entity.EntityId] = view;
            }

            if (view == null)
            {
                continue;
            }

            var index = (int)Mathf.Min(entity.Slot, slots.Length - 1);
            view.SetPosition(slots[index]);

            var targetable = _targeting && myTurn && entity.Faction == Team.Enemies && entity.Alive;
            var isLocal = me != null && me.EntityId == entity.EntityId;
            view.Bind(entity, activeId == entity.EntityId, isLocal, targetable, HandleTargetClicked);

            var occupant = GameManager.FindPlayer(entity.EntityId);
            view.SetReadyBanner(
                session != null
                    && session.Phase == BattlePhase.Waiting
                    && occupant != null
                    && occupant.Ready
            );
        }
    }

    void PruneMissing()
    {
        if (AnimationsPending())
        {
            return;
        }

        _stale.Clear();

        foreach (var pair in _views)
        {
            var entity = GameManager.FindEntity(pair.Key);
            if (entity == null || !entity.Alive)
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

    void HandleTargetClicked(ulong entityId)
    {
        if (!_targeting || !GameManager.IsLocalTurn())
        {
            return;
        }

        var skillDefId = _pendingSkillId;
        ClearTargeting();

        if (skillDefId == 0)
        {
            GameManager.Attack(entityId);
            return;
        }

        GameManager.CastSkill(skillDefId, entityId);
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

        if (row.Kind != LogKind.Attack)
        {
            return;
        }

        if (row.ActorEntityId == 0 || row.TargetEntityId == 0)
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

        if (
            _views.TryGetValue(row.ActorEntityId, out var actor)
            && _views.TryGetValue(row.TargetEntityId, out var target)
            && actor != null
            && target != null
        )
        {
            actor.PlayLunge(target.Home);
            yield return new WaitForSeconds(0.14f);
            target.PlayHit();
            // Waiting on a fixed duration rather than the lunge coroutine keeps
            // the queue moving even if the card is destroyed mid-strike.
            yield return new WaitForSeconds(EntityView.LungeSeconds - 0.14f);
        }

        _animating = false;

        // A defeated combatant only leaves once its last hit has played out.
        if (_pendingHits.Count == 0)
        {
            Refresh();
        }
    }
}
