using System;
using SpacetimeDB;

/// Pure combat math and character templates. Nothing here touches the database,
/// so the numbers can be reasoned about (and changed) in one place.
public static partial class Module
{
    public const uint SessionId = 1;
    public const uint MaxPartySize = 3;
    public const uint MaxEnemySlots = 4;
    /// Old default pack; HP and EXP are scaled so a stage's totals stay near this size.
    public const int ReferencePackSize = 3;
    /// Weights for pack sizes 1, 2, 3, 4. Uniform 25% each.
    public static readonly int[] PackSizeWeights = { 1, 1, 1, 1 };
    public const uint RestStopEvery = 3;
    public const uint BiomeLength = 10;
    public const uint BossInterval = 10;
    public const uint BiomeCount = 4;
    /// New runs start here. Keep at 1 in committed code.
    public const uint DebugStartStage = 1;
    public const int StageScalePercent = 8;

    public const int BossHpMultiplier = 5;
    public const int BossDamageMultiplierBps = 13000;
    public const uint BossExpMultiplier = 5;
    /// Flame Sweep on every Nth boss turn (turns 3, 6, 9, ...).
    public const int FlameSweepInterval = 3;
    public const int FlameSweepBurnStack = 3;
    public const int FlameSweepBurnCount = 2;
    public const string BossName = "Magma Colossus";
    public const string BossKind = "MagmaColossus";
    public const string BossBasicAttackName = "Magma Smash";
    public const string FlameSweepName = "Flame Sweep";

    /// First word of the boss title, e.g. Magma Colossus → Magma.
    public static string BossAttributeName
    {
        get
        {
            var space = BossName.IndexOf(' ');
            return space < 0 ? BossName : BossName.Substring(0, space);
        }
    }

    public const int BagCapacity = 12;
    public const int InventoryCapacity = 9;
    public const uint InventoryNone = 255;
    /// When true, join fills the 3x3 with the Health Amulet and every base weapon.
    public const bool SeedTestItems = false;
    public const int FocusManaGain = 20;
    public const int AmethystSashFocusMana = 50;
    public const int GuardiansPendantReduceBps = 700;
    public const int CountessNecklaceHeal = 2;
    public const int HolyGrailHeal = 5;
    public const int HiddenDreamcatcherDodgePercent = 5;
    public const int EmeraldPendantManaDiscount = 5;
    public const int RubyScepterBurnPower = 3;
    public const int RedCocoonReflect = 1;
    public const int RootedBladeSpeedPenalty = 1;
    public const int DragonsFireBurnCap = 30;
    public const uint CheatCharacterLevel = 999;
    public const int ManaRegenPerTurn = 2;

