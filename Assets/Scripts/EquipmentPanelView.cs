using System;
using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;

/// Bottom-left gear squares: weapon, three armor slots, and an amulet.
/// Worn pieces unequip on click; an empty square equips the first matching bag
/// item. Boots stay in data and still affect stats even though they are not shown.
public class EquipmentPanelView : MonoBehaviour
{
    const string EmptyCaption = "EMPTY";

    static readonly EquipSlot[] GearSlots =
    {
        EquipSlot.Weapon,
        EquipSlot.Helmet,
        EquipSlot.Chestplate,
        EquipSlot.Leggings,
        EquipSlot.Amulet,
    };

    sealed class Cell
    {
        public Image Background;
        public Image Icon;
        public Text Caption;
        public Button Button;
        public ItemHoverTip Hover;
        public ulong ItemId;
        public Action<ulong> Action;
    }

    public Action<ulong> OnEquip;
    public Action<ulong> OnUnequip;

    readonly List<Cell> _cells = new List<Cell>();
    ItemTooltipView _tooltip;

    public static EquipmentPanelView Create(Transform parent)
    {
        var panel = UiFactory.Panel(parent, "Equipment", UiFactory.PanelColor);
        var view = panel.gameObject.AddComponent<EquipmentPanelView>();

        var title = UiFactory.Label(
            panel.transform,
            "Title",
            "EQUIPMENT",
            16,
            TextAnchor.UpperLeft,
            UiFactory.MutedColor
        );
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(-20f, 22f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -8f);

        var grid = UiFactory.NewRect(panel.transform, "Slots");
        grid.anchorMin = Vector2.zero;
        grid.anchorMax = Vector2.one;
        grid.offsetMin = new Vector2(12f, 12f);
        grid.offsetMax = new Vector2(-12f, -34f);

        var canvas = parent.GetComponentInParent<Canvas>();
        view._tooltip = ItemTooltipView.Create(canvas != null ? canvas.transform : parent);

        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(62f, 62f);
        layout.spacing = new Vector2(8f, 8f);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 2;

        foreach (var slot in GearSlots)
        {
            view._cells.Add(view.MakeCell(grid, slot.ToString()));
        }

        return view;
    }

    Cell MakeCell(Transform parent, string caption)
    {
        var background = UiFactory.Panel(parent, $"Slot_{caption}", UiFactory.SlotColor);
        background.raycastTarget = true;

        var icon = UiFactory.Graphic(
            background.transform,
            "Icon",
            PlaceholderArt.Solid(Color.white),
            Color.white
        );
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        UiFactory.Anchor(icon.rectTransform, new Vector2(0.12f, 0.22f), new Vector2(0.88f, 0.92f));
        icon.gameObject.SetActive(false);

        var label = UiFactory.Label(
            background.transform,
            "Label",
            EmptyCaption,
            16,
            TextAnchor.MiddleCenter,
            UiFactory.MutedColor
        );
        UiFactory.Anchor(label.rectTransform, Vector2.zero, Vector2.one);
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 10;
        label.resizeTextMaxSize = 16;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;

        var button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        button.colors = colors;

        var hover = background.gameObject.AddComponent<ItemHoverTip>();

        var cell = new Cell
        {
            Background = background,
            Icon = icon,
            Caption = label,
            Button = button,
            Hover = hover,
        };
        button.onClick.AddListener(() => cell.Action?.Invoke(cell.ItemId));
        return cell;
    }

    public void Render(Entity me, bool allowChanges)
    {
        for (var i = 0; i < _cells.Count; i++)
        {
            var slot = GearSlots[i];
            if (me == null)
            {
                SetEmpty(_cells[i], 0, null, false);
                continue;
            }

            var worn = GameManager.EquippedIn(slot);
            var def = worn == null ? null : GameManager.ItemDefOf(worn);
            if (worn != null && def != null)
            {
                Fill(_cells[i], def, worn.Id, OnUnequip, allowChanges, _tooltip);
                continue;
            }

            var spare = FirstBagItemFor(slot);
            SetEmpty(_cells[i], spare, spare == 0 ? null : OnEquip, allowChanges);
        }
    }

    static ulong FirstBagItemFor(EquipSlot slot)
    {
        foreach (var item in GameManager.BagItems())
        {
            var def = GameManager.ItemDefOf(item);
            if (def == null)
            {
                continue;
            }

            if (SlotMatches(def, slot))
            {
                return item.Id;
            }
        }

        return 0;
    }

    static bool SlotMatches(ItemDef def, EquipSlot slot)
    {
        if (slot == EquipSlot.Weapon)
        {
            return def.Kind == ItemKind.Weapon;
        }

        if (slot == EquipSlot.Amulet)
        {
            return def.Kind == ItemKind.Amulet;
        }

        return def.Kind == ItemKind.Armor && SlotForArmor(def.ArmorSlot) == slot;
    }

    static EquipSlot SlotForArmor(ArmorSlot armor) =>
        armor switch
        {
            ArmorSlot.Helmet => EquipSlot.Helmet,
            ArmorSlot.Chestplate => EquipSlot.Chestplate,
            ArmorSlot.Leggings => EquipSlot.Leggings,
            ArmorSlot.Boots => EquipSlot.Boots,
            _ => EquipSlot.Bag,
        };

    static void SetEmpty(Cell cell, ulong itemId, Action<ulong> action, bool allowChanges)
    {
        cell.Caption.text = EmptyCaption;
        cell.Caption.color = UiFactory.MutedColor;
        cell.Caption.gameObject.SetActive(true);
        cell.Icon.gameObject.SetActive(false);
        cell.Background.color = UiFactory.SlotColor;
        cell.ItemId = itemId;
        cell.Action = action;
        cell.Button.interactable = allowChanges && action != null && itemId != 0;
        cell.Hover.Bind(null, null);
    }

    static void Fill(
        Cell cell,
        ItemDef def,
        ulong itemId,
        Action<ulong> action,
        bool allowChanges,
        ItemTooltipView tooltip
    )
    {
        var icon = AmuletArt.Icon(def.Name);
        var showIcon = icon != null;
        cell.Icon.sprite = icon;
        cell.Icon.color = Color.white;
        cell.Icon.gameObject.SetActive(showIcon);
        cell.Caption.text = def.ShortName;
        cell.Caption.color = UiFactory.TextColor;
        cell.Caption.gameObject.SetActive(!showIcon);
        cell.Background.color = new Color(0.27f, 0.25f, 0.20f, 1f);
        cell.ItemId = itemId;
        cell.Action = action;
        cell.Button.interactable = allowChanges && action != null && itemId != 0;
        cell.Hover.Bind(tooltip, def);
    }
}
