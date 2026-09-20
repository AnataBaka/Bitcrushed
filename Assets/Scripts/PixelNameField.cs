using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// Lobby name box. Pixel 9-slice frame, boldpixels text, block caret.
/// Filters to the same charset as the JoinGame reducer; the server still validates.
public class PixelNameField : MonoBehaviour,
    IPointerClickHandler,
    ISelectHandler,
    IDeselectHandler
{
    /// Must match Module.PlayerNameMaxLength / PlayerNameAllowedChars.
    public const int MaxLength = 12;
    public const string AllowedChars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 '-";
    public const float Height = 40f;
    public const float Pad = 8f;
    const float CaretBlink = 0.5f;
    const string Placeholder = "Name (blank = random)";

    public static bool IsEditing { get; private set; }

    public Action OnSubmit;

    Image _frame;
    Text _text;
    Text _placeholder;
    Image _caret;
    Image _selection;
    string _value = "";
    int _caretIndex;
    int _anchor = -1;
    bool _focused;
    float _blink;
    Sprite _idleSlice;
    Sprite _focusSlice;

    public string Value => _value;

    public static PixelNameField Create(Transform parent)
    {
        var frame = UiFactory.NewRect(parent, "NameField");
        var image = frame.gameObject.AddComponent<Image>();
        var idle = PlaceholderArt.PixelSlice(
            UiFactory.SlotColor,
            new Color(0.08f, 0.08f, 0.10f, 1f)
        );
        var focus = PlaceholderArt.PixelSlice(UiFactory.SlotColor, UiFactory.ActiveColor);
        image.sprite = idle;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        image.raycastTarget = true;

        var layout = frame.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = Height;
        layout.preferredHeight = Height;
        layout.flexibleHeight = 0f;

        var field = frame.gameObject.AddComponent<PixelNameField>();
        field._frame = image;
        field._idleSlice = idle;
        field._focusSlice = focus;

        field._selection = UiFactory.Graphic(
            frame,
            "Selection",
            PlaceholderArt.FlatWhite(),
            Color.Lerp(UiFactory.SlotColor, UiFactory.ActiveColor, 0.45f)
        );
        field._selection.preserveAspect = false;
        field._selection.gameObject.SetActive(false);

        field._placeholder = UiFactory.Label(
            frame,
            "Placeholder",
            Placeholder,
            16,
            TextAnchor.MiddleLeft,
            UiFactory.MutedColor
        );
        UiFactory.Anchor(field._placeholder.rectTransform, Vector2.zero, Vector2.one);
        field._placeholder.rectTransform.offsetMin = new Vector2(Pad, 0f);
        field._placeholder.rectTransform.offsetMax = new Vector2(-Pad, 0f);
        field._placeholder.horizontalOverflow = HorizontalWrapMode.Overflow;
        field._placeholder.verticalOverflow = VerticalWrapMode.Overflow;

        field._text = UiFactory.Label(
            frame,
            "Value",
            "",
            16,
            TextAnchor.MiddleLeft,
            UiFactory.TextColor
        );
        UiFactory.Anchor(field._text.rectTransform, Vector2.zero, Vector2.one);
        field._text.rectTransform.offsetMin = new Vector2(Pad, 0f);
        field._text.rectTransform.offsetMax = new Vector2(-Pad, 0f);
        field._text.horizontalOverflow = HorizontalWrapMode.Overflow;
        field._text.verticalOverflow = VerticalWrapMode.Overflow;
        field._text.supportRichText = false;
        field._placeholder.supportRichText = false;

        field._caret = UiFactory.Graphic(
            frame,
            "Caret",
            PlaceholderArt.FlatWhite(),
            UiFactory.TextColor
        );
        field._caret.preserveAspect = false;
        field._caret.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        field._caret.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        field._caret.rectTransform.pivot = new Vector2(0f, 0.5f);
        field._caret.rectTransform.sizeDelta = new Vector2(GameFont.Pixel, GameFont.Resolve(16));
        field._caret.gameObject.SetActive(false);

        var selectable = frame.gameObject.AddComponent<Selectable>();
        selectable.targetGraphic = image;
        selectable.transition = Selectable.Transition.None;

        return field;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        EventSystem.current?.SetSelectedGameObject(gameObject);
        MoveCaretToEnd();
    }

    public void OnSelect(BaseEventData eventData)
    {
        _focused = true;
        IsEditing = true;
        _blink = 0f;
        _frame.sprite = _focusSlice;
        Keyboard.onTextInput += OnTextInput;
        Refresh();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        Keyboard.onTextInput -= OnTextInput;
        _focused = false;
        IsEditing = false;
        _frame.sprite = _idleSlice;
        _caret.gameObject.SetActive(false);
        _selection.gameObject.SetActive(false);
        _anchor = -1;
    }

    void OnTextInput(char c)
    {
        if (!_focused || char.IsControl(c))
        {
            return;
        }

        var kb = Keyboard.current;
        if (kb != null && (kb.ctrlKey.isPressed || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed))
        {
            return;
        }

        Insert(c.ToString());
        Refresh();
    }

    void Update()
    {
        if (!_focused)
        {
            return;
        }

        _blink += Time.unscaledDeltaTime;
        var on = (_blink % (CaretBlink * 2f)) < CaretBlink;
        _caret.gameObject.SetActive(on);
        LayoutCaret();
        PollKeys();
    }

    void OnDisable()
    {
        Keyboard.onTextInput -= OnTextInput;
        IsEditing = false;
        _focused = false;
    }

    void PollKeys()
    {
        var kb = Keyboard.current;
        if (kb == null)
        {
            return;
        }

        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
        {
            OnSubmit?.Invoke();
            return;
        }

        var ctrl = kb.ctrlKey.isPressed || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed;
        if (ctrl && kb.vKey.wasPressedThisFrame)
        {
            Insert(GUIUtility.systemCopyBuffer ?? "");
            Refresh();
            return;
        }

        if (ctrl && kb.aKey.wasPressedThisFrame)
        {
            _anchor = 0;
            _caretIndex = _value.Length;
            Refresh();
            return;
        }

        if (ctrl && kb.cKey.wasPressedThisFrame && HasSelection())
        {
            GUIUtility.systemCopyBuffer = SelectedText();
            return;
        }

        if (kb.backspaceKey.wasPressedThisFrame)
        {
            if (HasSelection())
            {
                DeleteSelection();
            }
            else if (_caretIndex > 0)
            {
                _value = _value.Remove(_caretIndex - 1, 1);
                _caretIndex--;
            }

            Refresh();
            return;
        }

        if (kb.deleteKey.wasPressedThisFrame)
        {
            if (HasSelection())
            {
                DeleteSelection();
            }
            else if (_caretIndex < _value.Length)
            {
                _value = _value.Remove(_caretIndex, 1);
            }

            Refresh();
            return;
        }

        if (kb.leftArrowKey.wasPressedThisFrame)
        {
            _caretIndex = Mathf.Max(0, _caretIndex - 1);
            _anchor = -1;
            Refresh();
            return;
        }

        if (kb.rightArrowKey.wasPressedThisFrame)
        {
            _caretIndex = Mathf.Min(_value.Length, _caretIndex + 1);
            _anchor = -1;
            Refresh();
            return;
        }

        if (kb.homeKey.wasPressedThisFrame)
        {
            _caretIndex = 0;
            _anchor = -1;
            Refresh();
            return;
        }

        if (kb.endKey.wasPressedThisFrame)
        {
            _caretIndex = _value.Length;
            _anchor = -1;
            Refresh();
        }
    }

    void Insert(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return;
        }

        if (HasSelection())
        {
            DeleteSelection();
        }

        foreach (var c in raw)
        {
            if (_value.Length >= MaxLength)
            {
                break;
            }

            if (!Allowed(c))
            {
                continue;
            }

            if (c == ' ' && (_caretIndex == 0 || (_caretIndex > 0 && _value[_caretIndex - 1] == ' ')))
            {
                continue;
            }

            _value = _value.Insert(_caretIndex, c.ToString());
            _caretIndex++;
        }

        _blink = 0f;
    }

    static bool Allowed(char c)
    {
        if (c == '<' || c == '>' || char.IsControl(c))
        {
            return false;
        }

        if (AllowedChars.IndexOf(c) < 0)
        {
            return false;
        }

        var font = GameFont.Ui;
        return font == null || font.HasCharacter(c);
    }

    bool HasSelection() => _anchor >= 0 && _anchor != _caretIndex;

    string SelectedText()
    {
        var a = Mathf.Min(_anchor, _caretIndex);
        var b = Mathf.Max(_anchor, _caretIndex);
        return _value.Substring(a, b - a);
    }

    void DeleteSelection()
    {
        var a = Mathf.Min(_anchor, _caretIndex);
        var b = Mathf.Max(_anchor, _caretIndex);
        _value = _value.Remove(a, b - a);
        _caretIndex = a;
        _anchor = -1;
    }

    void MoveCaretToEnd()
    {
        _caretIndex = _value.Length;
        _anchor = -1;
        _blink = 0f;
        Refresh();
    }

    void Refresh()
    {
        _text.text = _value;
        _placeholder.enabled = string.IsNullOrEmpty(_value);
        LayoutCaret();
        LayoutSelection();
    }

    float WidthOf(string text)
    {
        if (string.IsNullOrEmpty(text) || _text.font == null)
        {
            return 0f;
        }

        _text.font.RequestCharactersInTexture(text, _text.fontSize, _text.fontStyle);
        var width = 0f;
        foreach (var c in text)
        {
            if (_text.font.GetCharacterInfo(c, out var info, _text.fontSize, _text.fontStyle))
            {
                width += info.advance;
            }
            else
            {
                width += _text.fontSize;
            }
        }

        return width;
    }

    void LayoutCaret()
    {
        if (_caret == null)
        {
            return;
        }

        var x = Pad + WidthOf(_value.Substring(0, Mathf.Clamp(_caretIndex, 0, _value.Length)));
        _caret.rectTransform.anchoredPosition = new Vector2(x, 0f);
        _caret.rectTransform.sizeDelta = new Vector2(GameFont.Pixel, GameFont.Resolve(16));
    }

    void LayoutSelection()
    {
        if (_selection == null)
        {
            return;
        }

        if (!_focused || !HasSelection())
        {
            _selection.gameObject.SetActive(false);
            return;
        }

        var a = Mathf.Min(_anchor, _caretIndex);
        var b = Mathf.Max(_anchor, _caretIndex);
        var x0 = Pad + WidthOf(_value.Substring(0, a));
        var x1 = Pad + WidthOf(_value.Substring(0, b));
        var rt = _selection.rectTransform;
        rt.anchorMin = new Vector2(0f, 0.2f);
        rt.anchorMax = new Vector2(0f, 0.8f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x0, 0f);
        rt.sizeDelta = new Vector2(Mathf.Max(GameFont.Pixel, x1 - x0), 0f);
        _selection.gameObject.SetActive(true);
        _selection.transform.SetAsFirstSibling();
    }
}
