using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;

/// Inspect card. Reads table rows and, for the local living player, calls
/// SpendStatPoint when a + is clicked. It never computes new stat values.
public class StatPopupView : MonoBehaviour
{
    const float Width = 336f;
    const float CursorGap = 16f;
    const float EdgePad = 12f;
    const float HeaderHeight = 60f;
    const float LabelWidth = 112f;
    const float ValueWidth = 96f;
    const float PlusWidth = 24f;
    const float RowHeight = 22f;

    static readonly StatType[] SpendableStats =
    {
        StatType.Strength,
        StatType.Speed,
        StatType.Intelligence,
        StatType.Dexterity,
    };

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
    Text _strengthText;
    Text _speedText;
    Text _intelligenceText;
    Text _dexterityText;
    Button _strengthPlus;
    Button _speedPlus;
    Button _intelligencePlus;
    Button _dexterityPlus;
    Text _pointsText;
    GameObject _pointsRow;
    Text _levelText;
    Text _xpText;
    Text _weaponText;
    Text _amuletText;
    GameObject _playerExtras;

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
        view._title.resizeTextForBestFit = true;
        view._title.resizeTextMinSize = 14;
        view._title.resizeTextMaxSize = 22;
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

        var body = UiFactory.NewRect(panel.transform, "Body");
        body.anchorMin = new Vector2(0f, 0f);
        body.anchorMax = new Vector2(1f, 1f);
        body.offsetMin = new Vector2(14f, 10f);
        body.offsetMax = new Vector2(-14f, -HeaderHeight);
        var layout = body.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 3f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.padding = new RectOffset(0, 0, 0, 0);

        MakeResourceRow(body, "Health", UiFactory.HpColor, out view._hpFill, out view._hpText);
        MakeResourceRow(body, "Mana", UiFactory.ManaColor, out view._manaFill, out view._manaText);
        view._strengthText = MakeStatRow(body, "Strength", StatType.Strength, out view._strengthPlus);
        view._speedText = MakeStatRow(body, "Speed", StatType.Speed, out view._speedPlus);
        view._intelligenceText = MakeStatRow(
            body,
            "Intelligence",
            StatType.Intelligence,
            out view._intelligencePlus
        );
        view._dexterityText = MakeStatRow(body, "Dexterity", StatType.Dexterity, out view._dexterityPlus);

        var extras = UiFactory.NewRect(body, "PlayerExtras");
        extras.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight * 5f;
        var extrasLayout = extras.gameObject.AddComponent<VerticalLayoutGroup>();
        extrasLayout.spacing = 3f;
        extrasLayout.childAlignment = TextAnchor.UpperLeft;
        extrasLayout.childControlHeight = true;
        extrasLayout.childControlWidth = true;
        extrasLayout.childForceExpandHeight = false;
        extrasLayout.childForceExpandWidth = true;
        view._playerExtras = extras.gameObject;
        view._pointsText = MakeValueRow(extras, "Available Skill Points", out view._pointsRow);
        view._levelText = MakeValueRow(extras, "Level");
        view._xpText = MakeValueRow(extras, "EXP");
        view._weaponText = MakeValueRow(extras, "Weapon");
        view._amuletText = MakeValueRow(extras, "Amulet");

