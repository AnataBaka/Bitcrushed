using System.Collections.Generic;
using SpacetimeDB;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;

/// Shared LEVEL UP banner. Fires when a known player's CharacterLevel increases
/// after the subscription is primed. Never from log text, snapshots, joins, or
/// reconnects. Cosmetic only: does not intercept clicks or pause the game.
public class LevelUpBannerView : MonoBehaviour
{
    public const int TitleSize = 72;
    public const float HoldSeconds = 2f;
    public const float PopSeconds = 0.16f;
    public const float FadeOutSeconds = 0.35f;
    const float PopScale = 0.82f;
    const int MaxLines = 3;

    static readonly Color TitleColor = new Color(1f, 0.86f, 0.28f, 1f);
    static readonly Color OutlineColor = new Color(0.08f, 0.05f, 0.02f, 0.95f);
    static readonly Color SubColor = new Color(0.96f, 0.93f, 0.82f, 1f);

    readonly Dictionary<Identity, uint> _levels = new Dictionary<Identity, uint>();
    readonly List<Identity> _scratch = new List<Identity>();
    readonly List<string> _lines = new List<string>();

    RectTransform _root;
    CanvasGroup _group;
    Text _sub;
    bool _primed;
    bool _dirty;
    float _t;
    Phase _phase = Phase.Hidden;

    enum Phase
    {
        Hidden,
        Pop,
        Hold,
        Fade,
    }

    public static LevelUpBannerView Create(Transform canvas)
    {
        var root = UiFactory.NewRect(canvas, "LevelUpBanner");
        root.anchorMin = new Vector2(0.5f, 1f);
        root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.sizeDelta = new Vector2(920f, 168f);
        root.anchoredPosition = new Vector2(0f, -88f);

        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var title = UiFactory.Label(
            root,
            "Title",
            "LEVEL UP",
            TitleSize,
            TextAnchor.LowerCenter,
            TitleColor
        );
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        UiFactory.Anchor(title.rectTransform, new Vector2(0f, 0.42f), Vector2.one);
        var outline = title.gameObject.AddComponent<Outline>();
        outline.effectColor = OutlineColor;
        outline.effectDistance = GameFont.OutlineDistance(TitleSize);
        outline.useGraphicAlpha = true;

        var sub = UiFactory.Label(root, "Sub", "", 24, TextAnchor.UpperCenter, SubColor);
        sub.horizontalOverflow = HorizontalWrapMode.Overflow;
        sub.verticalOverflow = VerticalWrapMode.Overflow;
        UiFactory.Anchor(sub.rectTransform, Vector2.zero, new Vector2(1f, 0.48f));

        var view = root.gameObject.AddComponent<LevelUpBannerView>();
        view._root = root;
        view._group = group;
        view._sub = sub;
        return view;
    }

    public void Raise() => transform.SetAsLastSibling();

    void OnEnable()
    {
        GameManager.StateChanged += MarkDirty;
        _dirty = true;
    }

    void OnDisable()
    {
        GameManager.StateChanged -= MarkDirty;
    }

    void MarkDirty() => _dirty = true;

    void LateUpdate()
    {
        if (_dirty)
        {
            _dirty = false;
            ObservePlayers();
        }

        TickBanner();
    }

    void ObservePlayers()
    {
        if (GameManager.Instance == null || !GameManager.Instance.SubscriptionReady || GameManager.Conn == null)
        {
            _primed = false;
            _levels.Clear();
            return;
        }

        if (!_primed)
        {
            CaptureBaselines();
            _primed = true;
            return;
        }

        _lines.Clear();
        var seen = new HashSet<Identity>();
        foreach (var player in GameManager.Conn.Db.Player.Iter())
        {
            seen.Add(player.Identity);
            if (!_levels.TryGetValue(player.Identity, out var previous))
            {
                _levels[player.Identity] = player.CharacterLevel;
                continue;
            }

            if (player.CharacterLevel > previous)
            {
                var entity = GameManager.FindEntity(player.EntityId);
                var name =
                    entity != null && !string.IsNullOrEmpty(entity.Name)
                        ? entity.Name
                        : "A hero";
                _lines.Add($"{name} reached level {player.CharacterLevel}!");
            }

            _levels[player.Identity] = player.CharacterLevel;
        }

        _scratch.Clear();
        foreach (var id in _levels.Keys)
        {
            if (!seen.Contains(id))
            {
                _scratch.Add(id);
            }
        }

        foreach (var id in _scratch)
        {
            _levels.Remove(id);
        }

        if (_lines.Count > 0)
        {
            Show(_lines);
        }
    }

    void CaptureBaselines()
    {
        _levels.Clear();
        foreach (var player in GameManager.Conn.Db.Player.Iter())
        {
            _levels[player.Identity] = player.CharacterLevel;
        }
    }

    void Show(List<string> lines)
    {
        var count = Mathf.Min(MaxLines, lines.Count);
        var text = lines[0];
        for (var i = 1; i < count; i++)
        {
            text += "\n" + lines[i];
        }

        _sub.text = text;
        _phase = Phase.Pop;
        _t = 0f;
        _group.alpha = 0f;
        _root.localScale = Vector3.one * PopScale;
    }

    void TickBanner()
    {
        if (_phase == Phase.Hidden)
        {
            return;
        }

        if (_phase == Phase.Pop)
        {
            _t += Time.deltaTime / PopSeconds;
            var u = Mathf.Clamp01(_t);
            var e = u * u * (3f - (2f * u));
            _group.alpha = e;
            _root.localScale = Vector3.Lerp(Vector3.one * PopScale, Vector3.one, e);
            if (u >= 1f)
            {
                _phase = Phase.Hold;
                _t = 0f;
                _group.alpha = 1f;
                _root.localScale = Vector3.one;
            }

            return;
        }

        if (_phase == Phase.Hold)
        {
            _t += Time.deltaTime;
            if (_t >= HoldSeconds)
            {
                _phase = Phase.Fade;
                _t = 0f;
            }

            return;
        }

        _t += Time.deltaTime / FadeOutSeconds;
        var fade = Mathf.Clamp01(_t);
        _group.alpha = 1f - fade;
        if (fade >= 1f)
        {
            _phase = Phase.Hidden;
            _group.alpha = 0f;
            _root.localScale = Vector3.one;
        }
    }
}
