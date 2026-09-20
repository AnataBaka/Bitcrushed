using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Pixel-art ACTIVE silhouette halo. Generated from the sprite's opaque mask
/// and drawn behind the body. Per-band alpha is the texture-audit exception:
/// texels are otherwise fully opaque or fully transparent.
public class ActiveHalo : MonoBehaviour
{
    public const int HaloWidth = 3;

    static readonly Color32[] Bands =
    {
        ToBand(0.90f),
        ToBand(0.55f),
        ToBand(0.28f),
    };

    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    RectTransform _rt;
    Image _image;
    Image _source;
    Sprite _lastSprite;
    Vector2 _lastSize;
    bool _visible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Cache.Clear();
    }

    static Color32 ToBand(float alpha)
    {
        var c = UiFactory.ActiveColor;
        return new Color32(
            (byte)Mathf.RoundToInt(c.r * 255f),
            (byte)Mathf.RoundToInt(c.g * 255f),
            (byte)Mathf.RoundToInt(c.b * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f)
        );
    }

    public static ActiveHalo Attach(RectTransform card, Image source)
    {
        var rt = UiFactory.NewRect(card, "ActiveHalo");
        rt.SetAsFirstSibling();
        var image = rt.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.color = Color.white;
        image.useSpriteMesh = false;
        var halo = rt.gameObject.AddComponent<ActiveHalo>();
        halo._rt = rt;
        halo._image = image;
        halo._source = source;
        halo.SetVisible(false);
        return halo;
    }

    public void SetVisible(bool visible)
    {
        _visible = visible;
        gameObject.SetActive(visible);
        if (visible)
        {
            FollowSource();
            Rebuild(false);
        }
    }

    void LateUpdate()
    {
        if (!_visible || _source == null)
        {
            return;
        }

        FollowSource();
        Rebuild(false);
    }

    void FollowSource()
    {
        if (_source == null || _rt == null)
        {
            return;
        }

        var src = _source.rectTransform;
        _rt.anchorMin = src.anchorMin;
        _rt.anchorMax = src.anchorMax;
        _rt.pivot = src.pivot;
        _rt.localScale = src.localScale;
        _rt.localRotation = src.localRotation;

        var pad = HaloWidth * 2f;
        _rt.sizeDelta = src.sizeDelta + new Vector2(pad, pad);
        _rt.anchoredPosition =
            src.anchoredPosition
            + new Vector2(pad * (src.pivot.x - 0.5f), pad * (src.pivot.y - 0.5f));
    }

    void Rebuild(bool force)
    {
        if (_source == null || _image == null)
        {
            return;
        }

        var sprite = _source.sprite;
        var size = _source.rectTransform.sizeDelta;
        if (!force && sprite == _lastSprite && size == _lastSize)
        {
            return;
        }

        _lastSprite = sprite;
        _lastSize = size;
        _image.sprite = sprite == null ? null : HaloFor(sprite);
    }

    static Sprite HaloFor(Sprite sprite)
    {
        var key = Key(sprite);
        if (Cache.TryGetValue(key, out var cached) && cached != null)
        {
            return cached;
        }

        if (!TryReadMask(sprite, out var mask, out var width, out var height))
        {
            return null;
        }

        var halo = BuildHalo(mask, width, height, sprite.pixelsPerUnit);
        Cache[key] = halo;
        return halo;
    }

    static string Key(Sprite sprite)
    {
        var tex = sprite.texture;
        var rect = sprite.textureRect;
        var texId = tex != null ? tex.GetEntityId() : default;
        return $"{texId}:{sprite.GetEntityId()}:{rect.x:0}:{rect.y:0}:{rect.width:0}:{rect.height:0}";
    }

    static bool TryReadMask(Sprite sprite, out bool[] mask, out int width, out int height)
    {
        mask = null;
        width = 0;
        height = 0;
        if (sprite == null || sprite.texture == null)
        {
            return false;
        }

        var tex = sprite.texture;
        var spriteRect = sprite.rect;
        width = Mathf.Max(1, Mathf.RoundToInt(spriteRect.width));
        height = Mathf.Max(1, Mathf.RoundToInt(spriteRect.height));
        var packed = sprite.textureRect;
        var packW = Mathf.Max(1, Mathf.RoundToInt(packed.width));
        var packH = Mathf.Max(1, Mathf.RoundToInt(packed.height));
        var x0 = Mathf.RoundToInt(packed.x);
        var y0 = Mathf.RoundToInt(packed.y);
        var offset = sprite.textureRectOffset;
        var destX = Mathf.RoundToInt(offset.x);
        var destY = Mathf.RoundToInt(offset.y);

        Color32[] pixels;
        int texW;
        int texH;
        if (tex.isReadable)
        {
            pixels = tex.GetPixels32();
            texW = tex.width;
            texH = tex.height;
        }
        else if (!TryCopyPixels(tex, out pixels, out texW, out texH))
        {
            return false;
        }

        mask = new bool[width * height];
        for (var y = 0; y < packH; y++)
        {
            var srcY = y0 + y;
            var canvasY = destY + y;
            if (srcY < 0 || srcY >= texH || canvasY < 0 || canvasY >= height)
            {
                continue;
            }

            for (var x = 0; x < packW; x++)
            {
                var srcX = x0 + x;
                var canvasX = destX + x;
                if (srcX < 0 || srcX >= texW || canvasX < 0 || canvasX >= width)
                {
                    continue;
                }

                mask[canvasY * width + canvasX] = pixels[srcY * texW + srcX].a >= 128;
            }
        }

        return true;
    }

    static bool TryCopyPixels(Texture tex, out Color32[] pixels, out int width, out int height)
    {
        pixels = null;
        width = tex.width;
        height = tex.height;
        RenderTexture tmp = null;
        Texture2D copy = null;
        try
        {
            tmp = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, tmp);
            var prev = RenderTexture.active;
            RenderTexture.active = tmp;
            copy = PlaceholderArt.PixelTexture(width, height);
            copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            pixels = copy.GetPixels32();
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (tmp != null)
            {
                RenderTexture.ReleaseTemporary(tmp);
            }

            if (copy != null)
            {
                Object.Destroy(copy);
            }
        }
    }

    static Sprite BuildHalo(bool[] mask, int width, int height, float pixelsPerUnit)
    {
        var pad = HaloWidth;
        var outW = width + pad * 2;
        var outH = height + pad * 2;
        var dist = new byte[outW * outH];
        for (var i = 0; i < dist.Length; i++)
        {
            dist[i] = 255;
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (mask[y * width + x])
                {
                    dist[(y + pad) * outW + (x + pad)] = 0;
                }
            }
        }

        for (byte band = 1; band <= HaloWidth; band++)
        {
            var prev = (byte)(band - 1);
            for (var y = 0; y < outH; y++)
            {
                for (var x = 0; x < outW; x++)
                {
                    var i = y * outW + x;
                    if (dist[i] != 255)
                    {
                        continue;
                    }

                    if (HasNeighbor(dist, outW, outH, x, y, prev))
                    {
                        dist[i] = band;
                    }
                }
            }
        }

        var pixels = new Color32[outW * outH];
        for (var i = 0; i < dist.Length; i++)
        {
            var d = dist[i];
            if (d >= 1 && d <= HaloWidth)
            {
                pixels[i] = Bands[d - 1];
            }
        }

        var texture = PixelStyle.Texture(outW, outH);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return PixelStyle.Sprite(
            texture,
            new Vector2(0.5f, 0.5f),
            Mathf.Max(1f, pixelsPerUnit)
        );
    }

    static bool HasNeighbor(byte[] dist, int width, int height, int x, int y, byte value)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            var ny = y + dy;
            if (ny < 0 || ny >= height)
            {
                continue;
            }

            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                var nx = x + dx;
                if (nx < 0 || nx >= width)
                {
                    continue;
                }

                if (dist[ny * width + nx] == value)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