        view._playerExtras.SetActive(false);
        view.gameObject.SetActive(false);
        return view;
    }

    static RectTransform MakeRow(Transform parent, string name)
    {
        var row = UiFactory.NewRect(parent, name);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = false;
        var element = row.gameObject.AddComponent<LayoutElement>();
        element.minHeight = RowHeight;
        element.preferredHeight = RowHeight;
        element.flexibleWidth = 1f;
        return row;
    }

    static Text MakeLabel(Transform row, string caption)
    {
        var label = UiFactory.Label(row, "Label", caption, 15, TextAnchor.MiddleLeft, UiFactory.MutedColor);
        label.fontStyle = FontStyle.Bold;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        var element = label.gameObject.AddComponent<LayoutElement>();
        element.minWidth = LabelWidth;
        element.preferredWidth = LabelWidth;
        element.flexibleWidth = 0f;
        return label;
    }

    static Text MakeValue(Transform row)
    {
        var value = UiFactory.Label(row, "Value", "", 15, TextAnchor.MiddleRight, UiFactory.TextColor);
        value.horizontalOverflow = HorizontalWrapMode.Overflow;
        var element = value.gameObject.AddComponent<LayoutElement>();
        element.minWidth = ValueWidth;
        element.preferredWidth = ValueWidth;
        element.flexibleWidth = 0f;
        return value;
    }

    static void MakeResourceRow(
        Transform parent,
        string caption,
        Color color,
        out Image fill,
        out Text value
    )
    {
        var row = MakeRow(parent, caption + "Row");
        MakeLabel(row, caption);

        var barHost = UiFactory.NewRect(row, "BarHost");
        var hostElement = barHost.gameObject.AddComponent<LayoutElement>();
        hostElement.minWidth = ValueWidth;
        hostElement.preferredWidth = ValueWidth;
        hostElement.flexibleWidth = 1f;
        hostElement.minHeight = 14f;
        hostElement.preferredHeight = 14f;
        fill = UiFactory.Bar(barHost, "Bar", color, out value);
        value.alignment = TextAnchor.MiddleRight;
        value.fontSize = 15;
        value.horizontalOverflow = HorizontalWrapMode.Overflow;
        value.rectTransform.offsetMin = new Vector2(4f, 0f);
        value.rectTransform.offsetMax = new Vector2(-2f, 0f);

        MakePlusSpacer(row);
    }

    static Text MakeValueRow(Transform parent, string caption) =>
        MakeValueRow(parent, caption, out _);

    static Text MakeValueRow(Transform parent, string caption, out GameObject rowObject)
    {
        var row = MakeRow(parent, caption + "Row");
        rowObject = row.gameObject;
        MakeLabel(row, caption);

        var spacer = UiFactory.NewRect(row, "Spacer");
        var spacerElement = spacer.gameObject.AddComponent<LayoutElement>();
        spacerElement.minWidth = 8f;
        spacerElement.flexibleWidth = 1f;

        var value = MakeValue(row);
        MakePlusSpacer(row);
        return value;
    }

    static Text MakeStatRow(Transform parent, string caption, StatType stat, out Button plus)
    {
        var row = MakeRow(parent, caption + "Row");
        MakeLabel(row, caption);

        var spacer = UiFactory.NewRect(row, "Spacer");
        var spacerElement = spacer.gameObject.AddComponent<LayoutElement>();
        spacerElement.minWidth = 8f;
        spacerElement.flexibleWidth = 1f;

        var value = MakeValue(row);
        plus = MakePlusButton(row, stat);
        return value;
    }

    static void MakePlusSpacer(Transform row)
    {
        var spacer = UiFactory.NewRect(row, "PlusSlot");
        var element = spacer.gameObject.AddComponent<LayoutElement>();
        element.minWidth = PlusWidth;
        element.preferredWidth = PlusWidth;
        element.flexibleWidth = 0f;
    }

    static Button MakePlusButton(Transform row, StatType stat)
    {
        var button = UiFactory.TextButton(row, "Plus", "+", 16, RowHeight);
        var element = button.gameObject.GetComponent<LayoutElement>();
        element.minWidth = PlusWidth;
        element.preferredWidth = PlusWidth;
        element.flexibleWidth = 0f;
        element.minHeight = RowHeight;
        element.preferredHeight = RowHeight;
        button.onClick.AddListener(
            () =>
            {
                if (!IsSpendable(stat))
                {
                    return;
                }

                var player = GameManager.LocalPlayer();
                if (player == null || player.UnspentStatPoints == 0)
                {
                    return;
                }

                GameManager.SpendStatPoint(stat);
            }
        );
        return button;
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

        var entity = GameManager.FindEntity(_entityId) ?? CombatHpPresenter.Ghost(_entityId);
        var session = GameManager.Session();
        if (
            entity == null
            || !CombatHpPresenter.IsTargetable(entity)
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
        if (isEnemy)
        {
            _title.text = entity.Name;
            _subtitle.text = EnemySpriteLibrary.DisplayName(
                ClassSpriteArt.SpriteClassFor(entity.ClassName, entity.EntityId, true)
            );
        }
        else
        {
            _title.text = entity.Name;
            _subtitle.text = entity.ClassName;
        }

        var hp = CombatHpPresenter.DisplayedHp(entity);
        UiFactory.SetBar(_hpFill, hp, entity.MaxHp);
        _hpText.text = $"{hp}/{entity.MaxHp}";
        UiFactory.SetBar(_manaFill, entity.Mana, entity.MaxMana);
        _manaText.text = $"{entity.Mana}/{entity.MaxMana}";
        _strengthText.text = entity.Strength.ToString();
        _speedText.text = entity.Speed.ToString();
        _intelligenceText.text = entity.Intelligence.ToString();
        _dexterityText.text = entity.Dexterity.ToString();

        var occupant = GameManager.FindPlayer(entity.EntityId);
        var local = GameManager.LocalEntity();
        var isOwn = local != null && local.EntityId == entity.EntityId && occupant != null;
        var points = occupant == null ? 0u : occupant.UnspentStatPoints;
        var canSpend = isOwn && occupant != null && points > 0;
        SetPlus(_strengthPlus, isOwn, canSpend);
        SetPlus(_speedPlus, isOwn, canSpend);
        SetPlus(_intelligencePlus, isOwn, canSpend);
        SetPlus(_dexterityPlus, isOwn, canSpend);

        if (occupant == null)
        {
            _playerExtras.SetActive(false);
            _root.sizeDelta = new Vector2(Width, 236f);
            Place(_screenPoint);
            return;
        }

        _playerExtras.SetActive(true);
        if (_pointsRow != null)
        {
            _pointsRow.SetActive(true);
        }

        var need = GameManager.XpToNextLevel(occupant.CharacterLevel);
        _pointsText.text = occupant.UnspentStatPoints.ToString();
        _levelText.text = occupant.CharacterLevel.ToString();
        _xpText.text = $"{occupant.Xp}/{need}";
        _weaponText.text = GameManager.EquippedName(occupant.Identity, EquipSlot.Weapon);
        _amuletText.text = GameManager.EquippedName(occupant.Identity, EquipSlot.Amulet);
        _root.sizeDelta = new Vector2(Width, 364f);
        Place(_screenPoint);
    }

    static void SetPlus(Button plus, bool visible, bool interactable)
    {
        if (plus == null)
        {
            return;
        }

        plus.interactable = visible && interactable;
        if (plus.targetGraphic != null)
        {
            plus.targetGraphic.enabled = visible;
        }

        var label = plus.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.enabled = visible;
        }
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

        var anchorRef = new Vector2(
            Mathf.Lerp(parentRect.xMin, parentRect.xMax, _root.anchorMin.x),
            Mathf.Lerp(parentRect.yMin, parentRect.yMax, _root.anchorMin.y)
        );
        _root.anchoredPosition = new Vector2(x, y) - anchorRef;
    }

    static bool IsSpendable(StatType stat)
    {
        foreach (var allowed in SpendableStats)
        {
            if (allowed == stat)
            {
                return true;
            }
        }

        return false;
    }

    Camera EventCamera()
    {
        if (_canvasComponent == null || _canvasComponent.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return _canvasComponent.worldCamera;
    }
}
