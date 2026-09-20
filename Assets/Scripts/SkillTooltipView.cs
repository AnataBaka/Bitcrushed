using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// Floating inspect card for skill-menu hover. Lives on the root canvas so the
/// skill list's RectMask2D cannot clip it, and does not raycast so it cannot
/// steal the pointer from the button underneath.
public class SkillTooltipView : MonoBehaviour
{
    const float Width = 340f;
    const float CursorGap = 18f;
    const float EdgePad = 10f;

    RectTransform _root;
    RectTransform _canvas;
    Canvas _canvasComponent;
    Text _title;
    Text _mana;
    Text _damage;
    Text _body;
    Text _note;
    GameObject _noteRow;
    object _owner;

    public bool IsOpen => gameObject.activeSelf;

    public static SkillTooltipView Create(Transform canvas)
    {
        var panel = UiFactory.RoundedPanel(canvas, "SkillTooltip", new Color(0.10f, 0.11f, 0.14f, 0.97f));
        panel.raycastTarget = false;

        var view = panel.gameObject.AddComponent<SkillTooltipView>();
        view._root = panel.rectTransform;
        view._canvas = canvas.GetComponent<RectTransform>();
        view._canvasComponent = canvas.GetComponent<Canvas>();
        view._root.anchorMin = new Vector2(0f, 0f);
        view._root.anchorMax = new Vector2(0f, 0f);
        view._root.pivot = new Vector2(0f, 1f);
        view._root.sizeDelta = new Vector2(Width, 180f);

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

        var header = UiFactory.RoundedPanel(panel.transform, "Header", new Color(0.22f, 0.32f, 0.48f, 1f));
        header.raycastTarget = false;
        var headerElement = header.gameObject.AddComponent<LayoutElement>();
        headerElement.minHeight = 40f;
        headerElement.preferredHeight = 40f;
        headerElement.flexibleWidth = 1f;

        view._title = UiFactory.Label(
            header.transform,
            "Title",
            "",
            16,
            TextAnchor.MiddleLeft,
            UiFactory.TextColor
        );
        view._title.horizontalOverflow = HorizontalWrapMode.Wrap;
        view._title.raycastTarget = false;
        UiFactory.Anchor(view._title.rectTransform, Vector2.zero, Vector2.one);
        view._title.rectTransform.offsetMin = new Vector2(12f, 0f);
        view._title.rectTransform.offsetMax = new Vector2(-12f, 0f);

        view._mana = MakeStatLine(panel.transform, "MP cost");
        view._damage = MakeStatLine(panel.transform, "Damage");
        view._body = MakeWrapped(panel.transform, "Body", 16, UiFactory.TextColor);
        view._note = MakeWrapped(panel.transform, "Note", 16, UiFactory.ActiveColor);
        view._noteRow = view._note.gameObject;

        view.gameObject.SetActive(false);
        return view;
    }

    static Text MakeStatLine(Transform parent, string label)
    {
        var row = UiFactory.NewRect(parent, label + "Row");
        var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 8f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlHeight = true;
        rowLayout.childControlWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childForceExpandWidth = true;
        var rowElement = row.gameObject.AddComponent<LayoutElement>();
        rowElement.minHeight = 20f;
        rowElement.preferredHeight = 20f;
        rowElement.flexibleWidth = 1f;

        var caption = UiFactory.Label(row, "Label", label, 16, TextAnchor.MiddleLeft, UiFactory.MutedColor);
        caption.raycastTarget = false;
        var captionElement = caption.gameObject.AddComponent<LayoutElement>();
        captionElement.minWidth = 78f;
        captionElement.preferredWidth = 78f;
        captionElement.flexibleWidth = 0f;

        var value = UiFactory.Label(row, "Value", "", 16, TextAnchor.MiddleLeft, UiFactory.TextColor);
        value.horizontalOverflow = HorizontalWrapMode.Wrap;
        value.raycastTarget = false;
        var valueElement = value.gameObject.AddComponent<LayoutElement>();
        valueElement.flexibleWidth = 1f;
        valueElement.minWidth = 80f;
        return value;
    }

    static Text MakeWrapped(Transform parent, string name, int size, Color color)
    {
        var text = UiFactory.Label(parent, name, "", size, TextAnchor.UpperLeft, color);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        var fitter = text.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var element = text.gameObject.AddComponent<LayoutElement>();
        element.flexibleWidth = 1f;
        element.minWidth = Width - 40f;
        return text;
    }

    public void Show(object owner, SkillInspect.Info info)
    {
        _owner = owner;
        _title.text = info.Title;
        _mana.text = info.Mana;
        _damage.text = info.Damage;
        _body.text = info.Description;
        var hasNote = !string.IsNullOrEmpty(info.Note);
        _noteRow.SetActive(hasNote);
        _note.text = hasNote ? info.Note : "";
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
        if (!IsOpen)
        {
            return;
        }

        Place(PointerScreenPoint());
    }

    void Place(Vector2 screenPoint)
    {
        if (_canvas == null)
        {
            return;
        }

        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas,
                screenPoint,
                EventCamera(),
                out var cursor
            )
        )
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
            size.y = 180f;
        }

        // Prefer the left of the cursor: the skill menu sits on the right edge.
        var x = cursor.x - CursorGap - size.x;
        var y = cursor.y + 8f;
        if (x < parentRect.xMin + EdgePad)
        {
            x = cursor.x + CursorGap;
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

    Camera EventCamera()
    {
        if (_canvasComponent == null || _canvasComponent.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return _canvasComponent.worldCamera;
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

/// Pointer hook on a skill button. Works while the button is greyed out so locked
/// skills (Overthrow, Grandshot, …) can still be inspected.
public class SkillHoverTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    SkillTooltipView _tooltip;
    SkillDef _skill;
    bool _basic;

    public void Bind(SkillTooltipView tooltip, SkillDef skill, bool basic)
    {
        _tooltip = tooltip;
        _skill = skill;
        _basic = basic;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_tooltip == null)
        {
            return;
        }

        var caster = GameManager.LocalEntity();
        var session = GameManager.Session();
        var info = _basic
            ? SkillInspect.ForBasicAttack(caster)
            : SkillInspect.ForSkill(_skill, caster, session);
        _tooltip.Show(this, info);
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
