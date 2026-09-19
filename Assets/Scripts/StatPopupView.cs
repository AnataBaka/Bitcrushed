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
    const float OffsetX = 18f;
    const float OffsetY = -18f;
    const float EdgePad = 10f;

    RectTransform _root;
    RectTransform _canvas;
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

    public bool IsOpen => gameObject.activeSelf;
    public ulong EntityId => _entityId;

    public static StatPopupView Create(Transform canvas)
    {
        var panel = UiFactory.RoundedPanel(canvas, "StatPopup", new Color(0.10f, 0.11f, 0.14f, 0.96f));
        panel.raycastTarget = true;

        var view = panel.gameObject.AddComponent<StatPopupView>();
        view._root = panel.rectTransform;
        view._canvas = canvas.GetComponent<RectTransform>();
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

        var hpRow = UiFactory.NewRect(panel.transform, "HpRow");
        hpRow.anchorMin = new Vector2(0f, 1f);
        hpRow.anchorMax = new Vector2(1f, 1f);
        hpRow.pivot = new Vector2(0.5f, 1f);
        hpRow.sizeDelta = new Vector2(-24f, 22f);
        hpRow.anchoredPosition = new Vector2(0f, -68f);
        view._hpFill = UiFactory.Bar(hpRow, "Hp", UiFactory.HpColor, out view._hpText);

        var manaRow = UiFactory.NewRect(panel.transform, "ManaRow");
        manaRow.anchorMin = new Vector2(0f, 1f);
        manaRow.anchorMax = new Vector2(1f, 1f);
        manaRow.pivot = new Vector2(0.5f, 1f);
        manaRow.sizeDelta = new Vector2(-24f, 22f);
        manaRow.anchoredPosition = new Vector2(0f, -94f);
        view._manaFill = UiFactory.Bar(manaRow, "Mana", UiFactory.ManaColor, out view._manaText);

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
        view._xp.rectTransform.anchoredPosition = new Vector2(0f, -122f);

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
        view._body.rectTransform.offsetMax = new Vector2(-14f, -168f);

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

    public void Open(ulong entityId, Vector2 screenPoint)
    {
        _entityId = entityId;
        var session = GameManager.Session();
        _phaseWhenOpened = session?.Phase ?? BattlePhase.Waiting;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
        Place(screenPoint);
    }

    public void Close()
    {
        _entityId = 0;
        gameObject.SetActive(false);
    }

    public bool ContainsScreenPoint(Vector2 screenPoint)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(_root, screenPoint, null);
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
        _subtitle.text = isEnemy ? entity.ClassName : entity.ClassName;

        UiFactory.SetBar(_hpFill, entity.Hp, entity.MaxHp);
        _hpText.text = $"{entity.Hp}/{entity.MaxHp} hp";
        UiFactory.SetBar(_manaFill, entity.Mana, entity.MaxMana);
        _manaText.text = $"{entity.Mana}/{entity.MaxMana} mp";

        _body.text =
            $"Strength    {entity.Strength}\n"
            + $"Damage      {entity.Atk}\n"
            + $"Speed       {entity.Speed}";

        var occupant = GameManager.FindPlayer(entity.EntityId);
        if (occupant == null)
        {
            _xpRow.SetActive(false);
            _gear.text = "";
            _root.sizeDelta = new Vector2(Width, 248f);
            return;
        }

        BindPlayerProgress(occupant);
        _gear.text =
            $"WPN  {GameManager.EquippedName(occupant.Identity, EquipSlot.Weapon)}\n"
            + $"HLM  {GameManager.EquippedName(occupant.Identity, EquipSlot.Helmet)}\n"
            + $"CHS  {GameManager.EquippedName(occupant.Identity, EquipSlot.Chestplate)}\n"
            + $"LEG  {GameManager.EquippedName(occupant.Identity, EquipSlot.Leggings)}";
        _root.sizeDelta = new Vector2(Width, _xpRow.activeSelf ? 360f : 320f);
    }

    void BindPlayerProgress(Player occupant)
    {
        _xpRow.SetActive(true);
        var need = GameManager.XpToNextLevel(occupant.CharacterLevel);
        _xp.text = $"Lv {occupant.CharacterLevel}    EXP  {occupant.Xp} / {need}";
    }

    public void Place(Vector2 screenPoint)
    {
        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas,
                screenPoint,
                null,
                out var local
            )
        )
        {
            return;
        }

        var pos = local + new Vector2(OffsetX, OffsetY);
        var canvasRect = _canvas.rect;
        var size = _root.sizeDelta;
        var minX = canvasRect.xMin + EdgePad;
        var maxX = canvasRect.xMax - EdgePad - size.x;
        var minY = canvasRect.yMin + EdgePad + size.y;
        var maxY = canvasRect.yMax - EdgePad;
        pos.x = Mathf.Clamp(pos.x, minX, Mathf.Max(minX, maxX));
        pos.y = Mathf.Clamp(pos.y, minY, Mathf.Max(minY, maxY));
        _root.anchoredPosition = pos;
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