    /// Dexterity doubles as the dodge chance, capped so nobody is untouchable.
    public const int MaxDodgePercent = 25;
    /// Enemy dodge is this many times smaller than the shared Dexterity formula.
    public const int EnemyDodgeDivisor = 3;
    /// Archer passive: 2% dodge per Dexterity (200 basis points).
    public const int ArcherDodgeBpsPerDex = 200;
    /// Archer DEX dodge is capped at 50% before skill bonuses such as Restring.
    public const int ArcherMaxDodgeBps = 5000;
    /// Archer passive: +0.5 damage per Dexterity, stored as tenths.
    public const int ArcherDamageTenthsPerDex = 5;
    /// Knight STR stat passive: +0.2 damage per Strength, stored as tenths.
    public const int KnightDamageTenthsPerStrength = 2;
    /// Mage passive: +0.2 spell damage per Intelligence, stored as tenths.
    public const int MageSpellDamageTenthsPerInt = 2;
    /// Mage passive: +0.4 max mana per Intelligence, stored as tenths.
    public const int MageManaTenthsPerInt = 4;
    /// Ninja Speed stat passive: +0.5 damage per Speed on all attacks, stored as tenths.
    public const int NinjaDamageTenthsPerSpeed = 5;
    /// Ninja crit: 0.1% per Speed stat (10 basis points). Uses Speed, not CombatSpeed.
    public const int NinjaCritBpsPerSpeed = 10;
    public const int NinjaSpeedLeadForFirstAction = 5;
    public const int NinjaGuaranteedFirstSpeed = 999999999;
    /// Non-archer dodge: 1% per Dexterity, then capped.
    public const int StandardDodgeBpsPerDex = 100;
    public const int BpsPerPercent = 100;
    public const int MaxDodgeBps = 9500;
    public const int FragileDamageBpsPerStack = 1000;
    public const int WeakDamageBpsPerStack = 1000;
    public const int BludgeonFragileBps = 15000;
    public const int GrandUndertakingEnemyHpBps = 5000;
    public const int GrandUndertakingAllyHpBps = 1000;
    public const int NinjaSpeedPowerCap = 5;
    public const int FinishTheJobTurnRequirement = 4;
    /// Stance +8/turn is uncapped in the spec; a cap keeps long L30 fights from snowballing.
    public const int FinishTheJobPowerCap = 40;
    public const int RushNextTurnSpeed = 99999;
    public const int EvadeDamageThreshold = 20;
    public const int FuriosoManaCost = 100;
    public const int FuriosoHitCount = 9;
    public const int FuriosoBaseDamage = 5;
    public const int FuriosoBonusPerHit = 3;
    public const int GrandshotManaCost = 100;
    public const uint GrandshotLevelRequired = 30;
    public const int GrandshotBaseDamage = 50;
    public const int GrandshotDamagePerDodge = 50;
    public const int GrandshotDodgeCap = 4;
    public const int SnipeDamage = 10;
    public const int SnipeManaCost = 50;
    public const uint SnipeLevelRequired = 1;
    public const int OverthrowEnragedStacks = 12;
    public const int OverthrowDamage = 42;
    public const int OverthrowManaCost = 40;
    public const int OverthrowWeakStacks = 3;
    public const int OverthrowFragileStacks = 4;
    public const int SpearBaseManaCost = 45;
    public const int VerticalCutBaseManaCost = 80;
    public const int SkillManaDiscountPerUse = 15;
    public const int MagicBulletStageCount = 7;
    /// Burn stack (per-tick damage) caps at 25. Extra applications still add duration.
    public const int BurnStackCap = 25;

    /// Main stat rolls 3-6, the other three roll 1-3.
    public const int MainStatMin = 3;
    public const int MainStatMax = 6;
    public const int OffStatMin = 1;
    public const int OffStatMax = 3;

    /// Character level L needs `100 * L^1.5` EXP to reach L+1.
    public const uint ExpCurveBase = 100;
    /// Each kill grants 25 EXP times the current stage number.
    public const uint KillExpPerStage = 25;
    /// Unspent points granted to a living player on each character level-up.
    public const uint StatPointsPerLevel = 3;

    /// Per-point growth applied by SpendStatPoint. Health and Mana are not
    /// spendable. One level still grants StatPointsPerLevel points across
    /// Strength, Speed, Intelligence, and Dexterity.
    public const int StatPointStrength = 1;
    public const int StatPointDexterity = 1;
    public const int StatPointIntelligence = 1;
    public const int StatPointSpeed = 1;

    /// DisplayPos for TurnOrder rows that should not appear in the client list.
    public const uint TurnListHiddenPos = 255;

    public static bool IsSpendableStat(StatType stat) =>
        stat is StatType.Strength
            or StatType.Speed
            or StatType.Intelligence
            or StatType.Dexterity;

    // Enemy actions are spaced out so the battle log stays readable.
    public const long EnemyTurnDelayMicros = 2_000_000;
    /// Pause on the cleared-stage screen before rest or the next battle.
    public const long StageTransitionDelayMicros = 3_000_000;

    static readonly string[] PartyNames = { "Aria", "Bran", "Cael" };

    // ------------------------------------------------------------- characters

    public static string ClassName(PlayerClass playerClass) =>
        playerClass switch
        {
            PlayerClass.Knight => "Knight",
            PlayerClass.Mage => "Mage",
            PlayerClass.Ninja => "Ninja",
            PlayerClass.Archer => "Archer",
            _ => "Adventurer",
        };

