using System;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;

/// One combatant card: shape, name, HP bar with cur/max, and (players only) a
/// mana bar. Purely presentational; clicking only reports the entity id upward.
public class EntityView : MonoBehaviour
{
    public ulong EntityId { get; private set; }
    public RectTransform Root => _root;
    public Vector2 Home { get; private set; }
    public bool Busy;

    RectTransform _root;
    Image _card;
    Image _shape;
    Image _hpFill;
    Image _manaFill;
    Text _nameText;
    Text _hpText;
    Text _manaText;
    Text _tagText;
    Button _button;
    RectTransform _manaRow;

    Action<ulong> _onClick;

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
        view._button.onClick.AddListener(view.HandleClick);

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

        // Name + bars stack under the shape.
        var footer = UiFactory.NewRect(card.transform, "Footer");
        footer.anchorMin = new Vector2(0f, 0f);
        footer.anchorMax = new Vector2(1f, 0f);
        footer.pivot = new Vector2(0.5f, 0f);
        footer.sizeDelta = new Vector2(0f, showMana ? 74f : 50f);
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
        hpRow.anchorMin = new Vector2(0f, 1f);
        hpRow.anchorMax = new Vector2(1f, 1f);
        hpRow.pivot = new Vector2(0.5f, 1f);
        hpRow.sizeDelta = new Vector2(0f, 20f);
        hpRow.anchoredPosition = new Vector2(0f, -26f);
        view._hpFill = UiFactory.Bar(hpRow, "Hp", UiFactory.HpColor, out view._hpText);
        UiFactory.Anchor(
            view._hpFill.transform.parent.GetComponent<RectTransform>(),
            Vector2.zero,
            Vector2.one
        );

        if (showMana)
        {
            view._manaRow = UiFactory.NewRect(footer, "ManaRow");
            view._manaRow.anchorMin = new Vector2(0f, 1f);
            view._manaRow.anchorMax = new Vector2(1f, 1f);
            view._manaRow.pivot = new Vector2(0.5f, 1f);
            view._manaRow.sizeDelta = new Vector2(0f, 20f);
            view._manaRow.anchoredPosition = new Vector2(0f, -48f);
            view._manaFill = UiFactory.Bar(
                view._manaRow,
                "Mana",
                UiFactory.ManaColor,
                out view._manaText
            );
            UiFactory.Anchor(
                view._manaFill.transform.parent.GetComponent<RectTransform>(),
                Vector2.zero,
                Vector2.one
            );
        }

        return view;
    }

    public void SetPosition(Vector2 anchoredPosition)
    {
        Home = anchoredPosition;
        _root.anchorMin = Vector2.zero;
        _root.anchorMax = Vector2.zero;
        _root.pivot = new Vector2(0.5f, 0f);
        if (!Busy)
        {
            _root.anchoredPosition = anchoredPosition;
        }
    }

    public void Bind(
        Entity entity,
        bool isActive,
        bool isLocal,
        bool targetable,
        Action<ulong> onClick
    )
    {
        EntityId = entity.EntityId;
        _onClick = onClick;

        var isEnemy = entity.Faction == SpacetimeDB.Types.Team.Enemies;
        var color = isEnemy
            ? PlaceholderArt.EnemyColor(entity.Slot)
            : PlaceholderArt.ClassColor(entity.ClassName);
        var shape = isEnemy ? ShapeKind.Mound : PlaceholderArt.ClassShape(entity.ClassName);

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

        _button.interactable = targetable;
    }

    void HandleClick() => _onClick?.Invoke(EntityId);
}
