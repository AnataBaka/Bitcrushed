using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using SpacetimeDB.Types;

/// Hover card for equipment slots and inventory cells. Body is a vertical
/// layout: name pill, kind, stats, then effect. The box sizes to its content.
public class ItemTooltipView : MonoBehaviour
{
    const float Width = 320f;
    const float CursorGap = 18f;
    const float EdgePad = 10f;
    const int Pad = 12;
    const float Spacing = 6f;

    RectTransform _root;
    RectTransform _canvas;
    Canvas _canvasComponent;
    Text _title;
    Text _slot;
    Text _stats;
    Text _effect;
    object _owner;

    public bool IsOpen => gameObject.activeSelf;

    public static ItemTooltipView Create(Transform canvas)
    {
        var panel = UiFactory.RoundedPanel(canvas, "ItemTooltip", new Color(0.10f, 0.11f, 0.14f, 0.97f));
        panel.raycastTarget = false;

        var view = panel.gameObject.AddComponent<ItemTooltipView>();
        view._root = panel.rectTransform;
        view._canvas = canvas.GetComponent<RectTransform>();
        view._canvasComponent = canvas.GetComponent<Canvas>();
        view._root.anchorMin = new Vector2(0f, 0f);
        view._root.anchorMax = new Vector2(0f, 0f);
        view._root.pivot = new Vector2(0f, 0f);
        view._root.sizeDelta = new Vector2(Width, 0f);

        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
        outline.effectDistance = new Vector2(2f, -2f);

        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(Pad, Pad, Pad, Pad);
        layout.spacing = Spacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var header = UiFactory.RoundedPanel(panel.transform, "Header", new Color(0.38f, 0.28f, 0.16f, 1f));
        header.raycastTarget = false;
        var headerElement = header.gameObject.AddComponent<LayoutElement>();
        headerElement.minHeight = 40f;
        headerElement.preferredHeight = 40f;
        headerElement.flexibleHeight = 0f;
        headerElement.flexibleWidth = 1f;

        view._title = UiFactory.Label(
            header.transform,
            "Title",
            "",
            18,
            TextAnchor.MiddleLeft,
            UiFactory.TextColor
        );
        view._title.fontStyle = FontStyle.Bold;
        view._title.horizontalOverflow = HorizontalWrapMode.Wrap;
        view._title.raycastTarget = false;
        UiFactory.Anchor(view._title.rectTransform, Vector2.zero, Vector2.one);
        view._title.rectTransform.offsetMin = new Vector2(Pad, 0f);
        view._title.rectTransform.offsetMax = new Vector2(-Pad, 0f);

        view._slot = MakeBodyLabel(panel.transform, "Slot", 14, UiFactory.MutedColor);
        view._stats = MakeBodyLabel(panel.transform, "Stats", 15, UiFactory.TextColor);
        view._effect = MakeBodyLabel(panel.transform, "Effect", 15, UiFactory.TextColor);

        view.gameObject.SetActive(false);
        return view;
    }

    static Text MakeBodyLabel(Transform parent, string name, int size, Color color)
    {
        var label = UiFactory.Label(parent, name, "", size, TextAnchor.UpperLeft, color);
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        var element = label.gameObject.AddComponent<LayoutElement>();
        element.flexibleHeight = 0f;
        element.flexibleWidth = 1f;
        return label;
    }

