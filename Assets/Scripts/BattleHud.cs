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
        new Vector2(360f, 120f),
        new Vector2(540f, 290f),
        new Vector2(720f, 460f),
    };

    static readonly Vector2[] EnemySlots =
    {
        new Vector2(1580f, 110f),
        new Vector2(1460f, 340f),
        new Vector2(1340f, 570f),
    };

    static readonly Vector2 PlayerCardSize = new Vector2(300f, 215f);
    static readonly Vector2 EnemyCardSize = new Vector2(320f, 230f);

    RectTransform _field;
    BattleLogView _log;
    ActionMenuView _menu;
    InventoryView _inventory;
    TurnMeterView _turns;
    RectTransform _overlay;
    Text _overlayText;
    Text _connectionLabel;

    readonly Dictionary<ulong, EntityView> _views = new Dictionary<ulong, EntityView>();
    readonly List<ulong> _stale = new List<ulong>();
    readonly Queue<CombatEvent> _lunges = new Queue<CombatEvent>();

    bool _targeting;
    uint _pendingSkillId;
    bool _lunging;
    uint _lastEventId;
    bool _eventsBound;

    public void Init(
        RectTransform field,
        BattleLogView log,
        ActionMenuView menu,
        InventoryView inventory,
        TurnMeterView turns,
        RectTransform overlay,
        Text overlayText,
        Text connectionLabel
    )
    {
        _field = field;
        _log = log;
        _menu = menu;
        _inventory = inventory;
        _turns = turns;
        _overlay = overlay;
        _overlayText = overlayText;
        _connectionLabel = connectionLabel;

        _menu.OnJoin = GameManager.JoinGame;
        _menu.OnStartBattle = GameManager.StartBattle;
        _menu.OnFocus = () =>
        {
            _targeting = false;
            GameManager.Focus();
        };
        _menu.OnUseItem = item =>
        {
            _targeting = false;
            GameManager.UseItem(item);
        };
        _menu.OnSkillSelected = skillId =>
        {
            var skill = GameManager.SkillDefOf(skillId);
            _pendingSkillId = skillId;
            if (skill != null && (skill.TargetCount == 0 || skill.BaseDamage == 0))
            {
                _targeting = false;
                GameManager.Attack(0, skillId);
                return;
            }

            _targeting = true;
        };

        Refresh();
    }

    void OnEnable() => GameManager.StateChanged += Refresh;

    void OnDisable() => GameManager.StateChanged -= Refresh;

    void Start() => Refresh();

    void Update()
    {
        BindEvents();
        if (!_lunging && _lunges.Count > 0)
        {
            StartCoroutine(PlayLunge(_lunges.Dequeue()));
        }
    }

    void BindEvents()
    {
        if (_eventsBound || GameManager.Conn == null)
        {
            return;
        }

        GameManager.Conn.Db.CombatEvent.OnInsert += OnCombatEvent;
        _eventsBound = true;
    }

    void OnCombatEvent(EventContext _, CombatEvent row)
    {
        if (row.Id <= _lastEventId)
        {
            return;
        }

        _lastEventId = row.Id;
        if (
            (
                row.ActionType == SpacetimeDB.Types.CombatActionType.Attack
                || row.ActionType == SpacetimeDB.Types.CombatActionType.Spell
            )
            && row.ActorEntityId != row.TargetEntityId
        )
        {
            _lunges.Enqueue(row);
        }
    }

    IEnumerator PlayLunge(CombatEvent row)
    {
        _lunging = true;
        if (!_views.TryGetValue(row.ActorEntityId, out var actor) || actor == null)
        {
            _lunging = false;
            yield break;
        }

        _views.TryGetValue(row.TargetEntityId, out var target);
        actor.Busy = true;
        var start = actor.Root.anchoredPosition;
        var dest =
            target == null
                ? Vector2.Lerp(actor.Home, actor.Home + new Vector2(80f, 0f), 1f)
                : Vector2.Lerp(actor.Home, target.Home, 0.78f);
        yield return MoveTo(actor.Root, start, dest, 0.16f);
        if (target != null)
        {
            StartCoroutine(PunchScale(target.Root));
        }

        yield return new WaitForSeconds(0.08f);
        if (!NextLungeSameActor(row))
        {
            yield return MoveTo(actor.Root, dest, actor.Home, 0.2f);
            actor.Root.anchoredPosition = actor.Home;
            actor.Busy = false;
        }

        _lunging = false;
    }

    bool NextLungeSameActor(CombatEvent row)
    {
        if (_lunges.Count == 0)
        {
            return false;
        }

        return _lunges.Peek().ActorEntityId == row.ActorEntityId;
    }

    static IEnumerator MoveTo(RectTransform actor, Vector2 from, Vector2 to, float duration)
    {
        var t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            var s = t * t * (3f - 2f * t);
            actor.anchoredPosition = Vector2.Lerp(from, to, s);
            yield return null;
        }
    }

    static IEnumerator PunchScale(RectTransform target)
    {
        var home = target.localScale;
        var t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.18f;
            var bump = 1f + Mathf.Sin(t * Mathf.PI) * 0.18f;
            target.localScale = home * bump;
            yield return null;
        }

        target.localScale = home;
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
            _targeting = false;
            _pendingSkillId = 0;
        }

        SyncTeam(GameManager.TeamMembers(Team.Players), PlayerSlots, PlayerCardSize, true, me);
        SyncTeam(GameManager.TeamMembers(Team.Enemies), EnemySlots, EnemyCardSize, false, me);
        PruneMissing();

        _log.SetLines(GameManager.LogLines(60));
        _menu.Render(session, me, myTurn, _targeting);
        _inventory.Render(GameManager.LocalPlayer(), me);
        _turns.Render(session);

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
            if (!entity.Alive)
            {
                continue;
            }

            if (!_views.TryGetValue(entity.EntityId, out var view) || view == null)
            {
                view = EntityView.Create(_field, entity.Name, cardSize, showMana);
                _views[entity.EntityId] = view;
            }

            var index = (int)Mathf.Min(entity.Slot, slots.Length - 1);
            view.SetPosition(slots[index]);

            var targetable = _targeting && myTurn && entity.Faction == Team.Enemies;
            var isLocal = me != null && me.EntityId == entity.EntityId;
            view.Bind(entity, activeId == entity.EntityId, isLocal, targetable, HandleTargetClicked);
        }
    }

    void PruneMissing()
    {
        _stale.Clear();

        foreach (var pair in _views)
        {
            var entity = GameManager.Conn?.Db.Entity.EntityId.Find(pair.Key);
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

    void HandleTargetClicked(ulong entityId)
    {
        if (!_targeting || !GameManager.IsLocalTurn() || _pendingSkillId == 0)
        {
            return;
        }

        var skillId = _pendingSkillId;
        _targeting = false;
        _pendingSkillId = 0;
        GameManager.Attack(entityId, skillId);
    }
}
