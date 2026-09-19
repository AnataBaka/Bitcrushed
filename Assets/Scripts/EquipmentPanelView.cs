using System;
using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;

/// Bottom-left panel: the worn equipment grid over the carried bag. Clicking a
/// worn piece stows it, clicking carried gear wears it, clicking a potion drinks
/// it. Decides nothing itself; every click becomes a reducer call.
public class EquipmentPanelView : MonoBehaviour
{
    const int BagCapacity = 12;

    static readonly (EquipSlot Slot, string Caption)[] GearSlots =
    {
        (EquipSlot.Weapon, "WPN"),
        (EquipSlot.Helmet, "HLM"),
        (EquipSlot.Chestplate, "CHS"),
        (EquipSlot.Leggings, "LEG"),
        (EquipSlot.Boots, "BTS"),
    };

    /// A single square in either grid. The listener is wired once and reads
    /// whatever the last Render stored, so clicks never need rebinding.
    sealed class Cell
    {
        public Image Background;
        public Image Icon;
        public Text Caption;
        public Button Button;
        public ulong ItemId;
        public Action<ulong> Action;
    }

    public Action<ulong> OnEquip;
    public Action<ulong> OnUnequip;
    public Action<ulong> OnUse;

    readonly List<Cell> _gearCells = new List<Cell>();
    readonly List<Cell> _bagCells = new List<Cell>();

    Text _readout;

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
        title.rectTransform.sizeDelta = new Vector2(-24f, 22f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -8f);

        view._readout = UiFactory.Label(
            panel.transform,
            "Readout",
            "",
            15,
            TextAnchor.UpperRight,
            UiFactory.MutedColor
        );
        view._readout.rectTransform.anchorMin = new Vector2(0f, 1f);
        view._readout.rectTransform.anchorMax = new Vector2(1f, 1f);
        view._readout.rectTransform.pivot = new Vector2(0.5f, 1f);
        view._readout.rectTransform.sizeDelta = new Vector2(-24f, 22f);
        view._readout.rectTransform.anchoredPosition = new Vector2(0f, -8f);

        var gear = Grid(panel.transform, "GearSlots", new Vector2(66f, 66f), 5);
        UiFactory.Anchor(gear, new Vector2(0f, 0.56f), new Vector2(1f, 1f));
        gear.offsetMin = new Vector2(12f, 4f);
        gear.offsetMax = new Vector2(-12f, -32f);
        foreach (var slot in GearSlots)
        {
            view._gearCells.Add(view.MakeCell(gear, slot.Caption, 15));
        }

        var bagTitle = UiFactory.Label(
            panel.transform,
            "BagTitle",
            "BAG",
            15,
            TextAnchor.UpperLeft,
            UiFactory.MutedColor
        );
        bagTitle.rectTransform.anchorMin = new Vector2(0f, 0.56f);
        bagTitle.rectTransform.anchorMax = new Vector2(1f, 0.56f);
        bagTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
        bagTitle.rectTransform.sizeDelta = new Vector2(-24f, 18f);
        bagTitle.rectTransform.anchoredPosition = new Vector2(0f, -1f);

        var bag = Grid(panel.transform, "BagSlots", new Vector2(56f, 52f), 6);
        UiFactory.Anchor(bag, Vector2.zero, new Vector2(1f, 0.56f));
        bag.offsetMin = new Vector2(12f, 10f);
        bag.offsetMax = new Vector2(-12f, -20f);
        for (var i = 0; i < BagCapacity; i++)
        {
            view._bagCells.Add(view.MakeCell(bag, "", 13));
        }

