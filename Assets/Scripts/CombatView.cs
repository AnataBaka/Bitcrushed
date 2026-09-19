using System.Collections;
using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using CombatActionType = SpacetimeDB.Types.CombatActionType;

public class CombatView : MonoBehaviour
{
    const int SlotCount = 3;
    const int BagColumns = 4;
    const int BagRows = 3;
    const int BagCapacity = BagColumns * BagRows;
    const float OrbScale = 0.72f;
    const int BattleLogMax = 12;
    const float GearCell = 68f;
    const float GearGap = 8f;

    [SerializeField] Sprite playerSprite;
    [SerializeField] Sprite enemySprite;

    readonly SlotView[] _players = new SlotView[SlotCount];
    readonly SlotView[] _enemies = new SlotView[SlotCount];
    readonly Queue<CombatEvent> _lunges = new();

    Button _attacks;
    Button _focus;
    Button _items;
    GameObject _menu;
    Text _toast;
    Text _hudName;
    Image _hudHpFill;
    Image _hudMpFill;
    float _hudHpAmount = 1f;
    float _hudMpAmount = 1f;
    float _hudHpShown = 1f;
    float _hudMpShown = 1f;
    Transform _turnRow;
    Transform _armorCol;
    Transform _weaponSlot;
    Transform _bagGrid;
    RectTransform _hudRoot;
    GameObject _logPanel;
    Text _logText;
    Button _logToggle;
    Text _hudHpText;
    Text _hudMpText;
    Text _hudPotText;
    readonly List<string> _battleLog = new();
    bool _logOpen = true;
    bool _menuOpen;
    bool _itemsMenu;
    bool _canAct;
    bool _eventsBound;
    bool _lunging;
    uint _lastEventId;
    Sprite _whiteSprite;
    static Sprite _uiSprite;
    Font _font;

    sealed class SlotView
    {
        public Transform Root;
        public Vector3 Home;
        public SpriteRenderer Orb;
        public TextMesh NameLabel;
        public Transform HpFill;
        public Transform MpFill;
        public TextMesh HpLabel;
        public TextMesh MpLabel;
        public float ShownHp = 1f;
        public float ShownMp = 1f;
        public bool Busy;
        public bool IsTurn;
    }

