using System.Collections.Generic;
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
    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
    static Font _font;

    public static Font UiFont
    {
        get
        {
            if (_font != null)
            {
                return _font;
            }

            foreach (var builtin in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try
                {
                    _font = Resources.GetBuiltinResource<Font>(builtin);
                }
                catch
                {
                    _font = null;
                }

                if (_font != null)
                {
                    return _font;
                }
            }

            _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            return _font;
        }
    }

    public static Sprite Solid(Color color) => Shape(ShapeKind.Rect, color, 8, 8);

    /// 9-slice rounded panel so popups can be any size without stretching corners.
    public static Sprite RoundedSlice(Color color, int size = 64, int radius = 14)
    {
        var key = $"round:{ColorUtility.ToHtmlStringRGBA(color)}:{size}:{radius}";
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        var pixels = new Color32[size * size];
        var outline = new Color(color.r * 0.45f, color.g * 0.45f, color.b * 0.45f, 1f);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dist = RoundedRectDistance(x + 0.5f, y + 0.5f, size, radius);
                if (dist > 1.2f)
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                }
                else if (dist > 0f)
                {
                    pixels[y * size + x] = (Color32)outline;
                }
                else
                {
                    pixels[y * size + x] = (Color32)color;
                }
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        var border = radius + 2f;
        var sprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border)
        );
        Cache[key] = sprite;
        return sprite;
    }

    static float RoundedRectDistance(float x, float y, int size, int radius)
    {
        var min = radius + 1f;
        var max = size - radius - 1f;
        var cx = Mathf.Clamp(x, min, max);
        var cy = Mathf.Clamp(y, min, max);
        if (x >= min && x <= max || y >= min && y <= max)
        {
            var insideX = x >= 1f && x <= size - 1f;
            var insideY = y >= 1f && y <= size - 1f;
            return insideX && insideY ? -1f : 1f;
        }

        var dx = x - cx;
        var dy = y - cy;
        return Mathf.Sqrt((dx * dx) + (dy * dy)) - radius;
    }

    /// Full-screen backdrop tint. Generated so the project still needs no art files.
    public static Sprite VerticalGradient(Color top, Color bottom, int height = 256)
    {
        var key = $"grad:{ColorUtility.ToHtmlStringRGBA(top)}:{ColorUtility.ToHtmlStringRGBA(bottom)}:{height}";
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var texture = new Texture2D(4, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        var pixels = new Color32[4 * height];
        for (var y = 0; y < height; y++)
        {
            var t = height <= 1 ? 0f : (float)y / (height - 1);
            var color = (Color32)Color.Lerp(bottom, top, t);
            pixels[y * 4] = color;
            pixels[y * 4 + 1] = color;
            pixels[y * 4 + 2] = color;
            pixels[y * 4 + 3] = color;
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        var sprite = Sprite.Create(
            texture,
            new Rect(0, 0, 4, height),
            new Vector2(0.5f, 0.5f),
            100f
        );
        Cache[key] = sprite;
        return sprite;
    }

    public static Sprite Shape(ShapeKind kind, Color color, int width = 128, int height = 128)
    {
        var key = $"{kind}:{ColorUtility.ToHtmlStringRGBA(color)}:{width}x{height}";
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        var pixels = new Color32[width * height];
        var outline = new Color(color.r * 0.45f, color.g * 0.45f, color.b * 0.45f, 1f);
        var stepX = 2f / width;
        var stepY = 2f / height;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var u = (x + 0.5f) / width;
                var v = (y + 0.5f) / height;

                if (!Inside(kind, u, v))
                {
                    pixels[y * width + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                var interior =
                    Inside(kind, u + stepX, v)
                    && Inside(kind, u - stepX, v)
                    && Inside(kind, u, v + stepY)
                    && Inside(kind, u, v - stepY);

                pixels[y * width + x] = interior ? (Color32)color : (Color32)outline;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        var sprite = Sprite.Create(
            texture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            100f
        );
        Cache[key] = sprite;
        return sprite;
    }

    static bool Inside(ShapeKind kind, float u, float v)
    {
        if (u < 0f || u > 1f || v < 0f || v > 1f)
        {
            return false;
        }

        switch (kind)
        {
            case ShapeKind.Rect:
                return true;

            case ShapeKind.Circle:
            {
                var dx = u - 0.5f;
                var dy = v - 0.5f;
                return (dx * dx) + (dy * dy) <= 0.25f;
            }

            case ShapeKind.Diamond:
                return Mathf.Abs(u - 0.5f) + Mathf.Abs(v - 0.5f) <= 0.5f;

            case ShapeKind.Triangle:
                return Mathf.Abs(u - 0.5f) <= 0.5f * (1f - v);

            case ShapeKind.Mound:
            {
                if (v < 0.04f)
                {
                    return false;
                }

                var dx = (u - 0.5f) / 0.5f;
                var dy = (v - 0.04f) / 0.96f;
                return (dx * dx) + (dy * dy) <= 1f;
            }

            default:
                return false;
        }
    }

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
        image.sprite = PlaceholderArt.Solid(Color.white);
        image.color = color;
        image.type = Image.Type.Sliced;
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
        label.font = PlaceholderArt.UiFont;
        label.text = text;
        label.fontSize = size;
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

    /// Background track plus a left-to-right fill. Stretches to fill `parent`,
    /// so the caller only has to size the row.
    public static Image Bar(Transform parent, string name, Color fillColor, out Text valueText)
    {
        var track = NewRect(parent, name);
        Anchor(track, Vector2.zero, Vector2.one);
        var trackImage = track.gameObject.AddComponent<Image>();
        trackImage.sprite = PlaceholderArt.Solid(Color.white);
        trackImage.color = new Color(0.07f, 0.07f, 0.09f, 0.72f);
        trackImage.type = Image.Type.Simple;
        trackImage.raycastTarget = false;

        var fillRt = NewRect(track, "Fill");
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(2f, 2f);
        fillRt.offsetMax = new Vector2(-2f, -2f);
        var fill = fillRt.gameObject.AddComponent<Image>();
        fill.sprite = PlaceholderArt.Solid(Color.white);
        fill.color = fillColor;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
        fill.raycastTarget = false;

        valueText = Label(track, "Value", "", 12, TextAnchor.MiddleCenter, TextColor);
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
    }
}
