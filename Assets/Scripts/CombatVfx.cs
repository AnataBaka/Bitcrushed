using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public enum HitEffectKind
{
    Impact,
    Explosion2,
    BigHit,
    BloodImpact,
    CurvedImpact,
}

/// Plays a one-shot VFX flipbook centered on the combatant that was hit. Effects
/// are scaled from the visible body so the 69px blood burst and the 557px Big
/// Hit land at a similar on-screen size, including padded class sprites.
public class CombatVfx : MonoBehaviour
{
    public const float Fps = 30f;

    static Sprite[] _impact;
    static Sprite[] _explosion2;
    static Sprite[] _bigHit;
    static Sprite[] _bloodImpact;
    static Sprite[] _curvedImpact;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _impact = null;
        _explosion2 = null;
        _bigHit = null;
        _bloodImpact = null;
        _curvedImpact = null;
    }

    public static void Spawn(RectTransform field, RectTransform at, HitEffectKind kind)
    {
        if (field == null || at == null)
        {
            return;
        }

        SpawnAt(field, WorldCenter(at), DisplaySize(kind, at), kind);
    }

    public static void SpawnAt(RectTransform field, Vector3 worldCenter, float size, HitEffectKind kind)
    {
        if (field == null)
        {
            return;
        }

        var frames = Frames(kind);
        if (frames == null || frames.Length == 0)
        {
            return;
        }

        var go = new GameObject(kind.ToString(), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(field, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.position = worldCenter;
        rt.SetAsLastSibling();

        var image = go.GetComponent<Image>();
        image.sprite = frames[0];
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.type = Image.Type.Simple;

        var flip = go.AddComponent<SpriteFlipbook>();
        var vfx = go.AddComponent<CombatVfx>();
        vfx.StartCoroutine(vfx.PlayAndDestroy(flip, frames));
    }

    /// Screen-filling Explosion_2 (or other kind) centered on the battlefield.
    public static IEnumerator PlayFullscreen(RectTransform field, HitEffectKind kind)
    {
        if (field == null)
        {
            yield break;
        }

        var frames = Frames(kind);
        if (frames == null || frames.Length == 0)
        {
            yield break;
        }

        var size = Mathf.Max(field.rect.width, field.rect.height);
        if (size < 8f)
        {
            size = Mathf.Max(field.sizeDelta.x, field.sizeDelta.y);
        }

        size = Mathf.Max(size * 1.15f, 1100f);

        var go = new GameObject(
            kind + "_Fullscreen",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(field, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.position = WorldCenter(field);
        rt.SetAsLastSibling();

        var image = go.GetComponent<Image>();
        image.sprite = frames[0];
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.type = Image.Type.Simple;

        var flip = go.AddComponent<SpriteFlipbook>();
        var vfx = go.AddComponent<CombatVfx>();
        yield return vfx.PlayAndDestroy(flip, frames);
    }

    IEnumerator PlayAndDestroy(SpriteFlipbook flip, Sprite[] frames)
    {
        yield return flip.PlayOnce(frames, Fps);
        Destroy(gameObject);
    }

    static Sprite[] Frames(HitEffectKind kind)
    {
        switch (kind)
        {
            case HitEffectKind.Impact:
                return _impact ??= SpriteFrameLoader.LoadFolder(
                    "Sprites/Effect_Impact/Frames/Effect_Impact_1",
                    FilterMode.Bilinear
                );
            case HitEffectKind.Explosion2:
                return _explosion2 ??= SpriteFrameLoader.LoadFolder(
                    "Sprites/Effect_Explosion2/Frames/Effect_Explosion2_1",
                    FilterMode.Bilinear
                );
            case HitEffectKind.BigHit:
                return _bigHit ??= SpriteFrameLoader.LoadFolder(
                    "Sprites/Effect_BigHit/Frames/Effect_BigHit_1",
                    FilterMode.Bilinear
                );
            case HitEffectKind.BloodImpact:
                return _bloodImpact ??= SpriteFrameLoader.LoadFolder(
                    "Sprites/Effect_BloodImpact/Frames/Effect_BloodImpact_1",
                    FilterMode.Bilinear
                );
            case HitEffectKind.CurvedImpact:
                return _curvedImpact ??= BuildCurvedImpactFrames();
            default:
                return Array.Empty<Sprite>();
        }
    }

    /// Pixel crescent slash for Grandshot. Named to match the Curved Impact
    /// VFX pack; generated here so the move still has a distinct hit.
    static Sprite[] BuildCurvedImpactFrames()
    {
        const int size = 96;
        const int count = 20;
        var frames = new Sprite[count];
        for (var i = 0; i < count; i++)
        {
            var t = i / (float)(count - 1);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"CurvedImpact_{i:00}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[size * size];
            var fade = 1f - (t * t);
            var radius = Mathf.Lerp(10f, 40f, t);
            var thickness = Mathf.Lerp(13f, 3.2f, t);
            var startAng = Mathf.Lerp(200f, 40f, t);
            var sweep = Mathf.Lerp(70f, 130f, t);
            var cx = size * 0.50f;
            var cy = size * 0.48f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - cx;
                    var dy = y + 0.5f - cy;
                    var dist = Mathf.Sqrt((dx * dx) + (dy * dy));
                    var ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (ang < 0f)
                    {
                        ang += 360f;
                    }

                    var rel = ang - startAng;
                    if (rel < 0f)
                    {
                        rel += 360f;
                    }

                    if (rel > sweep)
                    {
                        continue;
                    }

                    var ring = Mathf.Abs(dist - radius);
                    if (ring > thickness)
                    {
                        continue;
                    }

                    var along = rel / sweep;
                    var edge = 1f - (ring / thickness);
                    var core = edge * edge;
                    var spark = along > 0.72f ? (along - 0.72f) / 0.28f : 0f;
                    var alpha = Mathf.Clamp01((0.35f + (0.65f * core) + (0.25f * spark)) * fade);
                    if (alpha <= 0.02f)
                    {
                        continue;
                    }

                    var white = (byte)Mathf.Clamp(210 + (core * 45f), 0, 255);
                    var gold = (byte)Mathf.Clamp(140 + (core * 90f) + (spark * 40f), 0, 255);
                    var a = (byte)Mathf.Clamp(alpha * 255f, 0, 255);
                    pixels[(y * size) + x] = new Color32(white, gold, (byte)(90 + (spark * 80f)), a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            frames[i] = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect
            );
            frames[i].name = texture.name;
        }

        return frames;
    }

    static float DisplaySize(HitEffectKind kind, RectTransform at)
    {
        var shape = BodySize(at);
        float size;
        switch (kind)
        {
            case HitEffectKind.Impact:
                size = shape * 1.75f;
                break;
            case HitEffectKind.Explosion2:
                size = shape * 2.15f;
                break;
            case HitEffectKind.BigHit:
                size = shape * 2.35f;
                break;
            case HitEffectKind.BloodImpact:
                size = shape * 1.55f;
                break;
            case HitEffectKind.CurvedImpact:
                size = shape * 2.45f;
                break;
            default:
                size = shape * 1.6f;
                break;
        }

        return Mathf.Clamp(size, 88f, 280f);
    }

    /// Padded class canvases (128px art in a ~218px UI square) would otherwise
    /// make VFX dwarf the drawn body. Placeholder enemy shapes stay as-is.
    static float BodySize(RectTransform at)
    {
        var width = at.rect.width;
        var height = at.rect.height;
        if (width < 8f || height < 8f)
        {
            width = at.sizeDelta.x;
            height = at.sizeDelta.y;
        }

        var max = Mathf.Max(width, height);
        if (max < 8f)
        {
            max = 96f;
        }

        if (max > 160f)
        {
            return max * 0.55f;
        }

        return max;
    }

    public static Vector3 WorldCenter(RectTransform at)
    {
        var corners = new Vector3[4];
        at.GetWorldCorners(corners);
        return (corners[0] + corners[2]) * 0.5f;
    }
}
