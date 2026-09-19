using SpacetimeDB;

// TowerBasedDemo combat math used by the SpacetimeDB module.
// Fact-check vs the previous lobby-only Lib.cs:
//   Class HP was 100/80/70/75. Spec is Warrior 80, Archer 50, Mage 30, Rogue 60.
//   JoinGame used to take a class pick. Spec rolls one of the four classes.
//   Main stat is rolled 3-6; the other three stats are 1-3.
//   Dealt = (CHR DAMAGE + Strength + ATK) + floor(Strength / 10).
//   CHR DAMAGE is skill/weapon base plus the class stat (DEX/INT/SPD). Warrior STR
//   is not added there because Strength is already in the published formula.
//   Taken damage subtracts current Defense so Defend (+20 DEF, +50 MP) has an effect.
//   XP to next level = Level * 100. Kill XP = 25 * Floor. Each level grants 2 stats.
public static partial class Module
{
    public const uint SessionId = 1;
    public const uint MaxPartySize = 3;
    public const uint StatCap = 99;
    public const uint MaxDodgePercent = 25;
    public const uint MpRegenPerTurn = 10;
    public const uint DefendDefenseBonus = 20;
    public const uint DefendManaRecover = 50;
    public const uint StatPointsPerLevel = 2;
    public const uint MainStatMin = 3;
    public const uint MainStatMax = 6;
    public const uint OffStatMin = 1;
    public const uint OffStatMax = 3;
    public const uint ClashDamageBonus = 1;
    public const uint SpriteVariantCount = 8;

    /// <summary>
    /// XP required to go from `level` to `level + 1` is `level * 100`.
    /// Level 1 needs 100 XP, level 2 needs 200 XP, and so on.
    /// </summary>
    public static uint XpToNextLevel(uint level) => SaturatingMul(level, 100);

    /// <summary>
    /// Each kill grants `25 * floor` XP to every living party member.
    /// </summary>
    public static uint KillXp(uint floor) => SaturatingMul(25, floor == 0 ? 1 : floor);

    /// <summary>
    /// Biomes change every 10 floors: Forest 1-10, Ocean 11-20, Hell 21-30, then repeat.
    /// </summary>
    public static Biome BiomeForFloor(uint floor)
    {
        var index = ((floor == 0 ? 1 : floor) - 1) / 10 % 3;
        return index switch
        {
            0 => Biome.Forest,
            1 => Biome.Ocean,
            _ => Biome.Hell,
        };
    }

    public static bool IsBossFloor(uint floor) => floor > 0 && floor % 10 == 0;

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

    /// <summary>
    /// CHR DAMAGE is skill/weapon base damage plus the class damage stat.
    /// Warrior STR is NOT added here because the published formula already adds Strength.
    /// </summary>
    public static uint ClassDamageStat(PlayerClass classChoice, uint dexterity, uint intelligence, uint speed) =>
        classChoice switch
        {
            PlayerClass.Warrior => 0,
            PlayerClass.Archer => dexterity,
            PlayerClass.Mage => intelligence,
            PlayerClass.Rogue => speed,
            _ => 0,
        };

    public static uint CharacterDamage(PlayerClass classChoice, uint skillOrWeaponDamage, uint dexterity, uint intelligence, uint speed) =>
        SaturatingAdd(skillOrWeaponDamage, ClassDamageStat(classChoice, dexterity, intelligence, speed));

    /// <summary>
    /// Dealt DAMAGE = (CHR DAMAGE + Strength + ATK) + floor(Strength * 0.1).
    /// 1 stat = 1 damage. Enrage +2 STR therefore adds +2 from the Strength term.
    /// The extra 0.1 per STR is integer-truncated so STR 0-9 adds 0 extra, STR 10-19 adds 1.
    /// </summary>
    public static uint DealtDamage(uint chrDamage, uint strength, uint atk)
    {
        var core = SaturatingAdd(SaturatingAdd(chrDamage, strength), atk);
        return SaturatingAdd(core, strength / 10);
    }

    public static uint EffectiveStrength(uint strength, uint strengthBuff) => SaturatingAdd(strength, strengthBuff);

    public static uint IncomingAfterDefense(uint incoming, uint defense) => SaturatingSub(incoming, defense);

    public static uint DodgeChance(uint dexterity) => dexterity > MaxDodgePercent ? MaxDodgePercent : dexterity;

    public static uint ClampStat(uint value) => value > StatCap ? StatCap : value;

    public static uint SaturatingAdd(uint a, uint b) => a > uint.MaxValue - b ? uint.MaxValue : a + b;

    public static uint SaturatingSub(uint a, uint b) => a > b ? a - b : 0;

    public static uint SaturatingMul(uint a, uint b)
    {
        if (a == 0 || b == 0)
        {
            return 0;
        }

        return a > uint.MaxValue / b ? uint.MaxValue : a * b;
    }

    public static uint TraitSpeedBonus(Biome biome) => biome == Biome.Ocean ? 2u : 0u;

    public static uint TraitAtkBonus(Biome biome) => biome == Biome.Hell ? 2u : 0u;

    public static uint TraitDexterityBonus(Biome biome) => biome == Biome.Forest ? 2u : 0u;

    public static string TraitName(Biome biome) => biome switch
    {
        Biome.Ocean => "Oceanic",
        Biome.Hell => "Infernal",
        _ => "Sylvan",
    };

    public static uint ScaledEnemyHealth(uint baseHealth, uint floor, bool boss)
    {
        var scaled = SaturatingAdd(baseHealth, SaturatingMul(floor, 4));
        return boss ? SaturatingAdd(scaled, SaturatingMul(floor, 6)) : scaled;
    }

    public static uint ScaledEnemyAtk(uint baseAtk, uint floor, bool boss)
    {
        var scaled = SaturatingAdd(baseAtk, floor / 2);
        return boss ? SaturatingAdd(scaled, 3) : scaled;
    }

    public static uint ScaledEnemyStrength(uint baseStrength, uint floor, bool boss)
    {
        var scaled = SaturatingAdd(baseStrength, floor / 3);
        return boss ? SaturatingAdd(scaled, 2) : scaled;
    }

    public static uint RollInclusive(Random rng, uint min, uint max)
    {
        if (max < min)
        {
            (min, max) = (max, min);
        }

        return (uint)rng.Next((int)min, (int)max + 1);
    }

    public static T Pick<T>(Random rng, T[] options) => options[rng.Next(options.Length)];
}
