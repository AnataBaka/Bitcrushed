using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;

/// Bottom-left bag + equipment panel. Click equipped gear to unequip, bag gear
/// to equip. Consumables stay in the Items menu, not the bag grid.
public class InventoryView : MonoBehaviour
{
    const int BagCapacity = 12;
    const int BagColumns = 4;

    Transform _armorCol;
    Transform _weaponSlot;
    Transform _bagGrid;
    Transform _statRow;
    Text _title;

    public static InventoryView Create(Transform parent)
    {
        var panel = UiFactory.Panel(parent, "Inventory", UiFactory.PanelColor);
        var view = panel.gameObject.AddComponent<InventoryView>();

        view._title = UiFactory.Label(
            panel.transform,
            "Title",
            "BAG / GEAR",
            16,
            TextAnchor.UpperLeft,
            UiFactory.MutedColor
        );
        view._title.rectTransform.anchorMin = new Vector2(0f, 1f);
        view._title.rectTransform.anchorMax = new Vector2(1f, 1f);
        view._title.rectTransform.pivot = new Vector2(0.5f, 1f);
        view._title.rectTransform.sizeDelta = new Vector2(-16f, 20f);
        view._title.rectTransform.anchoredPosition = new Vector2(0f, -6f);

        view._armorCol = MakeGrid(panel.transform, "Armor", new Vector2(0.04f, 0.22f), new Vector2(0.30f, 0.88f), 1, 36f);
        view._weaponSlot = MakeGrid(panel.transform, "Weapon", new Vector2(0.04f, 0.04f), new Vector2(0.30f, 0.20f), 1, 36f);
        view._bagGrid = MakeGrid(panel.transform, "Bag", new Vector2(0.32f, 0.34f), new Vector2(0.97f, 0.88f), BagColumns, 34f);
        view._statRow = MakeGrid(panel.transform, "Stats", new Vector2(0.32f, 0.04f), new Vector2(0.97f, 0.32f), 4, 42f);
        return view;
    }

    static Transform MakeGrid(Transform parent, string name, Vector2 min, Vector2 max, int columns, float cell)
    {
        var rect = UiFactory.NewRect(parent, name);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var grid = rect.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(cell, cell);
        grid.spacing = new Vector2(4f, 4f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.UpperLeft;
        return rect;
    }

    public void Render(Player local, Entity me)
    {
        Clear(_armorCol);
        Clear(_weaponSlot);
        Clear(_bagGrid);
        Clear(_statRow);

        if (local == null || me == null)
        {
            _title.text = "BAG / GEAR";
            CreateSlot(_armorCol, "Helm", null, true);
            CreateSlot(_armorCol, "Chest", null, true);
            CreateSlot(_armorCol, "Legs", null, true);
            CreateSlot(_armorCol, "Boots", null, true);
            CreateSlot(_weaponSlot, "WPN", null, true);
            for (var i = 0; i < BagCapacity; i++)
            {
                CreateSlot(_bagGrid, "", null, false);
            }

            return;
        }

        _title.text = $"{me.Name}  {me.ClassName}";
        CreateSlot(_armorCol, "Helm", GameManager.FindEquipped(local, EquipSlot.Helmet), true);
        CreateSlot(_armorCol, "Chest", GameManager.FindEquipped(local, EquipSlot.Chestplate), true);
        CreateSlot(_armorCol, "Legs", GameManager.FindEquipped(local, EquipSlot.Leggings), true);
        CreateSlot(_armorCol, "Boots", GameManager.FindEquipped(local, EquipSlot.Boots), true);
        CreateSlot(_weaponSlot, "WPN", GameManager.FindEquipped(local, EquipSlot.Weapon), true);

        var bag = GameManager.BagItems(local);
        for (var i = 0; i < BagCapacity; i++)
        {
            CreateSlot(_bagGrid, "", i < bag.Count ? bag[i] : null, false);
        }

        CreateStat(_statRow, "STR", me.Strength);
        CreateStat(_statRow, "DEX", me.Dexterity);
        CreateStat(_statRow, "INT", me.Intelligence);
        CreateStat(_statRow, "SPD", me.Speed);
    }

    void CreateSlot(Transform parent, string emptyLabel, PlayerItem item, bool equipped)
    {
        var def = item == null ? null : GameManager.ItemDefOf(item);
        var caption = emptyLabel;
        uint itemId = 0;
        if (item != null && def != null)
        {
            itemId = item.Id;
            caption = SlotCaption(def, item);
        }

        var button = UiFactory.TextButton(parent, string.IsNullOrEmpty(emptyLabel) ? "Bag" : emptyLabel, caption, 11, 34f);
        var image = button.GetComponent<Image>();
        var label = button.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.fontSize = 11;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
        }

        if (item == null)
        {
            button.interactable = false;
            image.color = new Color(0.18f, 0.18f, 0.22f, 0.9f);
            if (label != null)
            {
                label.text = emptyLabel;
                label.color = UiFactory.MutedColor;
            }
        }
        else
        {
            button.interactable = true;
            image.color = equipped
                ? new Color(0.42f, 0.34f, 0.18f, 1f)
                : new Color(0.22f, 0.28f, 0.42f, 1f);
            if (def != null)
            {
                image.sprite = PlaceholderArt.ItemIcon(def);
            }

            button.onClick.AddListener(() => OnClick(itemId, equipped));
        }
    }

    static void CreateStat(Transform parent, string name, int value)
    {
        var box = UiFactory.Panel(parent, name, new Color(0.16f, 0.16f, 0.20f, 1f));
        var label = UiFactory.Label(
            box.transform,
            "Value",
            $"{name}\n{value}",
            12,
            TextAnchor.MiddleCenter,
            UiFactory.TextColor
        );
        UiFactory.Anchor(label.rectTransform, Vector2.zero, Vector2.one);
    }

    static string SlotCaption(ItemDef def, PlayerItem item)
    {
        if (item.HealthPips > 0)
        {
            return $"{ShortName(def.Name)}\n+{item.HealthPips}HP";
        }

        return ShortName(def.Name);
    }

    static string ShortName(string name)
    {
        return name
            .Replace("Starter ", "")
            .Replace("Leather ", "")
            .Replace(" Potion", " Pot");
    }

    static void OnClick(uint itemId, bool equipped)
    {
        if (itemId == 0)
        {
            return;
        }

        if (equipped)
        {
            GameManager.UnequipItem(itemId);
            return;
        }

        GameManager.EquipItem(itemId);
    }

    static void Clear(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (var i = parent.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }
}
