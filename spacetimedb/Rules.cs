using SpacetimeDB;

/// Combat math ported from Anthony's module. Tower/floor scaling is omitted.
public static partial class Module
{
    public const uint SessionId = 1;
    public const uint MaxPartySize = 3;
    public const uint EnemyCount = 3;
    public const uint StatCap = 99;
    public const uint MaxDodgePercent = 25;
    public const uint MpRegenPerTurn = 2;
    public const uint FocusManaRecover = 20;
    public const uint MainStatMin = 3;
    public const uint MainStatMax = 6;
    public const uint OffStatMin = 1;
    public const uint OffStatMax = 3;
    public const uint ClashDamageBonus = 1;
    public const uint BagCapacity = 12;
    public const uint ArmorPipMin = 1;
    public const long EnemyTurnDelayMicros = 2_000_000;

    public static uint ArmorPipCap(ArmorSlot slot) => slot switch
    {
        ArmorSlot.Helmet => 5,
        ArmorSlot.Chestplate => 6,
        ArmorSlot.Leggings => 5,
        ArmorSlot.Boots => 4,
        _ => 0,
    };

    public static uint ClassMaxHealth(PlayerClass classChoice) => classChoice switch
    {
        PlayerClass.Warrior => 80,
        PlayerClass.Archer => 50,
        PlayerClass.Mage => 30,
        PlayerClass.Rogue => 60,
        _ => 50,
    };

    public static uint ClassBaseMana(PlayerClass classChoice) => classChoice switch
    {
        PlayerClass.Warrior => 40,
        PlayerClass.Archer => 50,
        PlayerClass.Mage => 100,
        PlayerClass.Rogue => 50,
        _ => 50,
    };

    public static WeaponType ClassWeapon(PlayerClass classChoice) => classChoice switch
    {
        PlayerClass.Warrior => WeaponType.Sword,
        PlayerClass.Archer => WeaponType.Bow,
        PlayerClass.Mage => WeaponType.Staff,
        PlayerClass.Rogue => WeaponType.Dagger,
        _ => WeaponType.None,
    };

    public static StatType MainStat(PlayerClass classChoice) => classChoice switch
    {
        PlayerClass.Warrior => StatType.Strength,
        PlayerClass.Archer => StatType.Dexterity,
        PlayerClass.Rogue => StatType.Speed,
        PlayerClass.Mage => StatType.Intelligence,
        _ => StatType.Strength,
    };

    public static string ClassNameOf(PlayerClass classChoice) => classChoice switch
    {
        PlayerClass.Warrior => "Warrior",
        PlayerClass.Mage => "Mage",
        PlayerClass.Rogue => "Rogue",
        PlayerClass.Archer => "Archer",
        _ => "Adventurer",
    };

    /// Warrior STR is not added here; DealtDamage already includes Strength.
    public static uint ClassDamageStat(
        PlayerClass classChoice,
        uint dexterity,
        uint intelligence,
        uint speed
    ) =>
        classChoice switch
        {
            PlayerClass.Warrior => 0,
            PlayerClass.Archer => dexterity,
            PlayerClass.Mage => intelligence,
            PlayerClass.Rogue => speed,
            _ => 0,
        };

    public static uint CharacterDamage(
        PlayerClass classChoice,
        uint skillOrWeaponDamage,
        uint dexterity,
        uint intelligence,
        uint speed
    ) => SaturatingAdd(skillOrWeaponDamage, ClassDamageStat(classChoice, dexterity, intelligence, speed));

    /// Dealt = (CHR + Strength + ATK) + floor(Strength / 10).
    public static uint DealtDamage(uint chrDamage, uint strength, uint atk)
    {
        var core = SaturatingAdd(SaturatingAdd(chrDamage, strength), atk);
        return SaturatingAdd(core, strength / 10);
    }

    public static uint EffectiveStrength(uint strength, uint strengthBuff) =>
        SaturatingAdd(strength, strengthBuff);

    public static uint IncomingAfterDefense(uint incoming, uint defense) => SaturatingSub(incoming, defense);

    public static uint DodgeChance(uint dexterity) =>
        dexterity > MaxDodgePercent ? MaxDodgePercent : dexterity;

    public static uint ClampStat(uint value) => value > StatCap ? StatCap : value;

    public static uint SaturatingAdd(uint a, uint b) => a > uint.MaxValue - b ? uint.MaxValue : a + b;

    public static uint SaturatingSub(uint a, uint b) => a > b ? a - b : 0;

    public static uint RollInclusive(Random rng, uint min, uint max)
    {
        if (max < min)
        {
            (min, max) = (max, min);
        }

        return (uint)rng.Next((int)min, (int)max + 1);
    }

    public static T Pick<T>(Random rng, T[] options) => options[rng.Next(options.Length)];

    public static int ToInt(uint value) => value > int.MaxValue ? int.MaxValue : (int)value;

    public static uint ToUInt(int value) => value < 0 ? 0 : (uint)value;
}
