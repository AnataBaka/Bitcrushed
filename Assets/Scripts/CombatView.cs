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
    const float OrbScale = 1.12f;
    const string HealthPotionName = "Health Potion";

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
    Text _hudHpText;
    Text _hudMpText;
    Text _hudPotText;
    bool _menuOpen;
    bool _itemsMenu;
    bool _eventsBound;
    bool _lunging;
    uint _lastEventId;
    Sprite _whiteSprite;
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
        camera.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
    }

    void CreateParty()
    {
        var playerXs = new[] { -4.2f, -3.4f, -4.2f };
        var enemyXs = new[] { 4.2f, 3.4f, 4.2f };
        var ys = new[] { 1.9f, 0.25f, -1.4f };

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
            NameLabel = CreateWorldText(go.transform, new Vector3(0f, 0.92f, 0f), 34, Color.white),
            HpFill = CreateBar(go.transform, new Vector3(0f, -0.62f, 0f), new Color(0.82f, 0.18f, 0.22f)),
            MpFill = CreateBar(go.transform, new Vector3(0f, -0.76f, 0f), new Color(0.25f, 0.55f, 0.95f)),
            HpLabel = CreateWorldText(go.transform, new Vector3(0f, -0.62f, 0f), 22, Color.white),
            MpLabel = CreateWorldText(go.transform, new Vector3(0f, -0.76f, 0f), 22, Color.white),
        };
        slot.HpLabel.characterSize = 0.28f;
        slot.MpLabel.characterSize = 0.28f;
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

        var hud = Panel(canvasGo.transform, "LocalHud", new Vector2(0.27f, 0.11f), new Vector2(0.73f, 0.23f), new Color(0.07f, 0.08f, 0.12f, 0.82f));
        _hudName = Label(hud.transform, "Name", "Ready", 22, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.55f), new Vector2(0.5f, 0.95f));
        _hudPotText = Label(hud.transform, "Pots", "Pots x1", 20, TextAnchor.MiddleRight, new Vector2(0.5f, 0.55f), new Vector2(0.96f, 0.95f));
        _hudHpFill = Bar(hud.transform, "Hp", new Vector2(0.04f, 0.28f), new Vector2(0.96f, 0.52f), new Color(0.78f, 0.18f, 0.24f));
        _hudMpFill = Bar(hud.transform, "Mp", new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.26f), new Color(0.22f, 0.5f, 0.95f));
        _hudHpText = Label(_hudHpFill.transform.parent, "HpText", "HP", 16, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
        _hudMpText = Label(_hudMpFill.transform.parent, "MpText", "MP", 16, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);

        var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(canvasGo.transform, false);
        var rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.22f, 0.025f);
        rowRect.anchorMax = new Vector2(0.78f, 0.1f);
        rowRect.offsetMin = Vector2.zero;
        rowRect.offsetMax = Vector2.zero;
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = true;
        layout.padding = new RectOffset(8, 8, 4, 4);

        _attacks = CreateButton(row.transform, "Attacks", ToggleAttacksMenu);
        _focus = CreateButton(row.transform, "Focus", OnFocus);
        _items = CreateButton(row.transform, "Items", ToggleItemsMenu);

        _menu = Panel(canvasGo.transform, "ActionMenu", new Vector2(0.22f, 0.115f), new Vector2(0.5f, 0.48f), new Color(0.06f, 0.07f, 0.11f, 0.94f));
        var menuLayout = _menu.AddComponent<VerticalLayoutGroup>();
        menuLayout.spacing = 8f;
        menuLayout.padding = new RectOffset(12, 12, 12, 12);
        menuLayout.childAlignment = TextAnchor.UpperCenter;
        menuLayout.childForceExpandHeight = false;
        menuLayout.childForceExpandWidth = true;
        menuLayout.childControlHeight = true;
        menuLayout.childControlWidth = true;
        _menu.SetActive(false);

        _toast = Label(canvasGo.transform, "Toast", "", 26, TextAnchor.MiddleCenter, new Vector2(0.2f, 0.84f), new Vector2(0.8f, 0.94f));
        _toast.color = new Color(1f, 0.93f, 0.72f);
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
        bg.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.95f);

        var fillGo = new GameObject(name + "Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(bg.transform, false);
        var fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);
        var image = fillGo.GetComponent<Image>();
        image.color = fill;
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillAmount = 1f;
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
        image.color = new Color(0.16f, 0.19f, 0.28f, 0.96f);
        var outline = go.GetComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.16f);
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

        if (row.ActionType == CombatActionType.Attack || row.ActionType == CombatActionType.Spell)
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
        var home = actor.Home;
        var dest = Vector3.Lerp(home, target.Home, 0.78f);
        yield return MoveTo(actor.Root, home, dest, 0.16f);
        if (target.Orb != null)
        {
            StartCoroutine(PunchScale(target.Root));
        }

        yield return MoveTo(actor.Root, dest, home, 0.2f);
        actor.Root.position = home;
        actor.Busy = false;
        _lunging = false;
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

        var canAct = local != null
            && local.Alive
            && target != null
            && session != null
            && session.Phase == GamePhase.Combat
            && session.ActiveKind == CombatantKind.Player
            && session.ActiveCombatantId == local.Slot;

        SetButtons(canAct);
        if (!canAct)
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
            _hudHpFill.fillAmount = 0f;
            _hudMpFill.fillAmount = 0f;
            return;
        }

        _hudName.text = $"{local.Name}  {local.Class}";
        _hudHpText.text = $"HP  {local.CurrHealth}/{local.MaxHealth}";
        _hudMpText.text = $"MP  {local.CurrMana}/{local.MaxMana}";
        _hudPotText.text = $"Pots x{HealthPotionCount(local)}";
        _hudHpFill.fillAmount = local.MaxHealth == 0 ? 0f : (float)local.CurrHealth / local.MaxHealth;
        _hudMpFill.fillAmount = local.MaxMana == 0 ? 0f : (float)local.CurrMana / local.MaxMana;
    }

    void BindPlayer(SlotView slot, Player player, Player local, GameSession session)
    {
        if (player == null)
        {
            slot.Root.gameObject.SetActive(true);
            slot.Orb.color = new Color(0.28f, 0.3f, 0.34f, 0.4f);
            slot.NameLabel.text = "Empty";
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
        slot.Orb.color = !enemy.Alive
            ? new Color(0.35f, 0.35f, 0.35f)
            : target != null && target.Id == enemy.Id ? new Color(0.95f, 0.45f, 0.42f) : new Color(0.86f, 0.28f, 0.28f);
        SetBar(slot, enemy.CurrHealth, enemy.MaxHealth, enemy.CurrMana, enemy.MaxMana, enemy.CurrHealth, enemy.CurrMana);
    }

    static void SetBar(SlotView slot, float hp, float maxHp, float mp, float maxMp, uint hpValue, uint mpValue)
    {
        slot.ShownHp = maxHp <= 0 ? 0f : hp / maxHp;
        slot.ShownMp = maxMp <= 0 ? 0f : mp / maxMp;
        slot.HpLabel.text = maxHp <= 0 ? "" : $"{hpValue}/{maxHp}";
        slot.MpLabel.text = maxMp <= 0 ? "" : $"{mpValue}/{maxMp}";
    }

    void SmoothBars()
    {
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

    static Color ClassColor(PlayerClass classChoice) => classChoice switch
    {
        PlayerClass.Warrior => new Color(0.86f, 0.34f, 0.24f),
        PlayerClass.Archer => new Color(0.32f, 0.74f, 0.34f),
        PlayerClass.Mage => new Color(0.35f, 0.55f, 0.98f),
        PlayerClass.Rogue => new Color(0.66f, 0.34f, 0.86f),
        _ => Color.white,
    };

    void SetButtons(bool enabled)
    {
        if (_attacks == null)
        {
            return;
        }

        _attacks.interactable = enabled;
        _focus.interactable = enabled;
        _items.interactable = enabled;
    }

    void ToggleAttacksMenu()
    {
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
            var count = HealthPotionCount(GameManager.Instance?.GetLocalPlayer());
            var drink = CreateButton(_menu.transform, count == 0 ? "No health pots" : $"Drink Health Pot  x{count}", DrinkHealthPot);
            drink.interactable = count > 0;
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

    void DrinkHealthPot()
    {
        var potion = FindHealthPotion(GameManager.Instance?.GetLocalPlayer());
        if (potion == null || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.SubmitAction(CombatActionType.Item, 0, 0, potion.Id);
        CloseMenu();
    }

    static uint HealthPotionCount(Player local)
    {
        var potion = FindHealthPotion(local);
        return potion == null ? 0 : potion.Quantity;
    }

    static PlayerItem FindHealthPotion(Player local)
    {
        if (local == null || GameManager.Conn == null)
        {
            return null;
        }

        foreach (var item in GameManager.Conn.Db.PlayerItem.Iter())
        {
            if (item.Owner != local.Identity)
            {
                continue;
            }

            var def = GameManager.Conn.Db.ItemDef.Id.Find(item.ItemDefId);
            if (def != null && def.Name == HealthPotionName)
            {
                return item;
            }
        }

        return null;
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
