using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;

/// Left-edge name list of the server DisplayPos queue. Current actor is always
/// row 0. Unity never reorders; it only tweens rows when DisplayPos changes.
public class TurnOrderListView : MonoBehaviour
{
    public const float ShiftSeconds = 0.35f;
    const float FadeSeconds = 0.22f;
    const float Width = 110f;
    const float RowHeight = 22f;
    const float TopPad = 8f;
    const int MaxSlots = 7;
    const int NameClip = 11;

    static readonly Color Backing = new Color(0.06f, 0.07f, 0.09f, 0.55f);
    static readonly Color PlayerColor = new Color(0.82f, 0.90f, 0.98f, 1f);
    static readonly Color EnemyColor = new Color(0.93f, 0.62f, 0.56f, 1f);

    readonly Dictionary<ulong, Row> _rows = new Dictionary<ulong, Row>();
    readonly List<ulong> _scratch = new List<ulong>();

    uint _round;
    uint _stage;
    bool _wasBattle;

    class Row
    {
        public RectTransform Rt;
        public CanvasGroup Group;
        public Text Label;
        public bool Enemy;
        public int Slot;
        public bool Leaving;
        public float AnimT = 1f;
        public float FromY;
        public float ToY;
        public float Fade = 1f;
    }

    public static TurnOrderListView Create(Transform field)
    {
        var root = UiFactory.NewRect(field, "TurnOrderList");
        var panel = root.gameObject.AddComponent<Image>();
        panel.sprite = PlaceholderArt.FlatWhite();
        panel.color = Backing;
        panel.type = Image.Type.Simple;
        panel.raycastTarget = false;
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.sizeDelta = new Vector2(Width, TopPad + (MaxSlots * RowHeight) + 8f);
        root.anchoredPosition = new Vector2(8f, -64f);

        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        var view = root.gameObject.AddComponent<TurnOrderListView>();
        view.gameObject.SetActive(false);
        return view;
    }

    public void Render(GameSession session)
    {
        var inBattle = session != null && session.Phase == BattlePhase.InBattle;
        if (!inBattle)
        {
            ClearAll();
            gameObject.SetActive(false);
            _wasBattle = false;
            return;
        }

        gameObject.SetActive(true);
        var snap =
            !_wasBattle
            || session.Round != _round
            || session.StageNumber != _stage;
        _wasBattle = true;
        _round = session.Round;
        _stage = session.StageNumber;

        var living = new HashSet<ulong>();
        var queue = GameManager.TurnQueue();
        foreach (var entry in queue)
        {
            if (entry.DisplayPos >= MaxSlots)
            {
                continue;
            }

            var entity = GameManager.FindEntity(entry.EntityId);
            if (entity == null || !entity.Alive)
            {
                continue;
            }

            living.Add(entity.EntityId);
            BindRow(entity, (int)entry.DisplayPos, snap);
        }

        _scratch.Clear();
        foreach (var pair in _rows)
        {
            if (!living.Contains(pair.Key))
            {
                _scratch.Add(pair.Key);
            }
        }

        foreach (var id in _scratch)
        {
            if (snap)
            {
                DestroyRow(id);
            }
            else
            {
                BeginFade(id);
            }
        }
    }

    void BindRow(Entity entity, int slot, bool snap)
    {
        if (!_rows.TryGetValue(entity.EntityId, out var row) || row == null || row.Rt == null)
        {
            if (_rows.ContainsKey(entity.EntityId))
            {
                _rows.Remove(entity.EntityId);
            }

            row = MakeRow(entity);
            _rows[entity.EntityId] = row;
            row.Slot = slot;
            row.ToY = YForSlot(slot);
            row.FromY = row.ToY;
            row.AnimT = 1f;
            row.Rt.anchoredPosition = new Vector2(6f, row.ToY);
        }
        else if (row.Leaving)
        {
            return;
        }

        row.Enemy = entity.Faction == Team.Enemies;
        ApplyLabel(row, entity.Name, slot == 0);
        MoveTo(row, slot, snap);
    }

