using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Local 3x3 gear grid. Reads PlayerItem rows and calls EquipItem / DropItem.
/// No inventory math lives here.
public class InventoryPopupView : MonoBehaviour, IPointerClickHandler
{
    public const float CellSize = 68f;
    public const float CellGap = 6f;
    public const float Pad = 10f;
    public const float TitleHeight = 22f;
    public const int Grid = 3;
    const float EdgePad = 12f;

    static readonly float PanelWidth = (Pad * 2f) + (CellSize * Grid) + (CellGap * (Grid - 1));
    static readonly float PanelHeight =
        Pad + TitleHeight + 4f + (CellSize * Grid) + (CellGap * (Grid - 1)) + Pad;

    RectTransform _canvas;
    Canvas _canvasComponent;
    RectTransform _panel;
    RectTransform _context;
    Button _equipButton;
    Button _dropButton;
    Text _equipLabel;
    Text _dropLabel;
    Cell[] _cells;
    uint _contextSlot;
    RectTransform _bagButton;
    ItemTooltipView _tooltip;
    System.Func<bool> _tooltipBlocked;

    public bool IsOpen => gameObject.activeSelf;
    public bool ContextOpen => _context != null && _context.gameObject.activeSelf;

    sealed class Cell : MonoBehaviour, IPointerClickHandler
    {
        public uint Index;
        public Image Frame;
        public Image Icon;
        public Text Caption;
        public InventoryPopupView Host;
        public ItemHoverTip Hover;
        public bool Occupied;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Host?.NotifyPanelClicked();
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Right || !Occupied)
            {
                return;
            }

