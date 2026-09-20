using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;

/// Left-edge name list of the server DisplayPos queue. Current actor is always
/// DisplayPos 0. Unity never reorders; it only tweens rows when DisplayPos changes.
public class TurnOrderListView : MonoBehaviour
{
    public const float ShiftSeconds = 0.35f;
    const float FadeSeconds = 0.22f;
    /// Floor for a biome-prefixed pack name plus the A/B suffix at 14px plus padding.
    const float Width = 188f;
    const float EdgePad = 10f;
    const float HeaderHeight = 22f;
    const float RowHeight = 22f;
    const float TopPad = 6f;
    const float BottomPad = 8f;
    const int MaxSlots = 7;
    const int NameClip = 18;

    static readonly Color Backing = new Color(0.10f, 0.11f, 0.14f, 0.82f);
    static readonly Color PlayerColor = new Color(0.82f, 0.90f, 0.98f, 1f);
    static readonly Color EnemyColor = new Color(0.93f, 0.62f, 0.56f, 1f);
    static readonly Color Highlight = new Color(0.95f, 0.82f, 0.30f, 0.28f);

    readonly Dictionary<ulong, Row> _rows = new Dictionary<ulong, Row>();
    readonly List<ulong> _scratch = new List<ulong>();

    uint _stage;
    bool _wasBattle;
    bool _pending;

    class Row
    {
        public RectTransform Rt;
        public CanvasGroup Group;
        public Text Label;
        public Image Highlight;
        public bool Enemy;
        public int Slot;
        public bool Leaving;
        public bool Crossing;
        public float AnimT = 1f;
        public float FromY;
        public float ToY;
        public float Fade = 1f;
    }

    public static TurnOrderListView Create(Transform canvas)
    {
        var root = UiFactory.NewRect(canvas, "TurnOrderList");
        var panel = root.gameObject.AddComponent<Image>();
        panel.sprite = PlaceholderArt.FlatWhite();
        panel.color = Backing;
        panel.type = Image.Type.Simple;
        panel.raycastTarget = false;
        root.anchorMin = new Vector2(0f, 0.5f);
        root.anchorMax = new Vector2(0f, 0.5f);
        root.pivot = new Vector2(0f, 0.5f);
        root.sizeDelta = new Vector2(Width, PanelHeight);
        root.anchoredPosition = new Vector2(EdgePad, 0f);

        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        var header = UiFactory.Label(
            root,
            "Header",
            "TURN ORDER",
            16,
            TextAnchor.MiddleCenter,
            UiFactory.MutedColor
        );
        header.raycastTarget = false;
        header.rectTransform.anchorMin = new Vector2(0f, 1f);
        header.rectTransform.anchorMax = new Vector2(1f, 1f);
        header.rectTransform.pivot = new Vector2(0.5f, 1f);
        header.rectTransform.sizeDelta = new Vector2(0f, HeaderHeight);
        header.rectTransform.anchoredPosition = Vector2.zero;

        var view = root.gameObject.AddComponent<TurnOrderListView>();
        view.gameObject.SetActive(false);
        return view;
    }

