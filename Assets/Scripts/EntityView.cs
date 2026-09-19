using System;
using System.Collections;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// One combatant card: shape, name, HP bar with cur/max, and (players only) a
/// mana bar. Purely presentational; clicking only reports the entity id upward.
public class EntityView : MonoBehaviour, IPointerClickHandler
{
    public ulong EntityId { get; private set; }

    RectTransform _root;
    Image _card;
    Image _shape;
    Image _hpFill;
    Image _manaFill;
    Text _nameText;
    Text _hpText;
    Text _manaText;
    Text _tagText;
    Text _readyBanner;
    Text _statusText;
    Button _button;
    RectTransform _manaRow;

    Action<ulong, Vector2> _onClick;

    public RectTransform Rect => _root;

    Vector2 _home;
    bool _lunging;
    Coroutine _hit;

    /// Where the card sits when nothing is animating.
    public Vector2 Home => _home;

    public static EntityView Create(Transform parent, string name, Vector2 size, bool showMana)
    {
        var card = UiFactory.Panel(parent, name, new Color(0f, 0f, 0f, 0f));
        card.rectTransform.sizeDelta = size;
        card.raycastTarget = true;

        var view = card.gameObject.AddComponent<EntityView>();
        view._root = card.rectTransform;
        view._card = card;

        view._button = card.gameObject.AddComponent<Button>();
        view._button.transition = Selectable.Transition.None;

        // Shape sits in the upper portion of the card.
        var shapeRect = UiFactory.NewRect(card.transform, "Shape");
        shapeRect.anchorMin = new Vector2(0.5f, 1f);
        shapeRect.anchorMax = new Vector2(0.5f, 1f);
        shapeRect.pivot = new Vector2(0.5f, 1f);
        shapeRect.sizeDelta = new Vector2(size.y * 0.62f, size.y * 0.62f);
        shapeRect.anchoredPosition = Vector2.zero;
        view._shape = shapeRect.gameObject.AddComponent<Image>();
        view._shape.preserveAspect = true;
        view._shape.raycastTarget = false;

        view._tagText = UiFactory.Label(
            card.transform,
            "Tag",
            "",
            16,
            TextAnchor.UpperCenter,
            UiFactory.ActiveColor
        );
        view._tagText.rectTransform.anchorMin = new Vector2(0f, 1f);
        view._tagText.rectTransform.anchorMax = new Vector2(1f, 1f);
        view._tagText.rectTransform.pivot = new Vector2(0.5f, 0f);
        view._tagText.rectTransform.sizeDelta = new Vector2(0f, 20f);
        view._tagText.rectTransform.anchoredPosition = new Vector2(0f, 2f);

        view._readyBanner = UiFactory.Label(
            card.transform,
            "ReadyBanner",
            "",
            18,
            TextAnchor.MiddleCenter,
            new Color(0.35f, 0.88f, 0.42f, 1f)
        );
        view._readyBanner.fontStyle = FontStyle.Bold;
        view._readyBanner.rectTransform.anchorMin = new Vector2(0f, 1f);
        view._readyBanner.rectTransform.anchorMax = new Vector2(1f, 1f);
        view._readyBanner.rectTransform.pivot = new Vector2(0.5f, 0f);
        view._readyBanner.rectTransform.sizeDelta = new Vector2(0f, 24f);
        view._readyBanner.rectTransform.anchoredPosition = new Vector2(0f, 22f);

        // Name + bars stack under the shape.
        var footer = UiFactory.NewRect(card.transform, "Footer");
        footer.anchorMin = new Vector2(0f, 0f);
        footer.anchorMax = new Vector2(1f, 0f);
        footer.pivot = new Vector2(0.5f, 0f);
        footer.sizeDelta = new Vector2(0f, showMana ? 78f : 58f);
        footer.anchoredPosition = Vector2.zero;

        view._nameText = UiFactory.Label(
            footer,
            "Name",
            name,
            20,
            TextAnchor.MiddleCenter,
            UiFactory.TextColor
        );
        view._nameText.rectTransform.anchorMin = new Vector2(0f, 1f);
        view._nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
        view._nameText.rectTransform.pivot = new Vector2(0.5f, 1f);
        view._nameText.rectTransform.sizeDelta = new Vector2(0f, 24f);
        view._nameText.rectTransform.anchoredPosition = Vector2.zero;

        var hpRow = UiFactory.NewRect(footer, "HpRow");
        hpRow.anchorMin = new Vector2(0.16f, 1f);
        hpRow.anchorMax = new Vector2(0.84f, 1f);
        hpRow.pivot = new Vector2(0.5f, 1f);
        hpRow.sizeDelta = new Vector2(0f, 16f);
        hpRow.anchoredPosition = new Vector2(0f, -24f);
        view._hpFill = UiFactory.Bar(hpRow, "Hp", UiFactory.HpColor, out view._hpText);

        if (showMana)
        {
            view._manaRow = UiFactory.NewRect(footer, "ManaRow");
            view._manaRow.anchorMin = new Vector2(0.16f, 1f);
            view._manaRow.anchorMax = new Vector2(0.84f, 1f);
            view._manaRow.pivot = new Vector2(0.5f, 1f);
            view._manaRow.sizeDelta = new Vector2(0f, 16f);
            view._manaRow.anchoredPosition = new Vector2(0f, -43f);
            view._manaFill = UiFactory.Bar(
                view._manaRow,
                "Mana",
                UiFactory.ManaColor,
                out view._manaText
            );
        }

        view._statusText = UiFactory.Label(
            footer,
            "Status",
            "",
            12,
            TextAnchor.MiddleCenter,
            UiFactory.MutedColor
        );
        view._statusText.rectTransform.anchorMin = new Vector2(0f, 0f);
        view._statusText.rectTransform.anchorMax = new Vector2(1f, 0f);
        view._statusText.rectTransform.pivot = new Vector2(0.5f, 0f);
        view._statusText.rectTransform.sizeDelta = new Vector2(0f, 16f);
        view._statusText.rectTransform.anchoredPosition = Vector2.zero;

        return view;
    }

