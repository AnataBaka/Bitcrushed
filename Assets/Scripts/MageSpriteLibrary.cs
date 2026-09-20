using UnityEngine;

/// Mage frame folders under Assets/Sprites/Mage.
public static class MageSpriteLibrary
{
    public const string ClassName = "Mage";

    public static Sprite[] Idle { get; private set; }
    public static Sprite[] Run { get; private set; }
    public static Sprite[] Attack1 { get; private set; }
    public static Sprite[] Attack2 { get; private set; }
    public static Sprite[] Charge1 { get; private set; }
    public static Sprite[] Charge2 { get; private set; }
    public static Sprite[] Hurt { get; private set; }
    public static Sprite[] Dying { get; private set; }

    public static bool Ready => Idle != null && Idle.Length > 0 && Run != null && Run.Length > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Idle = null;
        Run = null;
        Attack1 = null;
        Attack2 = null;
        Charge1 = null;
        Charge2 = null;
        Hurt = null;
        Dying = null;
    }

    public static void EnsureLoaded()
    {
        if (Ready)
        {
            return;
        }

        Idle = LoadPacked("Sprites/Mage/Idle");
        Run = LoadPacked("Sprites/Mage/Run");
        Attack1 = LoadPacked("Sprites/Mage/Attack_1");
        Attack2 = LoadPacked("Sprites/Mage/Attack_2");
        Charge1 = SpriteFrameLoader.LoadFolder("Sprites/Mage/Charge_1", FilterMode.Point);
        Charge2 = SpriteFrameLoader.LoadFolder("Sprites/Mage/Charge_2", FilterMode.Point);
        Hurt = LoadPacked("Sprites/Mage/Hurt");
        Dying = LoadPacked("Sprites/Mage/Dying");

        if (!Ready)
        {
            Debug.LogError(
                $"Mage sprites incomplete. idle={Len(Idle)} run={Len(Run)} a1={Len(Attack1)} a2={Len(Attack2)} c1={Len(Charge1)} c2={Len(Charge2)} hurt={Len(Hurt)} die={Len(Dying)}"
            );
        }
    }

    static Sprite[] LoadPacked(string folder) =>
        SpriteFrameLoader.PadToSquare(
            SpriteFrameLoader.LoadFolder(folder, FilterMode.Point),
            KnightSpriteLibrary.CanvasSize
        );

    static int Len(Sprite[] frames) => frames == null ? 0 : frames.Length;

    /// Stage actually cast, given the value stored after the skill resolves.
    public static int CastMagicBulletStage(int storedAfter)
    {
        if (storedAfter <= 1)
        {
            return 1;
        }

        if (storedAfter >= 7)
        {
            return 7;
        }

        return storedAfter - 1;
    }

    /// Charge_1 / Attack_1 for larger spells; Charge_2 / Attack_2 for quicker bolts.
    public static bool UsesLargeCharge(string actionName, int magicBulletStageAfter)
    {
        switch (actionName)
        {
            case "Fireball":
                return true;
            case "Magic Bullet":
                return CastMagicBulletStage(magicBulletStageAfter) >= 3;
            default:
                return false;
        }
    }

    public static Sprite[] AttackClipFor(string actionName, int magicBulletStageAfter) =>
        UsesLargeCharge(actionName, magicBulletStageAfter) ? Attack1 : Attack2;

    public static Sprite[] ChargeClipFor(string actionName, int magicBulletStageAfter) =>
        UsesLargeCharge(actionName, magicBulletStageAfter) ? Charge1 : Charge2;

    public static bool FiresCharge(string actionName)
    {
        switch (actionName)
        {
            case "Staff Jab":
            case "Magic Missile":
            case "Fireball":
            case "Magic Bullet":
                return true;
            default:
                return false;
        }
    }

    public static bool TryHitEffect(string actionName, int magicBulletStageAfter, out HitEffectKind kind)
    {
        switch (actionName)
        {
            case "Staff Jab":
            case "Magic Missile":
                kind = HitEffectKind.Impact;
                return true;
            case "Fireball":
                kind = HitEffectKind.BigHit;
                return true;
            case "Magic Bullet":
                kind = MagicBulletEffect(CastMagicBulletStage(magicBulletStageAfter));
                return true;
            default:
                kind = default;
                return false;
        }
    }

    static HitEffectKind MagicBulletEffect(int stage)
    {
        if (stage <= 2)
        {
            return HitEffectKind.Impact;
        }

        if (stage <= 5)
        {
            return HitEffectKind.Explosion2;
        }

        return HitEffectKind.BloodImpact;
    }

    public static float ChargeDuration(string actionName, int magicBulletStageAfter)
    {
        if (!UsesLargeCharge(actionName, magicBulletStageAfter))
        {
            return 0.14f;
        }

        var stage = CastMagicBulletStage(magicBulletStageAfter);
        if (actionName == "Magic Bullet" && stage >= 6)
        {
            return 0.18f;
        }

        return 0.22f;
    }

    public static float ChargeSize(string actionName, int magicBulletStageAfter)
    {
        if (!UsesLargeCharge(actionName, magicBulletStageAfter))
        {
            return 56f;
        }

        var stage = CastMagicBulletStage(magicBulletStageAfter);
        if (actionName == "Magic Bullet" && stage >= 6)
        {
            return 110f;
        }

        return 92f;
    }

    /// Staff tip / gathered bolt, as a fraction of the body rect.
    public static Vector2 MuzzleOffset(string actionName, int magicBulletStageAfter) =>
        UsesLargeCharge(actionName, magicBulletStageAfter)
            ? new Vector2(0.24f, 0.06f)
            : new Vector2(0.26f, 0.02f);
}
