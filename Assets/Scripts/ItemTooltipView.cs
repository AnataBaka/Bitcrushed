using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using SpacetimeDB.Types;

/// Hover card for equipment slots. Shows the item name, slot, art, and passive.
public class ItemTooltipView : MonoBehaviour
{
    const float Width = 320f;
    const float CursorGap = 18f;
    const float EdgePad = 10f;

    RectTransform _root;
    RectTransform _canvas;
    Canvas _canvasComponent;
    Image _icon;
    GameObject _iconRow;
    Text _title;
    Text _slot;
    Text _body;
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
        view._root.pivot = new Vector2(0f, 1f);
        view._root.sizeDelta = new Vector2(Width, 160f);

        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
        outline.effectDistance = new Vector2(2f, -2f);

        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 6f;
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
        view._title.rectTransform.offsetMin = new Vector2(12f, 0f);
        view._title.rectTransform.offsetMax = new Vector2(-12f, 0f);

        var iconRow = UiFactory.NewRect(panel.transform, "IconRow");
        var iconLayout = iconRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        iconLayout.childAlignment = TextAnchor.MiddleLeft;
        iconLayout.childControlHeight = false;
        iconLayout.childControlWidth = false;
        var iconRowElement = iconRow.gameObject.AddComponent<LayoutElement>();
        iconRowElement.minHeight = 72f;
        iconRowElement.preferredHeight = 72f;
        view._iconRow = iconRow.gameObject;

        view._icon = UiFactory.Graphic(iconRow, "Icon", PlaceholderArt.Solid(Color.white), Color.white);
        view._icon.raycastTarget = false;
        view._icon.preserveAspect = true;
        var iconRt = view._icon.rectTransform;
        iconRt.sizeDelta = new Vector2(64f, 64f);

        view._slot = UiFactory.Label(panel.transform, "Slot", "", 14, TextAnchor.MiddleLeft, UiFactory.MutedColor);
        view._slot.raycastTarget = false;
        view._body = UiFactory.Label(panel.transform, "Body", "", 15, TextAnchor.UpperLeft, UiFactory.TextColor);
        view._body.horizontalOverflow = HorizontalWrapMode.Wrap;
        view._body.verticalOverflow = VerticalWrapMode.Overflow;
        view._body.raycastTarget = false;
        var bodyFitter = view._body.gameObject.AddComponent<ContentSizeFitter>();
        bodyFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        bodyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        view.gameObject.SetActive(false);
        return view;
    }

    public void Show(object owner, ItemInspect.Info info, Sprite icon)
    {
        _owner = owner;
        _title.text = info.Title;
        _slot.text = info.Slot;
        _body.text = info.Description;
        var hasIcon = icon != null;
        _iconRow.SetActive(hasIcon);
        if (hasIcon)
        {
            _icon.sprite = icon;
            _icon.color = Color.white;
        }

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
            size.y = 160f;
        }

        // Equipment sits on the left, so prefer the right of the cursor.
        var x = cursor.x + CursorGap;
        var y = cursor.y + 8f;
        if (x + size.x > parentRect.xMax - EdgePad)
        {
            x = cursor.x - CursorGap - size.x;
        }

        if (y - size.y < parentRect.yMin + EdgePad)
        {
            y = parentRect.yMin + EdgePad + size.y;
        }

        if (y > parentRect.yMax - EdgePad)
        {
            y = parentRect.yMax - EdgePad;
        }

        var minX = parentRect.xMin + EdgePad;
        var maxX = parentRect.xMax - EdgePad - size.x;
        x = Mathf.Clamp(x, minX, Mathf.Max(minX, maxX));

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
    ItemTooltipView _tooltip;
    ItemDef _def;

    public void Bind(ItemTooltipView tooltip, ItemDef def)
    {
        _tooltip = tooltip;
        _def = def;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_tooltip == null || _def == null)
        {
            return;
        }

        _tooltip.Show(this, ItemInspect.For(_def), AmuletArt.Icon(_def.Name));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _tooltip?.HideIfOwner(this);
    }

    void OnDestroy()
    {
        _tooltip?.HideIfOwner(this);
    }
}