    public static int ClassBaseHp(PlayerClass playerClass) =>
        playerClass switch
        {
            PlayerClass.Knight => 80,
            PlayerClass.Mage => 40,
            PlayerClass.Ninja => 20,
            PlayerClass.Archer => 50,
            _ => 80,
        };

    /// HP per level in tenths so Knight/Ninja 2.5 growth stays exact before rounding.
    public static int ClassHpGrowthTenths(PlayerClass playerClass) =>
        playerClass is PlayerClass.Knight or PlayerClass.Ninja ? 25 : 20;

    /// MaxHP(L) = BaseHP + growth*L, rounded to nearest whole HP.
    public static int ClassMaxHp(PlayerClass playerClass, uint characterLevel = 1)
    {
        var level = characterLevel == 0 ? 1 : (int)characterLevel;
        var tenths = ClassBaseHp(playerClass) * 10 + ClassHpGrowthTenths(playerClass) * level;
        return Math.Max(1, (tenths + 5) / 10);
    }

    public static int ClassMaxMana(PlayerClass playerClass) =>
        playerClass switch
        {
            PlayerClass.Knight => 100,
            PlayerClass.Mage => 150,
            PlayerClass.Ninja => 100,
            PlayerClass.Archer => 100,
            _ => 100,
        };

    /// The stat a class rolls high in, and the stat that feeds its damage.
    public static StatType MainStat(PlayerClass playerClass) =>
        playerClass switch
        {
            PlayerClass.Knight => StatType.Strength,
            PlayerClass.Mage => StatType.Intelligence,
            PlayerClass.Ninja => StatType.Speed,
            PlayerClass.Archer => StatType.Dexterity,
            _ => StatType.Strength,
        };

    public static WeaponType ClassWeapon(PlayerClass playerClass) =>
        playerClass switch
        {
            PlayerClass.Knight => WeaponType.Sword,
            PlayerClass.Mage => WeaponType.Staff,
            PlayerClass.Ninja => WeaponType.Katana,
            PlayerClass.Archer => WeaponType.Bow,
            _ => WeaponType.None,
        };

    public static string StarterWeaponName(PlayerClass playerClass) =>
        playerClass switch
        {
            PlayerClass.Knight => "Chipped Sword",
            PlayerClass.Mage => "Azure Cane",
            PlayerClass.Ninja => "Rusted Pommel",
            PlayerClass.Archer => "Golden Bow",
            _ => "Chipped Sword",
        };

    /// Stable catalog keys. Unique loot can share Golden Bow / Azure Cane names.
    public static string StarterWeaponShortName(PlayerClass playerClass) =>
        playerClass switch
        {
            PlayerClass.Knight => "CSW",
            PlayerClass.Mage => "WCN",
            PlayerClass.Ninja => "KTN",
            PlayerClass.Archer => "WBW",
            _ => "CSW",
        };

    public static string BasicAttackName(PlayerClass playerClass) =>
        playerClass switch
        {
            PlayerClass.Knight => "Sword Swing",
            PlayerClass.Mage => "Staff Jab",
            PlayerClass.Ninja => "Quick Cut",
            PlayerClass.Archer => "Snap Shot",
            _ => "Strike",
        };

    /// Knights win speed ties so their High priority actually shows up in the queue.
    public static int ClassTurnPriority(PlayerClass playerClass) =>
        playerClass == PlayerClass.Knight ? 0 : 1;

    public static string PartyName(uint slot) => PartyNames[slot % (uint)PartyNames.Length];

    public static class AmuletNames
    {
        public const string AmethystSash = "Amethyst Sash";
        public const string GoldenCross = "Golden Cross";
        public const string GuardiansPendant = "Guardian's Pendant";
        public const string CountessNecklace = "Countess' Necklace";
        public const string EyeOfTheWatcher = "Eye of the Watcher";
        public const string SigilOfTheOld = "Sigil of the Old";
        public const string DragonflyCharm = "Dragonfly Charm";
        public const string TwinAmethystCharm = "Twin Amethyst Charm";
        public const string DragonsFire = "Dragons' Fire";
        public const string EmeraldPendant = "Emerald Pendant";
        public const string JusticesWings = "Justices' Wings";
        public const string HolyGrail = "Holy Grail";
        public const string HiddenDreamcatcher = "Hidden Dreamcatcher";
        public const string RootedBlade = "Rooted Blade";
        public const string RedCocoon = "Red Cocoon";
        public const string RubyScepter = "Ruby Scepter";
    }

