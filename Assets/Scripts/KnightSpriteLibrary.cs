using UnityEngine;

/// Knight_1 frame folders under Assets/Sprites.
public static class KnightSpriteLibrary
{
    public const string ClassName = "Knight";
    public const int CanvasSize = 128;

    public static Sprite[] Idle { get; private set; }
    public static Sprite[] Run { get; private set; }
    public static Sprite[] Attack1 { get; private set; }
    public static Sprite[] Attack2 { get; private set; }
    public static Sprite[] Attack3 { get; private set; }
    public static Sprite[] Hurt { get; private set; }
    public static Sprite[] Dying { get; private set; }

    public static bool Ready => Idle != null && Idle.Length > 0 && Run != null && Run.Length > 0;

    public static bool Matches(string className) => Ready && className == ClassName;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Idle = null;
        Run = null;
        Attack1 = null;
        Attack2 = null;
        Attack3 = null;
        Hurt = null;
        Dying = null;
    }

    public static void EnsureLoaded()
    {
        if (Ready)
        {
            return;
        }

        Idle = SpriteFrameLoader.LoadFolder("Sprites/Knight_1/Idle", FilterMode.Point);
        Run = SpriteFrameLoader.LoadFolder("Sprites/Knight_1/Run", FilterMode.Point);
        Attack1 = SpriteFrameLoader.LoadFolder("Sprites/Knight_1/Attack_1", FilterMode.Point);
        Attack2 = SpriteFrameLoader.LoadFolder("Sprites/Knight_1/Attack_2", FilterMode.Point);
        Attack3 = SpriteFrameLoader.LoadFolder("Sprites/Knight_1/Attack_3", FilterMode.Point);
        Hurt = SpriteFrameLoader.LoadFolder("Sprites/Knight_1/Hurt", FilterMode.Point);
        Dying = SpriteFrameLoader.LoadFolder("Sprites/Knight_1/Dying", FilterMode.Point);

        if (!Ready)
        {
            Debug.LogError(
                $"Knight sprites incomplete. idle={Len(Idle)} run={Len(Run)} a1={Len(Attack1)} a2={Len(Attack2)} a3={Len(Attack3)} hurt={Len(Hurt)} die={Len(Dying)}"
            );
        }
    }

    static int Len(Sprite[] frames) => frames == null ? 0 : frames.Length;

    /// Bash / Rush / Sword Swing → Attack_1; Cleave / Triple Slash / Furioso →
    /// Attack_2; Bludgeon → Attack_3.
    public static Sprite[] AttackClipFor(string actionName)
    {
        switch (actionName)
        {
            case "Cleave":
            case "Triple Slash":
            case "Furioso":
                return Attack2;
            case "Bludgeon":
                return Attack3;
            default:
                return Attack1;
        }
    }

    public static bool TryHitEffect(string actionName, out HitEffectKind kind)
    {
        switch (actionName)
        {
            case "Bash":
            case "Rush":
            case "Sword Swing":
                kind = HitEffectKind.Impact;
                return true;
            case "Cleave":
                kind = HitEffectKind.Explosion2;
                return true;
            case "Bludgeon":
            case "Triple Slash":
                kind = HitEffectKind.BigHit;
                return true;
            case "Furioso":
                kind = HitEffectKind.BloodImpact;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}
