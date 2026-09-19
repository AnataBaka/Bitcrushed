using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// Short-lived slash / shockwave overlays so a hit reads harder than a sprite
/// swap. Built from code so the project still does not need extra art files.
public class CombatVfx : MonoBehaviour
{
    public static void Spawn(RectTransform field, RectTransform at, bool heavy)
    {
        if (field == null || at == null)
        {
            return;
        }

        var go = new GameObject(heavy ? "HeavyImpact" : "Impact", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(field, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        rt.position = at.position;
        rt.SetAsLastSibling();

        var vfx = go.AddComponent<CombatVfx>();
        vfx.StartCoroutine(vfx.Play(rt, heavy));
    }

    IEnumerator Play(RectTransform root, bool heavy)
    {
        var slash = MakeGraphic(root, SlashSprite(), heavy ? 220f : 150f, new Color(1f, 0.95f, 0.75f, 1f));
        slash.localRotation = Quaternion.Euler(0f, 0f, heavy ? -38f : -28f);

        RectTransform slash2 = null;
        if (heavy)
        {
            slash2 = MakeGraphic(root, SlashSprite(), 180f, new Color(1f, 0.7f, 0.25f, 0.95f));
            slash2.localRotation = Quaternion.Euler(0f, 0f, 22f);
        }

        var ring = MakeGraphic(root, RingSprite(), heavy ? 40f : 28f, new Color(1f, 0.82f, 0.35f, 0.95f));

        var flash = MakeGraphic(
            root,
            PlaceholderArt.Solid(Color.white),
            heavy ? 90f : 56f,
            new Color(1f, 1f, 1f, heavy ? 0.65f : 0.4f)
        );

        var duration = heavy ? 0.32f : 0.2f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            var fade = 1f - t;

            slash.localScale = Vector3.one * Mathf.Lerp(0.45f, heavy ? 1.55f : 1.15f, t);
            SetAlpha(slash, fade);

            if (slash2 != null)
            {
                slash2.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.35f, t);
                SetAlpha(slash2, fade * 0.85f);
            }

            ring.localScale = Vector3.one * Mathf.Lerp(0.2f, heavy ? 2.4f : 1.7f, t);
            SetAlpha(ring, fade * 0.85f);

            flash.localScale = Vector3.one * Mathf.Lerp(1f, 1.6f, t);
            SetAlpha(flash, fade * (heavy ? 0.7f : 0.45f));

            yield return null;
        }

        Destroy(gameObject);
    }

    static RectTransform MakeGraphic(Transform parent, Sprite sprite, float size, Color color)
    {
        var rt = UiFactory.NewRect(parent, "Vfx");
        rt.sizeDelta = new Vector2(size, size);
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return rt;
    }

    static void SetAlpha(RectTransform rt, float alpha)
    {
        var image = rt.GetComponent<Image>();
        if (image == null)
        {
            return;
        }

        var color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }

    static Sprite _slash;
    static Sprite _ring;

    static Sprite SlashSprite()
    {
        if (_slash != null)
        {
            return _slash;
        }

        const int width = 96;
        const int height = 24;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        var pixels = new Color32[width * height];
        var mid = (height - 1) * 0.5f;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var u = x / (float)(width - 1);
                var taper = 1f - Mathf.Abs((u - 0.5f) * 2f);
                var thickness = 2.2f + (taper * 7f);
                var dist = Mathf.Abs(y - mid);
                var a = Mathf.Clamp01(1f - (dist / thickness)) * (0.35f + (taper * 0.65f));
                if (a <= 0.02f)
                {
                    continue;
                }

                pixels[y * width + x] = new Color(1f, 0.95f, 0.7f, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        _slash = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        return _slash;
    }

    static Sprite RingSprite()
    {
        if (_ring != null)
        {
            return _ring;
        }

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        var pixels = new Color32[size * size];
        var cx = (size - 1) * 0.5f;
        var radius = size * 0.38f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - cx;
                var dy = y - cx;
                var dist = Mathf.Sqrt((dx * dx) + (dy * dy));
                var a = 1f - Mathf.Abs(dist - radius) / 3.2f;
                if (a <= 0f)
                {
                    continue;
                }

                pixels[y * size + x] = new Color(1f, 0.85f, 0.4f, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        _ring = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _ring;
    }
}
