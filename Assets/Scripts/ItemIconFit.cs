using UnityEngine;
using UnityEngine.UI;

/// Shared item-icon layout: one icon band plus a name strip so equipment
/// slots and inventory cells cannot drift apart.
public static class ItemIconFit
{
    /// Icon occupies this fraction of the shorter slot side.
    public const float AreaFraction = 0.62f;
    public const float EquipmentHeaderBand = 0.20f;
    public const float EquipmentNameBand = 0.30f;
    public const float InventoryNameBand = 0.36f;

    public static void LayoutEquipment(RectTransform icon, Text name)
    {
        PlaceIconBand(icon, EquipmentHeaderBand, EquipmentNameBand);
        PlaceNameStrip(name, EquipmentNameBand, 8, 13);
    }

    public static void LayoutInventory(RectTransform icon, Text name)
    {
        PlaceIconBand(icon, 0f, InventoryNameBand);
        PlaceNameStrip(name, InventoryNameBand, 7, 10);
    }

    public static void Bind(Image icon, Sprite sprite)
    {
        icon.sprite = sprite;
        icon.color = Color.white;
        icon.preserveAspect = true;
        icon.type = Image.Type.Simple;
        icon.raycastTarget = false;
        FitInBand(icon.rectTransform);
    }

    static void PlaceIconBand(RectTransform icon, float headerBand, float nameBand)
    {
        var inset = (1f - AreaFraction) * 0.5f;
        icon.anchorMin = new Vector2(inset, nameBand);
        icon.anchorMax = new Vector2(1f - inset, 1f - headerBand);
        icon.pivot = new Vector2(0.5f, 0.5f);
        icon.offsetMin = Vector2.zero;
        icon.offsetMax = Vector2.zero;
        icon.anchoredPosition = Vector2.zero;
        icon.sizeDelta = Vector2.zero;
    }

    static void PlaceNameStrip(Text name, float nameBand, int minSize, int maxSize)
    {
        name.rectTransform.anchorMin = new Vector2(0f, 0f);
        name.rectTransform.anchorMax = new Vector2(1f, nameBand);
        name.rectTransform.offsetMin = new Vector2(3f, 2f);
        name.rectTransform.offsetMax = new Vector2(-3f, 0f);
        name.alignment = TextAnchor.UpperCenter;
        name.horizontalOverflow = HorizontalWrapMode.Wrap;
        name.verticalOverflow = VerticalWrapMode.Truncate;
        name.resizeTextForBestFit = true;
        name.resizeTextMinSize = minSize;
        name.resizeTextMaxSize = maxSize;
        name.raycastTarget = false;
    }

    static void FitInBand(RectTransform icon)
    {
        icon.offsetMin = Vector2.zero;
        icon.offsetMax = Vector2.zero;
        icon.sizeDelta = Vector2.zero;
        icon.anchoredPosition = Vector2.zero;
    }
}