    public static class UniqueWeaponNames
    {
        public const string ChippedSword = "Chipped Sword";
        public const string JaggedSword = "Jagged Sword";
        public const string CrimsonBlade = "Crimson Blade";
        public const string GoldenBow = "Golden Bow";
        public const string EmeraldBow = "Emerald Bow";
        public const string CrimsonBow = "Crimson Bow";
        public const string AzureCane = "Azure Cane";
        public const string ElegantCane = "Elegant Cane";
        public const string StaffOfTheQueen = "Staff of the Queen";
        public const string RustedPummel = "Rusted Pummel";
        public const string CrimsonDagger = "Crimson Dagger";
        public const string AzureDagger = "Azure Dagger";
    }

    public static readonly string[] AllUniqueWeaponNames =
    {
        UniqueWeaponNames.ChippedSword,
        UniqueWeaponNames.JaggedSword,
        UniqueWeaponNames.CrimsonBlade,
        UniqueWeaponNames.GoldenBow,
        UniqueWeaponNames.EmeraldBow,
        UniqueWeaponNames.CrimsonBow,
        UniqueWeaponNames.AzureCane,
        UniqueWeaponNames.ElegantCane,
        UniqueWeaponNames.StaffOfTheQueen,
        UniqueWeaponNames.RustedPummel,
        UniqueWeaponNames.CrimsonDagger,
        UniqueWeaponNames.AzureDagger,
    };

    public static string[] UniqueWeaponsFor(PlayerClass playerClass) =>
        playerClass switch
        {
            PlayerClass.Knight => new[]
            {
                UniqueWeaponNames.ChippedSword,
                UniqueWeaponNames.JaggedSword,
                UniqueWeaponNames.CrimsonBlade,
            },
            PlayerClass.Archer => new[]
            {
                UniqueWeaponNames.GoldenBow,
                UniqueWeaponNames.EmeraldBow,
                UniqueWeaponNames.CrimsonBow,
            },
            PlayerClass.Mage => new[]
            {
                UniqueWeaponNames.AzureCane,
                UniqueWeaponNames.ElegantCane,
                UniqueWeaponNames.StaffOfTheQueen,
            },
            PlayerClass.Ninja => new[]
            {
                UniqueWeaponNames.RustedPummel,
                UniqueWeaponNames.CrimsonDagger,
                UniqueWeaponNames.AzureDagger,
            },
            _ => Array.Empty<string>(),
        };

    public static readonly string[] AllAmuletNames =
    {
        AmuletNames.AmethystSash,
        AmuletNames.GoldenCross,
        AmuletNames.GuardiansPendant,
        AmuletNames.CountessNecklace,
        AmuletNames.EyeOfTheWatcher,
        AmuletNames.SigilOfTheOld,
        AmuletNames.DragonflyCharm,
        AmuletNames.TwinAmethystCharm,
        AmuletNames.DragonsFire,
        AmuletNames.EmeraldPendant,
        AmuletNames.JusticesWings,
        AmuletNames.HolyGrail,
        AmuletNames.HiddenDreamcatcher,
        AmuletNames.RootedBlade,
        AmuletNames.RedCocoon,
        AmuletNames.RubyScepter,
    };

    public static class SkillNames
    {
        public const string Bash = "Bash";
        public const string Rush = "Rush";
        public const string Embolden = "Embolden";
        public const string GallantPride = "Gallant Pride";
        public const string Cleave = "Cleave";
        public const string Bludgeon = "Bludgeon";
        public const string Terrify = "Terrify";
        public const string TripleSlash = "Triple Slash";
        public const string Furioso = "Furioso";

        public const string Shoot = "Shoot";
        public const string Restring = "Restring";
        public const string Scheme = "Scheme";
        public const string Evade = "Evade";
        public const string RainDown = "Rain Down";
        public const string Snipe = "Snipe";
        public const string CurvedShot = "Curved Shot";
        public const string Grandshot = "Grandshot";