    public void Show(object owner, ItemInspect.Info info, Sprite icon)
    {
        _owner = owner;
        _title.text = info.Title;
        _slot.text = info.Slot;
        _stats.text = info.Stats ?? "";
        _effect.text = info.Effect ?? "";
        _stats.gameObject.SetActive(!string.IsNullOrEmpty(_stats.text));
        _effect.gameObject.SetActive(!string.IsNullOrEmpty(_effect.text));

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_root);
        Place(PointerScreenPoint());
    }

    public void Hide()
    {
        _owner = null;
        gameObject.SetActive(false);
    }

    public void HideIfOwner(object owner)
    {
        if (IsOpen && ReferenceEquals(_owner, owner))
        {
            Hide();
        }
    }

    void LateUpdate()
    {
        if (IsOpen)
        {
            Place(PointerScreenPoint());
        }
    }

    void Place(Vector2 screenPoint)
    {
        if (_canvas == null)
        {
            return;
        }

        Camera camera = null;
        if (_canvasComponent != null && _canvasComponent.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            camera = _canvasComponent.worldCamera;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screenPoint, camera, out var cursor))
        {
            return;
        }

        var parentRect = _canvas.rect;
        var size = _root.rect.size;
        if (size.x < 1f)
        {
            size.x = Width;
        }

        if (size.y < 1f)
        {
            size.y = 80f;
        }

        // Equipment sits bottom-left, so grow up and to the right of the cursor.
        var x = cursor.x + CursorGap;
        var y = cursor.y + CursorGap;
        if (x + size.x > parentRect.xMax - EdgePad)
        {
            x = cursor.x - CursorGap - size.x;
        }

        if (y + size.y > parentRect.yMax - EdgePad)
        {
            y = parentRect.yMax - EdgePad - size.y;
        }

        if (y < parentRect.yMin + EdgePad)
        {
            y = parentRect.yMin + EdgePad;
        }

        var minX = parentRect.xMin + EdgePad;
        var maxX = parentRect.xMax - EdgePad - size.x;
        x = Mathf.Clamp(x, minX, Mathf.Max(minX, maxX));
        var minY = parentRect.yMin + EdgePad;
        var maxY = parentRect.yMax - EdgePad - size.y;
        y = Mathf.Clamp(y, minY, Mathf.Max(minY, maxY));

        var anchorRef = new Vector2(
            Mathf.Lerp(parentRect.xMin, parentRect.xMax, _root.anchorMin.x),
            Mathf.Lerp(parentRect.yMin, parentRect.yMax, _root.anchorMin.y)
        );
        _root.anchoredPosition = new Vector2(x, y) - anchorRef;
    }

    static Vector2 PointerScreenPoint()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current == null ? (Vector2)Input.mousePosition : Mouse.current.position.ReadValue();
#else
        return Input.mousePosition;
#endif
    }
}

public class ItemHoverTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public const float ShowDelaySeconds = 0.35f;

    ItemTooltipView _tooltip;
    System.Func<ItemDef> _defOf;
    System.Func<bool> _blocked;
    Coroutine _pending;
    bool _inside;

    public void Bind(ItemTooltipView tooltip, System.Func<ItemDef> defOf, System.Func<bool> blocked = null)
    {
        _tooltip = tooltip;
        _defOf = defOf;
        _blocked = blocked;
        if (_inside)
        {
            Refresh();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _inside = true;
        CancelPending();
        _pending = StartCoroutine(ShowAfterDelay());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _inside = false;
        CancelPending();
        _tooltip?.HideIfOwner(this);
    }

    public void Refresh()
    {
        if (!_inside)
        {
            return;
        }

        if (_blocked != null && _blocked())
        {
            CancelPending();
            _tooltip?.HideIfOwner(this);
            return;
        }

        var def = _defOf?.Invoke();
        if (def == null)
        {
            CancelPending();
            _tooltip?.HideIfOwner(this);
            return;
        }

        if (_tooltip != null && _tooltip.IsOpen)
        {
            ShowNow(def);
        }
    }

    public void HideIfBlocked()
    {
        if (_blocked != null && _blocked())
        {
            CancelPending();
            _tooltip?.HideIfOwner(this);
        }
    }

    IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSecondsRealtime(ShowDelaySeconds);
        _pending = null;
        if (!_inside || (_blocked != null && _blocked()))
        {
            yield break;
        }

        var def = _defOf?.Invoke();
        if (def == null)
        {
            yield break;
        }

        ShowNow(def);
    }

    void ShowNow(ItemDef def)
    {
        _tooltip?.Show(this, ItemInspect.For(def), GearArt.Sprite(def));
    }

    void CancelPending()
    {
        if (_pending != null)
        {
            StopCoroutine(_pending);
            _pending = null;
        }
    }

    void OnDisable()
    {
        _inside = false;
        CancelPending();
        _tooltip?.HideIfOwner(this);
    }

    void OnDestroy()
    {
        _tooltip?.HideIfOwner(this);
    }
}
