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
    RectTransform _skillsPage;
    RectTransform _skills;
    ScrollRect _skillsScroll;
    RectTransform _items;
    SkillTooltipView _skillTooltip;

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
        view._skillsPage = MakeScrollPage(panel.transform, "SkillsPage", 6f, out view._skills, out view._skillsScroll);
        view._items = MakePage(panel.transform, "ItemsPage", 6f);

        var canvas = panel.GetComponentInParent<Canvas>();
        view._skillTooltip = SkillTooltipView.Create(canvas != null ? canvas.transform : parent);

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

    static RectTransform MakeScrollPage(
        Transform parent,
        string name,
        float spacing,
        out RectTransform content,
        out ScrollRect scroll
    )
    {
        var page = UiFactory.NewRect(parent, name);
        page.anchorMin = Vector2.zero;
        page.anchorMax = Vector2.one;
        page.offsetMin = new Vector2(12f, 12f);
        page.offsetMax = new Vector2(-12f, -52f);

        scroll = page.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        scroll.inertia = true;

        var viewport = UiFactory.NewRect(page, "Viewport");
        UiFactory.Anchor(viewport, Vector2.zero, Vector2.one);
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
        viewportImage.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        content = UiFactory.NewRect(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.padding = new RectOffset(0, 0, 0, 4);

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;
        return page;
    }

    void Go(Page page)
    {
        _page = page;
        if (page != Page.Skills)
        {
            _skillTooltip?.Hide();
        }

        ApplyPage();
    }

    /// Returns the battle menu to Attack / Items / Focus after canceling a target pick.
    public void ShowRoot()
    {
        if (_page == Page.Lobby)
        {
            return;
        }

        _page = Page.Root;
        ApplyPage();
    }

    void ApplyPage()
    {
        _lobby.gameObject.SetActive(_page == Page.Lobby);
        _root.gameObject.SetActive(_page == Page.Root);
        _skillsPage.gameObject.SetActive(_page == Page.Skills);
        _items.gameObject.SetActive(_page == Page.Items);
        if (_page != Page.Skills)
        {
            _skillTooltip?.Hide();
        }
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

        if (session.Phase == BattlePhase.Waiting || session.Phase == BattlePhase.RestStop)
        {
            _page = Page.Lobby;
            ApplyPage();
            _status.text =
                session.Phase == BattlePhase.RestStop
                    ? "Rest Stop"
                    : $"Lobby {session.PlayerCount}/{session.MaxPlayers}";
            _joinButton.gameObject.SetActive(session.Phase == BattlePhase.Waiting);
            _joinButton.interactable =
                session.Phase == BattlePhase.Waiting
                && me == null
                && session.PlayerCount < session.MaxPlayers;
            var localPlayer = GameManager.LocalPlayer();
            var canReady =
                localPlayer != null
                && (session.Phase != BattlePhase.RestStop || (me != null && me.Alive));
            _readyButton.interactable = canReady;
            SetReadyCaption(localPlayer != null && localPlayer.Ready);
            return;
        }

        if (session.Phase == BattlePhase.StageTransition)
        {
            _page = Page.Root;
            ApplyPage();
            _status.text = "Stage cleared";
            SetPageInteractable(_root, false);
            return;
        }

        if (session.Phase != BattlePhase.InBattle)
        {
            _page = Page.Root;
            ApplyPage();
            _status.text = session.Phase == BattlePhase.Victory ? "Final Victory" : "Defeat";
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
            _status.text = targeting ? "Choose a target" : "Your turn";
        }
        else
        {
            _status.text = active == null ? "Waiting..." : $"Waiting for {active.Name}";
        }

        var canAct = myTurn && !targeting;
        SetPageInteractable(_root, canAct);

        RebuildSkills(me, session);
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
    void RebuildSkills(Entity me, GameSession session)
    {
        var skills = me == null ? new List<SkillDef>() : GameManager.LocalSkills();
        var signature = new StringBuilder(me == null ? "-" : me.BasicAttackName);
        signature.Append('|').Append(session == null ? 0 : session.Round);
        if (me != null)
        {
            signature
                .Append('|')
                .Append(me.SpearDiscount)
                .Append(':')
                .Append(me.VerticalCutDiscount)
                .Append(':')
                .Append(me.HasDodged)
                .Append(':')
                .Append(me.FinishTheJobUsed)
                .Append(':')
                .Append(me.FinishTheJobStance)
                .Append(':')
                .Append(me.NecromancyUsed)
                .Append(':')
                .Append(me.MagicBulletStage);
        }

        foreach (var skill in skills)
        {
            signature.Append('|').Append(skill.Id).Append(':').Append(skill.Name);
        }

        if (signature.ToString() == _skillsSignature)
        {
            return;
        }

        _skillsSignature = signature.ToString();
        _skillTooltip?.Hide();
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
            BindSkillHover(basic, null, basicAttack: true);
            _skillButtons.Add(basic);
        }

        foreach (var skill in skills)
        {
            var id = skill.Id;
            var cost = GameManager.EffectiveManaCost(skill, me);
            var caption = GameManager.SkillCaption(skill, me, session);

            var button = UiFactory.TextButton(
                _skills,
                skill.Name,
                caption,
                18,
                SubButtonHeight
            );
            button.onClick.AddListener(() => Select(id));
            BindSkillHover(button, skill, basicAttack: false);

            var element = button.gameObject.AddComponent<ActionCost>();
            element.ManaCost = cost;
            element.Ready = GameManager.SkillReadyToCast(skill, me, session);
            _skillButtons.Add(button);
        }

        var back = UiFactory.TextButton(_skills, "Back", "Back", 18, SubButtonHeight);
        back.onClick.AddListener(() => Go(Page.Root));

        if (_skillsScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            _skillsScroll.verticalNormalizedPosition = 1f;
        }
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
        if (cost == null)
        {
            return true;
        }

        return cost.Ready && me.Mana >= cost.ManaCost;
    }

    void BindSkillHover(Button button, SkillDef skill, bool basicAttack)
    {
        if (_skillTooltip == null)
        {
            return;
        }

        var hover = button.gameObject.AddComponent<SkillHoverTip>();
        hover.Bind(_skillTooltip, skill, basicAttack);
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
    public bool Ready = true;
}
