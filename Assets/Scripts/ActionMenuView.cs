using System;
using System.Collections.Generic;
using System.Text;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;

/// Bottom-right menu. Root shows Attack / Items / Focus; Attack and Items swap
/// the panel for a sub-list with a Back button. Only renders state and raises
/// events; it never decides whether an action is legal.
public class ActionMenuView : MonoBehaviour
{
    const float SubButtonHeight = 34f;

    enum Page
    {
        Lobby,
        Root,
        Skills,
        Items,
    }

    public Action OnJoin;
    public Action OnReady;
    public Action OnFocus;

    /// Raised with the bag item the player picked.
    public Action<ulong> OnUseItem;

    /// Raised when the player picked how to attack and must now click a target.
    /// 0 means the free basic attack, anything else is a SkillDef id.
    public Action<uint> OnAttackSelected;

    Text _status;
    RectTransform _lobby;
    RectTransform _root;
    RectTransform _skills;
    RectTransform _items;

    Button _joinButton;
    Button _readyButton;

    Page _page = Page.Root;
    ulong _pageOwner;

    // Sub-pages are rebuilt only when their contents actually change, so hovering
    // and clicking are not interrupted by every table update.
    string _skillsSignature = "";
    string _itemsSignature = "";

    readonly List<Button> _skillButtons = new List<Button>();
    readonly List<Button> _itemButtons = new List<Button>();

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

        view._lobby = MakePage(panel.transform, "LobbyPage", 10f);
        view._root = MakePage(panel.transform, "RootPage", 10f);
        view._skills = MakePage(panel.transform, "SkillsPage", 6f);
        view._items = MakePage(panel.transform, "ItemsPage", 6f);

        view._joinButton = UiFactory.TextButton(view._lobby, "Join", "Join Party");
        view._readyButton = UiFactory.TextButton(view._lobby, "Ready", "Ready Up");

        var attack = UiFactory.TextButton(view._root, "Attack", "Attack");
        var items = UiFactory.TextButton(view._root, "Items", "Items");
        var focus = UiFactory.TextButton(view._root, "Focus", "Focus");

        view._joinButton.onClick.AddListener(() => view.OnJoin?.Invoke());
        view._readyButton.onClick.AddListener(() => view.OnReady?.Invoke());

        attack.onClick.AddListener(() => view.Go(Page.Skills));
        items.onClick.AddListener(() => view.Go(Page.Items));
        focus.onClick.AddListener(
            () =>
            {
                view.Go(Page.Root);
                view.OnFocus?.Invoke();
            }
        );

        return view;
    }

    static RectTransform MakePage(Transform parent, string name, float spacing)
    {
        var page = UiFactory.NewRect(parent, name);
        page.anchorMin = Vector2.zero;
        page.anchorMax = Vector2.one;
        page.offsetMin = new Vector2(12f, 12f);
        page.offsetMax = new Vector2(-12f, -52f);

        var layout = page.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
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
            _readyButton.interactable = false;
            SetReadyCaption(false);
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
            var localPlayer = GameManager.LocalPlayer();
            _readyButton.interactable = localPlayer != null;
            SetReadyCaption(localPlayer != null && localPlayer.Ready);
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

        RebuildSkills(me);
        RebuildItems();

        foreach (var button in _skillButtons)
        {
            button.interactable = canAct && ButtonAffordable(button, me);
        }

        foreach (var button in _itemButtons)
        {
            button.interactable = canAct;
        }
    }

    void SetReadyCaption(bool ready)
    {
        var label = _readyButton.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.text = ready ? "Cancel Ready" : "Ready Up";
        }
    }

    /// Rebuilds the Attack sub-page: the free swing plus every learned skill.
    void RebuildSkills(Entity me)
    {
        var skills = me == null ? new List<SkillDef>() : GameManager.LocalSkills();
        var signature = new StringBuilder(me == null ? "-" : me.BasicAttackName);
        foreach (var skill in skills)
        {
            signature.Append('|').Append(skill.Id).Append(':').Append(skill.Name);
        }

        if (signature.ToString() == _skillsSignature)
        {
            return;
        }

        _skillsSignature = signature.ToString();
        ClearPage(_skills, _skillButtons);

        if (me != null)
        {
            var basic = UiFactory.TextButton(
                _skills,
                "Basic",
                $"{me.BasicAttackName}  (free)",
                18,
                SubButtonHeight
            );
            basic.onClick.AddListener(() => Select(0u));
            _skillButtons.Add(basic);
        }

        foreach (var skill in skills)
        {
            var id = skill.Id;
            var cost = skill.ManaCost;
            var spread = skill.TargetCount > 1 ? $" x{skill.TargetCount}" : "";
            var caption = skill.TargetCount == 0
                ? $"{skill.Name}  ({cost} mp, buff)"
                : $"{skill.Name}{spread}  ({cost} mp)";

            var button = UiFactory.TextButton(
                _skills,
                skill.Name,
                caption,
                18,
                SubButtonHeight
            );
            button.onClick.AddListener(() => Select(id));

            // Buffs resolve immediately, so remember the cost for the mana gate.
            var element = button.gameObject.AddComponent<ActionCost>();
            element.ManaCost = cost;
            _skillButtons.Add(button);
        }

        var back = UiFactory.TextButton(_skills, "Back", "Back", 18, SubButtonHeight);
        back.onClick.AddListener(() => Go(Page.Root));
    }

    /// Rebuilds the Items sub-page from whatever potions are in the bag.
    void RebuildItems()
    {
        var potions = GameManager.BagPotions();
        var signature = new StringBuilder();
        foreach (var potion in potions)
        {
            signature.Append(potion.Id).Append(':').Append(potion.Quantity).Append('|');
        }

        if (signature.ToString() == _itemsSignature)
        {
            return;
        }

        _itemsSignature = signature.ToString();
        ClearPage(_items, _itemButtons);

        foreach (var potion in potions)
        {
            var def = GameManager.ItemDefOf(potion);
            if (def == null)
            {
                continue;
            }

            var id = potion.Id;
            var button = UiFactory.TextButton(
                _items,
                def.Name,
                $"{def.Name}  x{potion.Quantity}",
                18,
                SubButtonHeight
            );
            button.onClick.AddListener(
                () =>
                {
                    Go(Page.Root);
                    OnUseItem?.Invoke(id);
                }
            );
            _itemButtons.Add(button);
        }

        if (potions.Count == 0)
        {
            var empty = UiFactory.TextButton(_items, "Empty", "No potions left", 18, SubButtonHeight);
            empty.interactable = false;
        }

        var back = UiFactory.TextButton(_items, "Back", "Back", 18, SubButtonHeight);
        back.onClick.AddListener(() => Go(Page.Root));
    }

    void Select(uint skillDefId)
    {
        Go(Page.Root);
        OnAttackSelected?.Invoke(skillDefId);
    }

    static bool ButtonAffordable(Button button, Entity me)
    {
        if (me == null)
        {
            return false;
        }

        var cost = button.GetComponent<ActionCost>();
        return cost == null || me.Mana >= cost.ManaCost;
    }

    static void ClearPage(RectTransform page, List<Button> tracked)
    {
        tracked.Clear();
        for (var i = page.childCount - 1; i >= 0; i--)
        {
            // Destroy only takes effect at end of frame, so detach first or the
            // layout group lays out the old and new buttons together.
            var child = page.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
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

/// Tags a menu button with the mana it costs, so the mana gate needs no lookup.
public class ActionCost : MonoBehaviour
{
    public int ManaCost;
}
