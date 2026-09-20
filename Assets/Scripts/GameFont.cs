using UnityEngine;
using UnityEngine.UI;

/// Single runtime font. Every uGUI Text is assigned this font at creation.
///
/// Sizes are uGUI points at the 1920x1080 canvas reference (Scale With Screen
/// Size, match 0.5). boldspixels' native cell is 8px; Snap keeps every size on
/// that grid so the raster stays sharp.
///
/// Resolve = snap(max(MinReadableSize, size + SizeBump)). SizeBump is +2
/// (~12.5% at 16pt); nearest 8px of 18 is 16, so roles already on the 16pt
/// grid stay put. Roles below MinReadableSize (8pt bars, slot titles, status,
/// inventory names) rise to 16.
///
/// Role            old    new
/// bar numbers       8     16
/// equip slot title  8     16
/// inventory names   8     16
/// dodge/status      8     16
/// name plates      16     16
/// BURN/BOSS/ACTIVE 16     16
/// READY            16     16
/// turn sidebar     16     16
/// log title        16     16
/// log body         16     24  (LogSize, not Resolve)
/// BAG              16     16
/// equipment names  16     16
/// action/tooltip   16     16
/// stat popup       16     16
/// biome label      16     16
/// default buttons  24     24
/// stage label      24     24
/// overlay sub      24     24
/// Esc menu         24     24
/// overlay title    64/96  64/96
/// LEVEL UP         72     72
/// damage numbers   16-40  16-40
public static class GameFont
{
    const string ResourceName = "boldspixels";
    static Font _font;

    public const int Pixel = 8;
    public const int SizeBump = 2;
    public const int MinReadableSize = 16;
    /// Battle-log body. Old 16pt + 8, inside the requested +5 to +10 range
    /// and on the 8px grid. Kept out of Resolve so a later SizeBump cannot
    /// stack this to 32.
    public const int LogSize = 24;

    public static int Snap(int size)
    {
        if (size <= 0)
        {
            return Pixel;
        }

        return Mathf.Max(Pixel, Mathf.RoundToInt(size / (float)Pixel) * Pixel);
    }

    public static int Resolve(int size)
    {
        return Snap(Mathf.Max(MinReadableSize, size + SizeBump));
    }

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
