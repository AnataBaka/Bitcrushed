using System;
using UnityEngine;

/// Shared lookup for class sprite packs. Knight, Ninja, Archer, Mage, and the
/// four battlefield enemy packs are animated from their sprite folders.
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
        MageSpriteLibrary.EnsureLoaded();
        EnemySpriteLibrary.EnsureLoaded();
    }

    public static string CanonicalClass(string className)
    {
        switch (className)
        {
            case "Warrior":
                return KnightSpriteLibrary.ClassName;
            case "Rogue":
                return NinjaSpriteLibrary.ClassName;
            case "Witch Doctor":
                return "Witch_Doctor";
            default:
                return className;
        }
    }

    public static bool HasSprites(string className)
    {
        var canonical = CanonicalClass(className);
        return canonical == KnightSpriteLibrary.ClassName && KnightSpriteLibrary.Ready
            || canonical == NinjaSpriteLibrary.ClassName && NinjaSpriteLibrary.Ready
            || canonical == ArcherSpriteLibrary.ClassName && ArcherSpriteLibrary.Ready
            || canonical == MageSpriteLibrary.ClassName && MageSpriteLibrary.Ready
            || EnemySpriteLibrary.IsKind(canonical) && EnemySpriteLibrary.Ready;
    }

    public static bool IsRanged(string className)
    {
        var canonical = CanonicalClass(className);
        return canonical == ArcherSpriteLibrary.ClassName
            || canonical == MageSpriteLibrary.ClassName;
    }

    public static bool IsMage(string className) =>
        CanonicalClass(className) == MageSpriteLibrary.ClassName;

    public static bool IsEnemySprite(string className) =>
        EnemySpriteLibrary.IsKind(CanonicalClass(className));

    /// Stable visual pack for an enemy. Existing Goblin/Troll/etc. rows hash to
    /// one of the four ripped kinds so old battles still get sprites.
    public static string SpriteClassFor(string className, ulong entityId, bool isEnemy)
    {
        var canonical = CanonicalClass(className);
        if (!isEnemy)
        {
            return canonical;
        }

        if (EnemySpriteLibrary.IsKind(canonical))
        {
            return EnemySpriteLibrary.CanonicalKind(canonical);
        }

        return EnemySpriteLibrary.PickKind(entityId);
    }

    public static string EnemyDisplayName(
        string className,
        string entityName,
        ulong entityId,
        string variantPrefix = null
    )
    {
        var kind = SpriteClassFor(className, entityId, true);
        var visual = EnemySpriteLibrary.DisplayName(kind);
        if (
            !string.IsNullOrEmpty(entityName)
            && entityName.IndexOf(visual, StringComparison.OrdinalIgnoreCase) >= 0
        )
        {
            return entityName;
        }

        if (!string.IsNullOrEmpty(variantPrefix))
        {
            return $"{variantPrefix} {visual}";
        }

        return visual;
    }

    /// Knight_1, Ninja, Archer, Mage, and enemy packs already face the party.
    /// Do not flip.
    public static bool FlipX(string className) => false;

    public static float TravelSpeed(string className)
    {
        var canonical = CanonicalClass(className);
        if (canonical == NinjaSpriteLibrary.ClassName)
        {
            return 11000f;
        }

        if (canonical == ArcherSpriteLibrary.ClassName || canonical == MageSpriteLibrary.ClassName)
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

        if (
            canonical == ArcherSpriteLibrary.ClassName
            || canonical == MageSpriteLibrary.ClassName
        )
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

        if (
            CanonicalClass(className) == ArcherSpriteLibrary.ClassName
            || CanonicalClass(className) == MageSpriteLibrary.ClassName
        )
        {
            return 0.42f;
        }

        return 0.38f;
    }

    public static float RunFpsFor(string className) =>
        CanonicalClass(className) == NinjaSpriteLibrary.ClassName ? 20f : RunFps;

    public static float AttackFpsFor(string className)
    {
        var canonical = CanonicalClass(className);
        if (canonical == ArcherSpriteLibrary.ClassName || canonical == MageSpriteLibrary.ClassName)
        {
            return 14f;
        }

        return AttackFps;
    }

    /// Bow-release / bolt-release point inside the attack clip.
    public static float AttackReleaseNormalized(
        string className,
        string actionName = null,
        int magicBulletStage = 0,
        bool casterAlive = true
    )
    {
        if (CanonicalClass(className) == MageSpriteLibrary.ClassName)
        {
            return MageSpriteLibrary.UsesLargeCharge(actionName, magicBulletStage, casterAlive)
                ? 0.70f
                : 0.62f;
        }

        return IsRanged(className) ? 0.62f : 0.55f;
    }

    public static Sprite[] Idle(string className)
    {
        var canonical = CanonicalClass(className);
        if (EnemySpriteLibrary.IsKind(canonical))
        {
            return EnemySpriteLibrary.IdleFor(canonical);
        }

        if (canonical == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.Idle;
        }

        if (canonical == ArcherSpriteLibrary.ClassName)
        {
            return ArcherSpriteLibrary.Idle;
        }

        if (canonical == MageSpriteLibrary.ClassName)
        {
            return MageSpriteLibrary.Idle;
        }

        return KnightSpriteLibrary.Idle;
    }

    public static Sprite[] Run(string className)
    {
        var canonical = CanonicalClass(className);
        if (EnemySpriteLibrary.IsKind(canonical))
        {
            return Array.Empty<Sprite>();
        }

        if (canonical == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.Run;
        }

        if (canonical == ArcherSpriteLibrary.ClassName)
        {
            return ArcherSpriteLibrary.Run;
        }

        if (canonical == MageSpriteLibrary.ClassName)
        {
            return MageSpriteLibrary.Run;
        }

        return KnightSpriteLibrary.Run;
    }

    public static Sprite[] Hurt(string className)
    {
        var canonical = CanonicalClass(className);
        if (EnemySpriteLibrary.IsKind(canonical))
        {
            return Array.Empty<Sprite>();
        }

        if (canonical == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.Hurt;
        }

        if (canonical == ArcherSpriteLibrary.ClassName)
        {
            return ArcherSpriteLibrary.Hurt;
        }

        if (canonical == MageSpriteLibrary.ClassName)
        {
            return MageSpriteLibrary.Hurt;
        }

        return KnightSpriteLibrary.Hurt;
    }

    public static Sprite[] Dying(string className)
    {
        var canonical = CanonicalClass(className);
        if (EnemySpriteLibrary.IsKind(canonical))
        {
            return Array.Empty<Sprite>();
        }

        if (canonical == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.Dying;
        }

        if (canonical == ArcherSpriteLibrary.ClassName)
        {
            return ArcherSpriteLibrary.Dying;
        }

        if (canonical == MageSpriteLibrary.ClassName)
        {
            return MageSpriteLibrary.Dying;
        }

        return KnightSpriteLibrary.Dying;
    }

    public static Sprite[] AttackClipFor(
        string className,
        string actionName,
        int magicBulletStage = 0,
        bool casterAlive = true
    )
    {
        var canonical = CanonicalClass(className);
        if (EnemySpriteLibrary.IsKind(canonical))
        {
            return EnemySpriteLibrary.AttackFor(canonical);
        }

        if (canonical == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.AttackClipFor(actionName);
        }

        if (canonical == ArcherSpriteLibrary.ClassName)
        {
            return ArcherSpriteLibrary.AttackClipFor(actionName);
        }

        if (canonical == MageSpriteLibrary.ClassName)
        {
            return MageSpriteLibrary.AttackClipFor(actionName, magicBulletStage, casterAlive);
        }

        return KnightSpriteLibrary.AttackClipFor(actionName);
    }

    public static bool TryHitEffect(
        string className,
        string actionName,
        out HitEffectKind kind,
        int magicBulletStage = 0,
        bool casterAlive = true
    )
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

        if (canonical == MageSpriteLibrary.ClassName)
        {
            return MageSpriteLibrary.TryHitEffect(actionName, magicBulletStage, out kind, casterAlive);
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

    public static bool FiresProjectile(
        string className,
        string actionName,
        int magicBulletStage = 0,
        bool casterAlive = true
    )
    {
        var canonical = CanonicalClass(className);
        if (canonical == ArcherSpriteLibrary.ClassName)
        {
            return ArcherSpriteLibrary.FiresArrow(actionName);
        }

        if (canonical == MageSpriteLibrary.ClassName)
        {
            return MageSpriteLibrary.FiresCharge(actionName, magicBulletStage, casterAlive);
        }

        return false;
    }

    public static Vector2 MuzzleOffset(
        string className,
        string actionName,
        int magicBulletStage = 0,
        bool casterAlive = true
    )
    {
        if (CanonicalClass(className) == MageSpriteLibrary.ClassName)
        {
            return MageSpriteLibrary.MuzzleOffset(actionName, magicBulletStage, casterAlive);
        }

        var y = ArcherSpriteLibrary.UsesCrouchShot(actionName) ? 0.04f : 0.15f;
        return new Vector2(0.12f, y);
    }

    public static bool IsGrandUndertakingHit(string message) =>
        !string.IsNullOrEmpty(message)
        && message.IndexOf("Grand Undertaking hits", StringComparison.Ordinal) >= 0;

    public static bool IsMagicBulletVii(
        string message,
        int damage,
        bool casterKnown,
        bool casterAlive,
        int magicBulletStage
    )
    {
        if (ActionNameFromLog(message) != "Magic Bullet")
        {
            return false;
        }

        if (damage >= 90)
        {
            return true;
        }

        return casterKnown && !casterAlive && magicBulletStage >= 7;
    }

    /// Pulls the skill / basic-attack name out of a battle-log strike line.
    public static string ActionNameFromLog(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return null;
        }

        if (IsGrandUndertakingHit(message))
        {
            return "Grand Undertaking";
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
