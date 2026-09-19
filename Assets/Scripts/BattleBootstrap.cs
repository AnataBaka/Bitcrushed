using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Builds the entire "Testing Fight Stage" hierarchy at runtime: event system,
/// canvas, battlefield, equipment placeholder, battle log, action menu and the
/// end-of-battle overlay. Nothing needs to be assembled by hand in the editor.
public class BattleBootstrap : MonoBehaviour
{
    [SerializeField]
    string serverUrl = "https://maincloud.spacetimedb.com";

    [SerializeField]
    string databaseName = "hophacks-party-vp2";

    static bool _built;

    /// Statics survive Play sessions when domain reload is disabled, which would
    /// otherwise make the second Play build nothing at all.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _built = false;

    /// Lets an empty scene come up fully formed on Play. If the component is
    /// already placed in the scene this does nothing.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        if (_built || FindAnyObjectByType<BattleBootstrap>() != null)
        {
            return;
        }

        new GameObject("BattleBootstrap").AddComponent<BattleBootstrap>();
    }

    void Awake()
    {
        if (_built)
        {
            return;
        }

        _built = true;
        Build();
    }

    void OnDestroy()
    {
        if (!gameObject.scene.isLoaded)
        {
            _built = false;
        }
    }

    void Build()
    {
        EnsureEventSystem();
        EnsureConnection();

        var canvas = BuildCanvas();
        BuildBackdrop(canvas);

        var connectionLabel = UiFactory.Label(
            canvas,
            "ConnectionLabel",
            "",
            16,
            TextAnchor.UpperLeft,
            UiFactory.MutedColor
        );
        connectionLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        connectionLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        connectionLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
        connectionLabel.rectTransform.sizeDelta = new Vector2(-32f, 24f);
        connectionLabel.rectTransform.anchoredPosition = new Vector2(0f, -6f);

        var stageLabel = UiFactory.Label(
            canvas,
            "StageLabel",
            "",
            22,
            TextAnchor.UpperCenter,
            UiFactory.ActiveColor
        );
        stageLabel.fontStyle = FontStyle.Bold;
        stageLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        stageLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        stageLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
        stageLabel.rectTransform.sizeDelta = new Vector2(-32f, 28f);
        stageLabel.rectTransform.anchoredPosition = new Vector2(0f, -28f);

        // Battlefield fills everything above the bottom bar.
        var field = UiFactory.NewRect(canvas, "Field");
        UiFactory.Anchor(field, new Vector2(0f, 0.26f), new Vector2(1f, 1f));

        var bottom = UiFactory.NewRect(canvas, "BottomBar");
        UiFactory.Anchor(bottom, Vector2.zero, new Vector2(1f, 0.26f));

        var equipment = EquipmentPanelView.Create(bottom);
        var equipmentRect = equipment.GetComponent<RectTransform>();
        UiFactory.Anchor(equipmentRect, new Vector2(0f, 0f), new Vector2(0.185f, 1f));
        equipmentRect.offsetMin = new Vector2(10f, 10f);
        equipmentRect.offsetMax = new Vector2(-5f, -10f);

        var log = BattleLogView.Create(bottom);
        var logRect = log.GetComponent<RectTransform>();
        UiFactory.Anchor(logRect, new Vector2(0.185f, 0f), new Vector2(0.75f, 1f));
        logRect.offsetMin = new Vector2(5f, 10f);
        logRect.offsetMax = new Vector2(-5f, -10f);

        var menu = ActionMenuView.Create(bottom);
        var menuRect = menu.GetComponent<RectTransform>();
        UiFactory.Anchor(menuRect, new Vector2(0.75f, 0f), new Vector2(1f, 1f));
        menuRect.offsetMin = new Vector2(5f, 10f);
        menuRect.offsetMax = new Vector2(-10f, -10f);

        var overlay = BuildOverlay(canvas, out var overlayText, out var overlaySubtext, out var resetButton);
        var popup = StatPopupView.Create(canvas);
        var turnStrip = TurnOrderStripView.Create(field);
        var escape = EscapeMenuView.Create(canvas, popup);

        var hud = gameObject.AddComponent<BattleHud>();
        hud.Init(
            field,
            log,
            menu,
            equipment,
            overlay,
            overlayText,
            overlaySubtext,
            resetButton,
            connectionLabel,
            stageLabel,
            popup,
            turnStrip,
            escape
        );
    }

    static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        // This project has the Input System package active; StandaloneInputModule
        // reads the legacy Input class and would throw, killing every UI click.
        var module = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        // A module added from code starts with no action asset, which silently
        // swallows all pointer input. Reflection keeps this compiling across
        // Input System versions that name the helper differently.
        if (module.actionsAsset == null)
        {
            var assign = module
                .GetType()
                .GetMethod(
                    "AssignDefaultActions",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                );
            assign?.Invoke(module, null);

            if (module.actionsAsset == null)
            {
                Debug.LogError(
                    "InputSystemUIInputModule has no action asset; UI clicks will not register. "
                        + "Assign project-wide Input Actions in Project Settings > Input System Package."
                );
            }
        }
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    void EnsureConnection()
    {
        var manager = FindAnyObjectByType<GameManager>();
        if (manager == null)
        {
            var go = new GameObject("SpacetimeDB");
            manager = go.AddComponent<GameManager>();
        }

        manager.Configure(serverUrl, databaseName);
    }

    /// Opaque full-canvas gradient so the camera clear color never shows through.
    /// Anchored to stretch at any aspect; raycasts off so it never eats clicks.
    static void BuildBackdrop(Transform canvas)
    {
        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 1f);
        }

        var backdrop = UiFactory.Panel(canvas, "Backdrop", Color.white);
        backdrop.sprite = PlaceholderArt.VerticalGradient(
            new Color(0.10f, 0.12f, 0.16f, 1f),
            new Color(0.04f, 0.05f, 0.07f, 1f)
        );
        backdrop.type = Image.Type.Simple;
        backdrop.preserveAspect = false;
        backdrop.raycastTarget = false;
        UiFactory.Anchor(backdrop.rectTransform, Vector2.zero, Vector2.one);
        backdrop.rectTransform.SetAsFirstSibling();
    }

    static Transform BuildCanvas()
    {
        var go = new GameObject(
            "BattleCanvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return go.transform;
    }

    static RectTransform BuildOverlay(
        Transform parent,
        out Text overlayText,
        out Text overlaySubtext,
        out GameObject resetButton
    )
    {
        var panel = UiFactory.Panel(parent, "Overlay", new Color(0f, 0f, 0f, 0.82f));
        UiFactory.Anchor(panel.rectTransform, Vector2.zero, Vector2.one);

        overlayText = UiFactory.Label(
            panel.transform,
            "Result",
            "",
            96,
            TextAnchor.MiddleCenter,
            UiFactory.TextColor
        );
        UiFactory.Anchor(overlayText.rectTransform, new Vector2(0f, 0.48f), new Vector2(1f, 0.78f));

        overlaySubtext = UiFactory.Label(
            panel.transform,
            "Next",
            "",
            32,
            TextAnchor.MiddleCenter,
            UiFactory.ActiveColor
        );
        UiFactory.Anchor(overlaySubtext.rectTransform, new Vector2(0.1f, 0.36f), new Vector2(0.9f, 0.48f));

        var reset = UiFactory.TextButton(panel.transform, "Reset", "Reset Stage (debug)", 22);
        var resetRect = reset.GetComponent<RectTransform>();
        resetRect.anchorMin = new Vector2(0.5f, 0.3f);
        resetRect.anchorMax = new Vector2(0.5f, 0.3f);
        resetRect.pivot = new Vector2(0.5f, 0.5f);
        resetRect.sizeDelta = new Vector2(320f, 56f);
        resetRect.anchoredPosition = Vector2.zero;
        reset.onClick.AddListener(GameManager.ResetStage);
        resetButton = reset.gameObject;

        panel.gameObject.SetActive(false);
        return panel.rectTransform;
    }
}
