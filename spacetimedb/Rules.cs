using System;
using SpacetimeDB;

/// Pure combat math and character templates. Nothing here touches the database,
/// so the numbers can be reasoned about (and changed) in one place.
public static partial class Module
{
    public const uint SessionId = 1;
    public const uint MaxPartySize = 3;
    public const uint EnemyCount = 2;

    public const int BagCapacity = 12;
    public const int FocusManaGain = 20;
    public const int ManaRegenPerTurn = 2;

    /// Dexterity doubles as the dodge chance, capped so nobody is untouchable.
    public const int MaxDodgePercent = 25;
    /// A faster attacker wins the clash and hits slightly harder.
    public const int ClashDamageBonus = 1;

    /// Main stat rolls 3-6, the other three roll 1-3.
    public const int MainStatMin = 3;
    public const int MainStatMax = 6;
    public const int OffStatMin = 1;
    public const int OffStatMax = 3;

    // Enemy actions are spaced out so the battle log stays readable.
    public const long EnemyTurnDelayMicros = 2_000_000;

    static readonly string[] PartyNames = { "Aria", "Bran", "Cael" };

    // ------------------------------------------------------------- characters

    public static string ClassName(PlayerClass playerClass) =>
        playerClass switch
        {
            PlayerClass.Warrior => "Warrior",
            PlayerClass.Mage => "Mage",
            PlayerClass.Rogue => "Rogue",
            PlayerClass.Archer => "Archer",
            _ => "Adventurer",
        };

    public static int ClassMaxHp(PlayerClass playerClass) =>
        playerClass switch
        {
            PlayerClass.Warrior => 120,
            PlayerClass.Mage => 70,
            PlayerClass.Rogue => 85,
            PlayerClass.Archer => 90,
            _ => 90,
        };

    public static int ClassMaxMana(PlayerClass playerClass) =>
        playerClass switch
        {
            PlayerClass.Warrior => 30,
            PlayerClass.Mage => 80,
            PlayerClass.Rogue => 45,
            PlayerClass.Archer => 50,
            _ => 40,
        };

    /// The stat a class rolls high in, and the stat that feeds its damage.
    public static StatType MainStat(PlayerClass playerClass) =>
        playerClass switch
        {
            PlayerClass.Warrior => StatType.Strength,
            PlayerClass.Mage => StatType.Intelligence,
            PlayerClass.Rogue => StatType.Speed,
            PlayerClass.Archer => StatType.Dexterity,
            _ => StatType.Strength,
        };

    public static WeaponType ClassWeapon(PlayerClass playerClass) =>
        playerClass switch
        {
            PlayerClass.Warrior => WeaponType.Sword,
            PlayerClass.Mage => WeaponType.Staff,
            PlayerClass.Rogue => WeaponType.Dagger,
            PlayerClass.Archer => WeaponType.Bow,
            _ => WeaponType.None,
        };

    public static string StarterWeaponName(PlayerClass playerClass) =>
        playerClass switch
        {
            PlayerClass.Warrior => "Iron Sword",
            PlayerClass.Mage => "Oak Staff",
            PlayerClass.Rogue => "Twin Daggers",
            PlayerClass.Archer => "Hunting Bow",
            _ => "Iron Sword",
        };

    public static string BasicAttackName(PlayerClass playerClass) =>
        playerClass switch
        {
            PlayerClass.Warrior => "Sword Swing",
            PlayerClass.Mage => "Staff Jab",
            PlayerClass.Rogue => "Quick Cut",
            PlayerClass.Archer => "Snap Shot",
            _ => "Strike",
        };

    public static string PartyName(uint slot) => PartyNames[slot % (uint)PartyNames.Length];

    // ------------------------------------------------------------------- math

    /// The damage stat a class adds on top of a skill's base. Warrior Strength is
    /// deliberately absent because Strength is already a term in DealtDamage.
    public static int ClassDamageStat(
        PlayerClass playerClass,
        int dexterity,
        int intelligence,
        int speed
    ) =>
        playerClass switch
        {
            PlayerClass.Warrior => 0,
            PlayerClass.Mage => intelligence,
            PlayerClass.Rogue => speed,
            PlayerClass.Archer => dexterity,
            _ => 0,
        };

    /// Dealt damage = (character damage + strength + attack) + strength / 10.
    /// The trailing term is integer-truncated, so 1 extra point per 10 Strength.
    public static int DealtDamage(int characterDamage, int strength, int atk)
    {
        var core = characterDamage + strength + atk;
        return Math.Max(0, core + (strength / 10));
    }

    public static int AfterDefense(int incoming, int defense) => Math.Max(0, incoming - defense);

    public static int DodgeChance(int dexterity) => Math.Clamp(dexterity, 0, MaxDodgePercent);

    public static int RollInclusive(Random rng, int min, int max) => rng.Next(min, max + 1);

    /// ctx.Rng is seeded per transaction, so three clients that join inside the
    /// same tick all roll identically. Mixing the caller's identity in fixes that
    /// while staying deterministic on replay, since sender and timestamp are both
    /// recorded with the reducer call.
    public static Random PerSenderRng(ReducerContext ctx)
    {
        var mix =
            (ulong)ctx.Timestamp.MicrosecondsSinceUnixEpoch
            ^ ((ulong)(uint)ctx.Sender.GetHashCode() * 0x9E3779B97F4A7C15UL);
        return new Random((int)(mix ^ (mix >> 32)));
    }

    public static T Pick<T>(Random rng, T[] options) => options[rng.Next(options.Length)];

    /// Main stat rolls 3-6, every other stat rolls 1-3.
    public static (int Strength, int Dexterity, int Intelligence, int Speed) RollStats(
        Random rng,
        PlayerClass playerClass
    )
    {
        var main = RollInclusive(rng, MainStatMin, MainStatMax);
        var strength = RollInclusive(rng, OffStatMin, OffStatMax);
        var dexterity = RollInclusive(rng, OffStatMin, OffStatMax);
        var intelligence = RollInclusive(rng, OffStatMin, OffStatMax);
        var speed = RollInclusive(rng, OffStatMin, OffStatMax);

        switch (MainStat(playerClass))
        {
            case StatType.Strength:
                strength = main;
                break;
            case StatType.Dexterity:
                dexterity = main;
                break;
            case StatType.Intelligence:
                intelligence = main;
                break;
            default:
                speed = main;
                break;
        }

        return (strength, dexterity, intelligence, speed);
    }

    public static EquipSlot SlotFor(ItemDef item) =>
        item.Kind switch
        {
            ItemKind.Weapon => EquipSlot.Weapon,
            ItemKind.Armor => item.ArmorSlot switch
            {
                ArmorSlot.Helmet => EquipSlot.Helmet,
                ArmorSlot.Chestplate => EquipSlot.Chestplate,
                ArmorSlot.Leggings => EquipSlot.Leggings,
                ArmorSlot.Boots => EquipSlot.Boots,
                _ => EquipSlot.Bag,
            },
            _ => EquipSlot.Bag,
        };
}
