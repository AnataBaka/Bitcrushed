using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;

/// Left edge of the battlefield: the round's turn order exactly as the module
/// built it, fastest first. Rows dim once a combatant has acted and the row for
/// whoever is up right now is highlighted.
public class TurnOrderView : MonoBehaviour
{
    const int MaxRows = 8;
    const float RowHeight = 34f;

    sealed class Row
    {
        public Image Background;
        public Text Label;
    }

    readonly List<Row> _rows = new List<Row>();
    RectTransform _list;

    public static TurnOrderView Create(Transform parent)
    {
        var panel = UiFactory.Panel(parent, "TurnOrder", UiFactory.PanelColor);
        var view = panel.gameObject.AddComponent<TurnOrderView>();

        var title = UiFactory.Label(
            panel.transform,
            "Title",
            "TURN ORDER",
            15,
            TextAnchor.UpperCenter,
            UiFactory.MutedColor
        );
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(-12f, 20f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -8f);

        view._list = UiFactory.NewRect(panel.transform, "Rows");
        view._list.anchorMin = Vector2.zero;
        view._list.anchorMax = Vector2.one;
        view._list.offsetMin = new Vector2(8f, 8f);
        view._list.offsetMax = new Vector2(-8f, -30f);

        var layout = view._list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 5f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.childAlignment = TextAnchor.UpperCenter;

        for (var i = 0; i < MaxRows; i++)
        {
            view._rows.Add(view.MakeRow());
        }

        return view;
    }

    Row MakeRow()
    {
        var background = UiFactory.Panel(_list, $"Row{_list.childCount}", UiFactory.SlotColor);
        background.raycastTarget = false;

        var element = background.gameObject.AddComponent<LayoutElement>();
        element.minHeight = RowHeight;
        element.preferredHeight = RowHeight;
        element.flexibleHeight = 0f;

        var label = UiFactory.Label(
            background.transform,
            "Label",
            "",
            15,
            TextAnchor.MiddleCenter,
            UiFactory.TextColor
        );
        UiFactory.Anchor(label.rectTransform, Vector2.zero, Vector2.one);

        background.gameObject.SetActive(false);
        return new Row { Background = background, Label = label };
    }

    public void Render(GameSession session)
    {
        var inBattle = session != null && session.Phase == BattlePhase.InBattle;
        var queue = inBattle ? GameManager.TurnQueue() : new List<TurnOrder>();
        var activeId = session?.ActiveEntityId ?? 0;

        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            if (i >= queue.Count)
            {
                row.Background.gameObject.SetActive(false);
                continue;
            }

            var entry = queue[i];
            var entity = GameManager.FindEntity(entry.EntityId);
            if (entity == null)
            {
                row.Background.gameObject.SetActive(false);
                continue;
            }

            row.Background.gameObject.SetActive(true);
            var rush = entry.IsRush ? "!" : "";
            row.Label.text = $"{entity.Name}{rush}  spd {entry.Speed}";

            var isActive = entry.EntityId == activeId;
            if (isActive)
            {
                row.Background.color = new Color(0.95f, 0.82f, 0.30f, 0.90f);
                row.Label.color = new Color(0.10f, 0.09f, 0.06f);
            }
            else
            {
                var acted = entry.HasActed;
                row.Background.color = acted
                    ? new Color(0.16f, 0.16f, 0.19f, 0.85f)
                    : UiFactory.SlotColor;
                row.Label.color = acted ? UiFactory.MutedColor : UiFactory.TextColor;
            }
        }

        gameObject.SetActive(inBattle && queue.Count > 0);
    }
}
