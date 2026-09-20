using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// Flies the Archer arrow sprite from the bow to a target. Path is chosen from
/// the skill name so Rain Down drops from above and Curved Shot arcs.
public class CombatProjectile : MonoBehaviour
{
    public static IEnumerator FireArrow(
        RectTransform field,
        Vector3 from,
        Vector3 to,
        string actionName,
        Sprite sprite
    )
    {
        if (field == null || sprite == null)
        {
            yield break;
        }

        var go = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(field, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        var size = DisplaySize(actionName);
        rt.sizeDelta = new Vector2(size, size);
        rt.SetAsLastSibling();

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.type = Image.Type.Simple;

        var flight = go.AddComponent<CombatProjectile>();
        yield return flight.Fly(rt, from, to, actionName);
        if (go != null)
        {
            Object.Destroy(go);
        }
    }

    /// Flies a looping charge flipbook. Charge_1 is an orb (no spin); Charge_2
    /// is a bolt that faces its velocity.
    public static IEnumerator FireCharge(
        RectTransform field,
        Vector3 from,
        Vector3 to,
        Sprite[] frames,
        float duration,
        float size,
        bool rotate
    )
    {
        if (field == null || frames == null || frames.Length == 0)
        {
            yield break;
        }

        var go = new GameObject("Charge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(field, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.SetAsLastSibling();

        var image = go.GetComponent<Image>();
        image.sprite = frames[0];
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.type = Image.Type.Simple;

        var flip = go.AddComponent<SpriteFlipbook>();
        flip.Play(frames, 18f, true);

        var flight = go.AddComponent<CombatProjectile>();
        yield return flight.FlyRaw(rt, from, to, duration, rotate);
        if (go != null)
        {
            Object.Destroy(go);
        }
    }

    IEnumerator Fly(RectTransform rt, Vector3 from, Vector3 to, string actionName)
    {
        var duration = Duration(actionName);
        var elapsed = 0f;
        var previous = from;
        rt.position = from;
        Aim(rt, to - from);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            var eased = Ease(t, actionName);
            var point = PointOnPath(from, to, eased, actionName);
            var delta = point - previous;
            if (delta.sqrMagnitude > 0.0001f)
            {
                Aim(rt, delta);
            }

            rt.position = point;
            previous = point;
            yield return null;
        }

        rt.position = to;
    }

    IEnumerator FlyRaw(RectTransform rt, Vector3 from, Vector3 to, float duration, bool rotate)
    {
        var elapsed = 0f;
        var previous = from;
        rt.position = from;
        if (rotate)
        {
            Aim(rt, to - from);
        }

        duration = Mathf.Max(0.05f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            var eased = t * t * (3f - (2f * t));
            var point = Vector3.LerpUnclamped(from, to, eased);
            var delta = point - previous;
            if (rotate && delta.sqrMagnitude > 0.0001f)
            {
                Aim(rt, delta);
            }

            rt.position = point;
            previous = point;
            yield return null;
        }

        rt.position = to;
    }

    static void Aim(RectTransform rt, Vector3 delta)
    {
        var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        rt.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    static Vector3 PointOnPath(Vector3 from, Vector3 to, float t, string actionName)
    {
        switch (actionName)
        {
            case "Rain Down":
                return RainPath(from, to, t);
            case "Curved Shot":
                return QuadBezier(from, ArcControl(from, to, 180f, 70f), to, t);
            case "Grandshot":
            case "Grand Shot":
                return QuadBezier(from, ArcControl(from, to, 90f, -40f), to, t);
            default:
                return Vector3.LerpUnclamped(from, to, t);
        }
    }

    /// Shoots up from the bow, then falls onto the target.
    static Vector3 RainPath(Vector3 from, Vector3 to, float t)
    {
        var apex = new Vector3(
            Mathf.Lerp(from.x, to.x, 0.28f),
            Mathf.Max(from.y, to.y) + 320f,
            from.z
        );
        if (t < 0.42f)
        {
            var u = t / 0.42f;
            return QuadBezier(from, new Vector3(from.x, from.y + 220f, from.z), apex, u);
        }

        var v = (t - 0.42f) / 0.58f;
        var drop = QuadBezier(apex, new Vector3(to.x, apex.y, to.z), to, v);
        return drop;
    }

    static Vector3 ArcControl(Vector3 from, Vector3 to, float height, float side)
    {
        var mid = (from + to) * 0.5f;
        return new Vector3(mid.x + side, Mathf.Max(from.y, to.y) + height, mid.z);
    }

    static Vector3 QuadBezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        var u = 1f - t;
        return (u * u * a) + (2f * u * t * b) + (t * t * c);
    }

    static float Ease(float t, string actionName)
    {
        switch (actionName)
        {
            case "Rain Down":
                return t;
            case "Snipe":
                return t * t * (3f - (2f * t));
            default:
                return t * t * (3f - (2f * t));
        }
    }

    static float Duration(string actionName)
    {
        switch (actionName)
        {
            case "Snipe":
                return 0.11f;
            case "Rain Down":
                return 0.38f;
            case "Curved Shot":
                return 0.26f;
            case "Grandshot":
            case "Grand Shot":
                return 0.16f;
            default:
                return 0.16f;
        }
    }

    static float DisplaySize(string actionName)
    {
        switch (actionName)
        {
            case "Grandshot":
            case "Grand Shot":
                return 92f;
            case "Snipe":
                return 58f;
            default:
                return 70f;
        }
    }
}
