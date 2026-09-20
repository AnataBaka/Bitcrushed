using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Bottom-centre log box. Newest line sits at the bottom and the view sticks to
/// the bottom as lines arrive.
public class BattleLogView : MonoBehaviour
{
    ScrollRect _scroll;
    Text _text;
    string _last = "";

    public static BattleLogView Create(Transform parent)
    {
        var panel = UiFactory.Panel(parent, "BattleLog", UiFactory.PanelColor);
        var view = panel.gameObject.AddComponent<BattleLogView>();

        var title = UiFactory.Label(
            panel.transform,
            "Title",
            "BATTLE LOG",
            16,
            TextAnchor.UpperLeft,
            UiFactory.MutedColor
        );
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(-24f, 22f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -8f);

        var scroll = panel.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        var viewport = UiFactory.NewRect(panel.transform, "Viewport");
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(14f, 12f);
        viewport.offsetMax = new Vector2(-14f, -32f);
        viewport.gameObject.AddComponent<RectMask2D>();

        var text = UiFactory.Label(
            viewport,
            "Content",
            "",
            GameFont.LogSize,
            TextAnchor.UpperLeft,
            UiFactory.TextColor
        );
        text.fontSize = GameFont.LogSize;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.lineSpacing = 1.125f;
        text.rectTransform.anchorMin = new Vector2(0f, 1f);
        text.rectTransform.anchorMax = new Vector2(1f, 1f);
        text.rectTransform.pivot = new Vector2(0.5f, 1f);
        text.rectTransform.offsetMin = new Vector2(0f, 0f);
        text.rectTransform.offsetMax = new Vector2(0f, 0f);

        var fitter = text.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = text.rectTransform;

        view._scroll = scroll;
        view._text = text;
        return view;
    }

    public void SetLines(IReadOnlyList<string> lines)
    {
        var joined = string.Join("\n", lines);
        if (joined == _last)
        {
            return;
        }

        _last = joined;
        _text.text = joined;

        Canvas.ForceUpdateCanvases();
        _scroll.verticalNormalizedPosition = 0f;
    }
}
