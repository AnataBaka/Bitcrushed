using UnityEngine;

/// Knight_1 sheets sliced at runtime into 128px frames. The in-game class name
/// is Warrior; the art pack labels the same character Knight_1.
public static class WarriorSpriteLibrary
{
    public const string ClassName = "Warrior";
    public const int FrameSize = 128;

    public const float IdleFps = 8f;
    public const float WalkFps = 10f;
    public const float AttackFps = 12f;
    public const float DeadFps = 8f;

    public static Sprite[] Idle { get; private set; }
    public static Sprite[] Walk { get; private set; }
    public static Sprite[] Attack { get; private set; }
    public static Sprite[] Dead { get; private set; }

    public static bool Ready =>
        Idle != null
        && Idle.Length > 0
        && Walk != null
        && Walk.Length > 0
        && Attack != null
        && Attack.Length > 0
        && Dead != null
        && Dead.Length > 0;

    public static bool Matches(string className) => Ready && className == ClassName;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Idle = null;
        Walk = null;
        Attack = null;
        Dead = null;
    }

    public static void EnsureLoaded()
    {
        if (Ready)
        {
            return;
        }

        Idle = Slice("Sprites/Warrior/Idle");
        Walk = Slice("Sprites/Warrior/Walk");
        Attack = Slice("Sprites/Warrior/Attack1");
        Dead = Slice("Sprites/Warrior/Dead");
    }

    static Sprite[] Slice(string resourcePath)
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

        // Feet sit toward the left of each cell so the sword has room to swing
        // right, toward the enemy line.
        var pivot = new Vector2(0.28f, 0f);
        var frames = new Sprite[count];
        for (var i = 0; i < count; i++)
        {
            frames[i] = Sprite.Create(
                texture,
                new Rect(i * FrameSize, 0f, FrameSize, FrameSize),
                pivot,
                100f,
                0,
                SpriteMeshType.FullRect
            );
            frames[i].name = $"{texture.name}_{i}";
        }

        return frames;
    }
}
