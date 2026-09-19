using System;
using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;

/// Bottom-left 2x2 squares from the screenshot HUD: weapon plus three armor slots.
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
    };

    sealed class Cell
    {
        public Image Background;
        public Text Caption;
        public Button Button;
        public ulong ItemId;
        public Action<ulong> Action;
    }

    public Action<ulong> OnEquip;
    public Action<ulong> OnUnequip;

    readonly List<Cell> _cells = new List<Cell>();

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

        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(70f, 70f);
        layout.spacing = new Vector2(10f, 10f);
        layout.childAlignment = TextAnchor.UpperLeft;

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

        var cell = new Cell
        {
            Background = background,
            Caption = label,
            Button = button,
        };
        button.onClick.AddListener(() => cell.Action?.Invoke(cell.ItemId));
        return cell;
    }

    public void Render(Entity me, bool allowChanges)
    {
        for (var i = 0; i < _cells.Count; i++)
        {
            var slot = GearSlots[i];
            var localPlayer = GameManager.LocalPlayer();
            if (
                me == null
                || (
                    slot != EquipSlot.Weapon
                    && localPlayer != null
                    && localPlayer.Class == PlayerClass.Ninja
                )
            )
            {
                SetEmpty(_cells[i], 0, null, false);
                continue;
            }

            var worn = GameManager.EquippedIn(slot);
            var def = worn == null ? null : GameManager.ItemDefOf(worn);
            if (worn != null && def != null)
            {
                Fill(_cells[i], def.ShortName, worn.Id, OnUnequip, allowChanges);
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
        cell.Background.color = UiFactory.SlotColor;
        cell.ItemId = itemId;
        cell.Action = action;
        cell.Button.interactable = allowChanges && action != null && itemId != 0;
    }

    static void Fill(Cell cell, string caption, ulong itemId, Action<ulong> action, bool allowChanges)
    {
        cell.Caption.text = caption;
        cell.Caption.color = UiFactory.TextColor;
        cell.Background.color = new Color(0.27f, 0.25f, 0.20f, 1f);
        cell.ItemId = itemId;
        cell.Action = action;
        cell.Button.interactable = allowChanges && action != null && itemId != 0;
    }
}
