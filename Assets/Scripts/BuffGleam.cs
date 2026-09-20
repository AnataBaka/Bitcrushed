using UnityEngine;
using UnityEngine.UI;

/// Holy-white silhouette gleam plus a scrolling shine clipped to the player
/// sprite. Restarts on every buff and does not block combat.
public class BuffGleam : MonoBehaviour
{
    public const float Duration = 1.5f;

    Image _source;
    Image _tint;
    Image _shine;
    Mask _mask;
    float _until;
    float _started;

    public static void Play(Image shape, float seconds = Duration)
    {
        if (shape == null)
        {
            return;
        }

        var gleam = shape.GetComponent<BuffGleam>();
        if (gleam == null)
        {
            gleam = shape.gameObject.AddComponent<BuffGleam>();
        }

        gleam.Begin(seconds);
    }

    void Begin(float seconds)
    {
        _source = GetComponent<Image>();
        if (_source == null)
        {
            return;
        }

        EnsureOverlay();
        ShowOverlay(true);
        _started = Time.unscaledTime;
        _until = _started + Mathf.Max(0.1f, seconds);
        enabled = true;
    }

    void EnsureOverlay()
    {
        if (_mask == null)
        {
            _mask = GetComponent<Mask>();
            if (_mask == null)
            {
                _mask = gameObject.AddComponent<Mask>();
            }

            _mask.showMaskGraphic = true;
        }

        if (_tint == null)
        {
            _tint = CreateLayer("BuffGleamTint");
            _tint.preserveAspect = true;
        }

        if (_shine == null)
        {
            _shine = CreateLayer("BuffGleamShine");
            _shine.sprite = PlaceholderArt.FlatWhite();
            _shine.preserveAspect = false;
            _shine.type = Image.Type.Simple;
        }
    }

    Image CreateLayer(string name)
    {
        var existing = transform.Find(name);
        if (existing != null)
        {
            var found = existing.GetComponent<Image>();
            if (found != null)
            {
                found.gameObject.SetActive(true);
                return found;
            }
        }

        var image = UiFactory.Graphic(transform, name, PlaceholderArt.FlatWhite(), Color.white);
        var rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        image.raycastTarget = false;
        return image;
    }

    void ShowOverlay(bool visible)
    {
        if (_tint != null)
        {
            _tint.gameObject.SetActive(visible);
        }

        if (_shine != null)
        {
            _shine.gameObject.SetActive(visible);
        }

        if (_mask != null)
        {
            _mask.enabled = visible;
        }
    }

    void LateUpdate()
    {
        if (_source == null || Time.unscaledTime >= _until)
        {
            Stop();
            return;
        }

        var t = Time.unscaledTime - _started;
        var fade = Mathf.Clamp01((_until - Time.unscaledTime) / 0.3f);
        var pulse = 0.5f + (0.5f * Mathf.Sin(t * 9.5f));

        var ivory = new Color(1f, 0.98f, 0.90f, 1f);
        var tint = Color.Lerp(Color.white, ivory, pulse * 0.45f);
        tint.a = (0.62f + (0.22f * pulse)) * fade;

        if (_tint != null)
        {
            _tint.sprite = _source.sprite;
            _tint.preserveAspect = _source.preserveAspect;
            _tint.color = tint;
            _tint.rectTransform.offsetMin = Vector2.zero;
            _tint.rectTransform.offsetMax = Vector2.zero;
        }

        if (_shine != null)
        {
            var width = Mathf.Max(18f, _source.rectTransform.rect.width * 0.28f);
            var height = _source.rectTransform.rect.height * 1.6f;
            _shine.rectTransform.sizeDelta = new Vector2(width, height);
            _shine.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _shine.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _shine.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            var span = _source.rectTransform.rect.width + width;
            var travel = Mathf.Repeat(t * 1.35f, 1f);
            var x = Mathf.Lerp(-span * 0.55f, span * 0.55f, travel);
            _shine.rectTransform.anchoredPosition = new Vector2(x, 0f);
            _shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 24f);
            _shine.color = new Color(1f, 1f, 1f, (0.42f + (0.22f * pulse)) * fade);
        }
    }

    void Stop()
    {
        ShowOverlay(false);
        enabled = false;
    }

    void OnDestroy()
    {
        if (_tint != null)
        {
            Destroy(_tint.gameObject);
        }

        if (_shine != null)
        {
            Destroy(_shine.gameObject);
        }
    }
}
