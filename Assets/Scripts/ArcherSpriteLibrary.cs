using UnityEngine;

/// Archer frame folders under Assets/Sprites/Archer.
public static class ArcherSpriteLibrary
{
    public const string ClassName = "Archer";

    public static Sprite[] Idle { get; private set; }
    public static Sprite[] Run { get; private set; }
    public static Sprite[] Attack1 { get; private set; }
    public static Sprite[] Attack2 { get; private set; }
    public static Sprite[] Hurt { get; private set; }
    public static Sprite[] Dying { get; private set; }
    public static Sprite Arrow { get; private set; }

    public static bool Ready => Idle != null && Idle.Length > 0 && Run != null && Run.Length > 0;

    public static bool Matches(string className) => Ready && className == ClassName;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Idle = null;
        Run = null;
        Attack1 = null;
        Attack2 = null;
        Hurt = null;
        Dying = null;
        Arrow = null;
    }

    public static void EnsureLoaded()
    {
        if (Ready)
        {
            return;
        }

        Idle = LoadPacked("Sprites/Archer/Idle");
        Run = LoadPacked("Sprites/Archer/Run");
        Attack1 = LoadPacked("Sprites/Archer/Attack_1");
        Attack2 = LoadPacked("Sprites/Archer/Attack_2");
        Hurt = LoadPacked("Sprites/Archer/Hurt");
        Dying = LoadPacked("Sprites/Archer/Dying");

        var arrows = SpriteFrameLoader.LoadFolder("Sprites/Archer/Arrow", FilterMode.Point);
        Arrow = arrows != null && arrows.Length > 0 ? arrows[0] : null;

        if (!Ready)
        {
            Debug.LogError(
                $"Archer sprites incomplete. idle={Len(Idle)} run={Len(Run)} a1={Len(Attack1)} a2={Len(Attack2)} hurt={Len(Hurt)} die={Len(Dying)} arrow={(Arrow != null ? 1 : 0)}"
            );
        }
    }

    static Sprite[] LoadPacked(string folder) =>
        SpriteFrameLoader.PadToSquare(
            SpriteFrameLoader.LoadFolder(folder, FilterMode.Point),
            KnightSpriteLibrary.CanvasSize
        );

    static int Len(Sprite[] frames) => frames == null ? 0 : frames.Length;

    /// Shoot / Curved Shot / Snap Shot → Attack_1; Snipe / Rain Down / Grandshot → Attack_2.
    public static Sprite[] AttackClipFor(string actionName)
    {
        switch (actionName)
        {
            case "Snipe":
            case "Rain Down":
            case "Grandshot":
            case "Grand Shot":
                return Attack2;
            default:
                return Attack1;
        }
    }

    public static bool TryHitEffect(string actionName, out HitEffectKind kind)
    {
        switch (actionName)
        {
            case "Shoot":
            case "Snap Shot":
                kind = HitEffectKind.Impact;
                return true;
            case "Rain Down":
            case "Curved Shot":
                kind = HitEffectKind.Explosion2;
                return true;
            case "Snipe":
                kind = HitEffectKind.BigHit;
                return true;
            case "Grandshot":
            case "Grand Shot":
                kind = HitEffectKind.CurvedImpact;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    public static bool FiresArrow(string actionName)
    {
        switch (actionName)
        {
            case "Shoot":
            case "Snap Shot":
            case "Snipe":
            case "Rain Down":
            case "Curved Shot":
            case "Grandshot":
            case "Grand Shot":
                return true;
            default:
                return !string.IsNullOrEmpty(actionName);
        }
    }
}
