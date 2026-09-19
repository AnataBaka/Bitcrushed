using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// View-only inspect card. Reads table rows and never calls reducers.
public class StatPopupView : MonoBehaviour
{
    const float Width = 276f;
    const float CursorGap = 16f;
    const float EdgePad = 12f;

    RectTransform _root;
    RectTransform _canvas;
    Canvas _canvasComponent;
    Image _header;
    Text _title;
    Text _subtitle;
    Image _hpFill;
    Text _hpText;
    Image _manaFill;
    Text _manaText;
    Text _body;
    Text _gear;
    Text _xp;
    GameObject _xpRow;

    ulong _entityId;
    BattlePhase _phaseWhenOpened;
    Vector2 _screenPoint;

    public bool IsOpen => gameObject.activeSelf;
    public ulong EntityId => _entityId;

    public static StatPopupView Create(Transform canvas)
    {
        var panel = UiFactory.RoundedPanel(canvas, "StatPopup", new Color(0.10f, 0.11f, 0.14f, 0.96f));
        panel.raycastTarget = true;

        var view = panel.gameObject.AddComponent<StatPopupView>();
        view._root = panel.rectTransform;
        view._canvas = canvas.GetComponent<RectTransform>();
        view._canvasComponent = canvas.GetComponent<Canvas>();
        view._root.anchorMin = new Vector2(0f, 0f);
        view._root.anchorMax = new Vector2(0f, 0f);
        view._root.pivot = new Vector2(0f, 1f);
        view._root.sizeDelta = new Vector2(Width, 360f);

        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
        outline.effectDistance = new Vector2(2f, -2f);

        var header = UiFactory.RoundedPanel(panel.transform, "Header", new Color(0.22f, 0.32f, 0.48f, 1f));
        view._header = header;
        header.raycastTarget = false;
        header.rectTransform.anchorMin = new Vector2(0f, 1f);
        header.rectTransform.anchorMax = new Vector2(1f, 1f);
        header.rectTransform.pivot = new Vector2(0.5f, 1f);
        header.rectTransform.sizeDelta = new Vector2(-12f, 52f);
        header.rectTransform.anchoredPosition = new Vector2(0f, -8f);

        view._title = UiFactory.Label(
            header.transform,
            "Title",
            "",
            20,
            TextAnchor.MiddleLeft,
            UiFactory.TextColor
        );
        view._title.fontStyle = FontStyle.Bold;
        view._title.horizontalOverflow = HorizontalWrapMode.Overflow;
        UiFactory.Anchor(view._title.rectTransform, new Vector2(0f, 0.42f), new Vector2(1f, 1f));
        view._title.rectTransform.offsetMin = new Vector2(12f, 0f);
        view._title.rectTransform.offsetMax = new Vector2(-12f, 0f);

        view._subtitle = UiFactory.Label(
            header.transform,
            "Subtitle",
            "",
            14,
            TextAnchor.UpperLeft,
            new Color(0.88f, 0.88f, 0.84f, 0.9f)
        );
        UiFactory.Anchor(view._subtitle.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.48f));
        view._subtitle.rectTransform.offsetMin = new Vector2(12f, 4f);
        view._subtitle.rectTransform.offsetMax = new Vector2(-12f, 0f);

        MakeResourceRow(
            panel.transform,
            "HpRow",
            "HP",
            UiFactory.HpColor,
            -68f,
            out view._hpFill,
            out view._hpText
        );
        MakeResourceRow(
            panel.transform,
            "ManaRow",
            "MP",
            UiFactory.ManaColor,
            -92f,
            out view._manaFill,
            out view._manaText
        );

        view._xp = UiFactory.Label(
            panel.transform,
            "Xp",
            "",
            15,
            TextAnchor.UpperLeft,
            UiFactory.TextColor
        );
        view._xpRow = view._xp.gameObject;
        view._xp.rectTransform.anchorMin = new Vector2(0f, 1f);
        view._xp.rectTransform.anchorMax = new Vector2(1f, 1f);
        view._xp.rectTransform.pivot = new Vector2(0.5f, 1f);
        view._xp.rectTransform.sizeDelta = new Vector2(-24f, 40f);
        view._xp.rectTransform.anchoredPosition = new Vector2(0f, -118f);

        view._body = UiFactory.Label(
            panel.transform,
            "Body",
            "",
            16,
            TextAnchor.UpperLeft,
            UiFactory.TextColor
        );
        view._body.rectTransform.anchorMin = new Vector2(0f, 0f);
        view._body.rectTransform.anchorMax = new Vector2(1f, 1f);
        view._body.rectTransform.offsetMin = new Vector2(14f, 78f);
        view._body.rectTransform.offsetMax = new Vector2(-14f, -160f);

        view._gear = UiFactory.Label(
            panel.transform,
            "Gear",
            "",
            14,
            TextAnchor.LowerLeft,
            UiFactory.MutedColor
        );
        view._gear.rectTransform.anchorMin = new Vector2(0f, 0f);
        view._gear.rectTransform.anchorMax = new Vector2(1f, 0f);
        view._gear.rectTransform.pivot = new Vector2(0.5f, 0f);
        view._gear.rectTransform.sizeDelta = new Vector2(-24f, 72f);
        view._gear.rectTransform.anchoredPosition = new Vector2(0f, 10f);

