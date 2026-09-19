using System;
using SpacetimeDB;

/// Pure combat math and character templates. Nothing here touches the database,
/// so the numbers can be reasoned about (and changed) in one place.
public static partial class Module
{
    public const uint SessionId = 1;
    public const uint MaxPartySize = 3;
    public const uint MaxEnemySlots = 4;
    public const uint MaxStageCount = 10;
    public const uint RestStopEvery = 3;
    public const int StageScalePercent = 8;

    public const int BagCapacity = 12;
    public const int FocusManaGain = 20;
    public const int ManaRegenPerTurn = 2;

    /// Dexterity doubles as the dodge chance, capped so nobody is untouchable.
    public const int MaxDodgePercent = 25;
    /// Enemy dodge is this many times smaller than the shared Dexterity formula.
    public const int EnemyDodgeDivisor = 3;

    /// Main stat rolls 3-6, the other three roll 1-3.
    public const int MainStatMin = 3;
    public const int MainStatMax = 6;
    public const int OffStatMin = 1;
    public const int OffStatMax = 3;

    /// Character level 1 needs 100 EXP, level 2 needs 200, and so on.
    public const uint ExpPerLevelStep = 100;
    /// Each kill grants 25 EXP times the current stage number.
    public const uint KillExpPerStage = 25;
    /// Unspent points granted to a living player on each character level-up.
    public const uint StatPointsPerLevel = 3;

    /// Per-point growth applied by SpendStatPoint. One level's 3 points total
    /// +3 across chosen stats, comparable to the old automatic +2 on the class
    /// main stat.
    public const int StatPointStrength = 1;
    public const int StatPointDexterity = 1;
    public const int StatPointIntelligence = 1;
    public const int StatPointSpeed = 1;

    // Enemy actions are spaced out so the battle log stays readable.
    public const long EnemyTurnDelayMicros = 2_000_000;
    /// Pause on the cleared-stage screen before rest or the next battle.
    public const long StageTransitionDelayMicros = 3_000_000;

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

    public static int DodgeChance(int dexterity, Team faction)
    {
        var chance = Math.Clamp(dexterity, 0, MaxDodgePercent);
        if (faction == Team.Enemies && EnemyDodgeDivisor > 1)
        {
            chance /= EnemyDodgeDivisor;
        }

        return chance;
    }

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

    public static uint SaturatingAdd(uint a, uint b) =>
        a > uint.MaxValue - b ? uint.MaxValue : a + b;

    public static uint SaturatingSub(uint a, uint b) => a > b ? a - b : 0;

    public static uint SaturatingMul(uint a, uint b)
    {
        if (a == 0 || b == 0)
        {
            return 0;
        }

        return a > uint.MaxValue / b ? uint.MaxValue : a * b;
    }

    /// EXP required to go from `level` to `level + 1` is `level * 100`.
    public static uint XpToNextLevel(uint level) => SaturatingMul(level, ExpPerLevelStep);

    /// Each kill grants `25 * stage` EXP to every living party member.
    public static uint KillXp(uint stage) =>
        SaturatingMul(KillExpPerStage, stage == 0 ? 1u : stage);

    public static int ApplyStageScale(int value, uint stage)
    {
        var n = stage < 1 ? 1u : stage;
        var scaled = value;
        for (uint i = 1; i < n; i++)
        {
            scaled = (scaled * (100 + StageScalePercent)) / 100;
        }

        return Math.Max(1, scaled);
    }

    /// 4-pack member is 1.0x. A solo spawn is beefier, not a raid boss.
    public static int PackVitalityBps(int count) =>
        count switch
        {
            1 => 18500,
            2 => 13000,
            3 => 11000,
            _ => 10000,
        };

    public static int PackPowerBps(int count) =>
        count switch
        {
            1 => 14500,
            2 => 12000,
            3 => 10800,
            _ => 10000,
        };

    public static int ScaleByBps(int value, int bps) =>
        Math.Max(1, (int)((long)value * bps / 10000));

    public readonly struct EnemyArchetype
    {
        public EnemyArchetype(
            string name,
            string kind,
            int maxHp,
            int maxMana,
            int strength,
            int dexterity,
            int intelligence,
            int speed,
            int atk,
            int defense,
            string basicAttackName,
            string skillName
        )
        {
            Name = name;
            Kind = kind;
            MaxHp = maxHp;
            MaxMana = maxMana;
            Strength = strength;
            Dexterity = dexterity;
            Intelligence = intelligence;
            Speed = speed;
            Atk = atk;
            Defense = defense;
            BasicAttackName = basicAttackName;
            SkillName = skillName;
        }

        public string Name { get; }
        public string Kind { get; }
        public int MaxHp { get; }
        public int MaxMana { get; }
        public int Strength { get; }
        public int Dexterity { get; }
        public int Intelligence { get; }
        public int Speed { get; }
        public int Atk { get; }
        public int Defense { get; }
        public string BasicAttackName { get; }
        public string SkillName { get; }
    }

    public static EnemyArchetype[] EnemyPool { get; } =
    {
        new EnemyArchetype("Goblin Raider", "Goblin", 90, 30, 4, 3, 1, 7, 4, 1, "Jab", "Rusty Slash"),
        new EnemyArchetype("Cave Troll", "Troll", 150, 40, 8, 1, 1, 3, 6, 3, "Club Sweep", "Boulder Smash"),
        new EnemyArchetype("Hex Shade", "Shade", 70, 55, 2, 3, 7, 8, 3, 0, "Hex Bolt", "Hex Bolt"),
        new EnemyArchetype("Ash Wolf", "Wolf", 85, 20, 5, 4, 1, 9, 5, 1, "Bite", "Bite"),
        new EnemyArchetype("Bone Guard", "Skeleton", 100, 25, 5, 2, 1, 5, 5, 2, "Bone Slash", "Bone Slash"),
    };

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
