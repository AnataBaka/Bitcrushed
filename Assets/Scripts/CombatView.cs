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
    const float OrbScale = 1.05f;

    [SerializeField] Sprite playerSprite;
    [SerializeField] Sprite enemySprite;

    readonly SlotView[] _players = new SlotView[SlotCount];
    readonly SlotView[] _enemies = new SlotView[SlotCount];

    Button _attacks;
    Button _defend;
    Button _items;
    GameObject _attacksMenu;
    bool _attacksMenuOpen;

    struct SlotView
    {
        public SpriteRenderer Orb;
        public TextMesh Label;
    }

    void Start()
    {
        SetupCamera();
        CreateParty();
        CreateButtons();
        GameManager.PartyChanged += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        GameManager.PartyChanged -= Refresh;
    }

    void SetupCamera()
    {
        var camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        camera.orthographic = true;
        camera.orthographicSize = 4.6f;
        camera.transform.position = new Vector3(0f, 0.2f, -10f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
    }

    void CreateParty()
    {
        var playerXs = new[] { -4.15f, -3.45f, -4.15f };
        var enemyXs = new[] { 4.15f, 3.45f, 4.15f };
        var ys = new[] { 1.85f, 0.2f, -1.45f };
        var playerColor = new Color(0.2f, 0.85f, 0.25f);
        var enemyColor = new Color(0.85f, 0.2f, 0.2f);

        for (var i = 0; i < SlotCount; i++)
        {
            _players[i] = CreateSlot($"Player{i}", new Vector3(playerXs[i], ys[i], 0f), playerColor, playerSprite);
            _enemies[i] = CreateSlot($"Enemy{i}", new Vector3(enemyXs[i], ys[i], 0f), enemyColor, enemySprite);
        }
    }

    SlotView CreateSlot(string name, Vector3 position, Color color, Sprite sprite)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * OrbScale;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite != null ? sprite : CreateOrbSprite(color);
        return new SlotView
        {
            Orb = renderer,
            Label = CreateLabel(go.transform, name),
        };
    }

    static Sprite CreateOrbSprite(Color color)
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
                var alpha = Mathf.Clamp01((radius - dist) / 4f);
                tex.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
            }
        }

        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
    }

    static TextMesh CreateLabel(Transform parent, string text)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.78f, 0f);
        go.transform.localScale = Vector3.one * 0.07f;
        var mesh = go.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.anchor = TextAnchor.LowerCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.fontSize = 36;
        mesh.color = Color.white;
        mesh.characterSize = 0.42f;
        var meshRenderer = go.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingOrder = 5;
        }
        return mesh;
    }

    void CreateButtons()
    {
        EnsureEventSystem();

        var canvasGo = new GameObject("CombatButtons");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(canvasGo.transform, false);
        var rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.28f, 0.018f);
        rowRect.anchorMax = new Vector2(0.72f, 0.078f);
        rowRect.offsetMin = Vector2.zero;
        rowRect.offsetMax = Vector2.zero;
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = true;
        layout.padding = new RectOffset(4, 4, 2, 2);

        _attacks = CreateButton(row.transform, "Attacks", ToggleAttacksMenu);
        _defend = CreateButton(row.transform, "Defend", OnDefend);
        _items = CreateButton(row.transform, "Items", OnItems);

        _attacksMenu = new GameObject("AttacksMenu", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        _attacksMenu.transform.SetParent(canvasGo.transform, false);
        var menuRect = _attacksMenu.GetComponent<RectTransform>();
        menuRect.anchorMin = new Vector2(0.28f, 0.09f);
        menuRect.anchorMax = new Vector2(0.5f, 0.38f);
        menuRect.offsetMin = Vector2.zero;
        menuRect.offsetMax = Vector2.zero;
        _attacksMenu.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.92f);
        var menuLayout = _attacksMenu.GetComponent<VerticalLayoutGroup>();
        menuLayout.spacing = 4f;
        menuLayout.padding = new RectOffset(8, 8, 8, 8);
        menuLayout.childAlignment = TextAnchor.UpperCenter;
        menuLayout.childForceExpandHeight = false;
        menuLayout.childForceExpandWidth = true;
        menuLayout.childControlHeight = true;
        menuLayout.childControlWidth = true;
        _attacksMenu.SetActive(false);
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
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 36f;
        var image = go.GetComponent<Image>();
        image.color = new Color(0.18f, 0.2f, 0.26f, 0.95f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

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
        text.fontSize = 16;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
        {
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return button;
    }

    void Refresh()
    {
        var gm = GameManager.Instance;
        var local = gm != null ? gm.GetLocalPlayer() : null;
        var session = gm != null ? gm.GetSession() : null;
        var target = FirstLivingEnemy();

        for (uint slot = 0; slot < SlotCount; slot++)
        {
            BindPlayer(_players[slot], gm != null ? gm.GetPlayerInSlot(slot) : null, local, session);
            BindEnemy(_enemies[slot], EnemyInSlot(slot), target);
        }

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
            CloseAttacksMenu();
        }
        else if (_attacksMenuOpen)
        {
            RebuildAttacksMenu();
        }
    }

    static void BindPlayer(SlotView slot, Player player, Player local, GameSession session)
    {
        if (player == null)
        {
            slot.Orb.gameObject.SetActive(true);
            slot.Orb.color = new Color(0.28f, 0.3f, 0.34f, 0.45f);
            slot.Label.text = "Empty";
            return;
        }

        slot.Orb.gameObject.SetActive(true);
        var isTurn = session != null
            && session.Phase == GamePhase.Combat
            && session.ActiveKind == CombatantKind.Player
            && session.ActiveCombatantId == player.Slot;
        var you = local != null && player.Identity == local.Identity ? " *" : "";
        slot.Label.text = $"{player.Name}{you}\n{player.Class}\nHP {player.CurrHealth}/{player.MaxHealth}";
        if (!player.Alive)
        {
            slot.Orb.color = new Color(0.35f, 0.35f, 0.35f);
        }
        else if (isTurn)
        {
            slot.Orb.color = new Color(1f, 0.92f, 0.45f);
        }
        else
        {
            slot.Orb.color = Color.white;
        }
    }

    static void BindEnemy(SlotView slot, Enemy enemy, Enemy target)
    {
        if (enemy == null)
        {
            slot.Orb.gameObject.SetActive(false);
            slot.Label.text = string.Empty;
            return;
        }

        slot.Orb.gameObject.SetActive(true);
        var mark = target != null && target.Id == enemy.Id ? " >" : "";
        slot.Label.text = $"{enemy.Name}{mark}\nHP {enemy.CurrHealth}/{enemy.MaxHealth}";
        slot.Orb.color = enemy.Alive
            ? (target != null && target.Id == enemy.Id ? new Color(1f, 0.55f, 0.55f) : Color.white)
            : new Color(0.35f, 0.35f, 0.35f);
    }

    void SetButtons(bool enabled)
    {
        if (_attacks == null)
        {
            return;
        }

        _attacks.interactable = enabled;
        _defend.interactable = enabled;
        _items.interactable = enabled;
    }

    void ToggleAttacksMenu()
    {
        _attacksMenuOpen = !_attacksMenuOpen;
        if (_attacksMenuOpen)
        {
            RebuildAttacksMenu();
            _attacksMenu.SetActive(true);
        }
        else
        {
            _attacksMenu.SetActive(false);
        }
    }

    void CloseAttacksMenu()
    {
        _attacksMenuOpen = false;
        if (_attacksMenu != null)
        {
            _attacksMenu.SetActive(false);
        }
    }

    void RebuildAttacksMenu()
    {
        if (_attacksMenu == null)
        {
            return;
        }

        for (var i = _attacksMenu.transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(_attacksMenu.transform.GetChild(i).gameObject);
        }

        var skills = UnlockedSkills();
        if (skills.Count == 0)
        {
            CreateButton(_attacksMenu.transform, "No spells unlocked", () => { });
            return;
        }

        foreach (var skill in skills)
        {
            var captured = skill;
            CreateButton(_attacksMenu.transform, $"{captured.Name}  {captured.ManaCost} MP", () => CastSpell(captured.Id));
        }
    }

    void CastSpell(uint skillId)
    {
        var enemy = FirstLivingEnemy();
        if (enemy == null || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.SubmitAction(CombatActionType.Spell, skillId, enemy.Id);
        CloseAttacksMenu();
    }

    void OnDefend()
    {
        CloseAttacksMenu();
        GameManager.Instance?.SubmitAction(CombatActionType.Defend);
    }

    void OnItems()
    {
        CloseAttacksMenu();
        var itemId = FirstConsumableId();
        if (itemId == 0 || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.SubmitAction(CombatActionType.Item, 0, 0, itemId);
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

    static uint FirstConsumableId()
    {
        var local = GameManager.Instance?.GetLocalPlayer();
        if (local == null || GameManager.Conn == null)
        {
            return 0;
        }

        foreach (var item in GameManager.Conn.Db.PlayerItem.Iter())
        {
            if (item.Owner != local.Identity)
            {
                continue;
            }

            var def = GameManager.Conn.Db.ItemDef.Id.Find(item.ItemDefId);
            if (def != null && def.Kind == ItemKind.Consumable)
            {
                return item.Id;
            }
        }

        return 0;
    }
}
