using System.Linq;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;

/// Left-side speed order strip. Highlights the active combatant and dims anyone
/// who has already acted this round.
public class TurnMeterView : MonoBehaviour
{
    Transform _list;

    public static TurnMeterView Create(Transform parent)
    {
        var panel = UiFactory.Panel(parent, "TurnMeter", new Color(0.10f, 0.10f, 0.13f, 0.72f));
        var view = panel.gameObject.AddComponent<TurnMeterView>();

        var title = UiFactory.Label(
            panel.transform,
            "Title",
            "TURN",
            14,
            TextAnchor.UpperCenter,
            UiFactory.MutedColor
        );
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(-8f, 18f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -4f);

        var list = UiFactory.NewRect(panel.transform, "List");
        list.anchorMin = Vector2.zero;
        list.anchorMax = Vector2.one;
        list.offsetMin = new Vector2(6f, 6f);
        list.offsetMax = new Vector2(-6f, -24f);
        var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        view._list = list;
        return view;
    }

    public void Render(GameSession session)
    {
        Clear(_list);
        if (GameManager.Conn == null || session == null || session.Phase != BattlePhase.InBattle)
        {
            return;
        }

        var rows = GameManager.Conn.Db.TurnOrder.Iter().OrderBy(r => r.Idx).ToList();
        foreach (var row in rows)
        {
            var entity = GameManager.Conn.Db.Entity.EntityId.Find(row.EntityId);
            if (entity == null)
            {
                continue;
            }

            var active = session.ActiveEntityId == row.EntityId;
            var chip = UiFactory.Panel(
                _list,
                entity.Name,
                active ? new Color(0.42f, 0.34f, 0.12f, 0.95f) : new Color(0.16f, 0.16f, 0.20f, 0.9f)
            );
            var layout = chip.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 28f;
            layout.preferredHeight = 28f;
            var color = row.HasActed && !active ? UiFactory.MutedColor : UiFactory.TextColor;
            if (row.IsRush)
            {
                color = UiFactory.ActiveColor;
            }

            var label = UiFactory.Label(
                chip.transform,
                "Label",
                $"{entity.Name}  SPD {row.Speed}",
                13,
                TextAnchor.MiddleLeft,
                color
            );
            UiFactory.Anchor(label.rectTransform, Vector2.zero, Vector2.one);
            label.rectTransform.offsetMin = new Vector2(6f, 0f);
        }
    }

    static void Clear(Transform parent)
    {
        for (var i = parent.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }
}