        public const string MagicMissile = "Magic Missile";
        public const string Fireball = "Fireball";
        public const string Concentrate = "Concentrate";
        public const string Pray = "Pray";
        public const string MagicBullet = "Magic Bullet";
        public const string GrandUndertaking = "Grand Undertaking";

        public const string Spear = "Spear";
        public const string VerticalCut = "Vertical Cut";
        public const string FocusSpirit = "Focus Spirit";
        public const string FinishTheJob = "Finish the Job";
        public const string Overthrow = "Overthrow";
    }

    public readonly struct MagicBulletStageDef
    {
        public MagicBulletStageDef(
            int stage,
            int damage,
            int hits,
            bool allEnemies,
            int burnStack,
            int burnCount,
            int speedDelta,
            bool killsCaster
        )
        {
            Stage = stage;
            Damage = damage;
            Hits = hits;
            AllEnemies = allEnemies;
            BurnStack = burnStack;
            BurnCount = burnCount;
            SpeedDelta = speedDelta;
            KillsCaster = killsCaster;
        }

        public int Stage { get; }
        public int Damage { get; }
        public int Hits { get; }
        public bool AllEnemies { get; }
        public int BurnStack { get; }
        public int BurnCount { get; }
        public int SpeedDelta { get; }
        public bool KillsCaster { get; }
    }

    public static MagicBulletStageDef MagicBulletStageDefOf(int stage) =>
        stage switch
        {
            2 => new MagicBulletStageDef(2, 3, 1, true, 1, 3, -1, false),
            3 => new MagicBulletStageDef(3, 5, 1, true, 3, 2, 0, false),
            4 => new MagicBulletStageDef(4, 7, 1, true, 3, 2, -1, false),
            5 => new MagicBulletStageDef(5, 8, 1, true, 4, 2, 0, false),
            6 => new MagicBulletStageDef(6, 5, 8, false, 10, 2, 0, false),
            7 => new MagicBulletStageDef(7, 90, 1, true, 0, 0, 0, true),
            _ => new MagicBulletStageDef(1, 5, 1, false, 2, 2, 0, false),
        };

    // ------------------------------------------------------------------- math

    /// Flat class passives: Knight +0.2/STR, Archer +0.5/DEX, Ninja +0.5/BaseSpeed,
    /// Mage +0.2/INT on spells. Ninja must be given BaseSpeed, never CombatSpeed.
    public static int ClassPassiveDamage(
        PlayerClass playerClass,
        int strength,
        int dexterity,
        int intelligence,
        int baseSpeed,
        bool isSpell
    )
    {
        var tenths = 0;
        if (playerClass == PlayerClass.Knight)
        {
            tenths += KnightDamageTenthsPerStrength * Math.Max(0, strength);
        }

        if (playerClass == PlayerClass.Archer)
        {
            tenths += ArcherDamageTenthsPerDex * Math.Max(0, dexterity);
        }

        if (playerClass == PlayerClass.Ninja)
        {
            tenths += NinjaDamageTenthsPerSpeed * NinjaPassiveBaseSpeed(baseSpeed);
        }

        if (playerClass == PlayerClass.Mage && isSpell)
        {
            tenths += MageSpellDamageTenthsPerInt * Math.Max(0, intelligence);
        }

        return tenths <= 0 ? 0 : (tenths + 5) / 10;
    }

    /// Passive 3 uses rolled/spent BaseSpeed only. CombatSpeed first-action
    /// (999999999) and temporary Speed sets must never feed this bonus.
    public static int NinjaPassiveBaseSpeed(int baseSpeed)
    {
        if (baseSpeed <= 0 || baseSpeed >= NinjaGuaranteedFirstSpeed)
        {
            return 0;
        }

        return baseSpeed;
    }

    public static int MageManaFromIntelligence(int intelligence) =>
        Math.Max(0, (MageManaTenthsPerInt * Math.Max(0, intelligence) + 5) / 10);

    public static int NinjaCritChanceBps(int speed) =>
        Math.Max(0, speed) * NinjaCritBpsPerSpeed;

    public static bool RollCrit(Random rng, int critBps) =>
        critBps > 0 && rng.Next(1, 10001) <= Math.Min(critBps, 10000);

