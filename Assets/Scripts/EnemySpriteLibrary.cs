using UnityEngine;

/// Idle / Attack packs for battlefield enemies. Folders match
/// Assets/Sprites/{Witch_Doctor,Canine,Slime,Skeleton}.
public static class EnemySpriteLibrary
{
    public static readonly string[] Kinds =
    {
        "Witch_Doctor",
        "Canine",
        "Slime",
        "Skeleton",
    };

    static Sprite[] _witchIdle;
    static Sprite[] _witchAttack;
    static Sprite[] _canineIdle;
    static Sprite[] _canineAttack;
    static Sprite[] _slimeIdle;
    static Sprite[] _slimeAttack;
    static Sprite[] _skeletonIdle;
    static Sprite[] _skeletonAttack;

    public static bool Ready =>
        Len(_witchIdle) > 0
        && Len(_canineIdle) > 0
        && Len(_slimeIdle) > 0
        && Len(_skeletonIdle) > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        _witchIdle = null;
        _witchAttack = null;
        _canineIdle = null;
        _canineAttack = null;
        _slimeIdle = null;
        _slimeAttack = null;
        _skeletonIdle = null;
        _skeletonAttack = null;
    }

    public static void EnsureLoaded()
    {
        if (Ready)
        {
            return;
        }

        _witchIdle = LoadPacked("Sprites/Witch_Doctor/Idle");
        _witchAttack = LoadPacked("Sprites/Witch_Doctor/Attack");
        _canineIdle = LoadPacked("Sprites/Canine/Idle");
        _canineAttack = LoadPacked("Sprites/Canine/Attack");
        _slimeIdle = LoadPacked("Sprites/Slime/Idle");
        _slimeAttack = LoadPacked("Sprites/Slime/Attack");
        _skeletonIdle = LoadPacked("Sprites/Skeleton/Idle");
        _skeletonAttack = LoadPacked("Sprites/Skeleton/Attack");

        if (!Ready)
        {
            Debug.LogError(
                $"Enemy sprites incomplete. wd={Len(_witchIdle)} canine={Len(_canineIdle)} slime={Len(_slimeIdle)} skel={Len(_skeletonIdle)}"
            );
        }
    }

    public static string CanonicalKind(string className)
    {
        switch (className)
        {
            case "Witch Doctor":
            case "Witch_Doctor":
                return "Witch_Doctor";
            case "Canine":
                return "Canine";
            case "Slime":
                return "Slime";
            case "Skeleton":
                return "Skeleton";
            default:
                return className;
        }
    }

    public static bool IsKind(string className)
    {
        if (string.IsNullOrEmpty(className))
        {
            return false;
        }

        switch (CanonicalKind(className))
        {
            case "Witch_Doctor":
            case "Canine":
            case "Slime":
            case "Skeleton":
                return true;
            default:
                return false;
        }
    }

    public static string DisplayName(string kind)
    {
        return CanonicalKind(kind) == "Witch_Doctor" ? "Witch Doctor" : CanonicalKind(kind);
    }

    public static string PickKind(ulong entityId) => Kinds[entityId % (ulong)Kinds.Length];

    public static Sprite[] IdleFor(string kind)
    {
        switch (CanonicalKind(kind))
        {
            case "Witch_Doctor":
                return _witchIdle;
            case "Canine":
                return _canineIdle;
            case "Slime":
                return _slimeIdle;
            case "Skeleton":
                return _skeletonIdle;
            default:
                return _witchIdle;
        }
    }

    public static Sprite[] AttackFor(string kind)
    {
        switch (CanonicalKind(kind))
        {
            case "Witch_Doctor":
                return _witchAttack;
            case "Canine":
                return _canineAttack;
            case "Slime":
                return _slimeAttack;
            case "Skeleton":
                return _skeletonAttack;
            default:
                return _witchAttack;
        }
    }

    static Sprite[] LoadPacked(string folder) =>
        SpriteFrameLoader.PadToSquare(
            SpriteFrameLoader.ForceFullRect(
                SpriteFrameLoader.LoadFolder(folder, FilterMode.Point)
            ),
            KnightSpriteLibrary.CanvasSize
        );

    static int Len(Sprite[] frames) => frames == null ? 0 : frames.Length;
}
