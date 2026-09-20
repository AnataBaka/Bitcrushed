using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;

/// Biome photo (or generated placeholder) behind sprites and UI. Unity does not
/// pick a biome; it paints the row the session currently points at.
public class BiomeBackdropView : MonoBehaviour
{
    /// Pixels the photo tucks behind the log so rounding cannot leave a seam.
    public const float BackdropBottomOverlap = 2f;

    Image _fill;
    RectTransform _mask;
    Image _photo;
    Text _label;
    RectTransform _logRect;
    WorldBiome _applied = (WorldBiome)(-1);
    bool _usingPhoto;
    Vector2 _lastMaskSize;
    Vector2 _lastLogTop;
    int _fitVersion;
    bool _fitting;
    readonly Vector3[] _corners = new Vector3[4];

    public int FitVersion => _fitVersion;
    public bool HasPhoto => _usingPhoto && _photo != null && _photo.sprite != null;
    public RectTransform PhotoRect => _photo != null ? _photo.rectTransform : null;

    public static BiomeBackdropView Create(Transform canvas)
    {
        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 1f);
        }

        BiomeBackdropArt.PreloadAll();

        var fill = UiFactory.Panel(canvas, "BiomeBackdrop", new Color(0.04f, 0.05f, 0.08f, 1f));
        fill.type = Image.Type.Simple;
        fill.raycastTarget = false;
        UiFactory.Anchor(fill.rectTransform, Vector2.zero, Vector2.one);
        fill.rectTransform.SetAsFirstSibling();

        var maskRt = UiFactory.NewRect(fill.transform, "Mask");
        UiFactory.Anchor(maskRt, Vector2.zero, Vector2.one);
        maskRt.gameObject.AddComponent<RectMask2D>();

        var photo = UiFactory.Panel(maskRt, "Photo", Color.white);
        photo.type = Image.Type.Simple;
        photo.preserveAspect = false;
        photo.raycastTarget = false;
        photo.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        photo.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        photo.rectTransform.pivot = new Vector2(0.5f, 0f);
        photo.rectTransform.anchoredPosition = Vector2.zero;

        var label = UiFactory.Label(
            maskRt,
            "BiomeName",
            "",
            18,
            TextAnchor.UpperCenter,
            new Color(0.92f, 0.93f, 0.88f, 0.72f)
        );
        label.fontStyle = FontStyle.Bold;
        UiFactory.Anchor(label.rectTransform, new Vector2(0.3f, 0.86f), new Vector2(0.7f, 0.94f));

        var view = fill.gameObject.AddComponent<BiomeBackdropView>();
        view._fill = fill;
        view._mask = maskRt;
        view._photo = photo;
        view._label = label;
        return view;
    }

    public void BindTarget(RectTransform logRect)
    {
        _logRect = logRect;
        Fit();
    }

    public void Render(GameSession session)
    {
        transform.SetAsFirstSibling();
        if (session == null)
        {
            return;
        }

        if (_applied == session.CurrentBiome && _photo.sprite != null)
        {
            return;
        }

        var biome = session.CurrentBiome;
        var def = GameManager.FindBiomeDef(biome);
        var photo = BiomeBackdropArt.SpriteFor(biome);
        if (photo != null)
        {
            _usingPhoto = true;
            _photo.sprite = photo;
            _photo.color = Color.white;
        }
        else
        {
            _usingPhoto = false;
            var top = def != null ? Rgb(def.BackTopR, def.BackTopG, def.BackTopB) : new Color(0.10f, 0.12f, 0.16f);
            var bot = def != null ? Rgb(def.BackBotR, def.BackBotG, def.BackBotB) : new Color(0.04f, 0.05f, 0.07f);
            _photo.sprite = PlaceholderArt.VerticalGradient(top, bot);
            _photo.color = Color.white;
        }

        _label.text = def != null ? def.Name : "";
        _applied = biome;
        Fit();
    }

    /// Maps a bottom-left image UV through the fitted photo into field anchors
    /// (entity pivot is bottom-center relative to the field's bottom-left).
    public bool TryMapImageUv(Vector2 imageUv, RectTransform field, out Vector2 anchored)
    {
        anchored = Vector2.zero;
        if (_photo == null || field == null)
        {
            return false;
        }

        var photo = _photo.rectTransform;
        var local = new Vector3(
            (imageUv.x - photo.pivot.x) * photo.rect.width,
            (imageUv.y - photo.pivot.y) * photo.rect.height,
            0f
        );
        var world = photo.TransformPoint(local);
        var fieldLocal = field.InverseTransformPoint(world);
        anchored = new Vector2(
            fieldLocal.x + field.pivot.x * field.rect.width,
            fieldLocal.y + field.pivot.y * field.rect.height
        );
        return true;
    }

    public void EnsureFit() => FitIfStale();

    void LateUpdate() => FitIfStale();

    void OnRectTransformDimensionsChange() => Fit();

    void FitIfStale()
    {
        if (_mask == null)
        {
            return;
        }

        var logTop = LogTopInParent();
        if (_mask.rect.size == _lastMaskSize && logTop == _lastLogTop)
        {
            return;
        }

        Fit();
    }

    void Fit()
    {
        if (_fitting || _fill == null || _mask == null || _photo == null)
        {
            return;
        }

        _fitting = true;
        SizeMaskToLog();
        Canvas.ForceUpdateCanvases();
        FitPhotoToMask();
        _lastMaskSize = _mask.rect.size;
        _lastLogTop = LogTopInParent();
        _fitVersion++;
        _fitting = false;
    }

    void SizeMaskToLog()
    {
        _mask.anchorMin = Vector2.zero;
        _mask.anchorMax = Vector2.one;
        _mask.offsetMax = Vector2.zero;

        var fromBottom = LogTopInParent().y - BackdropBottomOverlap;
        if (fromBottom < 0f)
        {
            fromBottom = 0f;
        }

        _mask.offsetMin = new Vector2(0f, fromBottom);
    }

    Vector2 LogTopInParent()
    {
        if (_logRect == null)
        {
            return Vector2.zero;
        }

        _logRect.GetWorldCorners(_corners);
        var local = _fill.rectTransform.InverseTransformPoint(_corners[1]);
        var parent = _fill.rectTransform.rect;
        return new Vector2(
            local.x + _fill.rectTransform.pivot.x * parent.width,
            local.y + _fill.rectTransform.pivot.y * parent.height
        );
    }

    void FitPhotoToMask()
    {
        var area = _mask.rect.size;
        if (area.x <= 1f || area.y <= 1f)
        {
            return;
        }

        if (!_usingPhoto || _photo.sprite == null)
        {
            _photo.rectTransform.anchorMin = Vector2.zero;
            _photo.rectTransform.anchorMax = Vector2.one;
            _photo.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _photo.rectTransform.offsetMin = Vector2.zero;
            _photo.rectTransform.offsetMax = Vector2.zero;
            _photo.rectTransform.localScale = Vector3.one;
            _photo.preserveAspect = false;
            return;
        }

        var tex = _photo.sprite.rect.size;
        if (tex.x <= 0f || tex.y <= 0f)
        {
            return;
        }

        var scale = Mathf.Max(area.x / tex.x, area.y / tex.y);
        var photoRt = _photo.rectTransform;
        photoRt.anchorMin = new Vector2(0.5f, 0f);
        photoRt.anchorMax = new Vector2(0.5f, 0f);
        photoRt.pivot = new Vector2(0.5f, 0f);
        photoRt.localScale = Vector3.one;
        photoRt.sizeDelta = tex * scale;
        photoRt.anchoredPosition = Vector2.zero;
        _photo.preserveAspect = false;
        _photo.type = Image.Type.Simple;
    }

    static Color Rgb(int r, int g, int b) =>
        new Color(Mathf.Clamp01(r / 255f), Mathf.Clamp01(g / 255f), Mathf.Clamp01(b / 255f), 1f);
}
