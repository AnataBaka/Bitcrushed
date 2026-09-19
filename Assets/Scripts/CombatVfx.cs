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
}

/// Plays a one-shot VFX flipbook centered on the enemy that was hit. Effects
/// are scaled from the target's shape so the 69px blood burst and the 557px
/// Big Hit land at a similar on-screen size.
public class CombatVfx : MonoBehaviour
{
    public const float Fps = 30f;

    static Sprite[] _impact;
    static Sprite[] _explosion2;
    static Sprite[] _bigHit;
    static Sprite[] _bloodImpact;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _impact = null;
        _explosion2 = null;
        _bigHit = null;
        _bloodImpact = null;
    }

    public static void Spawn(RectTransform field, RectTransform at, HitEffectKind kind)
    {
        if (field == null || at == null)
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
        var size = DisplaySize(kind, at);
        rt.sizeDelta = new Vector2(size, size);
        rt.position = WorldCenter(at);
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
            default:
                return Array.Empty<Sprite>();
        }
    }

    static float DisplaySize(HitEffectKind kind, RectTransform at)
    {
        var shape = Mathf.Max(at.rect.width, at.rect.height);
        if (shape < 8f)
        {
            shape = Mathf.Max(at.sizeDelta.x, at.sizeDelta.y);
        }

        if (shape < 8f)
        {
            shape = 96f;
        }

        switch (kind)
        {
            case HitEffectKind.Impact:
                return shape * 1.75f;
            case HitEffectKind.Explosion2:
                return shape * 2.15f;
            case HitEffectKind.BigHit:
                return shape * 2.35f;
            case HitEffectKind.BloodImpact:
                return shape * 1.55f;
            default:
                return shape * 1.6f;
        }
    }

    static Vector3 WorldCenter(RectTransform at)
    {
        var corners = new Vector3[4];
        at.GetWorldCorners(corners);
        return (corners[0] + corners[2]) * 0.5f;
    }
}
