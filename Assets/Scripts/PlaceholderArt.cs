using UnityEngine;
using UnityEngine.UI;

public enum ShapeKind
{
    Rect,
    Circle,
    Triangle,
    Diamond,
    Mound,
}

/// Every sprite and font in the battle scene is produced here at runtime so the
/// project needs no imported art assets.
public static class PlaceholderArt
{
    public static Texture2D PixelTexture(int width, int height) => PixelStyle.Texture(width, height);

    public static Sprite Solid(Color color) => Shape(ShapeKind.Rect, color, 8, 8);

    public static Sprite FlatWhite() => PixelStyle.FillWhite();

    public static Sprite RoundedSlice(Color color, int size = 64, int radius = 14) =>
        PixelStyle.RoundedSlice(color, size, radius);

    public static Sprite PixelSlice(Color fill, Color border, int size = 12) =>
        PixelStyle.BorderSlice(fill, border, size);

    public static Sprite VerticalGradient(Color top, Color bottom, int height = 256) =>
        PixelStyle.BandedVertical(top, bottom, height);

    public static Sprite Shape(ShapeKind kind, Color color, int width = 128, int height = 128) =>
        PixelStyle.Shape(kind, color, width, height);

    public static Color ClassColor(string className)
    {
        switch (className)
        {
            case "Knight":
            case "Warrior":
                return new Color(0.80f, 0.29f, 0.25f);
            case "Mage":
                return new Color(0.36f, 0.44f, 0.86f);
            case "Ninja":
            case "Rogue":
                return new Color(0.28f, 0.66f, 0.40f);
            case "Archer":
                return new Color(0.88f, 0.66f, 0.22f);
            default:
                return new Color(0.55f, 0.35f, 0.62f);
        }
    }

    public static ShapeKind ClassShape(string className)
    {
        switch (className)
        {
            case "Knight":
            case "Warrior":
                return ShapeKind.Rect;
            case "Mage":
                return ShapeKind.Diamond;
            case "Ninja":
            case "Rogue":
                return ShapeKind.Triangle;
            case "Archer":
                return ShapeKind.Circle;
            default:
                return ShapeKind.Circle;
        }
    }

    public static Color EnemyColor(uint slot) =>
        slot == 0 ? new Color(0.44f, 0.56f, 0.28f) : new Color(0.50f, 0.34f, 0.26f);

    public static (ShapeKind Shape, Color Color) EnemyVisual(string kind)
    {
        switch (kind)
        {
            case "Goblin":
                return (ShapeKind.Mound, new Color(0.44f, 0.56f, 0.28f));
            case "Troll":
                return (ShapeKind.Rect, new Color(0.50f, 0.34f, 0.26f));
            case "Shade":
                return (ShapeKind.Diamond, new Color(0.42f, 0.28f, 0.62f));
            case "Wolf":
                return (ShapeKind.Triangle, new Color(0.62f, 0.62f, 0.66f));
            case "Skeleton":
                return (ShapeKind.Circle, new Color(0.86f, 0.82f, 0.70f));
            case "MagmaColossus":
                return (ShapeKind.Diamond, new Color(0.95f, 0.32f, 0.06f));
            default:
                return (ShapeKind.Mound, new Color(0.50f, 0.34f, 0.26f));
        }
    }
}

/// Small helpers for hand-building uGUI hierarchies from code.
public static class UiFactory
{
    public const float ButtonHeight = 54f;

    public static readonly Color PanelColor = new Color(0.12f, 0.12f, 0.15f, 0.92f);
    public static readonly Color SlotColor = new Color(0.22f, 0.22f, 0.27f, 1f);
    public static readonly Color TextColor = new Color(0.94f, 0.94f, 0.90f, 1f);
    public static readonly Color MutedColor = new Color(0.62f, 0.62f, 0.66f, 1f);
    public static readonly Color HpColor = new Color(0.78f, 0.25f, 0.25f, 0.95f);
    public static readonly Color ManaColor = new Color(0.30f, 0.50f, 0.88f, 0.95f);
    public static readonly Color ActiveColor = new Color(0.95f, 0.82f, 0.30f, 1f);

    public static RectTransform NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    public static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public static Image Panel(Transform parent, string name, Color color)
    {
        var rt = NewRect(parent, name);
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = PlaceholderArt.FlatWhite();
        image.color = color;
        image.type = Image.Type.Simple;
        rt.gameObject.AddComponent<RectMask2D>();
        return image;
    }

    public static Image RoundedPanel(Transform parent, string name, Color color)
    {
        var rt = NewRect(parent, name);
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = PlaceholderArt.RoundedSlice(Color.white);
        image.color = color;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1.15f;
        return image;
    }

    public static Image Graphic(Transform parent, string name, Sprite sprite, Color color)
    {
        var rt = NewRect(parent, name);
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    public static Text Label(
        Transform parent,
        string name,
        string text,
        int size,
        TextAnchor anchor,
        Color color
    )
    {
        var rt = NewRect(parent, name);
        var label = rt.gameObject.AddComponent<Text>();
        label.font = GameFont.Ui;
        label.text = text;
        label.fontSize = GameFont.Resolve(size);
        label.alignment = anchor;
        label.color = color;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        return label;
    }

    public static Button TextButton(
        Transform parent,
        string name,
        string caption,
        int size = 24,
        float height = ButtonHeight
    )
    {
        var image = Panel(parent, name, SlotColor);
        image.raycastTarget = true;
        image.rectTransform.sizeDelta = new Vector2(image.rectTransform.sizeDelta.x, height);

        // A layout group sizes children from their preferred height, and an
        // Image's preferred height comes from its sprite (8px here). Without an
        // explicit LayoutElement the button collapses to a sliver that renders
        // its overflowing caption but cannot be clicked.
        var layout = image.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.flexibleHeight = 0f;

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        button.colors = colors;

        var label = Label(image.transform, "Label", caption, size, TextAnchor.MiddleCenter, TextColor);
        Anchor(label.rectTransform, Vector2.zero, Vector2.one);
        return button;
    }

    /// Dark track with a single solid fill edge to edge. No inset highlight.
    public static Image Bar(Transform parent, string name, Color fillColor, out Text valueText)
    {
        var track = NewRect(parent, name);
        Anchor(track, Vector2.zero, Vector2.one);
        var trackImage = track.gameObject.AddComponent<Image>();
        trackImage.sprite = PlaceholderArt.FlatWhite();
        trackImage.color = new Color(0.07f, 0.07f, 0.09f, 0.72f);
        trackImage.type = Image.Type.Simple;
        trackImage.raycastTarget = false;

        var fillRt = NewRect(track, "Fill");
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        var fill = fillRt.gameObject.AddComponent<Image>();
        fill.sprite = PlaceholderArt.FlatWhite();
        fill.color = fillColor;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
        fill.raycastTarget = false;

        valueText = Label(track, "Value", "", 8, TextAnchor.MiddleCenter, TextColor);
        Anchor(valueText.rectTransform, Vector2.zero, Vector2.one);
        return fill;
    }

    public static void SetBar(Image fill, int current, int max)
    {
        if (fill == null)
        {
            return;
        }

        var pct = max <= 0 ? 0f : Mathf.Clamp01((float)current / max);
        if (fill.type != Image.Type.Filled)
        {
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        fill.fillAmount = pct;
        fill.enabled = pct > 0.0001f;
    }
}