    public void SetPosition(Vector2 anchoredPosition)
    {
        _root.anchorMin = Vector2.zero;
        _root.anchorMax = Vector2.zero;
        _root.pivot = new Vector2(0.5f, 0f);
        _home = anchoredPosition;

        // A lunge in flight owns the position until it puts the card back.
        if (!_lunging)
        {
            _root.anchoredPosition = anchoredPosition;
        }
    }

    /// How long a full lunge takes, so callers can pace a volley of strikes.
    public const float LungeSeconds = 0.32f;

    /// Steps most of the way toward the target and back.
    public void PlayLunge(Vector2 targetPosition) => StartCoroutine(LungeRoutine(targetPosition));

    IEnumerator LungeRoutine(Vector2 targetPosition)
    {
        _lunging = true;
        var start = _home;
        var reach = Vector2.Lerp(start, targetPosition, 0.62f);

        yield return Slide(start, reach, 0.14f);
        yield return Slide(reach, _home, 0.18f);

        _root.anchoredPosition = _home;
        _lunging = false;
    }

    IEnumerator Slide(Vector2 from, Vector2 to, float duration)
    {
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            _root.anchoredPosition = Vector2.Lerp(from, to, t * t * (3f - (2f * t)));
            yield return null;
        }
    }

    /// Squash-and-flash on the receiving end of a hit.
    public void PlayHit()
    {
        if (_hit != null)
        {
            StopCoroutine(_hit);
            _shape.transform.localScale = Vector3.one;
        }

        _hit = StartCoroutine(HitRoutine());
    }

    IEnumerator HitRoutine()
    {
        var shape = _shape.transform;
        var elapsed = 0f;
        const float duration = 0.22f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            var bump = 1f + (Mathf.Sin(t * Mathf.PI) * 0.22f);
            shape.localScale = new Vector3(bump, bump, 1f);
            _shape.color = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.5f), 1f - t);
            yield return null;
        }

        shape.localScale = Vector3.one;
        _shape.color = Color.white;
        _hit = null;
    }

    public void Bind(
        Entity entity,
        bool isActive,
        bool isLocal,
        bool targetable,
        Action<ulong, Vector2> onClick
    )
    {
        EntityId = entity.EntityId;
        _onClick = onClick;

        var isEnemy = entity.Faction == SpacetimeDB.Types.Team.Enemies;
        var color = isEnemy
            ? PlaceholderArt.EnemyVisual(entity.ClassName).Color
            : PlaceholderArt.ClassColor(entity.ClassName);
        var shape = isEnemy
            ? PlaceholderArt.EnemyVisual(entity.ClassName).Shape
            : PlaceholderArt.ClassShape(entity.ClassName);

        _shape.sprite = PlaceholderArt.Shape(shape, color);
        _shape.color = Color.white;

        var suffix = isEnemy ? "" : $" ({entity.ClassName})";
        _nameText.text = $"{entity.Name}{suffix}{(isLocal ? " [you]" : "")}";
        _nameText.color = isActive ? UiFactory.ActiveColor : UiFactory.TextColor;

        UiFactory.SetBar(_hpFill, entity.Hp, entity.MaxHp);
        _hpText.text = $"{entity.Hp}/{entity.MaxHp} hp";

        if (_manaFill != null)
        {
            UiFactory.SetBar(_manaFill, entity.Mana, entity.MaxMana);
            _manaText.text = $"{entity.Mana}/{entity.MaxMana} mp";
        }

        if (targetable)
        {
            _tagText.text = "CLICK TO TARGET";
            _card.color = new Color(0.95f, 0.82f, 0.30f, 0.20f);
        }
        else if (isActive)
        {
            _tagText.text = "ACTIVE";
            _card.color = new Color(0.95f, 0.82f, 0.30f, 0.10f);
        }
        else
        {
            _tagText.text = "";
            _card.color = new Color(0f, 0f, 0f, 0f);
        }

        _button.interactable = entity.Alive || targetable;

        if (_statusText != null)
        {
            _statusText.text = StatusCaption(entity);
        }
    }

    static string StatusCaption(Entity entity)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (entity.StrengthBuff > 0)
        {
            parts.Add($"Enraged {entity.StrengthBuff}");
        }

        if (entity.NextTurnStrengthBonus > 0)
        {
            parts.Add($"Enraged next {entity.NextTurnStrengthBonus}");
        }

        if (entity.BurnStack > 0 && entity.BurnCount > 0)
        {
            parts.Add($"Burn {entity.BurnStack}x{entity.BurnCount}");
        }

        if (entity.FragileStacks > 0)
        {
            parts.Add($"Fragile {entity.FragileStacks}");
        }

        if (entity.WeakStacks > 0)
        {
            parts.Add($"Weak {entity.WeakStacks}");
        }

        if (entity.DodgeBonusPercent > 0)
        {
            parts.Add($"Dodge +{entity.DodgeBonusPercent}%");
        }

        if (entity.EvadeThreshold > 0)
        {
            parts.Add($"Evade <{entity.EvadeThreshold}");
        }

        if (entity.FinishTheJobStance)
        {
            parts.Add("Stance");
        }

        if (!entity.Alive)
        {
            parts.Add("Fallen");
        }

        return string.Join("  ", parts);
    }

    /// Driven from Player.Ready during lobby and rest stop.
    public void SetReadyBanner(bool visible)
    {
        if (_readyBanner == null)
        {
            return;
        }

        _readyBanner.text = visible ? "READY" : "";
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_button != null && !_button.interactable)
        {
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        _onClick?.Invoke(EntityId, eventData.position);
    }
}
