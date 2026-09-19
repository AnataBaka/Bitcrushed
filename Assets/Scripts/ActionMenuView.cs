using System;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;

/// Bottom-right menu. Root shows Attack / Items / Focus; Attack and Items swap
/// the panel for a sub-list with a Back button. Only renders state and raises
/// events; it never decides whether an action is legal.
public class ActionMenuView : MonoBehaviour
{
    enum Page
    {
        Lobby,
        Root,
        Skills,
        Items,
    }

    public Action OnJoin;
    public Action OnStartBattle;
    public Action OnFocus;
    public Action<ItemKind> OnUseItem;

    /// Raised when the player picked their attack skill and must now click a target.
    public Action OnSkillSelected;

    Text _status;
    RectTransform _lobby;
    RectTransform _root;
    RectTransform _skills;
    RectTransform _items;

    Button _joinButton;
    Button _startButton;
    Button _attackButton;
    Button _itemsButton;
    Button _focusButton;
    Button _skillButton;
    Text _skillLabel;
    Button _potionButton;
    Text _potionLabel;

    Page _page = Page.Root;
    ulong _pageOwner;

    public static ActionMenuView Create(Transform parent)
    {
        var panel = UiFactory.Panel(parent, "ActionMenu", UiFactory.PanelColor);
        var view = panel.gameObject.AddComponent<ActionMenuView>();

        view._status = UiFactory.Label(
            panel.transform,
            "Status",
            "",
            16,
            TextAnchor.UpperCenter,
            UiFactory.MutedColor
        );
        view._status.rectTransform.anchorMin = new Vector2(0f, 1f);
        view._status.rectTransform.anchorMax = new Vector2(1f, 1f);
        view._status.rectTransform.pivot = new Vector2(0.5f, 1f);
        view._status.rectTransform.sizeDelta = new Vector2(-16f, 44f);
        view._status.rectTransform.anchoredPosition = new Vector2(0f, -6f);

        view._lobby = MakePage(panel.transform, "LobbyPage");
        view._root = MakePage(panel.transform, "RootPage");
        view._skills = MakePage(panel.transform, "SkillsPage");
        view._items = MakePage(panel.transform, "ItemsPage");

        view._joinButton = UiFactory.TextButton(view._lobby, "Join", "Join Party");
        view._startButton = UiFactory.TextButton(view._lobby, "Start", "Start Battle");

        view._attackButton = UiFactory.TextButton(view._root, "Attack", "Attack");
        view._itemsButton = UiFactory.TextButton(view._root, "Items", "Items");
        view._focusButton = UiFactory.TextButton(view._root, "Focus", "Focus");

        view._skillButton = UiFactory.TextButton(view._skills, "Skill", "Skill", 20);
        view._skillLabel = view._skillButton.GetComponentInChildren<Text>();
        var skillBack = UiFactory.TextButton(view._skills, "Back", "Back", 20);

        view._potionButton = UiFactory.TextButton(view._items, "Potion", "Health Potion", 20);
        view._potionLabel = view._potionButton.GetComponentInChildren<Text>();
        var itemBack = UiFactory.TextButton(view._items, "Back", "Back", 20);

        view._joinButton.onClick.AddListener(() => view.OnJoin?.Invoke());
        view._startButton.onClick.AddListener(() => view.OnStartBattle?.Invoke());

        view._attackButton.onClick.AddListener(() => view.Go(Page.Skills));
        view._itemsButton.onClick.AddListener(() => view.Go(Page.Items));
        view._focusButton.onClick.AddListener(
            () =>
            {
                view.Go(Page.Root);
                view.OnFocus?.Invoke();
            }
        );

        skillBack.onClick.AddListener(() => view.Go(Page.Root));
        itemBack.onClick.AddListener(() => view.Go(Page.Root));

        view._skillButton.onClick.AddListener(
            () =>
            {
                view.Go(Page.Root);
                view.OnSkillSelected?.Invoke();
            }
        );

        view._potionButton.onClick.AddListener(
            () =>
            {
                view.Go(Page.Root);
                view.OnUseItem?.Invoke(ItemKind.HealthPotion);
            }
        );

        return view;
    }

    static RectTransform MakePage(Transform parent, string name)
    {
        var page = UiFactory.NewRect(parent, name);
        page.anchorMin = Vector2.zero;
        page.anchorMax = Vector2.one;
        page.offsetMin = new Vector2(12f, 12f);
        page.offsetMax = new Vector2(-12f, -52f);

        var layout = page.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.childAlignment = TextAnchor.UpperCenter;
        return page;
    }

    void Go(Page page)
    {
        _page = page;
        ApplyPage();
    }

    void ApplyPage()
    {
        _lobby.gameObject.SetActive(_page == Page.Lobby);
        _root.gameObject.SetActive(_page == Page.Root);
        _skills.gameObject.SetActive(_page == Page.Skills);
        _items.gameObject.SetActive(_page == Page.Items);
    }

    public void Render(GameSession session, Entity me, bool myTurn, bool targeting)
    {
        if (session == null)
        {
            _page = Page.Lobby;
            ApplyPage();
            _status.text = "Waiting for server...";
            _joinButton.interactable = false;
            _startButton.interactable = false;
            return;
        }

        // Collapse any open submenu as soon as the active entity changes.
        if (_pageOwner != session.ActiveEntityId)
        {
            _pageOwner = session.ActiveEntityId;
            if (_page == Page.Skills || _page == Page.Items)
            {
                _page = Page.Root;
            }
        }

        if (session.Phase == BattlePhase.Waiting)
        {
            _page = Page.Lobby;
            ApplyPage();
            _status.text = $"Lobby {session.PlayerCount}/{session.MaxPlayers}";
            _joinButton.interactable = me == null && session.PlayerCount < session.MaxPlayers;
            _startButton.interactable = me != null && session.PlayerCount > 0;
            return;
        }

        if (session.Phase != BattlePhase.InBattle)
        {
            _page = Page.Root;
            ApplyPage();
            _status.text = session.Phase == BattlePhase.Victory ? "Level Complete" : "Defeat";
            SetPageInteractable(_root, false);
            return;
        }

        if (_page == Page.Lobby)
        {
            _page = Page.Root;
        }

        ApplyPage();

        var active = GameManager.ActiveEntity();
        if (me == null)
        {
            _status.text = active == null ? "Battle in progress" : $"{active.Name}'s turn";
        }
        else if (!me.Alive)
        {
            _status.text = $"{me.Name} is defeated";
        }
        else if (myTurn)
        {
            _status.text = targeting ? "Choose an enemy target" : "Your turn";
        }
        else
        {
            _status.text = active == null ? "Waiting..." : $"Waiting for {active.Name}";
        }

        var canAct = myTurn && !targeting;
        SetPageInteractable(_root, canAct);
        SetPageInteractable(_skills, canAct);
        SetPageInteractable(_items, canAct);

        if (me != null)
        {
            _skillLabel.text = $"{me.SkillName}  ({me.SkillManaCost} mp)";
            _skillButton.interactable = canAct && me.Mana >= me.SkillManaCost;

            _potionLabel.text = $"Health Potion  x{me.Potions}";
            _potionButton.interactable = canAct && me.Potions > 0;
        }
    }

    static void SetPageInteractable(RectTransform page, bool interactable)
    {
        foreach (var button in page.GetComponentsInChildren<Button>(true))
        {
            button.interactable = interactable;
        }
    }
}