            Host?.OpenContext(Index, eventData.position);
        }
    }

    public static InventoryPopupView Create(Transform canvas, RectTransform bagButton)
    {
        var root = UiFactory.Panel(canvas, "InventoryPopup", new Color(0f, 0f, 0f, 0f));
        root.raycastTarget = true;
        UiFactory.Anchor(root.rectTransform, Vector2.zero, Vector2.one);

        var view = root.gameObject.AddComponent<InventoryPopupView>();
        view._canvas = canvas.GetComponent<RectTransform>();
        view._canvasComponent = canvas.GetComponent<Canvas>();
        view._bagButton = bagButton;

        var panel = UiFactory.RoundedPanel(root.transform, "Panel", new Color(0.10f, 0.11f, 0.14f, 0.96f));
        panel.raycastTarget = true;
        var panelRt = panel.rectTransform;
        panelRt.anchorMin = new Vector2(0f, 0f);
        panelRt.anchorMax = new Vector2(0f, 0f);
        panelRt.pivot = new Vector2(0f, 0f);
        panelRt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        view._panel = panelRt;

        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
        outline.effectDistance = new Vector2(2f, -2f);

        var catcher = panel.gameObject.AddComponent<PanelClickCatcher>();
        catcher.Host = view;

        var title = UiFactory.Label(
            panel.transform,
            "Title",
            "INVENTORY",
            14,
            TextAnchor.UpperLeft,
            UiFactory.MutedColor
        );
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(-(Pad * 2f), TitleHeight);
        title.rectTransform.anchoredPosition = new Vector2(0f, -Pad);

        var grid = UiFactory.NewRect(panel.transform, "Grid");
        grid.anchorMin = new Vector2(0f, 0f);
        grid.anchorMax = new Vector2(1f, 1f);
        grid.offsetMin = new Vector2(Pad, Pad);
        grid.offsetMax = new Vector2(-Pad, -(Pad + TitleHeight + 4f));

        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(CellSize, CellSize);
        layout.spacing = new Vector2(CellGap, CellGap);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = Grid;

        view._cells = new Cell[GameManager.InventoryCapacity];
        for (var i = 0; i < view._cells.Length; i++)
        {
            view._cells[i] = MakeCell(grid, view, (uint)i);
        }

        view._context = MakeContext(root.transform, view, out view._equipButton, out view._dropButton, out view._equipLabel, out view._dropLabel);
        view._context.gameObject.SetActive(false);
        view.gameObject.SetActive(false);
        return view;
    }

    public void BindTooltip(ItemTooltipView tooltip, System.Func<bool> blocked)
    {
        _tooltip = tooltip;
        _tooltipBlocked = blocked;
        if (_cells == null)
        {
            return;
        }

        foreach (var cell in _cells)
        {
            BindCellHover(cell);
        }
    }

    static Cell MakeCell(Transform parent, InventoryPopupView host, uint index)
    {
        var frame = UiFactory.Panel(parent, $"Cell_{index}", new Color(0.18f, 0.18f, 0.22f, 0.55f));
        frame.raycastTarget = true;

        var icon = UiFactory.Graphic(frame.transform, "Icon", PlaceholderArt.Solid(Color.white), Color.white);
        icon.gameObject.SetActive(false);

        var caption = UiFactory.Label(
            frame.transform,
            "Name",
            "",
            10,
            TextAnchor.UpperCenter,
            UiFactory.TextColor
        );
        ItemIconFit.LayoutInventory(icon.rectTransform, caption);

        var cell = frame.gameObject.AddComponent<Cell>();
        cell.Index = index;
        cell.Frame = frame;
        cell.Icon = icon;
        cell.Caption = caption;
        cell.Host = host;
        cell.Hover = frame.gameObject.AddComponent<ItemHoverTip>();
        return cell;
    }

    static RectTransform MakeContext(
        Transform parent,
        InventoryPopupView host,
        out Button equip,
        out Button drop,
        out Text equipLabel,
        out Text dropLabel
    )
    {
        var panel = UiFactory.RoundedPanel(parent, "Context", new Color(0.12f, 0.13f, 0.16f, 0.98f));
        panel.raycastTarget = true;
        var rt = panel.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(108f, 76f);

        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        equip = UiFactory.TextButton(panel.transform, "Equip", "Equip", 15, 30f);
        var equipRt = equip.GetComponent<RectTransform>();
        equipRt.anchorMin = new Vector2(0.5f, 1f);
        equipRt.anchorMax = new Vector2(0.5f, 1f);
        equipRt.pivot = new Vector2(0.5f, 1f);
        equipRt.sizeDelta = new Vector2(96f, 30f);
        equipRt.anchoredPosition = new Vector2(0f, -6f);
        equipLabel = equip.GetComponentInChildren<Text>();
        equip.onClick.AddListener(host.HandleEquipClicked);

        drop = UiFactory.TextButton(panel.transform, "Drop", "Drop", 15, 30f);
        var dropRt = drop.GetComponent<RectTransform>();
        dropRt.anchorMin = new Vector2(0.5f, 0f);
        dropRt.anchorMax = new Vector2(0.5f, 0f);
        dropRt.pivot = new Vector2(0.5f, 0f);
        dropRt.sizeDelta = new Vector2(96f, 30f);
        dropRt.anchoredPosition = new Vector2(0f, 6f);
        dropLabel = drop.GetComponentInChildren<Text>();
        drop.onClick.AddListener(host.HandleDropClicked);

        return rt;
    }

    public void Toggle(Vector2 screenPoint)
    {
        if (IsOpen)
        {
            Close();
            return;
        }

        Open(screenPoint);
    }

    public void Open(Vector2 screenPoint)
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        CloseContext();
        PlacePanel(screenPoint);
        Refresh();
    }

    public void Close()
    {
        CloseContext();
        _tooltip?.Hide();
        gameObject.SetActive(false);
    }

    public bool HandleEscape()
    {
        if (!IsOpen)
        {
            return false;
        }

        if (ContextOpen)
        {
            CloseContext();
            return true;
        }

        Close();
        return true;
    }

    public bool ContainsInteractivePoint(Vector2 screenPoint)
    {
        if (!IsOpen)
        {
            return false;
        }

        var camera = EventCamera();
        if (RectTransformUtility.RectangleContainsScreenPoint(_panel, screenPoint, camera))
        {
            return true;
        }

        return ContextOpen
            && RectTransformUtility.RectangleContainsScreenPoint(_context, screenPoint, camera);
    }

    public void Refresh()
    {
        if (!IsOpen)
        {
            return;
        }

        for (var i = 0; i < _cells.Length; i++)
        {
            FillCell(_cells[i], GameManager.InventoryAt((uint)i));
        }

        if (ContextOpen)
        {
            RefreshContextButtons();
        }

        foreach (var cell in _cells)
        {
            BindCellHover(cell);
            cell.Hover?.Refresh();
        }
    }

    static void FillCell(Cell cell, PlayerItem item)
    {
        var def = item == null ? null : GameManager.ItemDefOf(item);
        cell.Occupied = def != null;
        if (def == null)
        {
            cell.Frame.color = new Color(0.18f, 0.18f, 0.22f, 0.45f);
            cell.Icon.gameObject.SetActive(false);
            cell.Caption.text = "";
            return;
        }

        cell.Frame.color = new Color(0.24f, 0.23f, 0.20f, 1f);
        cell.Icon.gameObject.SetActive(true);
        ItemIconFit.Bind(cell.Icon, GearArt.Sprite(def));
        cell.Caption.text = def.ShortName;
        cell.Caption.color = UiFactory.TextColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (OverBag(eventData.position))
        {
            Close();
            return;
        }

        if (ContextOpen)
        {
            CloseContext();
            return;
        }

        Close();
    }

    public void NotifyPanelClicked()
    {
        if (ContextOpen)
        {
            CloseContext();
        }
    }

    void BindCellHover(Cell cell)
    {
        if (cell?.Hover == null || _tooltip == null)
        {
            return;
        }

        var index = cell.Index;
        cell.Hover.Bind(
            _tooltip,
            () =>
            {
                var item = GameManager.InventoryAt(index);
                return item == null ? null : GameManager.ItemDefOf(item);
            },
            () => ContextOpen || (_tooltipBlocked != null && _tooltipBlocked())
        );
    }

    void OpenContext(uint slotIndex, Vector2 screenPoint)
    {
        _tooltip?.Hide();
        _contextSlot = slotIndex;
        _context.gameObject.SetActive(true);
        _context.SetAsLastSibling();
        RefreshContextButtons();
        PlaceContext(screenPoint);
    }

    void CloseContext()
    {
        if (_context != null)
        {
            _context.gameObject.SetActive(false);
        }
    }

    void RefreshContextButtons()
    {
        var allowed = GameManager.EquipmentChangesAllowed();
        var item = GameManager.InventoryAt(_contextSlot);
        var def = item == null ? null : GameManager.ItemDefOf(item);
        var canEquip = allowed && GameManager.CanEquipDef(def);

        _equipButton.interactable = canEquip;
        _dropButton.interactable = allowed;
        if (_equipLabel != null)
        {
            _equipLabel.color = canEquip ? UiFactory.TextColor : UiFactory.MutedColor;
        }

        if (_dropLabel != null)
        {
            _dropLabel.color = allowed ? UiFactory.TextColor : UiFactory.MutedColor;
        }
    }

    void HandleEquipClicked()
    {
        if (!_equipButton.interactable)
        {
            return;
        }

        GameManager.EquipItem(_contextSlot);
        CloseContext();
    }

    void HandleDropClicked()
    {
        if (!_dropButton.interactable)
        {
            return;
        }

        GameManager.DropItem(_contextSlot);
        CloseContext();
    }

    void PlacePanel(Vector2 screenPoint)
    {
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
        var x = cursor.x;
        var y = cursor.y;
        var maxX = parentRect.xMax - EdgePad - PanelWidth;
        var maxY = parentRect.yMax - EdgePad - PanelHeight;
        var minX = parentRect.xMin + EdgePad;
        var minY = parentRect.yMin + EdgePad;
        x = Mathf.Clamp(x, minX, Mathf.Max(minX, maxX));
        y = Mathf.Clamp(y, minY, Mathf.Max(minY, maxY));

        var anchorRef = new Vector2(
            Mathf.Lerp(parentRect.xMin, parentRect.xMax, _panel.anchorMin.x),
            Mathf.Lerp(parentRect.yMin, parentRect.yMax, _panel.anchorMin.y)
        );
        _panel.anchoredPosition = new Vector2(x, y) - anchorRef;
    }

    void PlaceContext(Vector2 screenPoint)
    {
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
        var size = _context.sizeDelta;
        var x = cursor.x;
        var y = cursor.y;
        x = Mathf.Clamp(x, parentRect.xMin + EdgePad, parentRect.xMax - EdgePad - size.x);
        y = Mathf.Clamp(y, parentRect.yMin + EdgePad + size.y, parentRect.yMax - EdgePad);

        var anchorRef = new Vector2(
            Mathf.Lerp(parentRect.xMin, parentRect.xMax, _context.anchorMin.x),
            Mathf.Lerp(parentRect.yMin, parentRect.yMax, _context.anchorMin.y)
        );
        _context.anchoredPosition = new Vector2(x, y) - anchorRef;
    }

    bool OverBag(Vector2 screenPoint)
    {
        return _bagButton != null
            && RectTransformUtility.RectangleContainsScreenPoint(_bagButton, screenPoint, EventCamera());
    }

    Camera EventCamera()
    {
        if (_canvasComponent == null || _canvasComponent.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return _canvasComponent.worldCamera;
    }

    sealed class PanelClickCatcher : MonoBehaviour, IPointerClickHandler
    {
        public InventoryPopupView Host;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Host?.NotifyPanelClicked();
            }
        }
    }
}
