using System;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// Bottom-left Weapon and Amulet slots plus a backpack button. View-only:
/// equipping happens through the inventory popup.
public class EquipmentPanelView : MonoBehaviour
{
    const string EmptyCaption = "EMPTY";

    Image _weaponIcon;
    Text _weaponName;
    Image _amuletIcon;
    Text _amuletName;
    RectTransform _bagButton;

    public Action<Vector2> OnBagClicked;
    public RectTransform BagButtonRect => _bagButton;

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

        var slots = UiFactory.NewRect(panel.transform, "Slots");
        slots.anchorMin = new Vector2(0f, 0.38f);
        slots.anchorMax = new Vector2(1f, 1f);
        slots.offsetMin = new Vector2(10f, 4f);
        slots.offsetMax = new Vector2(-10f, -30f);

        view.MakeSlot(slots, "Weapon", 0f, out view._weaponIcon, out view._weaponName);
        view.MakeSlot(slots, "Amulet", 0.5f, out view._amuletIcon, out view._amuletName);

        var bag = UiFactory.Panel(panel.transform, "Bag", UiFactory.SlotColor);
        bag.raycastTarget = true;
        var bagRt = bag.rectTransform;
        bagRt.anchorMin = new Vector2(0.18f, 0.06f);
        bagRt.anchorMax = new Vector2(0.82f, 0.34f);
        bagRt.offsetMin = Vector2.zero;
        bagRt.offsetMax = Vector2.zero;
        view._bagButton = bagRt;

        var bagIcon = UiFactory.Graphic(
            bag.transform,
            "Icon",
            PlaceholderArt.Shape(ShapeKind.Mound, new Color(0.55f, 0.38f, 0.22f), 64, 64),
            Color.white
        );
        bagIcon.rectTransform.anchorMin = new Vector2(0.08f, 0.18f);
        bagIcon.rectTransform.anchorMax = new Vector2(0.38f, 0.88f);
        bagIcon.rectTransform.offsetMin = Vector2.zero;
        bagIcon.rectTransform.offsetMax = Vector2.zero;

        var bagLabel = UiFactory.Label(bag.transform, "Label", "BAG", 16, TextAnchor.MiddleLeft, UiFactory.TextColor);
        bagLabel.fontStyle = FontStyle.Bold;
        bagLabel.rectTransform.anchorMin = new Vector2(0.40f, 0f);
        bagLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        bagLabel.rectTransform.offsetMin = Vector2.zero;
        bagLabel.rectTransform.offsetMax = Vector2.zero;

        var button = bag.gameObject.AddComponent<Button>();
        button.targetGraphic = bag;
        button.onClick.AddListener(() => view.OnBagClicked?.Invoke(PointerScreenPoint()));

        return view;
    }

    void MakeSlot(RectTransform parent, string title, float xMin, out Image icon, out Text name)
    {
        var cell = UiFactory.Panel(parent, title, UiFactory.SlotColor);
        cell.raycastTarget = false;
        var rt = cell.rectTransform;
        rt.anchorMin = new Vector2(xMin, 0f);
        rt.anchorMax = new Vector2(xMin + 0.5f, 1f);
        rt.offsetMin = new Vector2(4f, 0f);
        rt.offsetMax = new Vector2(-4f, 0f);

        var header = UiFactory.Label(
            cell.transform,
            "SlotTitle",
            title,
            12,
            TextAnchor.UpperCenter,
            UiFactory.MutedColor
        );
        header.rectTransform.anchorMin = new Vector2(0f, 1f);
        header.rectTransform.anchorMax = new Vector2(1f, 1f);
        header.rectTransform.pivot = new Vector2(0.5f, 1f);
        header.rectTransform.sizeDelta = new Vector2(-6f, 16f);
        header.rectTransform.anchoredPosition = new Vector2(0f, -4f);

        icon = UiFactory.Graphic(cell.transform, "Icon", PlaceholderArt.Solid(Color.white), Color.white);
        icon.rectTransform.anchorMin = new Vector2(0.5f, 0.42f);
        icon.rectTransform.anchorMax = new Vector2(0.5f, 0.42f);
        icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icon.rectTransform.sizeDelta = new Vector2(28f, 28f);

        name = UiFactory.Label(cell.transform, "Name", EmptyCaption, 13, TextAnchor.UpperCenter, UiFactory.MutedColor);
        name.rectTransform.anchorMin = new Vector2(0f, 0f);
        name.rectTransform.anchorMax = new Vector2(1f, 0.38f);
        name.rectTransform.offsetMin = new Vector2(4f, 2f);
        name.rectTransform.offsetMax = new Vector2(-4f, 0f);
        name.horizontalOverflow = HorizontalWrapMode.Wrap;
        name.verticalOverflow = VerticalWrapMode.Truncate;
        name.resizeTextForBestFit = true;
        name.resizeTextMinSize = 8;
        name.resizeTextMaxSize = 13;
    }

    public void Render()
    {
        Fill(_weaponIcon, _weaponName, GameManager.EquippedIn(EquipSlot.Weapon));
        Fill(_amuletIcon, _amuletName, GameManager.EquippedIn(EquipSlot.Amulet));
    }

    static void Fill(Image icon, Text name, PlayerItem worn)
    {
        var def = worn == null ? null : GameManager.ItemDefOf(worn);
        if (def == null)
        {
            name.text = EmptyCaption;
            name.color = UiFactory.MutedColor;
            icon.sprite = PlaceholderArt.Shape(ShapeKind.Rect, new Color(0.35f, 0.35f, 0.40f, 0.35f), 32, 32);
            icon.color = Color.white;
            return;
        }

        name.text = def.Name;
        name.color = UiFactory.TextColor;
        icon.sprite = GearArt.Sprite(def);
        icon.color = Color.white;
    }

    static Vector2 PointerScreenPoint()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current == null ? (Vector2)Input.mousePosition : Mouse.current.position.ReadValue();
#else
        return Input.mousePosition;
#endif
    }
}

/// Code-generated placeholder icons for weapons (tinted rect) and amulets (circle).
public static class GearArt
{
    public static Sprite Sprite(ItemDef def)
    {
        if (def == null)
        {
            return PlaceholderArt.Shape(ShapeKind.Rect, new Color(0.35f, 0.35f, 0.40f, 0.35f), 48, 48);
        }

        if (def.Kind == ItemKind.Amulet)
        {
            return AmuletArt.Icon(def.Name)
                ?? PlaceholderArt.Shape(ShapeKind.Circle, new Color(0.82f, 0.62f, 0.28f), 48, 48);
        }

        return PlaceholderArt.Shape(ShapeKind.Rect, WeaponColor(def.WeaponType), 48, 48);
    }

    public static Color WeaponColor(WeaponType type) =>
        type switch
        {
            WeaponType.Sword => PlaceholderArt.ClassColor("Knight"),
            WeaponType.Staff => PlaceholderArt.ClassColor("Mage"),
            WeaponType.Katana => PlaceholderArt.ClassColor("Ninja"),
            WeaponType.Bow => PlaceholderArt.ClassColor("Archer"),
            _ => UiFactory.MutedColor,
        };
}