        view._xpRow.SetActive(false);
        view.gameObject.SetActive(false);
        return view;
    }

    static void MakeResourceRow(
        Transform panel,
        string name,
        string tag,
        Color color,
        float yFromTop,
        out Image fill,
        out Text value
    )
    {
        var row = UiFactory.NewRect(panel, name);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(-24f, 20f);
        row.anchoredPosition = new Vector2(0f, yFromTop);

        var label = UiFactory.Label(row, "Tag", tag, 13, TextAnchor.MiddleLeft, UiFactory.MutedColor);
        label.fontStyle = FontStyle.Bold;
        label.rectTransform.anchorMin = new Vector2(0f, 0f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(34f, 0f);
        label.rectTransform.anchoredPosition = Vector2.zero;

        var barHost = UiFactory.NewRect(row, "BarHost");
        barHost.anchorMin = Vector2.zero;
        barHost.anchorMax = Vector2.one;
        barHost.offsetMin = new Vector2(34f, 2f);
        barHost.offsetMax = new Vector2(0f, -2f);
        fill = UiFactory.Bar(barHost, "Bar", color, out value);
    }

    public void Open(ulong entityId, Vector2 screenPoint)
    {
        _entityId = entityId;
        _screenPoint = screenPoint;
        var session = GameManager.Session();
        _phaseWhenOpened = session?.Phase ?? BattlePhase.Waiting;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        Refresh();
    }

    public void Close()
    {
        _entityId = 0;
        gameObject.SetActive(false);
    }

    public bool ContainsScreenPoint(Vector2 screenPoint)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(_root, screenPoint, EventCamera());
    }

    public void Refresh()
    {
        if (!IsOpen)
        {
            return;
        }

        var entity = GameManager.FindEntity(_entityId);
        var session = GameManager.Session();
        if (
            entity == null
            || !entity.Alive
            || session == null
            || session.Phase != _phaseWhenOpened
        )
        {
            Close();
            return;
        }

        var isEnemy = entity.Faction == Team.Enemies;
        _header.color = isEnemy
            ? new Color(0.46f, 0.20f, 0.18f, 1f)
            : new Color(0.22f, 0.34f, 0.50f, 1f);
        _title.text = entity.Name;
        _subtitle.text = entity.ClassName;

        UiFactory.SetBar(_hpFill, entity.Hp, entity.MaxHp);
        _hpText.text = $"{entity.Hp}/{entity.MaxHp}";
        UiFactory.SetBar(_manaFill, entity.Mana, entity.MaxMana);
        _manaText.text = $"{entity.Mana}/{entity.MaxMana}";

        _body.text =
            $"Strength       {entity.Strength}\n"
            + $"Dexterity      {entity.Dexterity}\n"
            + $"Intelligence   {entity.Intelligence}\n"
            + $"Speed          {entity.Speed}";

        var occupant = GameManager.FindPlayer(entity.EntityId);
        if (occupant == null)
        {
            _xpRow.SetActive(false);
            _gear.text = "";
            _root.sizeDelta = new Vector2(Width, 280f);
            Place(_screenPoint);
            return;
        }

        BindPlayerProgress(occupant);
        _gear.text =
            $"WPN  {GameManager.EquippedName(occupant.Identity, EquipSlot.Weapon)}\n"
            + $"HLM  {GameManager.EquippedName(occupant.Identity, EquipSlot.Helmet)}\n"
            + $"CHS  {GameManager.EquippedName(occupant.Identity, EquipSlot.Chestplate)}\n"
            + $"LEG  {GameManager.EquippedName(occupant.Identity, EquipSlot.Leggings)}";
        _root.sizeDelta = new Vector2(Width, _xpRow.activeSelf ? 392f : 352f);
        Place(_screenPoint);
    }

    void BindPlayerProgress(Player occupant)
    {
        _xpRow.SetActive(true);
        var need = GameManager.XpToNextLevel(occupant.CharacterLevel);
        _xp.text = $"Lv {occupant.CharacterLevel}    EXP  {occupant.Xp} / {need}";
    }

    /// Puts the top-left of the panel next to the cursor, flipping to the left
    /// or above when the preferred side would leave the canvas, then clamps.
    public void Place(Vector2 screenPoint)
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
        var size = _root.sizeDelta;
        if (size.x < 1f || size.y < 1f)
        {
            size = _root.rect.size;
        }

        var x = cursor.x + CursorGap;
        var y = cursor.y - CursorGap;
        if (x + size.x > parentRect.xMax - EdgePad)
        {
            x = cursor.x - CursorGap - size.x;
        }

        if (y - size.y < parentRect.yMin + EdgePad)
        {
            y = cursor.y + CursorGap + size.y;
        }

        var minX = parentRect.xMin + EdgePad;
        var maxX = parentRect.xMax - EdgePad - size.x;
        var minY = parentRect.yMin + EdgePad + size.y;
        var maxY = parentRect.yMax - EdgePad;
        x = Mathf.Clamp(x, minX, Mathf.Max(minX, maxX));
        y = Mathf.Clamp(y, minY, Mathf.Max(minY, maxY));

        // ScreenPointToLocalPoint is relative to the canvas pivot. AnchoredPosition
        // is relative to this child's anchors, so convert before assigning.
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

    void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        if (CancelPressed())
        {
            Close();
        }
    }

    static bool CancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}
