using UnityEngine;
using UnityEngine.UI;

/// Single runtime font. Every uGUI Text is assigned this font at creation.
public static class GameFont
{
    const string ResourceName = "boldspixels";
    static Font _font;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Font.textureRebuilt -= OnTextureRebuilt;
        _font = null;
    }

    public static Font Ui
    {
        get
        {
            Ensure();
            return _font;
        }
    }

    public static void Apply(Text text)
    {
        if (text == null)
        {
            return;
        }

        Ensure();
        text.font = _font;
    }

    static void Ensure()
    {
        if (_font != null)
        {
            return;
        }

        _font = Resources.Load<Font>(ResourceName);
        if (_font == null)
        {
            Debug.LogError("Game font Resources/boldspixels is missing from the build.");
            return;
        }

        Font.textureRebuilt -= OnTextureRebuilt;
        Font.textureRebuilt += OnTextureRebuilt;
        Sharpen(_font);
    }

    static void OnTextureRebuilt(Font font)
    {
        if (font == _font)
        {
            Sharpen(font);
        }
    }

    static void Sharpen(Font font)
    {
        if (font == null || font.material == null)
        {
            return;
        }

        var texture = font.material.mainTexture;
        if (texture != null)
        {
            texture.filterMode = FilterMode.Point;
        }
    }
}
