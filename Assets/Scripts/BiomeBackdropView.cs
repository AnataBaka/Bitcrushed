using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;

/// Full-screen biome backdrop. Colors come from BiomeDef; Unity does not pick a
/// biome, it only paints the row the session currently points at.
public class BiomeBackdropView : MonoBehaviour
{
    Image _image;
    Text _label;
    WorldBiome _applied = (WorldBiome)(-1);

    public static BiomeBackdropView Create(Transform canvas)
    {
        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 1f);
        }

        var image = UiFactory.Panel(canvas, "BiomeBackdrop", Color.white);
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = false;
        UiFactory.Anchor(image.rectTransform, Vector2.zero, Vector2.one);
        image.rectTransform.SetAsFirstSibling();

        var label = UiFactory.Label(
            image.transform,
            "BiomeName",
            "",
            18,
            TextAnchor.UpperCenter,
            new Color(0.92f, 0.93f, 0.88f, 0.72f)
        );
        label.fontStyle = FontStyle.Bold;
        UiFactory.Anchor(label.rectTransform, new Vector2(0.3f, 0.86f), new Vector2(0.7f, 0.94f));

        var view = image.gameObject.AddComponent<BiomeBackdropView>();
        view._image = image;
        view._label = label;
        return view;
    }

    public void Render(GameSession session)
    {
        transform.SetAsFirstSibling();
        if (session == null)
        {
            return;
        }

        if (_applied == session.CurrentBiome && _image.sprite != null)
        {
            return;
        }

        var def = GameManager.FindBiomeDef(session.CurrentBiome);
        var top = def != null ? Rgb(def.BackTopR, def.BackTopG, def.BackTopB) : new Color(0.10f, 0.12f, 0.16f);
        var bot = def != null ? Rgb(def.BackBotR, def.BackBotG, def.BackBotB) : new Color(0.04f, 0.05f, 0.07f);
        _image.sprite = PlaceholderArt.VerticalGradient(top, bot);
        _image.color = Color.white;
        _label.text = def != null ? def.Name : "";
        _applied = session.CurrentBiome;
    }

    static Color Rgb(int r, int g, int b) =>
        new Color(Mathf.Clamp01(r / 255f), Mathf.Clamp01(g / 255f), Mathf.Clamp01(b / 255f), 1f);
}
