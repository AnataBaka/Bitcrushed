using UnityEngine;

/// Knight_1 sheets sliced at runtime into 128px frames. The in-game class name
/// is Warrior; the art pack labels the same character Knight_1.
///
/// CraftPix parks the knight in the lower-left of each cell so the sword has
/// room to swing. UI Images ignore sprite pivots and center the whole cell, so
/// each frame is copied onto a new texture with the idle body in the middle.
public static class WarriorSpriteLibrary
{
    public const string ClassName = "Warrior";
    public const int FrameSize = 128;

    public const float IdleFps = 8f;
    public const float RunFps = 14f;
    public const float AttackFps = 12f;
    public const float RunAttackFps = 14f;
    public const float DeadFps = 8f;

    public static Sprite[] Idle { get; private set; }
    public static Sprite[] Run { get; private set; }
    public static Sprite[] Attack { get; private set; }
    public static Sprite[] RunAttack { get; private set; }
    public static Sprite[] Dead { get; private set; }

    public static bool Ready =>
        Idle != null
        && Idle.Length > 0
        && Run != null
        && Run.Length > 0
        && Attack != null
        && Attack.Length > 0
        && RunAttack != null
        && RunAttack.Length > 0
        && Dead != null
        && Dead.Length > 0;

    public static bool Matches(string className) => Ready && className == ClassName;

    static Vector2 _bodyCenter;
    static bool _bodyCenterReady;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Idle = null;
        Run = null;
        Attack = null;
        RunAttack = null;
        Dead = null;
        _bodyCenterReady = false;
    }

    public static void EnsureLoaded()
    {
        if (Ready)
        {
            return;
        }

        Idle = Slice("Sprites/Warrior/Idle", captureBodyCenter: true);
        Run = Slice("Sprites/Warrior/Run");
        Attack = Slice("Sprites/Warrior/Attack1");
        RunAttack = Slice("Sprites/Warrior/RunAttack");
        Dead = Slice("Sprites/Warrior/Dead");
    }

    static Sprite[] Slice(string resourcePath, bool captureBodyCenter = false)
    {
        var texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            Debug.LogError($"Warrior sprite sheet missing at Resources/{resourcePath}.");
            return System.Array.Empty<Sprite>();
        }

        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        var count = texture.width / FrameSize;
        if (count <= 0)
        {
            Debug.LogError($"Warrior sprite sheet {resourcePath} is narrower than {FrameSize}px.");
            return System.Array.Empty<Sprite>();
        }

        Color32[] source;
        try
        {
            source = texture.GetPixels32();
        }
        catch (UnityException ex)
        {
            Debug.LogError($"Warrior sheet {resourcePath} is not readable: {ex.Message}");
            return System.Array.Empty<Sprite>();
        }

        if (captureBodyCenter || !_bodyCenterReady)
        {
            _bodyCenter = OpaqueCenter(source, texture.width, texture.height, 0);
            _bodyCenterReady = true;
        }

        var frames = new Sprite[count];
        for (var i = 0; i < count; i++)
        {
            frames[i] = RecenterFrame(source, texture.width, texture.height, i, texture.name);
        }

        return frames;
    }

    static Vector2 OpaqueCenter(Color32[] pixels, int sheetWidth, int sheetHeight, int frameIndex)
    {
        var x0 = frameIndex * FrameSize;
        long sumX = 0;
        long sumY = 0;
        long count = 0;

        for (var y = 0; y < FrameSize && y < sheetHeight; y++)
        {
            for (var x = 0; x < FrameSize; x++)
            {
                var px = pixels[y * sheetWidth + x0 + x];
                if (px.a < 16)
                {
                    continue;
                }

                sumX += x;
                sumY += y;
                count++;
            }
        }

        if (count == 0)
        {
            return new Vector2(FrameSize * 0.5f, FrameSize * 0.5f);
        }

        return new Vector2(sumX / (float)count, sumY / (float)count);
    }

    static Sprite RecenterFrame(
        Color32[] source,
        int sheetWidth,
        int sheetHeight,
        int frameIndex,
        string sheetName
    )
    {
        var x0 = frameIndex * FrameSize;
        var dest = new Color32[FrameSize * FrameSize];
        var shiftX = Mathf.RoundToInt((FrameSize * 0.5f) - _bodyCenter.x);
        var shiftY = Mathf.RoundToInt((FrameSize * 0.5f) - _bodyCenter.y);

        for (var y = 0; y < FrameSize && y < sheetHeight; y++)
        {
            for (var x = 0; x < FrameSize; x++)
            {
                var dx = x + shiftX;
                var dy = y + shiftY;
                if (dx < 0 || dx >= FrameSize || dy < 0 || dy >= FrameSize)
                {
                    continue;
                }

                dest[dy * FrameSize + dx] = source[y * sheetWidth + x0 + x];
            }
        }

        var tex = new Texture2D(FrameSize, FrameSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = $"{sheetName}_{frameIndex}",
        };
        tex.SetPixels32(dest);
        tex.Apply(false, false);

        var sprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, FrameSize, FrameSize),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect
        );
        sprite.name = tex.name;
        return sprite;
    }
}
