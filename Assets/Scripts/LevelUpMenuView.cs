using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;

/// Local-only spend UI. Shown when the subscribed Player row for this identity
/// has unspent points; other clients never see it.
public class LevelUpMenuView : MonoBehaviour
{
    const float Width = 420f;
    const float LabelWidth = 128f;
    const float ValueWidth = 72f;

    Text _points;
    Text _healthNow;
    Text _healthNext;
    Text _manaNow;
    Text _manaNext;
    Text _strengthNow;
    Text _strengthNext;
    Text _speedNow;
    Text _speedNext;
    Text _intelligenceNow;
    Text _intelligenceNext;
    Text _dexterityNow;
    Text _dexterityNext;
    Button _strengthPlus;
    Button _speedPlus;
    Button _intelligencePlus;
    Button _dexterityPlus;

    public bool IsOpen => gameObject.activeSelf;

    public static LevelUpMenuView Create(Transform canvas)
    {
        var panel = UiFactory.RoundedPanel(canvas, "LevelUpMenu", new Color(0.10f, 0.11f, 0.14f, 0.96f));
        panel.raycastTarget = true;
        var rt = panel.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(Width, 360f);

        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
        outline.effectDistance = new Vector2(2f, -2f);

        var view = panel.gameObject.AddComponent<LevelUpMenuView>();

        var header = UiFactory.RoundedPanel(panel.transform, "Header", new Color(0.22f, 0.32f, 0.48f, 1f));
        header.raycastTarget = false;
        header.rectTransform.anchorMin = new Vector2(0f, 1f);
        header.rectTransform.anchorMax = new Vector2(1f, 1f);
        header.rectTransform.pivot = new Vector2(0.5f, 1f);
        header.rectTransform.sizeDelta = new Vector2(-12f, 52f);
        header.rectTransform.anchoredPosition = new Vector2(0f, -8f);

        var title = UiFactory.Label(
            header.transform,
            "Title",
            "Level Up",
            22,
            TextAnchor.MiddleLeft,
            UiFactory.TextColor
        );
        title.fontStyle = FontStyle.Bold;
        UiFactory.Anchor(title.rectTransform, new Vector2(0f, 0.42f), new Vector2(1f, 1f));
        title.rectTransform.offsetMin = new Vector2(14f, 0f);
        title.rectTransform.offsetMax = new Vector2(-14f, 0f);

        view._points = UiFactory.Label(
            header.transform,
            "Points",
            "",
            15,
            TextAnchor.UpperLeft,
            UiFactory.ActiveColor
        );
        UiFactory.Anchor(view._points.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.48f));
        view._points.rectTransform.offsetMin = new Vector2(14f, 4f);
        view._points.rectTransform.offsetMax = new Vector2(-14f, 0f);

        var body = UiFactory.NewRect(panel.transform, "Body");
        body.anchorMin = new Vector2(0f, 0f);
        body.anchorMax = new Vector2(1f, 1f);
        body.offsetMin = new Vector2(16f, 16f);
        body.offsetMax = new Vector2(-16f, -68f);
        var layout = body.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        MakeReadRow(body, "Health", out view._healthNow, out view._healthNext);
        MakeReadRow(body, "Mana", out view._manaNow, out view._manaNext);
        view._strengthPlus = MakeSpendRow(body, "Strength", out view._strengthNow, out view._strengthNext, StatType.Strength);
        view._speedPlus = MakeSpendRow(body, "Speed", out view._speedNow, out view._speedNext, StatType.Speed);
        view._intelligencePlus = MakeSpendRow(
            body,
            "Intelligence",
            out view._intelligenceNow,
            out view._intelligenceNext,
            StatType.Intelligence
        );
        view._dexterityPlus = MakeSpendRow(
            body,
            "Dexterity",
            out view._dexterityNow,
            out view._dexterityNext,
            StatType.Dexterity
        );