        return view;
    }

    static RectTransform Grid(Transform parent, string name, Vector2 cellSize, int columns)
    {
        var rect = UiFactory.NewRect(parent, name);
        var layout = rect.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = cellSize;
        layout.spacing = new Vector2(8f, 6f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = columns;
        layout.childAlignment = TextAnchor.UpperLeft;
        return rect;
    }

    Cell MakeCell(Transform parent, string caption, int fontSize)
    {
        var background = UiFactory.Panel(parent, $"Slot_{parent.childCount}", UiFactory.SlotColor);
        background.raycastTarget = true;

        var icon = UiFactory.Graphic(
            background.transform,
            "Icon",
            PlaceholderArt.Shape(ShapeKind.Rect, Color.white),
            Color.white
        );
        icon.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        icon.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        icon.rectTransform.pivot = new Vector2(0.5f, 1f);
        icon.rectTransform.sizeDelta = new Vector2(34f, 34f);
        icon.rectTransform.anchoredPosition = new Vector2(0f, -3f);
        icon.gameObject.SetActive(false);

        var label = UiFactory.Label(
            background.transform,
            "Label",
            caption,
            fontSize,
            TextAnchor.LowerCenter,
            UiFactory.MutedColor
        );
        UiFactory.Anchor(label.rectTransform, Vector2.zero, Vector2.one);
        label.rectTransform.offsetMin = new Vector2(2f, 3f);
        label.rectTransform.offsetMax = new Vector2(-2f, -2f);

        var button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
        button.colors = colors;

        var cell = new Cell
        {
            Background = background,
            Icon = icon,
            Caption = label,
            Button = button,
        };
        button.onClick.AddListener(() => cell.Action?.Invoke(cell.ItemId));
        return cell;
    }

    public void Render(Entity me, bool myTurn)
    {
        if (me == null)
        {
            _readout.text = "";
            for (var i = 0; i < _gearCells.Count; i++)
            {
                SetEmpty(_gearCells[i], GearSlots[i].Caption);
            }

            foreach (var cell in _bagCells)
            {
                SetEmpty(cell, "");
            }

            return;
        }

        _readout.text = $"ATK {me.Atk}   DEF {me.Defense}";

        for (var i = 0; i < _gearCells.Count; i++)
        {
            var slot = GearSlots[i];
            var worn = GameManager.EquippedIn(slot.Slot);
            var def = worn == null ? null : GameManager.ItemDefOf(worn);
            if (def == null)
            {
                SetEmpty(_gearCells[i], slot.Caption);
                continue;
            }

            Fill(_gearCells[i], def, def.ShortName, worn.Id, OnUnequip, true);
        }

        var bag = GameManager.BagItems();
        for (var i = 0; i < _bagCells.Count; i++)
        {
            var cell = _bagCells[i];
            if (i >= bag.Count)
            {
                SetEmpty(cell, "");
                continue;
            }

            var item = bag[i];
            var def = GameManager.ItemDefOf(item);
            if (def == null)
            {
                SetEmpty(cell, "");
                continue;
            }

            if (def.Kind == ItemKind.Consumable)
            {
                var caption = item.Quantity > 1
                    ? $"{def.ShortName} x{item.Quantity}"
                    : def.ShortName;
                // Drinking is a turn action, so it waits for your turn. Swapping
                // gear is not, so it stays live.
                Fill(cell, def, caption, item.Id, OnUse, myTurn);
                continue;
            }

            Fill(cell, def, def.ShortName, item.Id, OnEquip, true);
        }
    }

    static void SetEmpty(Cell cell, string caption)
    {
        cell.Icon.gameObject.SetActive(false);
        cell.Caption.text = caption;
        cell.Caption.color = UiFactory.MutedColor;
        cell.Caption.alignment = TextAnchor.MiddleCenter;
        cell.Background.color = UiFactory.SlotColor;
        cell.Button.interactable = false;
        cell.ItemId = 0;
        cell.Action = null;
    }

    static void Fill(
        Cell cell,
        ItemDef def,
        string caption,
        ulong itemId,
        Action<ulong> action,
        bool interactable
    )
    {
        cell.Icon.gameObject.SetActive(true);
        cell.Icon.sprite = PlaceholderArt.Shape(ItemShape(def), ItemColor(def));
        cell.Icon.color = Color.white;
        cell.Caption.text = caption;
        cell.Caption.color = UiFactory.TextColor;
        cell.Caption.alignment = TextAnchor.LowerCenter;
        cell.Background.color = new Color(0.27f, 0.25f, 0.20f, 1f);
        cell.Button.interactable = interactable && action != null;
        cell.ItemId = itemId;
        cell.Action = action;
    }

    static ShapeKind ItemShape(ItemDef def)
    {
        if (def.Kind == ItemKind.Consumable)
        {
            return ShapeKind.Flask;
        }

        if (def.Kind == ItemKind.Weapon)
        {
            switch (def.WeaponType)
            {
                case WeaponType.Bow:
                    return ShapeKind.Bow;
                case WeaponType.Staff:
                    return ShapeKind.Rod;
                default:
                    return ShapeKind.Blade;
            }
        }

        switch (def.ArmorSlot)
        {
            case ArmorSlot.Helmet:
                return ShapeKind.Helm;
            case ArmorSlot.Leggings:
                return ShapeKind.Legs;
            case ArmorSlot.Boots:
                return ShapeKind.Boot;
            default:
                return ShapeKind.Vest;
        }
    }

    static Color ItemColor(ItemDef def)
    {
        if (def.Kind == ItemKind.Consumable)
        {
            return def.HealAmount > 0
                ? new Color(0.84f, 0.29f, 0.33f)
                : new Color(0.33f, 0.53f, 0.90f);
        }

        if (def.Kind == ItemKind.Weapon)
        {
            return new Color(0.72f, 0.76f, 0.82f);
        }

        // Mana-granting armour is the robe, everything else is leather.
        return def.MaxManaBonus > 0
            ? new Color(0.52f, 0.38f, 0.74f)
            : new Color(0.62f, 0.45f, 0.28f);
    }
}
