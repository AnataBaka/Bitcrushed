using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CombatActionType = SpacetimeDB.Types.CombatActionType;

public class CombatView : MonoBehaviour
{
    [SerializeField] Sprite playerSprite;
    [SerializeField] Sprite enemySprite;

    SpriteRenderer _playerOrb;
    SpriteRenderer _enemyOrb;
    TextMesh _playerLabel;
    TextMesh _enemyLabel;
    Button _attack;
    Button _spells;
    Button _defend;
    Button _items;

    void Start()
    {
        SetupCamera();
        _playerOrb = CreateOrb("Player", new Vector3(-3f, 0.4f, 0f), new Color(0.2f, 0.85f, 0.25f), playerSprite);
        _enemyOrb = CreateOrb("Enemy", new Vector3(3f, 0.4f, 0f), new Color(0.85f, 0.2f, 0.2f), enemySprite);
        _playerLabel = CreateLabel(_playerOrb.transform, "Player");
        _enemyLabel = CreateLabel(_enemyOrb.transform, "Enemy");
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
        camera.orthographicSize = 5f;
        camera.transform.position = new Vector3(0f, 0.4f, -10f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
    }

    SpriteRenderer CreateOrb(string name, Vector3 position, Color color, Sprite sprite)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 1.6f;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite != null ? sprite : CreateOrbSprite(color);

        return renderer;
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
        go.transform.localPosition = new Vector3(0f, 0.85f, 0f);
        go.transform.localScale = Vector3.one * 0.08f;
        var mesh = go.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.anchor = TextAnchor.LowerCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.fontSize = 48;
        mesh.color = Color.white;
        mesh.characterSize = 0.5f;
        var meshRenderer = go.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingOrder = 5;
        }
        return mesh;
    }

    void CreateButtons()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        var canvasGo = new GameObject("CombatButtons");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(canvasGo.transform, false);
        var rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.1f, 0.04f);
        rowRect.anchorMax = new Vector2(0.9f, 0.16f);
        rowRect.offsetMin = Vector2.zero;
        rowRect.offsetMax = Vector2.zero;
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = true;

        _attack = CreateButton(row.transform, "Attack", OnAttack);
        _spells = CreateButton(row.transform, "Spells", OnSpells);
        _defend = CreateButton(row.transform, "Defend", OnDefend);
        _items = CreateButton(row.transform, "Items", OnItems);
    }

    static Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
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
        text.fontSize = 22;
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
        var player = gm != null ? gm.GetLocalPlayer() : null;
        var enemy = FirstEnemy();
        var session = gm != null ? gm.GetSession() : null;

        if (player != null)
        {
            _playerLabel.text = $"{player.Name}\nHP {player.CurrHealth}/{player.MaxHealth}";
            _playerOrb.color = player.Alive ? Color.white : new Color(0.35f, 0.35f, 0.35f);
        }

        if (enemy != null)
        {
            _enemyLabel.text = $"{enemy.Name}\nHP {enemy.CurrHealth}/{enemy.MaxHealth}";
            _enemyOrb.color = enemy.Alive ? Color.white : new Color(0.35f, 0.35f, 0.35f);
        }

        var canAct = player != null
            && player.Alive
            && enemy != null
            && enemy.Alive
            && session != null
            && session.Phase == GamePhase.Combat
            && session.ActiveKind == CombatantKind.Player
            && session.ActiveCombatantId == player.Slot;

        SetButtons(canAct);
    }

    void SetButtons(bool enabled)
    {
        if (_attack == null)
        {
            return;
        }

        _attack.interactable = enabled;
        _spells.interactable = enabled;
        _defend.interactable = enabled;
        _items.interactable = enabled;
    }

    void OnAttack()
    {
        var enemy = FirstEnemy();
        if (enemy == null || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.SubmitAction(CombatActionType.Attack, 0, enemy.Id);
    }

    void OnSpells()
    {
        var enemy = FirstEnemy();
        var skillId = FirstUnlockedSkillId();
        if (enemy == null || skillId == 0 || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.SubmitAction(CombatActionType.Spell, skillId, enemy.Id);
    }

    void OnDefend()
    {
        GameManager.Instance?.SubmitAction(CombatActionType.Defend);
    }

    void OnItems()
    {
        var itemId = FirstConsumableId();
        if (itemId == 0 || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.SubmitAction(CombatActionType.Item, 0, 0, itemId);
    }

    static Enemy FirstEnemy()
    {
        if (GameManager.Conn == null)
        {
            return null;
        }

        foreach (var enemy in GameManager.Conn.Db.Enemy.Iter())
        {
            if (enemy.Alive)
            {
                return enemy;
            }
        }

        foreach (var enemy in GameManager.Conn.Db.Enemy.Iter())
        {
            return enemy;
        }

        return null;
    }

    static uint FirstUnlockedSkillId()
    {
        var local = GameManager.Instance?.GetLocalPlayer();
        if (local == null || GameManager.Conn == null)
        {
            return 0;
        }

        foreach (var owned in GameManager.Conn.Db.PlayerSkill.Iter())
        {
            if (owned.Owner != local.Identity || !owned.Unlocked)
            {
                continue;
            }

            return owned.SkillDefId;
        }

        return 0;
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
