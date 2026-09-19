using System;
using System.Collections.Generic;
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
    public Action<uint> OnUseItem;
    public Action<uint> OnSkillSelected;

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

    readonly List<Button> _skillButtons = new List<Button>();
    readonly List<Button> _itemButtons = new List<Button>();
    Button _skillBack;
    Button _itemBack;

    Page _page = Page.Root;
    ulong _pageOwner;
    string _skillKey = "";
    string _itemKey = "";

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

        view._skillBack = UiFactory.TextButton(view._skills, "Back", "Back", 20);
        view._itemBack = UiFactory.TextButton(view._items, "Back", "Back", 20);

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

        view._skillBack.onClick.AddListener(() => view.Go(Page.Root));
        view._itemBack.onClick.AddListener(() => view.Go(Page.Root));
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
        layout.spacing = 8f;
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
        var player = GameManager.LocalPlayer();
        SetPageInteractable(_root, canAct);
        _attackButton.interactable = canAct && GameManager.HasWeapon(player);

        RebuildSkills(me, canAct);
        RebuildItems(canAct);
    }

    void RebuildSkills(Entity me, bool canAct)
    {
        var skills = GameManager.LocalSkills();
        var key = "";
        foreach (var skill in skills)
        {
            key += $"{skill.Id}:{skill.ManaCost};";
        }

        if (key != _skillKey)
        {
            _skillKey = key;
            foreach (var button in _skillButtons)
            {
                if (button != null)
                {
                    DestroyImmediate(button.gameObject);
                }
            }

            _skillButtons.Clear();
            foreach (var skill in skills)
            {
                var captured = skill;
                var aoe = captured.TargetCount > 1 ? $" AoE{captured.TargetCount}" : "";
                var caption =
                    captured.BaseDamage == 0
                        ? $"{captured.Name}  ({captured.ManaCost} mp)"
                        : $"{captured.Name}  ({captured.ManaCost} mp){aoe}";
                var button = UiFactory.TextButton(_skills, captured.Name, caption, 16, 44f);
                button.transform.SetSiblingIndex(_skillButtons.Count);
                button.onClick.AddListener(
                    () =>
                    {
                        Go(Page.Root);
                        OnSkillSelected?.Invoke(captured.Id);
                    }
                );
                _skillButtons.Add(button);
            }

            _skillBack.transform.SetAsLastSibling();
        }

        if (me == null)
        {
            return;
        }

        foreach (var button in _skillButtons)
        {
            if (button == null)
            {
                continue;
            }

            var skill = FindSkillByButton(skills, button);
            button.interactable = canAct && skill != null && me.Mana >= (int)skill.ManaCost;
        }

        _skillBack.interactable = true;
    }

    static SkillDef FindSkillByButton(List<SkillDef> skills, Button button)
    {
        foreach (var skill in skills)
        {
            if (button.gameObject.name == skill.Name)
            {
                return skill;
            }
        }

        return null;
    }

    void RebuildItems(bool canAct)
    {
        var potions = GameManager.LocalPotions();
        var key = "";
        foreach (var potion in potions)
        {
            key += $"{potion.Id}:{potion.Quantity};";
        }

        if (key != _itemKey)
        {
            _itemKey = key;
            foreach (var button in _itemButtons)
            {
                if (button != null)
                {
                    DestroyImmediate(button.gameObject);
                }
            }

            _itemButtons.Clear();
            foreach (var potion in potions)
            {
                var captured = potion;
                var def = GameManager.ItemDefOf(captured);
                var name = def == null ? "Potion" : def.Name;
                var button = UiFactory.TextButton(
                    _items,
                    name,
                    $"{name}  x{captured.Quantity}",
                    16,
                    44f
                );
                button.transform.SetSiblingIndex(_itemButtons.Count);
                button.onClick.AddListener(
                    () =>
                    {
                        Go(Page.Root);
                        OnUseItem?.Invoke(captured.Id);
                    }
                );
                _itemButtons.Add(button);
            }

            _itemBack.transform.SetAsLastSibling();
        }

        foreach (var button in _itemButtons)
        {
            if (button != null)
            {
                button.interactable = canAct;
            }
        }

        _itemBack.interactable = true;
    }

    static void SetPageInteractable(RectTransform page, bool interactable)
    {
        foreach (var button in page.GetComponentsInChildren<Button>(true))
        {
            button.interactable = interactable;
        }
    }
}