    public static int EffectiveSpeed(Entity entity) =>
        entity.CombatSpeed != 0 ? entity.CombatSpeed : entity.Speed;

    public static bool HasForcedFirstSpeed(Entity entity) =>
        entity.GoFirstNextRound || EffectiveSpeed(entity) >= RushNextTurnSpeed;

    /// Ninja passive: +1 skill base power per Speed above the target, capped at +5.
    public static int NinjaSpeedPowerBonus(int ninjaSpeed, int targetSpeed) =>
        Math.Clamp(ninjaSpeed - targetSpeed, 0, NinjaSpeedPowerCap);

    public static int ScaleByBps(int value, int bps) =>
        Math.Max(0, (int)((long)value * bps / 10000));

    /// Fragile is a final damage-taken multiplier and applies to allies and enemies.
    public static int ApplyFragile(int damage, int fragileStacks)
    {
        if (damage <= 0 || fragileStacks <= 0)
        {
            return damage;
        }

        return Math.Max(0, ScaleByBps(damage, 10000 + (FragileDamageBpsPerStack * fragileStacks)));
    }

    public static int ApplyWeak(int damage, int weakStacks)
    {
        if (damage <= 0 || weakStacks <= 0)
        {
            return damage;
        }

        return Math.Max(0, ScaleByBps(damage, 10000 - (WeakDamageBpsPerStack * weakStacks)));
    }

    public static int ApplyBludgeonFragile(int damage) =>
        damage <= 0 ? 0 : ScaleByBps(damage, BludgeonFragileBps);

    public static int MagicBulletStageOf(Entity entity)
    {
        if (entity.MagicBulletStage < 1)
        {
            return 1;
        }

        return Math.Min(entity.MagicBulletStage, MagicBulletStageCount);
    }

    public static int EffectiveSkillManaCost(string skillName, int catalogCost, Entity caster)
    {
        if (skillName == SkillNames.Furioso)
        {
            return FuriosoManaCost;
        }

        if (skillName == SkillNames.Grandshot)
        {
            return GrandshotManaCost;
        }

        if (skillName == SkillNames.Overthrow)
        {
            return OverthrowManaCost;
        }

        if (skillName == SkillNames.Spear)
        {
            return Math.Max(0, SpearBaseManaCost - caster.SpearDiscount);
        }

        if (skillName == SkillNames.VerticalCut)
        {
            return Math.Max(0, VerticalCutBaseManaCost - caster.VerticalCutDiscount);
        }

        return catalogCost;
    }

    public static int ApplySpellManaDiscount(int cost, bool hasEmeraldPendant) =>
        hasEmeraldPendant ? Math.Max(0, cost - EmeraldPendantManaDiscount) : cost;

    public static int ApplyReceivedDamageReduction(int damage, bool hasGuardiansPendant)
    {
        if (damage <= 0 || !hasGuardiansPendant)
        {
            return damage;
        }

        return ScaleByBps(damage, 10000 - GuardiansPendantReduceBps);
    }

    public static int BurnCapFor(bool hasDragonsFire) =>
        hasDragonsFire ? DragonsFireBurnCap : BurnStackCap;

    public static int FocusManaFor(bool hasAmethystSash) =>
        hasAmethystSash ? AmethystSashFocusMana : FocusManaGain;

    public static int NextDiscountedManaCost(int currentCost) =>
        Math.Max(0, currentCost - SkillManaDiscountPerUse);

    public static int GrandshotCountedDodges(int dodgeCount) =>
        Math.Clamp(dodgeCount, 0, GrandshotDodgeCap);

    public static int GrandshotDamageOf(int dodgeCount) =>
        GrandshotBaseDamage + (GrandshotCountedDodges(dodgeCount) * GrandshotDamagePerDodge);

    /// Weapon/skill core. Class passives and Enraged stacks are added separately.
    public static int DealtDamage(int characterDamage, int atk) =>
        Math.Max(0, characterDamage + atk);

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