        view.gameObject.SetActive(false);
        return view;
    }

    static RectTransform MakeRow(Transform parent, string name)
    {
        var row = UiFactory.NewRect(parent, name);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = false;
        var element = row.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 32f;
        element.preferredHeight = 32f;
        element.flexibleWidth = 1f;
        return row;
    }

    static Text MakeFixed(Transform parent, string name, string text, float width, TextAnchor anchor, Color color)
    {
        var label = UiFactory.Label(parent, name, text, 16, anchor, color);
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        var element = label.gameObject.AddComponent<LayoutElement>();
        element.minWidth = width;
        element.preferredWidth = width;
        element.flexibleWidth = 0f;
        return label;
    }

    static void MakeReadRow(Transform parent, string caption, out Text now, out Text next)
    {
        var row = MakeRow(parent, caption + "Row");
        MakeFixed(row, "Label", caption, LabelWidth, TextAnchor.MiddleLeft, UiFactory.MutedColor);
        now = MakeFixed(row, "Now", "", ValueWidth, TextAnchor.MiddleRight, UiFactory.TextColor);
        next = MakeFixed(row, "Next", "", ValueWidth, TextAnchor.MiddleRight, UiFactory.TextColor);
        var spacer = UiFactory.NewRect(row, "NoButton");
        var spacerElement = spacer.gameObject.AddComponent<LayoutElement>();
        spacerElement.minWidth = 36f;
        spacerElement.preferredWidth = 36f;
    }

    static Button MakeSpendRow(
        Transform parent,
        string caption,
        out Text now,
        out Text next,
        StatType stat
    )
    {
        var row = MakeRow(parent, caption + "Row");
        MakeFixed(row, "Label", caption, LabelWidth, TextAnchor.MiddleLeft, UiFactory.MutedColor);
        now = MakeFixed(row, "Now", "", ValueWidth, TextAnchor.MiddleRight, UiFactory.TextColor);
        next = MakeFixed(row, "Next", "", ValueWidth, TextAnchor.MiddleRight, UiFactory.ActiveColor);
        var button = UiFactory.TextButton(row, "Plus", "+", 22, 32f);
        var element = button.gameObject.GetComponent<LayoutElement>();
        element.minWidth = 36f;
        element.preferredWidth = 36f;
        element.flexibleWidth = 0f;
        element.minHeight = 32f;
        element.preferredHeight = 32f;
        button.onClick.AddListener(() => GameManager.SpendStatPoint(stat));
        return button;
    }

    public void Render(GameSession session)
    {
        var player = GameManager.LocalPlayer();
        var entity = GameManager.LocalEntity();
        var hideOutcome =
            session != null
            && (session.Phase == BattlePhase.Victory || session.Phase == BattlePhase.Defeat);
        var show =
            !hideOutcome
            && player != null
            && entity != null
            && entity.Alive
            && player.UnspentStatPoints > 0;

        if (!show)
        {
            gameObject.SetActive(false);
            return;
        }

        var wasActive = gameObject.activeSelf;
        gameObject.SetActive(true);
        if (!wasActive)
        {
            transform.SetAsLastSibling();
        }

        _points.text = $"Points left: {player.UnspentStatPoints}";
        SetPair(_healthNow, _healthNext, $"{entity.Hp}/{entity.MaxHp}", $"{entity.Hp}/{entity.MaxHp}");
        SetPair(_manaNow, _manaNext, $"{entity.Mana}/{entity.MaxMana}", $"{entity.Mana}/{entity.MaxMana}");
        SetPair(_strengthNow, _strengthNext, entity.Strength.ToString(), (entity.Strength + 1).ToString());
        SetPair(_speedNow, _speedNext, entity.Speed.ToString(), (entity.Speed + 1).ToString());
        SetPair(
            _intelligenceNow,
            _intelligenceNext,
            entity.Intelligence.ToString(),
            (entity.Intelligence + 1).ToString()
        );
        SetPair(_dexterityNow, _dexterityNext, entity.Dexterity.ToString(), (entity.Dexterity + 1).ToString());

        var canSpend = player.UnspentStatPoints > 0;
        _strengthPlus.interactable = canSpend;
        _speedPlus.interactable = canSpend;
        _intelligencePlus.interactable = canSpend;
        _dexterityPlus.interactable = canSpend;
    }

    static void SetPair(Text now, Text next, string current, string after)
    {
        now.text = current;
        next.text = after;
    }
}
