using UnityEngine;

/// Ninja frame folders under Assets/Sprites/Ninja.
public static class NinjaSpriteLibrary
{
    public const string ClassName = "Ninja";

    public static Sprite[] Idle { get; private set; }
    public static Sprite[] Run { get; private set; }
    public static Sprite[] Attack1 { get; private set; }
    public static Sprite[] Attack2 { get; private set; }
    public static Sprite[] Hurt { get; private set; }
    public static Sprite[] Dying { get; private set; }

    public static bool Ready =>
        Idle != null
        && Idle.Length > 0
        && Run != null
        && Run.Length > 0
        && Attack1 != null
        && Attack1.Length > 0
        && Attack2 != null
        && Attack2.Length > 0
        && Hurt != null
        && Hurt.Length > 0
        && Dying != null
        && Dying.Length > 0;

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
    }

    public static void EnsureLoaded()
    {
        if (Ready)
        {
            return;
        }

        Idle = SpriteFrameLoader.LoadFolder("Sprites/Ninja/Idle", FilterMode.Point);
        Run = SpriteFrameLoader.LoadFolder("Sprites/Ninja/Run", FilterMode.Point);
        Attack1 = SpriteFrameLoader.LoadFolder("Sprites/Ninja/Attack_1", FilterMode.Point);
        Attack2 = SpriteFrameLoader.LoadFolder("Sprites/Ninja/Attack_2", FilterMode.Point);
        Hurt = SpriteFrameLoader.LoadFolder("Sprites/Ninja/Hurt", FilterMode.Point);
        Dying = SpriteFrameLoader.LoadFolder("Sprites/Ninja/Dying", FilterMode.Point);
    }

    /// Vertical Cut / Quick Cut → Attack_1; Spear / Overthrow → Attack_2.
    public static Sprite[] AttackClipFor(string actionName)
    {
        switch (actionName)
        {
            case "Spear":
            case "Overthrow":
                return Attack2;
            case "Vertical Cut":
            default:
                return Attack1;
        }
    }

    public static bool TryHitEffect(string actionName, out HitEffectKind kind)
    {
        switch (actionName)
        {
            case "Spear":
                kind = HitEffectKind.Impact;
                return true;
            case "Overthrow":
                kind = HitEffectKind.Explosion2;
                return true;
            case "Vertical Cut":
                kind = HitEffectKind.BigHit;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}