    /// Dodge chance in basis points (10000 = 100%). Archer uses 2% per DEX, capped
    /// at 50% before temporary bonuses such as Restring.
    public static int DodgeChanceBps(
        int dexterity,
        Team faction,
        int dodgeBonusPercent,
        bool archerPassive
    )
    {
        int bps;
        if (archerPassive)
        {
            bps = Math.Min(ArcherMaxDodgeBps, Math.Max(0, dexterity) * ArcherDodgeBpsPerDex);
        }
        else
        {
            bps = DodgeChance(dexterity, faction) * BpsPerPercent;
        }

        bps += Math.Max(0, dodgeBonusPercent) * BpsPerPercent;
        return Math.Clamp(bps, 0, MaxDodgeBps);
    }

    public static bool RollDodge(Random rng, int dodgeBps) =>
        dodgeBps > 0 && rng.Next(1, 10001) <= dodgeBps;

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

    /// EXP to go from `level` to `level + 1` is `100 * level^1.5`.
    public static uint XpToNextLevel(uint level)
    {
        var n = level == 0 ? 1u : level;
        var xp = ExpCurveBase * n * Math.Sqrt(n);
        if (xp >= uint.MaxValue)
        {
            return uint.MaxValue;
        }

        return (uint)Math.Round(xp, MidpointRounding.AwayFromZero);
    }

    /// Uses the stronger of floor and party level so a L30 group cannot farm weak early floors.
    /// Party level is capped at 35 so cheat-level characters do not spawn raid bosses.
    public static int EncounterScaleLevel(uint floor, uint partyLevel)
    {
        var stage = floor == 0 ? 1 : (int)floor;
        var level = partyLevel == 0 ? 1 : (int)partyLevel;
        return Math.Max(stage, Math.Clamp(level, 1, 35));
    }

    /// Early HP eased now that Snipe is 10, not 30. Late HP stays high for L30 nukes
    /// (Furioso ~153, Grandshot 100–250, Overthrow 54, Grand Undertaking 50% max HP).
    /// L1 ~52; L30 ~693 before party/pack.
    public static int EnemyHpBaseline(int level)
    {
        var n = Math.Max(1, level);
        return Math.Max(1, (int)Math.Round(45 + (6.0 * n) + (0.52 * n * n), MidpointRounding.AwayFromZero));
    }

    /// Tracks the longer late fights without over-tuning the opener. L1 ~6; L30 ~41.
    public static int EnemyAtkBaseline(int level)
    {
        var n = Math.Max(1, level);
        return Math.Max(1, (int)Math.Round(5 + (0.6 * n) + (0.02 * n * n), MidpointRounding.AwayFromZero));
    }

    /// Light late-game defense so flat nukes chip instead of deleting. 0 until level 10.
    public static int EnemyDefenseBaseline(int level)
    {
        var n = Math.Max(1, level);
        return Math.Max(0, (n - 10) / 5);
    }

    /// EnemyHP = baseline(L) * (P / 3) * (ReferencePackSize / packSize).
    public static int EnemyHpForEncounter(
        uint floor,
        uint partyLevel,
        int playerCount,
        int enemyCount
    )
    {
        var baseline = EnemyHpBaseline(EncounterScaleLevel(floor, partyLevel));
        var p = Math.Max(1, playerCount);
        var fromParty = baseline * (p / 3.0);
        var pack = Math.Clamp(enemyCount, 1, (int)MaxEnemySlots);
        return ClampStat(fromParty * ReferencePackSize / (double)pack, 1);
    }

    /// EnemyATK = baseline(L) * (P / 3).
    public static int EnemyAtkForEncounter(uint floor, uint partyLevel, int playerCount)
    {
        var baseline = EnemyAtkBaseline(EncounterScaleLevel(floor, partyLevel));
        var p = Math.Max(1, playerCount);
        return Math.Max(1, (int)Math.Round(baseline * (p / 3.0), MidpointRounding.AwayFromZero));
    }

    public static int EnemyStrengthForEncounter(uint floor, uint partyLevel)
    {
        var strength = 2 + 0.3 * EncounterScaleLevel(floor, partyLevel);
        return Math.Max(3, ClampStat(strength, 3));
    }

