using System;
using UnityEngine;

/// Shared lookup for class sprite packs. Knight, Ninja, and Archer are
/// animated; Mage still uses a placeholder shape.
public static class ClassSpriteArt
{
    public const float IdleFps = 8f;
    public const float RunFps = 14f;
    public const float AttackFps = 12f;
    public const float HurtFps = 10f;
    public const float DeadFps = 8f;

    static readonly string[] PlayerBuffNames =
    {
        "Embolden",
        "Gallant Pride",
        "Restring",
        "Scheme",
        "Evade",
        "Concentrate",
        "Pray",
        "Grand Undertaking",
        "Focus Spirit",
        "Finish the Job",
    };

    public static void EnsureLoaded()
    {
        KnightSpriteLibrary.EnsureLoaded();
        NinjaSpriteLibrary.EnsureLoaded();
        ArcherSpriteLibrary.EnsureLoaded();
    }

    public static string CanonicalClass(string className)
    {
        switch (className)
        {
            case "Warrior":
                return KnightSpriteLibrary.ClassName;
            case "Rogue":
                return NinjaSpriteLibrary.ClassName;
            default:
                return className;
        }
    }

    public static bool HasSprites(string className)
    {
        var canonical = CanonicalClass(className);
        return canonical == KnightSpriteLibrary.ClassName && KnightSpriteLibrary.Ready
            || canonical == NinjaSpriteLibrary.ClassName && NinjaSpriteLibrary.Ready
            || canonical == ArcherSpriteLibrary.ClassName && ArcherSpriteLibrary.Ready;
    }

    public static bool IsRanged(string className) =>
        CanonicalClass(className) == ArcherSpriteLibrary.ClassName;

    /// Knight_1, Ninja, and Archer source PNGs all face right. Do not flip.
    public static bool FlipX(string className) => false;

    public static float TravelSpeed(string className)
    {
        var canonical = CanonicalClass(className);
        if (canonical == NinjaSpriteLibrary.ClassName)
        {
            return 11000f;
        }

        if (canonical == ArcherSpriteLibrary.ClassName)
        {
            return 2600f;
        }

        return 4200f;
    }

    public static float MinTravelSeconds(string className)
    {
        var canonical = CanonicalClass(className);
        if (canonical == NinjaSpriteLibrary.ClassName)
        {
            return 0.04f;
        }

        if (canonical == ArcherSpriteLibrary.ClassName)
        {
            return 0.16f;
        }

        return 0.10f;
    }

    public static float MaxTravelSeconds(string className)
    {
        if (CanonicalClass(className) == NinjaSpriteLibrary.ClassName)
        {
            return 0.16f;
        }

        if (CanonicalClass(className) == ArcherSpriteLibrary.ClassName)
        {
            return 0.42f;
        }

        return 0.38f;
    }

    public static float RunFpsFor(string className) =>
        CanonicalClass(className) == NinjaSpriteLibrary.ClassName ? 20f : RunFps;

    public static float AttackFpsFor(string className) =>
        CanonicalClass(className) == ArcherSpriteLibrary.ClassName ? 14f : AttackFps;

    /// Bow-release point inside the attack clip. Later than a melee swing so
    /// the draw reads before the arrow leaves.
    public static float AttackReleaseNormalized(string className) =>
        IsRanged(className) ? 0.62f : 0.55f;

    public static Sprite[] Idle(string className)
    {
        var canonical = CanonicalClass(className);
        if (canonical == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.Idle;
        }

        if (canonical == ArcherSpriteLibrary.ClassName)
        {
            return ArcherSpriteLibrary.Idle;
        }

        return KnightSpriteLibrary.Idle;
    }

    public static Sprite[] Run(string className)
    {
        var canonical = CanonicalClass(className);
        if (canonical == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.Run;
        }

        if (canonical == ArcherSpriteLibrary.ClassName)
        {
            return ArcherSpriteLibrary.Run;
        }

        return KnightSpriteLibrary.Run;
    }

    public static Sprite[] Hurt(string className)
    {
        var canonical = CanonicalClass(className);
        if (canonical == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.Hurt;
        }

        if (canonical == ArcherSpriteLibrary.ClassName)
        {
            return ArcherSpriteLibrary.Hurt;
        }

        return KnightSpriteLibrary.Hurt;
    }

    public static Sprite[] Dying(string className)
    {
        var canonical = CanonicalClass(className);
        if (canonical == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.Dying;
        }

        if (canonical == ArcherSpriteLibrary.ClassName)
        {
            return ArcherSpriteLibrary.Dying;
        }

        return KnightSpriteLibrary.Dying;
    }

    public static Sprite[] AttackClipFor(string className, string actionName)
    {
        var canonical = CanonicalClass(className);
        if (canonical == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.AttackClipFor(actionName);
        }

        if (canonical == ArcherSpriteLibrary.ClassName)
        {
            return ArcherSpriteLibrary.AttackClipFor(actionName);
        }

        return KnightSpriteLibrary.AttackClipFor(actionName);
    }

    public static bool TryHitEffect(string className, string actionName, out HitEffectKind kind)
    {
        var canonical = CanonicalClass(className);
        if (canonical == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.TryHitEffect(actionName, out kind);
        }

        if (canonical == KnightSpriteLibrary.ClassName)
        {
            return KnightSpriteLibrary.TryHitEffect(actionName, out kind);
        }

        if (canonical == ArcherSpriteLibrary.ClassName)
        {
            return ArcherSpriteLibrary.TryHitEffect(actionName, out kind);
        }

        kind = default;
        return false;
    }

    public static Sprite ArrowSprite(string className)
    {
        if (CanonicalClass(className) == ArcherSpriteLibrary.ClassName)
        {
            ArcherSpriteLibrary.EnsureLoaded();
            return ArcherSpriteLibrary.Arrow;
        }

        return null;
    }

    public static bool FiresProjectile(string className, string actionName) =>
        IsRanged(className) && ArcherSpriteLibrary.FiresArrow(actionName);

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

    public static bool IsPlayerBuffMessage(string message, bool isHeal, bool isFocus)
    {
        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        if (isHeal)
        {
            if (message.IndexOf("uses Pray", StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            return message.IndexOf("recovers ", StringComparison.Ordinal) >= 0
                && message.IndexOf(" HP and ", StringComparison.Ordinal) >= 0;
        }

        if (!isFocus)
        {
            return false;
        }

        if (message.IndexOf("finishes the job", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        for (var i = 0; i < PlayerBuffNames.Length; i++)
        {
            if (message.IndexOf(PlayerBuffNames[i], StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
