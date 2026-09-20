using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

/// Esc overlay: Leave Game, click-outside-to-close, and a dimmer that eats
/// every click underneath. No game rules live here.
public class EscapeMenuView : MonoBehaviour, IPointerClickHandler
{
    public const float OverlayAlpha = 0.35f;
    const float LeaveWaitSeconds = 2f;

    RectTransform _panel;
    StatPopupView _popup;
    bool _leaving;
    readonly StringBuilder _typed = new StringBuilder();

    public bool IsOpen => gameObject.activeSelf;

    public static EscapeMenuView Create(Transform canvas, StatPopupView popup)
    {
        var overlay = UiFactory.Panel(canvas, "EscapeMenu", new Color(0f, 0f, 0f, OverlayAlpha));
        overlay.raycastTarget = true;
        UiFactory.Anchor(overlay.rectTransform, Vector2.zero, Vector2.one);

        var view = overlay.gameObject.AddComponent<EscapeMenuView>();
        view._popup = popup;

        var panel = UiFactory.RoundedPanel(overlay.transform, "Panel", new Color(0.10f, 0.11f, 0.14f, 0.98f));
        panel.raycastTarget = true;
        var panelRt = panel.rectTransform;
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(320f, 148f);
        panelRt.anchoredPosition = Vector2.zero;
        view._panel = panelRt;

        var title = UiFactory.Label(
            panel.transform,
            "Title",
            "Menu",
            24,
            TextAnchor.MiddleCenter,
            UiFactory.TextColor
        );
        title.rectTransform.anchorMin = new Vector2(0f, 0.62f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.offsetMin = Vector2.zero;
        title.rectTransform.offsetMax = Vector2.zero;

        var leave = UiFactory.TextButton(panel.transform, "Leave", "Leave Game", 24, 48f);
        var leaveRt = leave.GetComponent<RectTransform>();
        leaveRt.anchorMin = new Vector2(0.5f, 0.18f);
        leaveRt.anchorMax = new Vector2(0.5f, 0.18f);
        leaveRt.pivot = new Vector2(0.5f, 0.5f);
        leaveRt.sizeDelta = new Vector2(240f, 48f);
        leaveRt.anchoredPosition = Vector2.zero;
        leave.onClick.AddListener(view.HandleLeaveClicked);

        view.gameObject.SetActive(false);
        return view;
    }

    public void Close()
    {
        if (_leaving)
        {
            return;
        }

        gameObject.SetActive(false);
    }

    public void Toggle()
    {
        if (_leaving)
        {
            return;
        }

        if (IsOpen)
        {
            Close();
            return;
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void HandleEscape()
    {
        if (IsOpen)
        {
            Close();
            return;
        }

        if (_popup != null && _popup.IsOpen)
        {
            _popup.Close();
            return;
        }

        Toggle();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_leaving || eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (RectTransformUtility.RectangleContainsScreenPoint(_panel, eventData.position, eventData.pressEventCamera))
        {
            return;
        }

        Close();
    }

    void HandleLeaveClicked()
    {
        if (_leaving)
        {
            return;
        }

        StartCoroutine(LeaveRoutine());
    }

    IEnumerator LeaveRoutine()
    {
        _leaving = true;
        var hadPlayer = GameManager.LocalPlayer() != null;
        if (hadPlayer)
        {
            GameManager.LeaveGame();
            var deadline = Time.realtimeSinceStartup + LeaveWaitSeconds;
            while (GameManager.LocalPlayer() != null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        GameManager.Disconnect();
        QuitClient();
    }

    static void QuitClient()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#elif UNITY_WEBGL && !UNITY_EDITOR
        // Application.Quit is a no-op in WebGL; the tab stays open after disconnect.
#else
        Application.Quit();
#endif
    }

    void Update()
    {
        if (!IsOpen)
        {
            _typed.Clear();
            return;
        }

        transform.SetAsLastSibling();
        PollCheatTyping();
    }

    void PollCheatTyping()
    {
        var letter = TypedLetterThisFrame();
        if (letter == '\0')
        {
            return;
        }

        _typed.Append(letter);
        if (_typed.Length > 16)
        {
            _typed.Remove(0, _typed.Length - 16);
        }

        if (_typed.ToString().EndsWith("cheat"))
        {
            _typed.Clear();
            GameManager.Cheat();
        }
    }

    static char TypedLetterThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return '\0';
        }

        for (var i = 0; i < 26; i++)
        {
            var key = (Key)((int)Key.A + i);
            if (keyboard[key].wasPressedThisFrame)
            {
                return (char)('a' + i);
            }
        }

        return '\0';
#else
        for (var i = 0; i < 26; i++)
        {
            if (Input.GetKeyDown(KeyCode.A + i))
            {
                return (char)('a' + i);
            }
        }

        return '\0';
#endif
    }
}