    void MoveTo(Row row, int slot, bool snap)
    {
        var target = YForSlot(slot);
        row.Slot = slot;
        if (snap || Mathf.Abs(target - row.ToY) < 0.5f && row.AnimT >= 1f)
        {
            row.FromY = target;
            row.ToY = target;
            row.AnimT = 1f;
            row.Rt.anchoredPosition = new Vector2(6f, target);
            return;
        }

        if (Mathf.Abs(target - row.ToY) < 0.5f)
        {
            return;
        }

        var current = Vector2.Lerp(
            new Vector2(6f, row.FromY),
            new Vector2(6f, row.ToY),
            Ease(row.AnimT)
        );
        row.FromY = current.y;
        row.ToY = target;
        row.AnimT = 0f;
    }

    void BeginFade(ulong entityId)
    {
        if (!_rows.TryGetValue(entityId, out var row) || row == null)
        {
            _rows.Remove(entityId);
            return;
        }

        row.Leaving = true;
    }

    void DestroyRow(ulong entityId)
    {
        if (_rows.TryGetValue(entityId, out var row) && row != null && row.Rt != null)
        {
            Destroy(row.Rt.gameObject);
        }

        _rows.Remove(entityId);
    }

    void ClearAll()
    {
        foreach (var pair in _rows)
        {
            if (pair.Value != null && pair.Value.Rt != null)
            {
                Destroy(pair.Value.Rt.gameObject);
            }
        }

        _rows.Clear();
        _round = 0;
        _stage = 0;
    }

    void Update()
    {
        _scratch.Clear();
        foreach (var pair in _rows)
        {
            var row = pair.Value;
            if (row == null || row.Rt == null)
            {
                _scratch.Add(pair.Key);
                continue;
            }

            if (row.Leaving)
            {
                row.Fade = Mathf.Max(0f, row.Fade - (Time.deltaTime / FadeSeconds));
                row.Group.alpha = row.Fade;
                if (row.Fade <= 0f)
                {
                    _scratch.Add(pair.Key);
                }

                continue;
            }

            if (row.AnimT < 1f)
            {
                row.AnimT = Mathf.Min(1f, row.AnimT + (Time.deltaTime / ShiftSeconds));
                var y = Mathf.Lerp(row.FromY, row.ToY, Ease(row.AnimT));
                row.Rt.anchoredPosition = new Vector2(6f, y);
            }
        }

        foreach (var id in _scratch)
        {
            DestroyRow(id);
        }
    }

    Row MakeRow(Entity entity)
    {
        var rt = UiFactory.NewRect(transform, "TurnName");
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(-12f, RowHeight);
        rt.anchoredPosition = new Vector2(6f, YForSlot(0));

        var group = rt.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        var label = UiFactory.Label(
            rt,
            "Name",
            "",
            15,
            TextAnchor.MiddleLeft,
            PlayerColor
        );
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 10;
        label.resizeTextMaxSize = 15;
        UiFactory.Anchor(label.rectTransform, Vector2.zero, Vector2.one);

        return new Row
        {
            Rt = rt,
            Group = group,
            Label = label,
            Enemy = entity.Faction == Team.Enemies,
            Fade = 1f,
        };
    }

    static void ApplyLabel(Row row, string name, bool current)
    {
        var clipped = ClipName(name);
        row.Label.text = current ? $"> {clipped}" : $"  {clipped}";
        row.Label.fontStyle = current ? FontStyle.Bold : FontStyle.Normal;
        row.Label.color = current
            ? UiFactory.ActiveColor
            : (row.Enemy ? EnemyColor : PlayerColor);
    }

    static string ClipName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "?";
        }

        return name.Length <= NameClip ? name : name.Substring(0, NameClip - 1) + "…";
    }

    static float YForSlot(int slot) => -TopPad - (slot * RowHeight);

    static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - (2f * t));
    }
}
