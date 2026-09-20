using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// Cycles an Image through a sprite array. Used instead of Animator clips so
/// frame folders can be loaded in code without the Unity Animation window.
public class SpriteFlipbook : MonoBehaviour
{
    Image _image;
    Sprite[] _frames;
    float _secondsPerFrame = 0.125f;
    bool _loop;
    int _index;
    float _elapsed;
    bool _playing;

    public bool IsPlaying => _playing;

    void Awake()
    {
        _image = GetComponent<Image>();
    }

    public void Play(Sprite[] frames, float fps, bool loop)
    {
        if (frames == null || frames.Length == 0)
        {
            return;
        }

        if (_playing && _loop && loop && ReferenceEquals(_frames, frames))
        {
            return;
        }

        if (_image == null)
        {
            _image = GetComponent<Image>();
        }

        _frames = frames;
        _secondsPerFrame = fps <= 0f ? 0.125f : 1f / fps;
        _loop = loop;
        _index = 0;
        _elapsed = 0f;
        _playing = true;
        Apply();
    }

    public IEnumerator PlayOnce(Sprite[] frames, float fps)
    {
        Play(frames, fps, false);
        while (_playing)
        {
            yield return null;
        }
    }

    public void HoldLast()
    {
        _playing = false;
        _loop = false;
        if (_frames == null || _frames.Length == 0)
        {
            return;
        }

        _index = _frames.Length - 1;
        Apply();
    }

    void Update()
    {
        if (!_playing || _frames == null || _frames.Length == 0)
        {
            return;
        }

        _elapsed += Time.deltaTime;
        while (_elapsed >= _secondsPerFrame)
        {
            _elapsed -= _secondsPerFrame;
            if (_index + 1 >= _frames.Length)
            {
                if (_loop)
                {
                    _index = 0;
                }
                else
                {
                    _index = _frames.Length - 1;
                    _playing = false;
                    Apply();
                    return;
                }
            }
            else
            {
                _index++;
            }

            Apply();
        }
    }

    void Apply()
    {
        if (_image != null && _frames != null && _index >= 0 && _index < _frames.Length)
        {
            _image.useSpriteMesh = false;
            _image.sprite = _frames[_index];
        }
    }
}