    /// Saturate so huge stages cannot overflow int or produce NaN/Infinity.
    public static int ClampStat(double value, int min)
    {
        if (double.IsNaN(value) || double.IsPositiveInfinity(value) || value >= int.MaxValue)
        {
            return int.MaxValue;
        }

        if (double.IsNegativeInfinity(value) || value <= min)
        {
            return min;
        }

        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    public static WorldBiome BiomeOf(uint stage)
    {
        var n = stage < 1 ? 1u : stage;
        return (WorldBiome)(((n - 1) / BiomeLength) % BiomeCount);
    }

    public static bool IsBossStageNumber(uint stage) =>
        stage > 0 && stage % BossInterval == 0;

    /// Cadence rests and boss clears share this check so a stage that matches
    /// both (for example stage 30) produces one rest stop, not two.
    public static bool ShouldEnterRestStop(uint clearedStage) =>
        (RestStopEvery > 0 && clearedStage % RestStopEvery == 0)
        || IsBossStageNumber(clearedStage);

    /// Each kill grants `25 * stage` EXP to every living party member.
    public static uint KillXp(uint stage) =>
        SaturatingMul(KillExpPerStage, stage == 0 ? 1u : stage);

    public static int ApplyStageScale(int value, uint stage)
    {
        var n = stage < 1 ? 1u : stage;
        long scaled = value;
        for (uint i = 1; i < n; i++)
        {
            scaled = scaled * (100 + StageScalePercent) / 100;
            if (scaled >= int.MaxValue)
            {
                return int.MaxValue;
            }
        }

        return scaled < 1 ? 1 : (int)scaled;
    }

    /// Weighted 1-4. Weights are relative; zeros are skipped.
    public static int RollNonBossPackSize(Random rng)
    {
        var total = 0;
        for (var i = 0; i < PackSizeWeights.Length; i++)
        {
            total += Math.Max(0, PackSizeWeights[i]);
        }

        if (total <= 0 || rng == null)
        {
            return Math.Clamp(ReferencePackSize, 1, (int)MaxEnemySlots);
        }

        var pick = rng.Next(0, total);
        var acc = 0;
        for (var size = 1; size <= PackSizeWeights.Length; size++)
        {
            acc += Math.Max(0, PackSizeWeights[size - 1]);
            if (pick < acc)
            {
                return size;
            }
        }

        return PackSizeWeights.Length;
    }

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

    /// Visual-only labels and sprite packs. Archetype stats and granted SkillDef
    /// ids stay put; attack *display* names come from the visual type below.
    public static readonly string[] EnemyVisualKinds =
    {
        "Witch_Doctor",
        "Canine",
        "Slime",
        "Skeleton",
    };

    public static string EnemyVisualDisplayName(string kind) =>
        kind == "Witch_Doctor" || kind == "Witch Doctor" ? "Witch Doctor" : kind;

    public static string EnemyVisualKindKey(string kind) =>
        kind == "Witch Doctor" ? "Witch_Doctor" : kind;

    /// Display names for the type the player sees. Numbers still come from the
    /// archetype SkillDef granted at spawn.
    public static string EnemyVisualBasicAttack(string kind) =>
        EnemyVisualKindKey(kind) switch
        {
            "Witch_Doctor" => "Staff Strike",
            "Canine" => "Bite",
            "Slime" => "Splat",
            "Skeleton" => "Bone Slash",
            _ => "Strike",
        };

    public static string EnemyVisualSkillName(string kind) =>
        EnemyVisualKindKey(kind) switch
        {
            "Witch_Doctor" => "Hex Bolt",
            "Canine" => "Pounce",
            "Slime" => "Engulf",
            "Skeleton" => "Bone Slash",
            _ => "Strike",
        };

    public static string EnemyLoggedActionName(Entity attacker, string fallback)
    {
        if (attacker.Faction != Team.Enemies || attacker.IsBoss)
        {
            return fallback;
        }

        return EnemyVisualSkillName(attacker.ClassName);
    }

    public static string BossVisualDisplayName(string visualKind) =>
        $"{BossAttributeName} {EnemyVisualDisplayName(visualKind)}";

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
            ItemKind.Amulet => EquipSlot.Amulet,
            _ => EquipSlot.Bag,
        };
}
