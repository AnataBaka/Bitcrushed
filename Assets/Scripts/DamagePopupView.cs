using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Floating damage digits. Not parented to combatants, so a killing blow's
/// number keeps playing after the corpse is removed.
public class DamagePopupView : MonoBehaviour
{
    public const float FontSizeScreenHeight = 0.035f;
    public const float Duration = 0.8f;
    /// Matches EntityView's name-strip height.
    public const float FloatDistance = 24f;
    const float JitterX = 18f;
    const float StaggerY = 14f;
    const int PoolSize = 16;
    const float EdgePad = 8f;

    readonly Stack<Text> _pool = new Stack<Text>();
    readonly Dictionary<ulong, int> _live = new Dictionary<ulong, int>();
    RectTransform _root;
    Canvas _canvas;

    public static DamagePopupView Create(Transform canvas)
    {
        var rt = UiFactory.NewRect(canvas, "DamagePopups");
        UiFactory.Anchor(rt, Vector2.zero, Vector2.one);
        var view = rt.gameObject.AddComponent<DamagePopupView>();
        view._root = rt;
        view._canvas = canvas.GetComponent<Canvas>();
        for (var i = 0; i < PoolSize; i++)
        {
            view._pool.Push(view.MakeLabel());
        }

        return view;
    }

    public void Spawn(ulong targetId, int damage, Vector3 worldPoint)
    {
        if (damage <= 0)
        {
            return;
        }

        var label = _pool.Count > 0 ? _pool.Pop() : MakeLabel();
        _live.TryGetValue(targetId, out var stack);
        _live[targetId] = stack + 1;

        var size = FontSize();
        label.fontSize = size;
        label.text = damage.ToString();
        label.color = new Color(0.92f, 0.18f, 0.16f, 1f);
        label.gameObject.SetActive(true);
        label.rectTransform.SetAsLastSibling();

        var local = WorldToLocal(worldPoint);
        var jitter = ((stack % 3) - 1) * JitterX;
        local.x += jitter;
        local.y += 12f + stack * StaggerY;
        local = Clamp(local, size);
        label.rectTransform.anchoredPosition = local;

        StartCoroutine(Float(label, targetId, local));
    }

    Text MakeLabel()
    {
        var label = UiFactory.Label(
            _root,
            "Damage",
            "",
            24,
            TextAnchor.MiddleCenter,
            new Color(0.92f, 0.18f, 0.16f, 1f)
        );
        label.fontStyle = FontStyle.Bold;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        label.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(160f, 48f);
        var outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.08f, 0.04f, 0.04f, 0.92f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        label.gameObject.SetActive(false);
        return label;
    }

    IEnumerator Float(Text label, ulong targetId, Vector2 from)
    {
        var elapsed = 0f;
        while (elapsed < Duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / Duration);
            var pos = from + new Vector2(0f, FloatDistance * t);
            label.rectTransform.anchoredPosition = Clamp(pos, label.fontSize);
            var color = label.color;
            color.a = 1f - t;
            label.color = color;
            yield return null;
        }

        label.gameObject.SetActive(false);
        _pool.Push(label);
        if (_live.TryGetValue(targetId, out var stack))
        {
            stack -= 1;
            if (stack <= 0)
            {
                _live.Remove(targetId);
            }
            else
            {
                _live[targetId] = stack;
            }
        }
    }

    int FontSize()
    {
        var height = _root.rect.height;
        if (height < 8f)
        {
            height = 1080f;
        }

        return Mathf.Clamp(Mathf.RoundToInt(height * FontSizeScreenHeight), 18, 42);
    }

    Vector2 WorldToLocal(Vector3 worldPoint)
    {
        Camera camera = null;
        if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            camera = _canvas.worldCamera;
        }

        var screen = RectTransformUtility.WorldToScreenPoint(camera, worldPoint);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, camera, out var local);
        return local;
    }

    Vector2 Clamp(Vector2 local, int fontSize)
    {
        var rect = _root.rect;
        var halfW = 40f + fontSize * 0.6f;
        var halfH = fontSize * 0.7f;
        local.x = Mathf.Clamp(local.x, rect.xMin + EdgePad + halfW, rect.xMax - EdgePad - halfW);
        local.y = Mathf.Clamp(local.y, rect.yMin + EdgePad + halfH, rect.yMax - EdgePad - halfH);
        return local;
    }
}
