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

    public static bool HasSprites(string className) =>
        className == KnightSpriteLibrary.ClassName && KnightSpriteLibrary.Ready
        || className == NinjaSpriteLibrary.ClassName && NinjaSpriteLibrary.Ready;

    /// Ninja sheets face left; players stand on the left, so they flip toward
    /// the enemy line. Knight sheets already face right.
    public static bool FlipX(string className) => className == NinjaSpriteLibrary.ClassName;

    public static Sprite[] Idle(string className)
    {
        if (className == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.Idle;
        }

        return KnightSpriteLibrary.Idle;
    }

    public static Sprite[] Run(string className)
    {
        if (className == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.Run;
        }

        return KnightSpriteLibrary.Run;
    }

    public static Sprite[] Hurt(string className)
    {
        if (className == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.Hurt;
        }

        return KnightSpriteLibrary.Hurt;
    }

    public static Sprite[] Dying(string className)
    {
        if (className == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.Dying;
        }

        return KnightSpriteLibrary.Dying;
    }

    public static Sprite[] AttackClipFor(string className, string actionName)
    {
        if (className == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.AttackClipFor(actionName);
        }

        return KnightSpriteLibrary.AttackClipFor(actionName);
    }

    public static bool TryHitEffect(string className, string actionName, out HitEffectKind kind)
    {
        if (className == NinjaSpriteLibrary.ClassName)
        {
            return NinjaSpriteLibrary.TryHitEffect(actionName, out kind);
        }

        if (className == KnightSpriteLibrary.ClassName)
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
