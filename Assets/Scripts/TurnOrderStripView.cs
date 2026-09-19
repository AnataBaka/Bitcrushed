using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;

/// Far-left view of the server's current-round TurnOrder table. Does not sort
/// or invent order; it only draws Idx 0 at the top.
public class TurnOrderStripView : MonoBehaviour
{
    const float Width = 72f;
    const int MaxSlots = 7;
    const float IconSize = 44f;

    static readonly Color PlayerBorder = new Color(0.45f, 0.78f, 0.95f, 1f);
    static readonly Color EnemyBorder = new Color(0.90f, 0.38f, 0.32f, 1f);
    static readonly Color ActiveGlow = new Color(0.95f, 0.82f, 0.30f, 1f);

    readonly List<Slot> _slots = new List<Slot>();

    struct Slot
    {
        public GameObject Root;
        public CanvasGroup Group;
        public Image Border;
        public Image Shape;
        public Text Label;
    }

    public static TurnOrderStripView Create(Transform field)
    {
        var root = UiFactory.NewRect(field, "TurnOrderStrip");
        root.anchorMin = new Vector2(0f, 0f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 0.5f);
        root.sizeDelta = new Vector2(Width, -24f);
        root.anchoredPosition = new Vector2(8f, 0f);

        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(4, 4, 16, 16);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        var view = root.gameObject.AddComponent<TurnOrderStripView>();
        for (var i = 0; i < MaxSlots; i++)
        {
            view._slots.Add(MakeSlot(root));
        }

        view.gameObject.SetActive(false);
        return view;
    }

    static Slot MakeSlot(Transform parent)
    {
        var row = UiFactory.NewRect(parent, "TurnSlot");
        var rowElement = row.gameObject.AddComponent<LayoutElement>();
        rowElement.minHeight = IconSize + 16f;
        rowElement.preferredHeight = IconSize + 16f;
        rowElement.flexibleWidth = 1f;

        var canvasGroup = row.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;

        var border = UiFactory.Panel(row, "Border", PlayerBorder);
        border.raycastTarget = false;
        border.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        border.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        border.rectTransform.pivot = new Vector2(0.5f, 1f);
        border.rectTransform.sizeDelta = new Vector2(IconSize + 6f, IconSize + 6f);
        border.rectTransform.anchoredPosition = Vector2.zero;

        var shape = UiFactory.Graphic(border.transform, "Shape", PlaceholderArt.Solid(Color.white), Color.white);
        UiFactory.Anchor(shape.rectTransform, Vector2.zero, Vector2.one);
        shape.rectTransform.offsetMin = new Vector2(4f, 4f);
        shape.rectTransform.offsetMax = new Vector2(-4f, -4f);

        var label = UiFactory.Label(row, "Label", "", 14, TextAnchor.UpperCenter, UiFactory.TextColor);
        label.fontStyle = FontStyle.Bold;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.rectTransform.anchorMin = new Vector2(0f, 0f);
        label.rectTransform.anchorMax = new Vector2(1f, 0f);
        label.rectTransform.pivot = new Vector2(0.5f, 0f);
        label.rectTransform.sizeDelta = new Vector2(0f, 16f);
        label.rectTransform.anchoredPosition = Vector2.zero;

        return new Slot
        {
            Root = row.gameObject,
            Group = canvasGroup,
            Border = border,
            Shape = shape,
            Label = label,
        };
    }

    public void Render(GameSession session)
    {
        var inBattle = session != null && session.Phase == BattlePhase.InBattle;
        gameObject.SetActive(inBattle);
        if (!inBattle)
        {
            return;
        }

        var queue = GameManager.TurnQueue();
        var shown = 0;
        for (var i = 0; i < queue.Count && shown < _slots.Count; i++)
        {
            var entry = queue[i];
            var entity = GameManager.FindEntity(entry.EntityId);
            if (entity == null || !entity.Alive)
            {
                continue;
            }

            BindSlot(_slots[shown], entry, entity, session.ActiveEntityId);
            shown += 1;
        }

        for (var i = shown; i < _slots.Count; i++)
        {
            _slots[i].Root.SetActive(false);
        }
    }

    static void BindSlot(Slot slot, TurnOrder entry, Entity entity, ulong activeId)
    {
        slot.Root.SetActive(true);

        var isEnemy = entity.Faction == Team.Enemies;
        ShapeKind shape;
        Color color;
        if (isEnemy)
        {
            var visual = PlaceholderArt.EnemyVisual(entity.ClassName);
            shape = visual.Shape;
            color = visual.Color;
        }
        else
        {
            shape = PlaceholderArt.ClassShape(entity.ClassName);
            color = PlaceholderArt.ClassColor(entity.ClassName);
        }

        KnightSpriteLibrary.EnsureLoaded();
        if (!isEnemy && KnightSpriteLibrary.Matches(entity.ClassName) && KnightSpriteLibrary.Idle.Length > 0)
        {
            slot.Shape.sprite = KnightSpriteLibrary.Idle[0];
            slot.Shape.color = Color.white;
        }
        else
        {
            slot.Shape.sprite = PlaceholderArt.Shape(shape, color);
            slot.Shape.color = Color.white;
        }
        slot.Label.text = ShortLabel(entity.Name);

        var isActive = entity.EntityId == activeId;
        slot.Border.color = isActive ? ActiveGlow : (isEnemy ? EnemyBorder : PlayerBorder);
        slot.Group.alpha = entry.HasActed && !isActive ? 0.38f : 1f;
    }

    static string ShortLabel(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "?";
        }

        var word = name.Split(' ')[0];
        return word.Length <= 2 ? word : word.Substring(0, 2);
    }
}