    void Start()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        SetupCamera();
        CreateParty();
        CreateHud();
        GameManager.PartyChanged += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        GameManager.PartyChanged -= Refresh;
        if (_eventsBound && GameManager.Conn != null)
        {
            GameManager.Conn.Db.CombatEvent.OnInsert -= OnCombatEvent;
        }
    }

    void Update()
    {
        BindEvents();
        SmoothBars();
        if (!_lunging && _lunges.Count > 0)
        {
            StartCoroutine(PlayLunge(_lunges.Dequeue()));
        }
    }

    void SetupCamera()
    {
        var camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        camera.orthographic = true;
        camera.orthographicSize = 4.7f;
        camera.transform.position = new Vector3(0f, 0.25f, -10f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.07f, 0.08f, 0.14f);
    }

    void CreateParty()
    {
        var playerXs = new[] { -3.45f, -2.8f, -3.45f };
        var enemyXs = new[] { 3.85f, 3.2f, 3.85f };
        var ys = new[] { 1.85f, 0.35f, -1.15f };

        for (var i = 0; i < SlotCount; i++)
        {
            _players[i] = CreateSlot($"Player{i}", new Vector3(playerXs[i], ys[i], 0f), true);
            _enemies[i] = CreateSlot($"Enemy{i}", new Vector3(enemyXs[i], ys[i], 0f), false);
        }
    }

    SlotView CreateSlot(string name, Vector3 position, bool playerSide)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * OrbScale;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = playerSide
            ? (playerSprite != null ? playerSprite : CreateOrbSprite(new Color(0.25f, 0.8f, 0.4f)))
            : (enemySprite != null ? enemySprite : CreateOrbSprite(new Color(0.85f, 0.28f, 0.28f)));
        renderer.sortingOrder = 2;

        var slot = new SlotView
        {
            Root = go.transform,
            Home = position,
            Orb = renderer,
            NameLabel = CreateWorldText(go.transform, new Vector3(0f, 1.02f, 0f), 38, Color.white),
            HpFill = CreateBar(go.transform, new Vector3(0f, -0.62f, 0f), new Color(0.82f, 0.18f, 0.22f)),
            MpFill = CreateBar(go.transform, new Vector3(0f, -0.84f, 0f), new Color(0.25f, 0.55f, 0.95f)),
            HpLabel = CreateWorldText(go.transform, new Vector3(0f, -0.62f, 0f), 32, Color.white),
            MpLabel = CreateWorldText(go.transform, new Vector3(0f, -0.84f, 0f), 32, Color.white),
        };
        slot.HpLabel.characterSize = 0.34f;
        slot.MpLabel.characterSize = 0.34f;
        return slot;
    }

    Transform CreateBar(Transform parent, Vector3 localPos, Color fillColor)
    {
        var bg = new GameObject("BarBg");
        bg.transform.SetParent(parent, false);
        bg.transform.localPosition = localPos;
        bg.transform.localScale = new Vector3(1.15f, 0.12f, 1f);
        var bgRenderer = bg.AddComponent<SpriteRenderer>();
        bgRenderer.sprite = WhiteSprite();
        bgRenderer.color = new Color(0.08f, 0.08f, 0.1f, 0.9f);
        bgRenderer.sortingOrder = 6;

        var fill = new GameObject("BarFill");
        fill.transform.SetParent(bg.transform, false);
        fill.transform.localPosition = new Vector3(-0.5f, 0f, 0f);
        fill.transform.localScale = new Vector3(1f, 0.72f, 1f);
        var fillRenderer = fill.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = LeftWhiteSprite();
        fillRenderer.color = fillColor;
        fillRenderer.sortingOrder = 7;
        return fill.transform;
    }

    static TextMesh CreateWorldText(Transform parent, Vector3 localPos, int fontSize, Color color)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one * 0.08f;
        var mesh = go.AddComponent<TextMesh>();
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.fontSize = fontSize;
        mesh.color = color;
        mesh.characterSize = 0.4f;
        var meshRenderer = go.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingOrder = 8;
        }
        return mesh;
    }

    Sprite CreateOrbSprite(Color color)
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var radius = size * 0.46f;
        var center = (size - 1) * 0.5f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var dist = Mathf.Sqrt(dx * dx + dy * dy);
                var glow = Mathf.Clamp01((radius + 10f - dist) / 18f) * 0.25f;
                var alpha = Mathf.Max(Mathf.Clamp01((radius - dist) / 4f), glow);
                var inner = Mathf.Clamp01((radius * 0.55f - dist) / radius);
                var pixel = Color.Lerp(color, Color.white, inner * 0.35f);
                pixel.a = alpha;
                tex.SetPixel(x, y, pixel);
            }
        }

        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
    }

    Sprite WhiteSprite()
    {
        if (_whiteSprite != null)
        {
            return _whiteSprite;
        }

        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                tex.SetPixel(x, y, Color.white);
            }
        }

        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        _whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return _whiteSprite;
    }

    static Sprite UiSprite()
    {
        if (_uiSprite != null)
        {
            return _uiSprite;
        }

        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                tex.SetPixel(x, y, Color.white);
            }
        }

        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        _uiSprite = Sprite.Create(tex, new Rect(0f, 0f, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return _uiSprite;
    }

    Sprite LeftWhiteSprite()
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                tex.SetPixel(x, y, Color.white);
            }
        }

        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0f, 0f, 4, 4), new Vector2(0f, 0.5f), 4f);
    }

    void CreateHud()
    {
        EnsureEventSystem();
        var canvasGo = new GameObject("CombatHud");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var hud = Panel(canvasGo.transform, "LocalHud", new Vector2(0.30f, 0.02f), new Vector2(0.66f, 0.175f), new Color(0.08f, 0.09f, 0.16f, 0.92f));
        hud.GetComponent<Outline>().effectColor = new Color(0.95f, 0.78f, 0.28f, 0.45f);
        _hudRoot = hud.GetComponent<RectTransform>();
        var hudLayout = hud.AddComponent<VerticalLayoutGroup>();
        hudLayout.padding = new RectOffset(12, 12, 8, 8);
        hudLayout.spacing = 5f;
        hudLayout.childAlignment = TextAnchor.UpperCenter;
        hudLayout.childControlWidth = true;
        hudLayout.childControlHeight = true;
        hudLayout.childForceExpandWidth = true;
        hudLayout.childForceExpandHeight = false;

        var header = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        header.transform.SetParent(hud.transform, false);
        header.GetComponent<LayoutElement>().preferredHeight = 26f;
        header.GetComponent<LayoutElement>().flexibleHeight = 0f;
        var headerLayout = header.GetComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 8f;
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandHeight = true;
        headerLayout.childForceExpandWidth = true;

        _hudName = Label(header.transform, "Name", "Ready", 20, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one);
        _hudName.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        _hudPotText = Label(header.transform, "Pots", "Pots x1", 16, TextAnchor.MiddleRight, Vector2.zero, Vector2.one);
        var potLe = _hudPotText.gameObject.AddComponent<LayoutElement>();
        potLe.preferredWidth = 90f;
        potLe.flexibleWidth = 0f;
        _logToggle = CreateButton(header.transform, "Hide Log", ToggleBattleLog);
        var logLe = _logToggle.GetComponent<LayoutElement>();
        logLe.preferredWidth = 110f;
        logLe.preferredHeight = 24f;
        logLe.flexibleWidth = 0f;
        var logLabel = _logToggle.GetComponentInChildren<Text>();
        if (logLabel != null)
        {
            logLabel.fontSize = 15;
        }

        _hudHpFill = Bar(hud.transform, "Hp", Vector2.zero, Vector2.one, new Color(0.86f, 0.22f, 0.28f));
        _hudHpFill.transform.parent.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;
        _hudMpFill = Bar(hud.transform, "Mp", Vector2.zero, Vector2.one, new Color(0.28f, 0.58f, 1f));
        _hudMpFill.transform.parent.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;
        _hudHpText = Label(_hudHpFill.transform.parent, "HpText", "HP", 20, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
        _hudMpText = Label(_hudMpFill.transform.parent, "MpText", "MP", 20, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);

        _logPanel = Panel(hud.transform, "BattleLog", Vector2.zero, Vector2.one, new Color(0.05f, 0.06f, 0.1f, 0.92f));
        var logPanelLe = _logPanel.AddComponent<LayoutElement>();
        logPanelLe.preferredHeight = 168f;
        logPanelLe.flexibleHeight = 1f;
        _logText = Label(_logPanel.transform, "LogText", "Combat log", 15, TextAnchor.UpperLeft, new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.96f));
        _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _logText.verticalOverflow = VerticalWrapMode.Overflow;
        _logText.color = new Color(0.95f, 0.9f, 0.75f);
        ApplyLogVisibility();

        var turnHud = Panel(canvasGo.transform, "TurnHud", new Vector2(0.012f, 0.47f), new Vector2(0.155f, 0.985f), new Color(0.08f, 0.09f, 0.16f, 0.9f));
        turnHud.GetComponent<Outline>().effectColor = new Color(0.95f, 0.78f, 0.28f, 0.35f);
        var turnTitle = Label(turnHud.transform, "TurnTitle", "TURN", 16, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.92f), new Vector2(0.96f, 0.995f));
        turnTitle.color = new Color(0.95f, 0.82f, 0.4f);
        var turnRow = new GameObject("TurnRow", typeof(RectTransform), typeof(VerticalLayoutGroup));
        turnRow.transform.SetParent(turnHud.transform, false);
        var turnRect = turnRow.GetComponent<RectTransform>();
        turnRect.anchorMin = new Vector2(0.06f, 0.02f);
        turnRect.anchorMax = new Vector2(0.94f, 0.91f);
        turnRect.offsetMin = Vector2.zero;
        turnRect.offsetMax = Vector2.zero;
        var turnLayout = turnRow.GetComponent<VerticalLayoutGroup>();
        turnLayout.spacing = 6f;
        turnLayout.childAlignment = TextAnchor.UpperCenter;
        turnLayout.childForceExpandHeight = false;
        turnLayout.childForceExpandWidth = true;
        turnLayout.childControlHeight = true;
        turnLayout.childControlWidth = true;
        _turnRow = turnRow.transform;

        CreateInventoryPanel(canvasGo.transform);

        var actions = Panel(canvasGo.transform, "ActionRow", new Vector2(0.78f, 0.02f), new Vector2(0.985f, 0.22f), new Color(0.08f, 0.09f, 0.16f, 0.92f));
        actions.GetComponent<Outline>().effectColor = new Color(0.95f, 0.78f, 0.28f, 0.45f);
        var layout = actions.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.padding = new RectOffset(10, 10, 8, 8);

        _attacks = CreateButton(actions.transform, "Attacks", ToggleAttacksMenu);
        _focus = CreateButton(actions.transform, "Focus", OnFocus);
        _items = CreateButton(actions.transform, "Items", ToggleItemsMenu);

        _menu = Panel(canvasGo.transform, "ActionMenu", new Vector2(0.78f, 0.23f), new Vector2(0.985f, 0.62f), new Color(0.06f, 0.07f, 0.11f, 0.94f));
        var menuLayout = _menu.AddComponent<VerticalLayoutGroup>();
        menuLayout.spacing = 8f;
        menuLayout.padding = new RectOffset(12, 12, 12, 12);
        menuLayout.childAlignment = TextAnchor.UpperCenter;
        menuLayout.childForceExpandHeight = false;
        menuLayout.childForceExpandWidth = true;
        menuLayout.childControlHeight = true;
        menuLayout.childControlWidth = true;
        _menu.SetActive(false);

        _toast = Label(canvasGo.transform, "Toast", "", 26, TextAnchor.MiddleCenter, new Vector2(0.22f, 0.88f), new Vector2(0.78f, 0.97f));
        _toast.color = new Color(1f, 0.93f, 0.72f);
    }

    void CreateInventoryPanel(Transform canvas)
    {
        var inv = Panel(canvas, "InventoryHud", new Vector2(0.012f, 0.016f), new Vector2(0.272f, 0.445f), new Color(0.08f, 0.09f, 0.16f, 0.92f));
        inv.GetComponent<Outline>().effectColor = new Color(0.95f, 0.78f, 0.28f, 0.45f);
        var gearTitle = Label(inv.transform, "GearTitle", "GEAR", 16, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.91f), new Vector2(0.96f, 0.99f));
        gearTitle.color = new Color(0.95f, 0.82f, 0.4f);

        var armor = new GameObject("ArmorCol", typeof(RectTransform), typeof(GridLayoutGroup));
        armor.transform.SetParent(inv.transform, false);
        var armorRect = armor.GetComponent<RectTransform>();
        armorRect.anchorMin = new Vector2(0.045f, 0.24f);
        armorRect.anchorMax = new Vector2(0.27f, 0.90f);
        armorRect.offsetMin = Vector2.zero;
        armorRect.offsetMax = Vector2.zero;
        var armorGrid = armor.GetComponent<GridLayoutGroup>();
        armorGrid.cellSize = new Vector2(GearCell, GearCell);
        armorGrid.spacing = new Vector2(0f, GearGap);
        armorGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        armorGrid.constraintCount = 1;
        armorGrid.childAlignment = TextAnchor.UpperCenter;
        _armorCol = armor.transform;

        var weapon = new GameObject("WeaponSlot", typeof(RectTransform), typeof(GridLayoutGroup));
        weapon.transform.SetParent(inv.transform, false);
        var weaponRect = weapon.GetComponent<RectTransform>();
        weaponRect.anchorMin = new Vector2(0.045f, 0.035f);
        weaponRect.anchorMax = new Vector2(0.27f, 0.20f);
        weaponRect.offsetMin = Vector2.zero;
        weaponRect.offsetMax = Vector2.zero;
        var weaponGrid = weapon.GetComponent<GridLayoutGroup>();
        weaponGrid.cellSize = new Vector2(GearCell, GearCell);
        weaponGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        weaponGrid.constraintCount = 1;
        weaponGrid.childAlignment = TextAnchor.LowerCenter;
        _weaponSlot = weapon.transform;

        var bag = new GameObject("BagGrid", typeof(RectTransform), typeof(GridLayoutGroup));
        bag.transform.SetParent(inv.transform, false);
        var bagRect = bag.GetComponent<RectTransform>();
        bagRect.anchorMin = new Vector2(0.30f, 0.24f);
        bagRect.anchorMax = new Vector2(0.97f, 0.90f);
        bagRect.offsetMin = Vector2.zero;
        bagRect.offsetMax = Vector2.zero;
        var bagGrid = bag.GetComponent<GridLayoutGroup>();
        bagGrid.cellSize = new Vector2(GearCell, GearCell);
        bagGrid.spacing = new Vector2(GearGap, GearGap);
        bagGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        bagGrid.constraintCount = BagColumns;
        bagGrid.childAlignment = TextAnchor.UpperLeft;
        _bagGrid = bag.transform;
    }

    GameObject Panel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        go.GetComponent<Image>().sprite = WhiteSprite();
        go.GetComponent<Image>().color = color;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.12f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return go;
    }

    Image Bar(Transform parent, string name, Vector2 min, Vector2 max, Color fill)
    {
        var bg = new GameObject(name + "Bg", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(parent, false);
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = min;
        bgRect.anchorMax = max;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().sprite = WhiteSprite();
        bg.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.18f, 0.95f);

        var fillGo = new GameObject(name + "Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(bg.transform, false);
        var fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);
        var image = fillGo.GetComponent<Image>();
        image.sprite = WhiteSprite();
        image.color = fill;
        image.type = Image.Type.Simple;
        return image;
    }

    Text Label(Transform parent, string name, string value, int size, TextAnchor anchor, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var text = go.GetComponent<Text>();
        text.text = value;
        text.alignment = anchor;
        text.color = Color.white;
        text.fontSize = size;
        text.font = _font;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    static void EnsureEventSystem()
    {
        var eventSystem = FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            var go = new GameObject("EventSystem");
            eventSystem = go.AddComponent<EventSystem>();
        }

        var legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (legacyModule != null)
        {
            legacyModule.enabled = false;
            Destroy(legacyModule);
        }

        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            var module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
    }

    static Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 54f;
        var image = go.GetComponent<Image>();
        image.sprite = UiSprite();
        image.color = new Color(0.18f, 0.22f, 0.38f, 0.98f);
        var outline = go.GetComponent<Outline>();
        outline.effectColor = new Color(0.95f, 0.8f, 0.35f, 0.35f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);
        var colors = button.colors;
        colors.highlightedColor = new Color(0.28f, 0.34f, 0.48f, 1f);
        colors.pressedColor = new Color(0.12f, 0.14f, 0.2f, 1f);
        colors.disabledColor = new Color(0.2f, 0.2f, 0.22f, 0.6f);
        button.colors = colors;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textGo.GetComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.fontSize = 22;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        return button;
    }

    void BindEvents()
    {
        if (_eventsBound || GameManager.Conn == null)
        {
            return;
        }

        GameManager.Conn.Db.CombatEvent.OnInsert += OnCombatEvent;
        _eventsBound = true;
    }

    void OnCombatEvent(EventContext _, CombatEvent row)
    {
        if (row.Id <= _lastEventId)
        {
            return;
        }

        _lastEventId = row.Id;
        if (_toast != null)
        {
            _toast.text = row.Message;
        }

        AppendBattleLog(row.Message);

        if ((row.ActionType == CombatActionType.Attack || row.ActionType == CombatActionType.Spell)
            && (row.ActorKind != row.TargetKind || row.ActorId != row.TargetId))
        {
            _lunges.Enqueue(row);
        }
    }

    IEnumerator PlayLunge(CombatEvent row)
    {
        _lunging = true;
        var actor = ActorSlot(row.ActorKind, row.ActorId);
        var target = ActorSlot(row.TargetKind, row.TargetId);
        if (actor == null || target == null || actor == target)
        {
            _lunging = false;
            yield break;
        }

        actor.Busy = true;
        var dest = Vector3.Lerp(actor.Home, target.Home, 0.78f);
        yield return MoveTo(actor.Root, actor.Root.position, dest, 0.16f);
        if (target.Orb != null)
        {
            StartCoroutine(PunchScale(target.Root));
        }

        yield return new WaitForSeconds(0.08f);
        var chain = NextLungeSameActor(row);
        if (!chain)
        {
            yield return MoveTo(actor.Root, dest, actor.Home, 0.2f);
            actor.Root.position = actor.Home;
            actor.Busy = false;
        }

        _lunging = false;
    }

    bool NextLungeSameActor(CombatEvent row)
    {
        if (_lunges.Count == 0)
        {
            return false;
        }

        var next = _lunges.Peek();
        return next.ActorKind == row.ActorKind && next.ActorId == row.ActorId;
    }

    static IEnumerator MoveTo(Transform actor, Vector3 from, Vector3 to, float duration)
    {
        var t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            var s = t * t * (3f - 2f * t);
            actor.position = Vector3.Lerp(from, to, s);
            yield return null;
        }
    }

    static IEnumerator PunchScale(Transform target)
    {
        var home = target.localScale;
        var t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.18f;
            var bump = 1f + Mathf.Sin(t * Mathf.PI) * 0.18f;
            target.localScale = home * bump;
            yield return null;
        }

        target.localScale = home;
    }

    SlotView ActorSlot(CombatantKind kind, uint id)
    {
        if (kind == CombatantKind.Player)
        {
            return id < SlotCount ? _players[id] : null;
        }

        var enemy = EnemyById(id);
        if (enemy == null || enemy.Slot >= SlotCount)
        {
            return null;
        }

        return _enemies[enemy.Slot];
    }

    void Refresh()
    {
        BindEvents();
        var gm = GameManager.Instance;
        var local = gm != null ? gm.GetLocalPlayer() : null;
        var session = gm != null ? gm.GetSession() : null;
        var target = FirstLivingEnemy();

        for (uint slot = 0; slot < SlotCount; slot++)
        {
            BindPlayer(_players[slot], gm != null ? gm.GetPlayerInSlot(slot) : null, local, session);
            BindEnemy(_enemies[slot], EnemyInSlot(slot), target);
        }

        RefreshHud(local);
        RebuildTurnStrip(session);
        RebuildInventory(local);

        var canAct = local != null
            && local.Alive
            && target != null
            && session != null
            && session.Phase == GamePhase.Combat
            && session.ActiveKind == CombatantKind.Player
            && session.ActiveCombatantId == local.Slot;
        _canAct = canAct;

        SetButtons(canAct, local);
        if (!canAct)
        {
            CloseMenu();
        }
        else if (!HasWeapon(local) && _menuOpen && !_itemsMenu)
        {
            CloseMenu();
        }
        else if (_menuOpen)
        {
            RebuildMenu();
        }
    }

    void RefreshHud(Player local)
    {
        if (_hudName == null)
        {
            return;
        }

        if (local == null)
        {
            _hudName.text = "Connecting...";
            _hudHpText.text = "HP";
            _hudMpText.text = "MP";
        _hudPotText.text = "Pots x0";
            _hudHpAmount = 0f;
            _hudMpAmount = 0f;
            return;
        }

        _hudName.text = $"{local.Name}   {local.Class.ToString().ToUpperInvariant()}";
        _hudHpText.text = $"HP  {local.CurrHealth} / {local.MaxHealth}";
        _hudMpText.text = $"MP  {local.CurrMana} / {local.MaxMana}";
        _hudPotText.text = $"Pots x{PotionCount(local)}";
        _hudHpAmount = local.MaxHealth == 0 ? 0f : (float)local.CurrHealth / local.MaxHealth;
        _hudMpAmount = local.MaxMana == 0 ? 0f : (float)local.CurrMana / local.MaxMana;
    }

    void BindPlayer(SlotView slot, Player player, Player local, GameSession session)
    {
        if (player == null)
        {
            slot.Root.gameObject.SetActive(true);
            slot.Orb.color = new Color(0.28f, 0.3f, 0.34f, 0.4f);
            slot.NameLabel.text = "Empty";
            slot.IsTurn = false;
            SetBar(slot, 0f, 0f, 0, 0, 0, 0);
            return;
        }

        slot.Root.gameObject.SetActive(true);
        var you = local != null && player.Identity == local.Identity ? " *" : "";
        var isTurn = session != null
            && session.Phase == GamePhase.Combat
            && session.ActiveKind == CombatantKind.Player
            && session.ActiveCombatantId == player.Slot;
        slot.NameLabel.text = $"{player.Name}{you}\n{player.Class}";
        slot.IsTurn = isTurn;
        slot.Orb.color = !player.Alive
            ? new Color(0.35f, 0.35f, 0.35f)
            : isTurn ? Color.Lerp(ClassColor(player.Class), Color.white, 0.35f) : ClassColor(player.Class);
        SetBar(slot, player.CurrHealth, player.MaxHealth, player.CurrMana, player.MaxMana, player.CurrHealth, player.CurrMana);
    }

    void BindEnemy(SlotView slot, Enemy enemy, Enemy target)
    {
        if (enemy == null)
        {
            slot.Root.gameObject.SetActive(false);
            return;
        }

        slot.Root.gameObject.SetActive(true);
        var mark = target != null && target.Id == enemy.Id ? " >" : "";
        slot.NameLabel.text = $"{enemy.Name}{mark}";
        slot.IsTurn = false;
        if (GameManager.Instance != null)
        {
            var session = GameManager.Instance.GetSession();
            slot.IsTurn = session != null
                && session.Phase == GamePhase.Combat
                && session.ActiveKind == CombatantKind.Enemy
                && session.ActiveCombatantId == enemy.Id;
        }
        slot.Orb.color = !enemy.Alive
            ? new Color(0.35f, 0.35f, 0.35f)
            : target != null && target.Id == enemy.Id ? new Color(0.95f, 0.45f, 0.42f) : new Color(0.86f, 0.28f, 0.28f);
        SetBar(slot, enemy.CurrHealth, enemy.MaxHealth, enemy.CurrMana, enemy.MaxMana, enemy.CurrHealth, enemy.CurrMana);
    }

    static void SetBar(SlotView slot, float hp, float maxHp, float mp, float maxMp, uint hpValue, uint mpValue)
    {
        slot.ShownHp = maxHp <= 0 ? 0f : hp / maxHp;
        slot.ShownMp = maxMp <= 0 ? 0f : mp / maxMp;
        slot.HpLabel.text = maxHp <= 0 ? "" : $"{hpValue}";
        slot.MpLabel.text = maxMp <= 0 ? "" : $"{mpValue}";
    }

    void SmoothBars()
    {
        _hudHpShown = Mathf.MoveTowards(_hudHpShown, _hudHpAmount, Time.deltaTime * 2.4f);
        _hudMpShown = Mathf.MoveTowards(_hudMpShown, _hudMpAmount, Time.deltaTime * 2.4f);
        SetHudFill(_hudHpFill, _hudHpShown);
        SetHudFill(_hudMpFill, _hudMpShown);
        SmoothSlotBars(_players);
        SmoothSlotBars(_enemies);
        foreach (var slot in _players)
        {
            if (!slot.Busy)
            {
                slot.Root.position = Vector3.Lerp(slot.Root.position, slot.Home, Time.deltaTime * 8f);
            }
        }

        foreach (var slot in _enemies)
        {
            if (!slot.Busy)
            {
                slot.Root.position = Vector3.Lerp(slot.Root.position, slot.Home, Time.deltaTime * 8f);
            }
        }
    }

    static void SmoothSlotBars(SlotView[] slots)
    {
        foreach (var slot in slots)
        {
            if (slot.HpFill == null)
            {
                continue;
            }

            var hp = new Vector3(Mathf.Max(0.02f, slot.ShownHp), slot.HpFill.localScale.y, 1f);
            var mp = new Vector3(Mathf.Max(0.02f, slot.ShownMp), slot.MpFill.localScale.y, 1f);
            slot.HpFill.localScale = Vector3.Lerp(slot.HpFill.localScale, hp, Time.deltaTime * 10f);
            slot.MpFill.localScale = Vector3.Lerp(slot.MpFill.localScale, mp, Time.deltaTime * 10f);
        }
    }

    static void SetHudFill(Image fill, float amount)
    {
        if (fill == null)
        {
            return;
        }

        var rect = fill.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f);
        rect.offsetMin = new Vector2(3f, 3f);
        rect.offsetMax = new Vector2(-3f, -3f);
    }

    void RebuildTurnStrip(GameSession session)
    {
        if (_turnRow == null)
        {
            return;
        }

        for (var i = _turnRow.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(_turnRow.GetChild(i).gameObject);
        }

        if (GameManager.Conn == null || session == null || session.Phase != GamePhase.Combat)
        {
            Label(_turnRow, "Wait", "Waiting", 14, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            return;
        }

        var rows = new List<TurnOrder>();
        foreach (var row in GameManager.Conn.Db.TurnOrder.Iter())
        {
            rows.Add(row);
        }

        rows.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
        foreach (var row in rows)
        {
            var active = session.ActiveKind == row.Kind && session.ActiveCombatantId == row.CombatantId;
            var label = TurnLabel(row);
            var chip = CreateButton(_turnRow, $"{label}\nSPD {row.Speed}", () => { });
            chip.interactable = false;
            chip.GetComponent<LayoutElement>().preferredHeight = 44f;
            var image = chip.GetComponent<Image>();
            image.color = active
                ? new Color(0.95f, 0.78f, 0.28f, 0.95f)
                : row.HasActed
                    ? new Color(0.18f, 0.18f, 0.22f, 0.7f)
                    : new Color(0.2f, 0.24f, 0.4f, 0.9f);
            var text = chip.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.fontSize = 13;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.color = active ? new Color(0.12f, 0.1f, 0.06f) : Color.white;
            }
        }
    }

    static string TurnLabel(TurnOrder row)
    {
        if (row.Kind == CombatantKind.Player)
        {
            var player = GameManager.Instance?.GetPlayerInSlot(row.CombatantId);
            return player == null ? $"P{row.CombatantId}" : player.Name;
        }

        var enemy = EnemyById(row.CombatantId);
        return enemy == null ? $"E{row.CombatantId}" : enemy.Name;
    }

    static Color ClassColor(PlayerClass classChoice) => classChoice switch
    {
        PlayerClass.Warrior => new Color(0.86f, 0.34f, 0.24f),
        PlayerClass.Archer => new Color(0.32f, 0.74f, 0.34f),
        PlayerClass.Mage => new Color(0.35f, 0.55f, 0.98f),
        PlayerClass.Rogue => new Color(0.66f, 0.34f, 0.86f),
        _ => Color.white,
    };

    void SetButtons(bool enabled, Player local)
    {
        if (_attacks == null)
        {
            return;
        }

        var armed = HasWeapon(local);
        _attacks.interactable = enabled && armed;
        _focus.interactable = enabled;
        _items.interactable = enabled;
    }

    void ToggleAttacksMenu()
    {
        if (!HasWeapon(GameManager.Instance?.GetLocalPlayer()))
        {
            if (_toast != null)
            {
                _toast.text = "Equip a weapon to attack.";
            }

            AppendBattleLog("Equip a weapon to attack.");
            return;
        }

        if (_menuOpen && !_itemsMenu)
        {
            CloseMenu();
            return;
        }

        _itemsMenu = false;
        _menuOpen = true;
        RebuildMenu();
        _menu.SetActive(true);
    }

    void ToggleItemsMenu()
    {
        if (_menuOpen && _itemsMenu)
        {
            CloseMenu();
            return;
        }

        _itemsMenu = true;
        _menuOpen = true;
        RebuildMenu();
        _menu.SetActive(true);
    }

    void CloseMenu()
    {
        _menuOpen = false;
        _itemsMenu = false;
        if (_menu != null)
        {
            _menu.SetActive(false);
        }
    }

    void RebuildMenu()
    {
        for (var i = _menu.transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(_menu.transform.GetChild(i).gameObject);
        }

        if (_itemsMenu)
        {
            var potions = PotionItems(GameManager.Instance?.GetLocalPlayer());
            if (potions.Count == 0)
            {
                var empty = CreateButton(_menu.transform, "No potions", CloseMenu);
                empty.interactable = false;
                CreateButton(_menu.transform, "Back", CloseMenu);
                return;
            }

            foreach (var potion in potions)
            {
                var captured = potion;
                var def = ItemDefOf(captured);
                var name = def == null ? "Potion" : def.Name;
                CreateButton(_menu.transform, $"{name}  x{captured.Quantity}", () => DrinkPotion(captured.Id));
            }

            CreateButton(_menu.transform, "Back", CloseMenu);
            return;
        }

        var skills = UnlockedSkills();
        if (skills.Count == 0)
        {
            CreateButton(_menu.transform, "No spells unlocked", CloseMenu);
            return;
        }

        foreach (var skill in skills)
        {
            var captured = skill;
            CreateButton(_menu.transform, $"{captured.Name}   {captured.ManaCost} MP", () => CastSpell(captured.Id));
        }

        CreateButton(_menu.transform, "Back", CloseMenu);
    }

    void CastSpell(uint skillId)
    {
        var enemy = FirstLivingEnemy();
        if (enemy == null || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.SubmitAction(CombatActionType.Spell, skillId, enemy.Id);
        CloseMenu();
    }

    void OnFocus()
    {
        CloseMenu();
        GameManager.Instance?.SubmitAction(CombatActionType.Defend);
    }

    void DrinkPotion(uint itemId)
    {
        if (itemId == 0 || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.SubmitAction(CombatActionType.Item, 0, 0, itemId);
        CloseMenu();
    }

    void RebuildInventory(Player local)
    {
        ClearChildren(_armorCol);
        ClearChildren(_weaponSlot);
        ClearChildren(_bagGrid);
        if (_armorCol == null || _weaponSlot == null || _bagGrid == null)
        {
            return;
        }

        CreateInvSlot(_armorCol, "Helm", FindEquipped(local, EquipSlot.Helmet), true, ArmorSlot.Helmet);
        CreateInvSlot(_armorCol, "Chest", FindEquipped(local, EquipSlot.Chestplate), true, ArmorSlot.Chestplate);
        CreateInvSlot(_armorCol, "Legs", FindEquipped(local, EquipSlot.Leggings), true, ArmorSlot.Leggings);
        CreateInvSlot(_armorCol, "Boots", FindEquipped(local, EquipSlot.Boots), true, ArmorSlot.Boots);
        CreateInvSlot(_weaponSlot, "Weapon", FindEquipped(local, EquipSlot.Weapon), true, ArmorSlot.None);

        var bag = BagItems(local);
        for (var i = 0; i < BagCapacity; i++)
        {
            var item = i < bag.Count ? bag[i] : null;
            var pipSlot = ArmorSlot.None;
            if (item != null)
            {
                var def = ItemDefOf(item);
                if (def != null && def.Kind == ItemKind.Armor)
                {
                    pipSlot = def.ArmorSlot;
                }
            }

            CreateInvSlot(_bagGrid, "", item, false, pipSlot);
        }
    }

    void CreateInvSlot(Transform parent, string emptyLabel, PlayerItem item, bool equipped, ArmorSlot pipSlot)
    {
        var cell = new GameObject(string.IsNullOrEmpty(emptyLabel) ? "BagSlot" : emptyLabel, typeof(RectTransform));
        cell.transform.SetParent(parent, false);

        var text = emptyLabel;
        uint itemId = 0;
        uint filledPips = 0;
        var cap = PipCap(pipSlot);
        if (item != null)
        {
            itemId = item.Id;
            filledPips = item.HealthPips;
            var def = ItemDefOf(item);
            text = def == null ? emptyLabel : SlotText(def, item);
            if (def != null && def.Kind == ItemKind.Armor)
            {
                cap = PipCap(def.ArmorSlot);
            }
            else
            {
                cap = 0;
            }
        }

        var button = CreateButton(cell.transform, string.IsNullOrEmpty(text) ? " " : text, () => OnInventoryClick(itemId, equipped));
        var buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = Vector2.zero;
        buttonRect.anchorMax = Vector2.one;
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        var layout = button.GetComponent<LayoutElement>();
        layout.ignoreLayout = true;
        layout.preferredHeight = GearCell;
        var image = button.GetComponent<Image>();
        var label = button.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.fontSize = 12;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
        }

        if (item == null)
        {
            button.interactable = false;
            image.color = new Color(0.14f, 0.15f, 0.22f, 0.85f);
            if (label != null)
            {
                label.text = emptyLabel;
                label.color = new Color(0.7f, 0.72f, 0.8f, 0.7f);
            }
        }
        else
        {
            button.interactable = true;
            image.color = equipped
                ? new Color(0.28f, 0.24f, 0.16f, 0.98f)
                : new Color(0.18f, 0.22f, 0.38f, 0.98f);
        }

        if (cap > 0)
        {
            CreatePipRow(cell.transform, filledPips, cap);
            if (label != null)
            {
                var textRect = label.rectTransform;
                textRect.offsetMin = new Vector2(3f, 14f);
                textRect.offsetMax = new Vector2(-3f, -3f);
            }
        }
    }

    void CreatePipRow(Transform parent, uint filled, uint cap)
    {
        var row = new GameObject("Pips", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(parent, false);
        var rect = row.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.08f, 0.04f);
        rect.anchorMax = new Vector2(0.92f, 0.20f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 3f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.padding = new RectOffset(1, 1, 1, 1);
        for (var i = 0; i < cap; i++)
        {
            var pip = new GameObject("Pip", typeof(RectTransform), typeof(Image));
            pip.transform.SetParent(row.transform, false);
            pip.GetComponent<Image>().sprite = WhiteSprite();
            pip.GetComponent<Image>().color = i < filled
                ? new Color(0.92f, 0.3f, 0.32f, 1f)
                : new Color(0.16f, 0.14f, 0.18f, 0.95f);
        }
    }

    void OnInventoryClick(uint itemId, bool equipped)
    {
        if (itemId == 0 || GameManager.Instance == null || GameManager.Conn == null)
        {
            return;
        }

        var item = GameManager.Conn.Db.PlayerItem.Id.Find(itemId);
        if (item == null)
        {
            return;
        }

        if (equipped)
        {
            GameManager.Instance.UnequipItem(itemId);
            return;
        }

        var def = ItemDefOf(item);
        if (def == null)
        {
            return;
        }

        if (def.Kind == ItemKind.Consumable)
        {
            if (!_canAct)
            {
                if (_toast != null)
                {
                    _toast.text = "Wait for your turn to use items.";
                }

                return;
            }

            GameManager.Instance.SubmitAction(CombatActionType.Item, 0, 0, itemId);
            return;
        }

        GameManager.Instance.EquipItem(itemId);
    }

    static void ClearChildren(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (var i = parent.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }

    static PlayerItem FindEquipped(Player local, EquipSlot slot)
    {
        if (local == null || GameManager.Conn == null)
        {
            return null;
        }

        foreach (var item in GameManager.Conn.Db.PlayerItem.Iter())
        {
            if (item.Owner == local.Identity && item.EquippedSlot == slot)
            {
                return item;
            }
        }

        return null;
    }

    static List<PlayerItem> BagItems(Player local)
    {
        var bag = new List<PlayerItem>();
        if (local == null || GameManager.Conn == null)
        {
            return bag;
        }

        foreach (var item in GameManager.Conn.Db.PlayerItem.Iter())
        {
            if (item.Owner == local.Identity && item.EquippedSlot == EquipSlot.Bag)
            {
                var def = ItemDefOf(item);
                if (def != null && def.Kind != ItemKind.Consumable)
                {
                    bag.Add(item);
                }
            }
        }

        bag.Sort((a, b) => a.Id.CompareTo(b.Id));
        if (bag.Count > BagCapacity)
        {
            bag.RemoveRange(BagCapacity, bag.Count - BagCapacity);
        }

        return bag;
    }

    static ItemDef ItemDefOf(PlayerItem item)
    {
        return GameManager.Conn == null ? null : GameManager.Conn.Db.ItemDef.Id.Find(item.ItemDefId);
    }

    static string SlotText(ItemDef def, PlayerItem item)
    {
        return item.Quantity > 1 ? $"{def.Name}\nx{item.Quantity}" : def.Name;
    }

    void ToggleBattleLog()
    {
        _logOpen = !_logOpen;
        ApplyLogVisibility();
    }

    void ApplyLogVisibility()
    {
        if (_logPanel != null)
        {
            _logPanel.SetActive(_logOpen);
        }

        if (_hudRoot != null)
        {
            _hudRoot.anchorMax = new Vector2(0.66f, _logOpen ? 0.40f : 0.175f);
        }

        if (_logToggle != null)
        {
            var label = _logToggle.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = _logOpen ? "Hide Log" : "Log";
            }
        }

        RefreshBattleLog();
    }

    void AppendBattleLog(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        _battleLog.Add(message);
        while (_battleLog.Count > BattleLogMax)
        {
            _battleLog.RemoveAt(0);
        }

        RefreshBattleLog();
    }

    void RefreshBattleLog()
    {
        if (_logText == null)
        {
            return;
        }

        _logText.text = _battleLog.Count == 0 ? "Combat log" : string.Join("\n", _battleLog.ToArray());
    }

    static bool HasWeapon(Player local) => local != null && local.EquippedWeaponDefId != 0;

    static uint PipCap(ArmorSlot slot) => slot switch
    {
        ArmorSlot.Helmet => 5,
        ArmorSlot.Chestplate => 6,
        ArmorSlot.Leggings => 5,
        ArmorSlot.Boots => 4,
        _ => 0,
    };

    static uint PotionCount(Player local)
    {
        uint count = 0;
        foreach (var potion in PotionItems(local))
        {
            count += potion.Quantity;
        }

        return count;
    }

    static List<PlayerItem> PotionItems(Player local)
    {
        var potions = new List<PlayerItem>();
        if (local == null || GameManager.Conn == null)
        {
            return potions;
        }

        foreach (var item in GameManager.Conn.Db.PlayerItem.Iter())
        {
            if (item.Owner != local.Identity)
            {
                continue;
            }

            var def = ItemDefOf(item);
            if (def != null && def.Kind == ItemKind.Consumable)
            {
                potions.Add(item);
            }
        }

        potions.Sort((a, b) => a.Id.CompareTo(b.Id));
        return potions;
    }

    static Enemy FirstLivingEnemy()
    {
        if (GameManager.Conn == null)
        {
            return null;
        }

        Enemy first = null;
        foreach (var enemy in GameManager.Conn.Db.Enemy.Iter())
        {
            if (!enemy.Alive)
            {
                continue;
            }

            if (first == null || enemy.Slot < first.Slot)
            {
                first = enemy;
            }
        }

        return first;
    }

    static Enemy EnemyInSlot(uint slot)
    {
        if (GameManager.Conn == null)
        {
            return null;
        }

        foreach (var enemy in GameManager.Conn.Db.Enemy.Iter())
        {
            if (enemy.Slot == slot)
            {
                return enemy;
            }
        }

        return null;
    }

    static Enemy EnemyById(uint id)
    {
        if (GameManager.Conn == null)
        {
            return null;
        }

        return GameManager.Conn.Db.Enemy.Id.Find(id);
    }

    static List<SkillDef> UnlockedSkills()
    {
        var skills = new List<SkillDef>();
        var local = GameManager.Instance?.GetLocalPlayer();
        if (local == null || GameManager.Conn == null)
        {
            return skills;
        }

        foreach (var owned in GameManager.Conn.Db.PlayerSkill.Iter())
        {
            if (owned.Owner != local.Identity || !owned.Unlocked)
            {
                continue;
            }

            var skill = GameManager.Conn.Db.SkillDef.Id.Find(owned.SkillDefId);
            if (skill == null || skill.IsEnemySkill || skill.Class != local.Class)
            {
                continue;
            }

            skills.Add(skill);
        }

        skills.Sort((a, b) => a.Id.CompareTo(b.Id));
        return skills;
    }
}