    public void Render(GameSession session)
    {
        _pending = true;
        var inBattle = session != null && session.Phase == BattlePhase.InBattle;
        if (!inBattle)
        {
            ApplyQueued();
            return;
        }

        // Create() leaves the panel inactive. LateUpdate does not run on inactive
        // objects, so the deferred apply would never enable it. Wake it here;
        // once live, later updates still coalesce in LateUpdate for the slide.
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            ApplyQueued();
        }
    }

    void LateUpdate()
    {
        if (_pending)
        {
            ApplyQueued();
        }

        TickRows();
    }

    void ApplyQueued()
    {
        _pending = false;
        var session = GameManager.Session();
        var inBattle = session != null && session.Phase == BattlePhase.InBattle;
        if (!inBattle)
        {
            ClearAll();
            gameObject.SetActive(false);
            _wasBattle = false;
            return;
        }

        gameObject.SetActive(true);
        // Snap only on stage boundaries. A new combat round still rotates DisplayPos
        // (finished actor to the bottom); treating Round as a snap made player wraps
        // teleport because players are often last in the speed queue.
        var snap = !_wasBattle || session.StageNumber != _stage;
        _wasBattle = true;
        _stage = session.StageNumber;

        var living = new HashSet<ulong>();
        var queue = GameManager.TurnQueue();
        foreach (var entry in queue)
        {
            if (entry.DisplayPos >= MaxSlots)
            {
                continue;
            }

            var entity = GameManager.FindEntity(entry.EntityId) ?? CombatHpPresenter.Ghost(entry.EntityId);
            if (entity == null || !CombatHpPresenter.IsVisible(entity))
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
            row.Crossing = false;
            row.Rt.anchoredPosition = new Vector2(6f, row.ToY);
        }
        else if (row.Leaving)
        {
            return;
        }

        row.Enemy = entity.Faction == Team.Enemies;
        var label = entity.Name;
        ApplyLabel(row, label, slot == 0);
        MoveTo(row, slot, snap);
    }

    void MoveTo(Row row, int slot, bool snap)
    {
        var target = YForSlot(slot);
        var wrapping = !snap && slot > row.Slot && target < row.ToY - (RowHeight * 0.5f);
        if (snap)
        {
            row.Crossing = false;
            row.Slot = slot;
            row.FromY = target;
            row.ToY = target;
            row.AnimT = 1f;
            row.Rt.anchoredPosition = new Vector2(6f, target);
            if (row.Group != null)
            {
                row.Group.alpha = row.Fade;
            }

            return;
        }

        row.Slot = slot;
        if (Mathf.Abs(target - row.ToY) < 0.5f && row.AnimT >= 1f)
        {
            row.Crossing = false;
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

        var currentY = row.AnimT >= 1f
            ? row.ToY
            : Mathf.Lerp(row.FromY, row.ToY, Ease(row.AnimT));
        row.FromY = currentY;
        row.ToY = target;
        row.AnimT = 0f;
        if (wrapping)
        {
            row.Crossing = true;
            row.Rt.SetAsLastSibling();
        }
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
        _stage = 0;
        _pending = false;
    }

    void TickRows()
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
                if (row.Crossing)
                {
                    var dip = 1f - (Mathf.Sin(row.AnimT * Mathf.PI) * 0.58f);
                    row.Group.alpha = row.Fade * dip;
                    if (row.AnimT >= 1f)
                    {
                        row.Crossing = false;
                        row.Group.alpha = row.Fade;
                    }
                }
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

        var highlightRt = UiFactory.NewRect(rt, "Highlight");
        UiFactory.Anchor(highlightRt, Vector2.zero, Vector2.one);
        var highlight = highlightRt.gameObject.AddComponent<Image>();
        highlight.sprite = PlaceholderArt.FlatWhite();
        highlight.color = Highlight;
        highlight.raycastTarget = false;
        highlight.enabled = false;

        var label = UiFactory.Label(
            rt,
            "Name",
            "",
            16,
            TextAnchor.MiddleLeft,
            PlayerColor
        );
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.resizeTextForBestFit = false;
        UiFactory.Anchor(label.rectTransform, Vector2.zero, Vector2.one);
        label.rectTransform.offsetMin = new Vector2(6f, 0f);
        label.rectTransform.offsetMax = new Vector2(-4f, 0f);

        return new Row
        {
            Rt = rt,
            Group = group,
            Label = label,
            Highlight = highlight,
            Enemy = entity.Faction == Team.Enemies,
            Fade = 1f,
        };
    }

    static void ApplyLabel(Row row, string name, bool current)
    {
        var clipped = ClipName(name);
        row.Label.text = current ? $"> {clipped}" : $"  {clipped}";
        row.Label.fontStyle = FontStyle.Normal;
        row.Label.color = current
            ? UiFactory.ActiveColor
            : (row.Enemy ? EnemyColor : PlayerColor);
        if (row.Highlight != null)
        {
            row.Highlight.enabled = current;
        }
    }

    static string ClipName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "?";
        }

        return name.Length <= NameClip ? name : name.Substring(0, NameClip - 1) + "…";
    }

    static float PanelHeight => HeaderHeight + TopPad + (MaxSlots * RowHeight) + BottomPad;

    static float YForSlot(int slot) => -HeaderHeight - TopPad - (slot * RowHeight);

    static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - (2f * t));
    }
}
