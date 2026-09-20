using System;
using UnityEngine;

/// Shared lookup for class sprite packs. Knight and Ninja are animated; Mage
/// and Archer still use placeholder shapes.
public static class ClassSpriteArt
{
    public const float IdleFps = 8f;
    public const float RunFps = 14f;
    public const float AttackFps = 12f;
    public const float HurtFps = 10f;
    public const float DeadFps = 8f;

    public static void EnsureLoaded()
    {
        KnightSpriteLibrary.EnsureLoaded();
        NinjaSpriteLibrary.EnsureLoaded();
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
            || canonical == NinjaSpriteLibrary.ClassName && NinjaSpriteLibrary.Ready;
    }

    /// Knight_1 and Ninja source PNGs both face right. Do not flip either.
    public static bool FlipX(string className) => false;

    public static float TravelSpeed(string className) =>
        CanonicalClass(className) == NinjaSpriteLibrary.ClassName ? 11000f : 4200f;

    public static float MinTravelSeconds(string className) =>
        CanonicalClass(className) == NinjaSpriteLibrary.ClassName ? 0.04f : 0.10f;

    public static float MaxTravelSeconds(string className) =>
        CanonicalClass(className) == NinjaSpriteLibrary.ClassName ? 0.16f : 0.38f;

    public static float RunFpsFor(string className) =>
        CanonicalClass(className) == NinjaSpriteLibrary.ClassName ? 20f : RunFps;

    public static Sprite[] Idle(string className)
    {
        if (CanonicalClass(className) == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.Idle;
        }

        return KnightSpriteLibrary.Idle;
    }

    public static Sprite[] Run(string className)
    {
        if (CanonicalClass(className) == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.Run;
        }

        return KnightSpriteLibrary.Run;
    }

    public static Sprite[] Hurt(string className)
    {
        if (CanonicalClass(className) == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.Hurt;
        }

        return KnightSpriteLibrary.Hurt;
    }

    public static Sprite[] Dying(string className)
    {
        if (CanonicalClass(className) == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.Dying;
        }

        return KnightSpriteLibrary.Dying;
    }

    public static Sprite[] AttackClipFor(string className, string actionName)
    {
        if (CanonicalClass(className) == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.AttackClipFor(actionName);
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

        kind = default;
        return false;
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
}
