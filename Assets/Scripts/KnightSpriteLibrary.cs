using System;
using UnityEngine;

/// Knight_1 frame folders under Assets/Sprites. Only the Knight class uses
/// these; Mage, Archer, and Ninja keep their placeholder shapes.
public static class KnightSpriteLibrary
{
    public const string ClassName = "Knight";

    public const float IdleFps = 8f;
    public const float RunFps = 14f;
    public const float AttackFps = 12f;
    public const float DeadFps = 8f;

    public static Sprite[] Idle { get; private set; }
    public static Sprite[] Run { get; private set; }
    public static Sprite[] Attack1 { get; private set; }
    public static Sprite[] Attack2 { get; private set; }
    public static Sprite[] Attack3 { get; private set; }
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
        && Attack3 != null
        && Attack3.Length > 0;

    public static bool Matches(string className) => Ready && className == ClassName;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Idle = null;
        Run = null;
        Attack1 = null;
        Attack2 = null;
        Attack3 = null;
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
        Dying = SpriteFrameLoader.LoadFolder("Sprites/Knight_1/Dying", FilterMode.Point);
    }

    /// Pulls the skill / basic-attack name out of a battle-log strike line.
    public static string ActionNameFromLog(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return null;
        }

        const string uses = " uses ";
        const string on = " on ";
        var i = message.IndexOf(uses, StringComparison.Ordinal);
        if (i < 0)
        {
            return null;
        }

        var start = i + uses.Length;
        var j = message.IndexOf(on, start, StringComparison.Ordinal);
        if (j < 0)
        {
            return null;
        }

        return message.Substring(start, j - start);
    }

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
