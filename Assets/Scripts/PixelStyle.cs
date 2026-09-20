using System.Collections.Generic;
using UnityEngine;

/// One place every runtime-generated Texture2D and Sprite is built.
/// Logical pixels match boldpixels' 8px cell. Output is Point-filtered,
/// clamp-wrapped, uncompressed, and mip-free. Texels are fully opaque or
/// fully transparent unless the caller writes a translucent overlay color.
public static class PixelStyle
{
    /// Same as GameFont.Pixel. Generated art is decided on this grid and
    /// stamped as Pixel×Pixel blocks so aliasing is obvious.
    public const int Pixel = 8;

    public const int PixelsPerUnit = 100;

    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Cache.Clear();
    }

    public static int Snap(int value)
    {
        if (value <= 0)
        {
            return Pixel;
        }

        return Mathf.Max(Pixel, Mathf.RoundToInt(value / (float)Pixel) * Pixel);
    }

    public static Texture2D Texture(int width, int height)
    {
        return new Texture2D(Mathf.Max(1, width), Mathf.Max(1, height), TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
    }

    public static Sprite Sprite(
        Texture2D texture,
        Vector2 pivot,
        Vector4 border = default
    )
    {
        return Sprite(texture, pivot, PixelsPerUnit, border);
    }

    public static Sprite Sprite(
        Texture2D texture,
        Vector2 pivot,
        float pixelsPerUnit,
        Vector4 border = default
    )
    {
        if (texture == null)
        {
            return null;
        }

        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        return UnityEngine.Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            pivot,
            Mathf.Max(1f, pixelsPerUnit),
            0,
            SpriteMeshType.FullRect,
            border
        );
    }

    /// Keep the caller's alpha (Esc dim, inventory cells, empty slot wash).
    public static Color32 KeepAlpha(Color color)
    {
        return (Color32)color;
    }

    public static Sprite FillWhite()
    {
        const string key = "fill:white:2";
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var texture = Texture(2, 2);
        var white = new Color32(255, 255, 255, 255);
        texture.SetPixels32(new[] { white, white, white, white });
        texture.Apply(false, false);
        var sprite = Sprite(texture, new Vector2(0.5f, 0.5f));
        Cache[key] = sprite;
        return sprite;
    }

    /// Chunk 9-slice: one 8px square cut per corner, 8px rim, Point-scaled
    /// blocks. No circular arcs. Fits the 40px name pills without squashing.
    public static Sprite RoundedSlice(Color color, int size = 64, int radius = 14)
    {
        _ = radius;
        size = Mathf.Max(Pixel * 4, Snap(size));
        var cells = size / Pixel;
        // One 8px stair per corner. Two steps would make the 9-slice border
        // 24px and squash the 40px name pills.
        var notch = 1;
        notch = Mathf.Clamp(notch, 1, (cells / 2) - 1);
        var key = $"round:{ColorUtility.ToHtmlStringRGBA(color)}:{size}:{notch}";
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var texture = Texture(size, size);
        var pixels = new Color32[size * size];
        var fill = KeepAlpha(color);
        var outline = KeepAlpha(new Color(color.r * 0.45f, color.g * 0.45f, color.b * 0.45f, color.a));
        var clear = new Color32(0, 0, 0, 0);

        for (var ly = 0; ly < cells; ly++)
        {
            for (var lx = 0; lx < cells; lx++)
            {
                if (!InNotched(lx, ly, cells, notch))
                {
                    Stamp(pixels, size, size, lx, ly, clear);
                    continue;
                }

                var edge =
                    !InNotched(lx - 1, ly, cells, notch)
                    || !InNotched(lx + 1, ly, cells, notch)
                    || !InNotched(lx, ly - 1, cells, notch)
                    || !InNotched(lx, ly + 1, cells, notch);
                Stamp(pixels, size, size, lx, ly, edge ? outline : fill);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        var border = (notch + 1) * Pixel;
        var sprite = Sprite(texture, new Vector2(0.5f, 0.5f), new Vector4(border, border, border, border));
        Cache[key] = sprite;
        return sprite;
    }

    /// One 8px square notch per corner. No quarter-circles.
    static bool InNotched(int x, int y, int size, int notch)
    {
        if (x < 0 || y < 0 || x >= size || y >= size)
        {
            return false;
        }

        var dx = Mathf.Min(x, size - 1 - x);
        var dy = Mathf.Min(y, size - 1 - y);
        if (dx < notch && dy < notch)
        {
            return dx + dy >= notch;
        }

        return true;
    }

    static void Stamp(Color32[] pixels, int width, int height, int lx, int ly, Color32 color)
    {
        var x0 = lx * Pixel;
        var y0 = ly * Pixel;
        for (var y = 0; y < Pixel; y++)
        {
            var py = y0 + y;
            if (py < 0 || py >= height)
            {
                continue;
            }

            for (var x = 0; x < Pixel; x++)
            {
                var px = x0 + x;
                if (px < 0 || px >= width)
                {
                    continue;
                }

                pixels[py * width + px] = color;
            }
        }
    }

    /// 1-texel outlined panel, Point-filtered 9-slice.
    public static Sprite BorderSlice(Color fill, Color border, int size = 12)
    {
        size = Mathf.Max(4, size);
        var key = $"border:{ColorUtility.ToHtmlStringRGBA(fill)}:{ColorUtility.ToHtmlStringRGBA(border)}:{size}";
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var texture = Texture(size, size);
        var pixels = new Color32[size * size];
        var fill32 = KeepAlpha(fill);
        var border32 = KeepAlpha(border);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                pixels[y * size + x] = edge ? border32 : fill32;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        var sprite = Sprite(texture, new Vector2(0.5f, 0.5f), Vector4.one);
        Cache[key] = sprite;
        return sprite;
    }

    /// 4 flat bands from the original endpoints. No dithering.
    public static Sprite BandedVertical(Color top, Color bottom, int height = 256, int bands = 4)
    {
        bands = Mathf.Clamp(bands, 2, 4);
        height = Mathf.Max(bands, height);
        var key = $"band:{ColorUtility.ToHtmlStringRGBA(top)}:{ColorUtility.ToHtmlStringRGBA(bottom)}:{height}:{bands}";
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var width = Pixel;
        var texture = Texture(width, height);
        var pixels = new Color32[width * height];
        var stops = new Color32[bands];
        for (var i = 0; i < bands; i++)
        {
            var t = bands == 1 ? 0f : i / (float)(bands - 1);
            stops[i] = KeepAlpha(Color.Lerp(bottom, top, t));
        }

        for (var y = 0; y < height; y++)
        {
            var band = Mathf.Min(bands - 1, (y * bands) / height);
            var color = stops[band];
            for (var x = 0; x < width; x++)
            {
                pixels[y * width + x] = color;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        var sprite = Sprite(texture, new Vector2(0.5f, 0.5f));
        Cache[key] = sprite;
        return sprite;
    }

    public static Sprite Shape(ShapeKind kind, Color color, int width = 128, int height = 128)
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        var key = $"shape:{kind}:{ColorUtility.ToHtmlStringRGBA(color)}:{width}x{height}";
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var texture = Texture(width, height);
        var pixels = new Color32[width * height];
        var fill = KeepAlpha(color);
        var outline = KeepAlpha(new Color(color.r * 0.45f, color.g * 0.45f, color.b * 0.45f, color.a));
        var clear = new Color32(0, 0, 0, 0);
        var lw = Mathf.Max(1, width / Pixel);
        var lh = Mathf.Max(1, height / Pixel);

        for (var ly = 0; ly < lh; ly++)
        {
            for (var lx = 0; lx < lw; lx++)
            {
                if (!Inside(kind, lx, ly, lw, lh))
                {
                    Stamp(pixels, width, height, lx, ly, clear);
                    continue;
                }

                var interior =
                    Inside(kind, lx - 1, ly, lw, lh)
                    && Inside(kind, lx + 1, ly, lw, lh)
                    && Inside(kind, lx, ly - 1, lw, lh)
                    && Inside(kind, lx, ly + 1, lw, lh);
                Stamp(pixels, width, height, lx, ly, interior ? fill : outline);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        var sprite = Sprite(texture, new Vector2(0.5f, 0.5f));
        Cache[key] = sprite;
        return sprite;
    }

    static bool Inside(ShapeKind kind, int x, int y, int width, int height)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return false;
        }

        var u = (x + 0.5f) / width;
        var v = (y + 0.5f) / height;
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
}
