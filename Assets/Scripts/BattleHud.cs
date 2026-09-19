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
    RectTransform _overlay;
    Text _overlayText;
    Text _connectionLabel;

    readonly Dictionary<ulong, EntityView> _views = new Dictionary<ulong, EntityView>();
    readonly List<ulong> _stale = new List<ulong>();

    bool _targeting;

    public void Init(
        RectTransform field,
        BattleLogView log,
        ActionMenuView menu,
        RectTransform overlay,
        Text overlayText,
        Text connectionLabel
    )
    {
        _field = field;
        _log = log;
        _menu = menu;
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
        _menu.OnSkillSelected = () => _targeting = true;

        Refresh();
    }

    void OnEnable() => GameManager.StateChanged += Refresh;

    void OnDisable() => GameManager.StateChanged -= Refresh;

    void Start() => Refresh();

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
        }

        SyncTeam(GameManager.TeamMembers(Team.Players), PlayerSlots, PlayerCardSize, true, me);
        SyncTeam(GameManager.TeamMembers(Team.Enemies), EnemySlots, EnemyCardSize, false, me);
        PruneMissing();

        _log.SetLines(GameManager.LogLines(60));
        _menu.Render(session, me, myTurn, _targeting);

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
            // Defeated combatants leave the screen.
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
        if (!_targeting || !GameManager.IsLocalTurn())
        {
            return;
        }

        _targeting = false;
        GameManager.Attack(entityId);
    }
}
