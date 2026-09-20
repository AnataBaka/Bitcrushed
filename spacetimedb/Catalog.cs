using System;
using System.Linq;
using SpacetimeDB;

/// Seeds the skill and item catalogs and hands out starting loadouts.
public static partial class Module
{
    public static void SeedCatalog(ReducerContext ctx)
    {
        SeedSkills(ctx);
        SeedItems(ctx);
        SeedBiomeDefs(ctx);
        SeedBossDrops(ctx);
    }

    public const uint FirstBossDropTier = 1;

    public static void EnsureBossDrops(ReducerContext ctx)
    {
        if (ctx.Db.BossDrop.Count > 0)
        {
            return;
        }

        SeedBossDrops(ctx);
    }

    static void SeedBossDrops(ReducerContext ctx)
    {
        foreach (var row in ctx.Db.BossDrop.Iter().ToList())
        {
            ctx.Db.BossDrop.Id.Delete(row.Id);
        }

        foreach (
            var name in new[]
            {
                "Copper Sword",
                "Wooden Bow",
                "Crooked Stick",
                "Sharpened Katana",
                "Health Amulet",
            }
        )
        {
            ctx.Db.BossDrop.Insert(
                new BossDrop
                {
                    Id = 0,
                    Tier = FirstBossDropTier,
                    ItemDefId = RequireItem(ctx, name).Id,
                }
            );
        }
    }

    public static void EnsureBiomeDefs(ReducerContext ctx)
    {
        if (ctx.Db.BiomeDef.Count > 0)
        {
            return;
        }

        SeedBiomeDefs(ctx);
    }

    public static void SeedBiomeDefs(ReducerContext ctx)
    {
        foreach (var row in ctx.Db.BiomeDef.Iter().ToList())
        {
            ctx.Db.BiomeDef.Id.Delete(row.Id);
        }

        AddBiomeDef(
            ctx,
            WorldBiome.Plains,
            "Plains",
            "the Plains",
            92, 132, 72,
            42, 68, 32,
            "",
            255, 255, 255
        );
        AddBiomeDef(
            ctx,
            WorldBiome.Caves,
            "Caves",
            "the Caves",
            48, 42, 62,
            14, 12, 20,
            "Shadow",
            88, 70, 140
        );
        AddBiomeDef(
            ctx,
            WorldBiome.Volcano,
            "Volcano",
            "the Volcano",
            148, 52, 28,
            42, 12, 10,
            "Magma",
            220, 86, 40
        );
        AddBiomeDef(
            ctx,
            WorldBiome.Swamp,
            "Swamp",
            "the Swamp",
            52, 86, 58,
            16, 32, 24,
            "Bog",
            48, 120, 72
        );
        AddBiomeDef(
            ctx,
            WorldBiome.SnowyTundra,
            "Snowy Tundra",
            "the Snowy Tundra",
            186, 208, 226,
            82, 102, 124,
            "Frost",
            168, 214, 236
        );
    }

    static void AddBiomeDef(
        ReducerContext ctx,
        WorldBiome biome,
        string name,
        string theName,
        int topR,
        int topG,
        int topB,
        int botR,
        int botG,
        int botB,
        string variantPrefix,
        int tintR,
        int tintG,
        int tintB
    ) =>
        ctx.Db.BiomeDef.Insert(
            new BiomeDef
            {
                Id = (uint)biome,
                Kind = biome,
                Name = name,
                TheName = theName,
                BackTopR = topR,
                BackTopG = topG,
                BackTopB = topB,
                BackBotR = botR,
                BackBotG = botG,
                BackBotB = botB,
                VariantPrefix = variantPrefix,
                TintR = tintR,
                TintG = tintG,
                TintB = tintB,
            }
        );

    // ----------------------------------------------------------------- skills

    static void SeedSkills(ReducerContext ctx)
    {
        AddPlayerSkill(ctx, SkillNames.Bash, PlayerClass.Knight, 0, 5, 1, DamageType.Physical, 1);
        AddPlayerSkill(ctx, SkillNames.Rush, PlayerClass.Knight, 5, 3, 1, DamageType.Physical, 2);
        AddBuffSkill(ctx, SkillNames.Embolden, PlayerClass.Knight, 10, 3);
        AddBuffSkill(ctx, SkillNames.GallantPride, PlayerClass.Knight, 20, 4);
        AddPlayerSkill(ctx, SkillNames.Cleave, PlayerClass.Knight, 10, 7, 4, DamageType.Physical, 7);
        AddPlayerSkill(ctx, SkillNames.Bludgeon, PlayerClass.Knight, 50, 30, 1, DamageType.Physical, 15);
        AddBuffSkill(ctx, SkillNames.Terrify, PlayerClass.Knight, 20, 20);
        AddPlayerSkill(
            ctx,
            SkillNames.TripleSlash,
            PlayerClass.Knight,
            60,
            12,
            1,
            DamageType.Physical,
            25,
            hitCount: 3
        );
        AddPlayerSkill(
            ctx,
            SkillNames.Furioso,
            PlayerClass.Knight,
            FuriosoManaCost,
            FuriosoBaseDamage,
            1,
            DamageType.Physical,
            30,
            hitCount: FuriosoHitCount
        );

        AddPlayerSkill(ctx, SkillNames.Shoot, PlayerClass.Archer, 0, 4, 1, DamageType.Physical, 1);
        AddBuffSkill(ctx, SkillNames.Restring, PlayerClass.Archer, 10, 2);
        AddBuffSkill(ctx, SkillNames.Scheme, PlayerClass.Archer, 15, 5);
        AddBuffSkill(ctx, SkillNames.Evade, PlayerClass.Archer, 10, 5);
        AddPlayerSkill(
            ctx,
            SkillNames.RainDown,
            PlayerClass.Archer,
            30,
            4,
            4,
            DamageType.Physical,
            7,
            hitCount: 3
        );
        AddPlayerSkill(
            ctx,
            SkillNames.Snipe,
            PlayerClass.Archer,
            SnipeManaCost,
            SnipeDamage,
            1,
            DamageType.Physical,
            SnipeLevelRequired
        );
        AddPlayerSkill(
            ctx,
            SkillNames.CurvedShot,
            PlayerClass.Archer,
            75,
            17,
            4,
            DamageType.Physical,
            18,
            hitCount: 2
        );
        AddPlayerSkill(
            ctx,
            SkillNames.Grandshot,
            PlayerClass.Archer,
            GrandshotManaCost,
            GrandshotBaseDamage,
            1,
            DamageType.Physical,
            GrandshotLevelRequired
        );

        AddPlayerSkill(
            ctx,
            SkillNames.MagicMissile,
            PlayerClass.Mage,
            5,
            10,
            1,
            DamageType.Magical,
            1
        );
        AddPlayerSkill(ctx, SkillNames.Fireball, PlayerClass.Mage, 10, 2, 1, DamageType.Magical, 3);
        AddBuffSkill(ctx, SkillNames.Concentrate, PlayerClass.Mage, 15, 5);
        AddBuffSkill(ctx, SkillNames.Pray, PlayerClass.Mage, 30, 10);
        AddPlayerSkill(
            ctx,
            SkillNames.MagicBullet,
            PlayerClass.Mage,
            20,
            5,
            1,
            DamageType.Magical,
            15
        );
        AddBuffSkill(ctx, SkillNames.GrandUndertaking, PlayerClass.Mage, 100, 30);

        AddPlayerSkill(ctx, SkillNames.Spear, PlayerClass.Ninja, 45, 12, 1, DamageType.Physical, 1);
        AddPlayerSkill(
            ctx,
            SkillNames.VerticalCut,
            PlayerClass.Ninja,
            80,
            27,
            1,
            DamageType.Physical,
            5
        );
        AddBuffSkill(ctx, SkillNames.FocusSpirit, PlayerClass.Ninja, 50, 10);
        AddBuffSkill(ctx, SkillNames.FinishTheJob, PlayerClass.Ninja, 100, 30);
        AddPlayerSkill(
            ctx,
            SkillNames.Overthrow,
            PlayerClass.Ninja,
            OverthrowManaCost,
            OverthrowDamage,
            4,
            DamageType.Physical,
            30
        );

        AddEnemySkill(ctx, "Rusty Slash", 6, 8, 1, DamageType.Physical);
        AddEnemySkill(ctx, "Whirling Rust", 14, 4, 3, DamageType.Physical);
        AddEnemySkill(ctx, "Boulder Smash", 10, 12, 1, DamageType.Physical);
        AddEnemySkill(ctx, "Tremor", 18, 5, 3, DamageType.Physical);
        AddEnemySkill(ctx, "Hex Bolt", 8, 10, 1, DamageType.Magical);
        AddEnemySkill(ctx, "Bite", 5, 7, 1, DamageType.Physical);
        AddEnemySkill(ctx, "Bone Slash", 7, 8, 1, DamageType.Physical);
    }

    public static void EnsureSkillCatalog(ReducerContext ctx)
    {
        foreach (var skill in ctx.Db.SkillDef.Iter().ToList())
        {
            if (skill.Name == SkillNames.Furioso && skill.ManaCost != FuriosoManaCost)
            {
                ctx.Db.SkillDef.Id.Update(skill with { ManaCost = FuriosoManaCost });
            }

            if (
                skill.Name == SkillNames.Grandshot
                && (
                    skill.ManaCost != GrandshotManaCost
                    || skill.LevelRequired != GrandshotLevelRequired
                    || skill.BaseDamage != GrandshotBaseDamage
                )
            )
            {
                ctx.Db.SkillDef.Id.Update(
                    skill with
                    {
                        ManaCost = GrandshotManaCost,
                        LevelRequired = GrandshotLevelRequired,
                        BaseDamage = GrandshotBaseDamage,
                    }
                );
            }

            if (skill.Name == SkillNames.Overthrow && skill.ManaCost != OverthrowManaCost)
            {
                ctx.Db.SkillDef.Id.Update(skill with { ManaCost = OverthrowManaCost });
            }

            if (
                skill.Name == SkillNames.Snipe
                && (
                    skill.LevelRequired != SnipeLevelRequired
                    || skill.BaseDamage != SnipeDamage
                    || skill.ManaCost != SnipeManaCost
                )
            )
            {
                ctx.Db.SkillDef.Id.Update(
                    skill with
                    {
                        LevelRequired = SnipeLevelRequired,
                        BaseDamage = SnipeDamage,
                        ManaCost = SnipeManaCost,
                    }
                );
            }
        }

        foreach (var player in ctx.Db.Player.Iter())
        {
            GrantUnlockedSkills(ctx, player.EntityId, player.Class, player.CharacterLevel);
        }
    }

    public static void EnsureEnemyCatalog(ReducerContext ctx)
    {
        EnsureEnemySkill(ctx, "Hex Bolt", 8, 10, 1, DamageType.Magical);
        EnsureEnemySkill(ctx, "Bite", 5, 7, 1, DamageType.Physical);
        EnsureEnemySkill(ctx, "Bone Slash", 7, 8, 1, DamageType.Physical);
        EnsureEnemySkill(ctx, "Rusty Slash", 6, 8, 1, DamageType.Physical);
        EnsureEnemySkill(ctx, "Boulder Smash", 10, 12, 1, DamageType.Physical);
    }

    static void EnsureEnemySkill(
        ReducerContext ctx,
        string name,
        int manaCost,
        int baseDamage,
        int targetCount,
        DamageType damageType
    )
    {
        foreach (var skill in ctx.Db.SkillDef.Iter())
        {
            if (skill.Name == name)
            {
                return;
            }
        }

        AddEnemySkill(ctx, name, manaCost, baseDamage, targetCount, damageType);
    }

    static void AddPlayerSkill(
        ReducerContext ctx,
        string name,
        PlayerClass forClass,
        int manaCost,
        int baseDamage,
        int targetCount,
        DamageType damageType,
        uint levelRequired,
        int hitCount = 1
    ) =>
        ctx.Db.SkillDef.Insert(
            new SkillDef
            {
                Id = 0,
                Name = name,
                IsEnemySkill = false,
                ForClass = forClass,
                ManaCost = manaCost,
                BaseDamage = baseDamage,
                TargetCount = targetCount,
                AlwaysGoFirst = false,
                NextTurnStrengthBonus = 0,
                DamageType = damageType,
                LevelRequired = levelRequired,
                HitCount = hitCount < 1 ? 1 : hitCount,
            }
        );

    /// Self or party effect with no enemy click.
    static void AddBuffSkill(
        ReducerContext ctx,
        string name,
        PlayerClass forClass,
        int manaCost,
        uint levelRequired
    ) =>
        ctx.Db.SkillDef.Insert(
            new SkillDef
            {
                Id = 0,
                Name = name,
                IsEnemySkill = false,
                ForClass = forClass,
                ManaCost = manaCost,
                BaseDamage = 0,
                TargetCount = 0,
                AlwaysGoFirst = false,
                NextTurnStrengthBonus = 0,
                DamageType = DamageType.Physical,
                LevelRequired = levelRequired,
                HitCount = 1,
            }
        );

    static void AddEnemySkill(
        ReducerContext ctx,
        string name,
        int manaCost,
        int baseDamage,
        int targetCount,
        DamageType damageType
    ) =>
        ctx.Db.SkillDef.Insert(
            new SkillDef
            {
                Id = 0,
                Name = name,
                IsEnemySkill = true,
                ForClass = PlayerClass.Knight,
                ManaCost = manaCost,
                BaseDamage = baseDamage,
                TargetCount = targetCount,
                AlwaysGoFirst = false,
                NextTurnStrengthBonus = 0,
                DamageType = damageType,
                LevelRequired = 1,
                HitCount = 1,
            }
        );

    // ------------------------------------------------------------------ items

    static void SeedItems(ReducerContext ctx)
    {
        AddWeapon(ctx, "Chipped Sword", "CSW", WeaponType.Sword, atk: 12, strength: 1);
        AddWeapon(ctx, "Azure Cane", "WCN", WeaponType.Staff, atk: 6, intelligence: 2);
        AddWeapon(ctx, "Rusted Pommel", "KTN", WeaponType.Katana, atk: 9, speed: 2);
        AddWeapon(ctx, "Golden Bow", "WBW", WeaponType.Bow, atk: 10, dexterity: 2);
        AddWeapon(ctx, "Copper Sword", "CPS", WeaponType.Sword, atk: 18, strength: 2);
        AddWeapon(ctx, "Wooden Bow", "WDB", WeaponType.Bow, atk: 15, dexterity: 3);
        AddWeapon(ctx, "Crooked Stick", "CST", WeaponType.Staff, atk: 9, intelligence: 3);
        AddWeapon(ctx, "Sharpened Katana", "SKT", WeaponType.Katana, atk: 14, speed: 3);
        AddAmulet(ctx, "Health Amulet", "HPA", maxHp: 10);

        AddConsumable(ctx, "Health Potion", "HPT", heal: 30, mana: 0);
        AddConsumable(ctx, "Mana Potion", "MPT", heal: 0, mana: 30);

        SeedAmulets(ctx);
        SeedUniqueWeapons(ctx);
    }

    public static void EnsureItemCatalog(ReducerContext ctx)
    {
        foreach (var name in AllAmuletNames)
        {
            if (FindExistingItem(ctx, name) is not null)
            {
                continue;
            }

            SeedOneAmulet(ctx, name);
        }

        foreach (var name in AllUniqueWeaponNames)
        {
            if (FindExistingItem(ctx, name) is not null)
            {
                continue;
            }

            SeedOneUniqueWeapon(ctx, name);
        }

        RenameStarterWeapon(ctx, "WCN", "Azure Cane", "Wooden Cane");
        RenameStarterWeapon(ctx, "KTN", "Rusted Pommel", "Rusted Katana");
        RenameStarterWeapon(ctx, "WBW", "Golden Bow", "Weathered Bow");

        foreach (var item in ctx.Db.ItemDef.Iter().ToList())
        {
            var description = DescriptionFor(item.Name);
            if (item.Description == description)
            {
                continue;
            }

            ctx.Db.ItemDef.Id.Update(item with { Description = description });
        }
    }

    static void SeedAmulets(ReducerContext ctx)
    {
        foreach (var name in AllAmuletNames)
        {
            SeedOneAmulet(ctx, name);
        }
    }

    static ItemDef? FindExistingItem(ReducerContext ctx, string name)
    {
        foreach (var item in ctx.Db.ItemDef.Iter())
        {
            if (item.Name == name)
            {
                return item;
            }
        }

        return null;
    }

    static void SeedOneAmulet(ReducerContext ctx, string name)
    {
        switch (name)
        {
            case AmuletNames.AmethystSash:
                AddAmulet(ctx, name, "AMS");
                break;
            case AmuletNames.GoldenCross:
                AddAmulet(ctx, name, "GLC", maxHp: 8);
                break;
            case AmuletNames.GuardiansPendant:
                AddAmulet(ctx, name, "GRP");
                break;
            case AmuletNames.CountessNecklace:
                AddAmulet(ctx, name, "CNT");
                break;
            case AmuletNames.EyeOfTheWatcher:
                AddAmulet(ctx, name, "EYE", intelligence: 4);
                break;
            case AmuletNames.SigilOfTheOld:
                AddAmulet(ctx, name, "SIG", strength: 5, dexterity: -2);
                break;
            case AmuletNames.DragonflyCharm:
                AddAmulet(ctx, name, "DFC");
                break;
            case AmuletNames.TwinAmethystCharm:
                AddAmulet(ctx, name, "TAC", intelligence: 5);
                break;
            case AmuletNames.DragonsFire:
                AddAmulet(ctx, name, "DRF");
                break;
            case AmuletNames.EmeraldPendant:
                AddAmulet(ctx, name, "EMP");
                break;
            case AmuletNames.JusticesWings:
                AddAmulet(ctx, name, "JSW", speed: 3);
                break;
            case AmuletNames.HolyGrail:
                AddAmulet(ctx, name, "HGR");
                break;
            case AmuletNames.HiddenDreamcatcher:
                AddAmulet(ctx, name, "HDC");
                break;
            case AmuletNames.RootedBlade:
                AddAmulet(ctx, name, "RTB");
                break;
            case AmuletNames.RedCocoon:
                AddAmulet(ctx, name, "RCC");
                break;
            case AmuletNames.RubyScepter:
                AddAmulet(ctx, name, "RBS");
                break;
        }
    }

    static void SeedUniqueWeapons(ReducerContext ctx)
    {
        foreach (var name in AllUniqueWeaponNames)
        {
            SeedOneUniqueWeapon(ctx, name);
        }
    }

    static void SeedOneUniqueWeapon(ReducerContext ctx, string name)
    {
        switch (name)
        {
            case UniqueWeaponNames.ChippedSword:
                AddWeapon(ctx, name, "CHP", WeaponType.Sword, atk: 12, strength: 1);
                break;
            case UniqueWeaponNames.JaggedSword:
                AddWeapon(ctx, name, "JAG", WeaponType.Sword, atk: 12, strength: 2);
                break;
            case UniqueWeaponNames.CrimsonBlade:
                AddWeapon(ctx, name, "CRB", WeaponType.Sword, atk: 12, strength: 3);
                break;
            case UniqueWeaponNames.GoldenBow:
                AddWeapon(ctx, name, "GLB", WeaponType.Bow, atk: 10, dexterity: 1);
                break;
            case UniqueWeaponNames.EmeraldBow:
                AddWeapon(ctx, name, "EMB", WeaponType.Bow, atk: 10, dexterity: 2);
                break;
            case UniqueWeaponNames.CrimsonBow:
                AddWeapon(ctx, name, "CRW", WeaponType.Bow, atk: 10, dexterity: 3);
                break;
            case UniqueWeaponNames.AzureCane:
                AddWeapon(ctx, name, "AZC", WeaponType.Staff, atk: 6, intelligence: 1);
                break;
            case UniqueWeaponNames.ElegantCane:
                AddWeapon(ctx, name, "ELC", WeaponType.Staff, atk: 6, intelligence: 2);
                break;
            case UniqueWeaponNames.StaffOfTheQueen:
                AddWeapon(ctx, name, "SOQ", WeaponType.Staff, atk: 6, intelligence: 3);
                break;
            case UniqueWeaponNames.RustedPummel:
                AddWeapon(ctx, name, "RPM", WeaponType.Katana, atk: 9, speed: 1);
                break;
            case UniqueWeaponNames.CrimsonDagger:
                AddWeapon(ctx, name, "CRD", WeaponType.Katana, atk: 9, speed: 2);
                break;
            case UniqueWeaponNames.AzureDagger:
                AddWeapon(ctx, name, "AZD", WeaponType.Katana, atk: 9, speed: 3);
                break;
        }
    }

    static void AddWeapon(
        ReducerContext ctx,
        string name,
        string shortName,
        WeaponType weaponType,
        int atk,
        int strength = 0,
        int dexterity = 0,
        int intelligence = 0,
        int speed = 0
    ) =>
        ctx.Db.ItemDef.Insert(
            new ItemDef
            {
                Id = 0,
                Name = name,
                ShortName = shortName,
                Kind = ItemKind.Weapon,
                WeaponType = weaponType,
                AtkBonus = atk,
                DefenseBonus = 0,
                StrengthBonus = strength,
                DexterityBonus = dexterity,
                IntelligenceBonus = intelligence,
                SpeedBonus = speed,
                MaxHpBonus = 0,
                MaxManaBonus = 0,
                HealAmount = 0,
                ManaRestoreAmount = 0,
                Description = DescriptionFor(name),
            }
        );

    static void AddAmulet(ReducerContext ctx, string name, string shortName, int maxHp) =>
        ctx.Db.ItemDef.Insert(
            new ItemDef
            {
                Id = 0,
                Name = name,
                ShortName = shortName,
                Kind = ItemKind.Amulet,
                WeaponType = WeaponType.None,
                AtkBonus = 0,
                DefenseBonus = 0,
                StrengthBonus = 0,
                DexterityBonus = 0,
                IntelligenceBonus = 0,
                SpeedBonus = 0,
                MaxHpBonus = maxHp,
                MaxManaBonus = 0,
                HealAmount = 0,
                ManaRestoreAmount = 0,
                Description = DescriptionFor(name),
            }
        );

    static void AddConsumable(
        ReducerContext ctx,
        string name,
        string shortName,
        int heal,
        int mana
    ) =>
        ctx.Db.ItemDef.Insert(
            new ItemDef
            {
                Id = 0,
                Name = name,
                ShortName = shortName,
                Kind = ItemKind.Consumable,
                WeaponType = WeaponType.None,
                AtkBonus = 0,
                DefenseBonus = 0,
                StrengthBonus = 0,
                DexterityBonus = 0,
                IntelligenceBonus = 0,
                SpeedBonus = 0,
                MaxHpBonus = 0,
                MaxManaBonus = 0,
                HealAmount = heal,
                ManaRestoreAmount = mana,
                Description = DescriptionFor(name),
            }
        );

    static void AddAmulet(
        ReducerContext ctx,
        string name,
        string shortName,
        int strength = 0,
        int dexterity = 0,
        int intelligence = 0,
        int speed = 0,
        int maxHp = 0
    ) =>
        ctx.Db.ItemDef.Insert(
            new ItemDef
            {
                Id = 0,
                Name = name,
                ShortName = shortName,
                Kind = ItemKind.Amulet,
                WeaponType = WeaponType.None,
                AtkBonus = 0,
                DefenseBonus = 0,
                StrengthBonus = strength,
                DexterityBonus = dexterity,
                IntelligenceBonus = intelligence,
                SpeedBonus = speed,
                MaxHpBonus = maxHp,
                MaxManaBonus = 0,
                HealAmount = 0,
                ManaRestoreAmount = 0,
                Description = DescriptionFor(name),
            }
        );

    static string DescriptionFor(string name) =>
        name switch
        {
            AmuletNames.AmethystSash => "Focus restores 50 mana.",
            AmuletNames.GuardiansPendant => "Reduces incoming damage by 7%.",
            AmuletNames.CountessNecklace =>
                "Heal 2 HP whenever you deal attack damage. Multi-hits heal multiple times.",
            AmuletNames.DragonflyCharm => "Double Strength for one turn after an ally dies.",
            AmuletNames.DragonsFire => "Burn cap is increased to 30.",
            AmuletNames.EmeraldPendant => "Spells cost 5 less mana.",
            AmuletNames.HolyGrail => "Heal 5 HP on kill.",
            AmuletNames.HiddenDreamcatcher =>
                "Dodge chance is increased by 5%. Can exceed the Archer dodge cap of 50%.",
            AmuletNames.RootedBlade => "Each hit reduces the target's Speed by 1 next turn.",
            AmuletNames.RedCocoon => "When you take damage, deal 1 damage back to the attacker.",
            AmuletNames.RubyScepter => "Burn spells gain +3 base power.",
            _ => "",
        };

    // -------------------------------------------------------------- loadouts

    public static ItemDef RequireItem(ReducerContext ctx, string name)
    {
        foreach (var item in ctx.Db.ItemDef.Iter())
        {
            if (item.Name == name)
            {
                return item;
            }
        }

        throw new Exception($"Item catalog is missing {name}.");
    }

    static ItemDef RequireItemByShortName(ReducerContext ctx, string shortName)
    {
        foreach (var item in ctx.Db.ItemDef.Iter())
        {
            if (item.ShortName == shortName)
            {
                return item;
            }
        }

        throw new Exception($"Item catalog is missing short name {shortName}.");
    }

    static void RenameStarterWeapon(ReducerContext ctx, string shortName, string newName, string oldName)
    {
        foreach (var item in ctx.Db.ItemDef.Iter().ToList())
        {
            if (item.ShortName != shortName)
            {
                continue;
            }

            if (item.Name != newName)
            {
                ctx.Db.ItemDef.Id.Update(item with { Name = newName });
            }

            _ = oldName;
            return;
        }
    }

    static SkillDef RequireSkill(ReducerContext ctx, string name)
    {
        foreach (var skill in ctx.Db.SkillDef.Iter())
        {
            if (skill.Name == name)
            {
                return skill;
            }
        }

        throw new Exception($"Skill catalog is missing {name}.");
    }

    /// Class base weapon worn, amulet empty, 3x3 inventory empty.
    /// Potions stay in the action-menu bag and are not part of inventory.
    public static void GiveStartingLoadout(
        ReducerContext ctx,
        Identity owner,
        Entity entity,
        PlayerClass playerClass
    )
    {
        GiveStartingWeapon(ctx, owner, playerClass);

        GiveToBag(ctx, owner, RequireItem(ctx, "Health Potion").Id, 3);
        GiveToBag(ctx, owner, RequireItem(ctx, "Mana Potion").Id, 2);

        if (SeedTestItems)
        {
            TryAddItemToInventory(ctx, owner, RequireItem(ctx, "Health Amulet").Id);
            TryAddItemToInventory(ctx, owner, RequireItemByShortName(ctx, "CSW").Id);
            TryAddItemToInventory(ctx, owner, RequireItemByShortName(ctx, "WBW").Id);
            TryAddItemToInventory(ctx, owner, RequireItemByShortName(ctx, "WCN").Id);
            TryAddItemToInventory(ctx, owner, RequireItemByShortName(ctx, "KTN").Id);
        }

        GrantUnlockedSkills(ctx, entity.EntityId, playerClass, 1);
        RecomputeStats(ctx, owner);
    }

    static void EquipFresh(ReducerContext ctx, Identity owner, ItemDef item) =>
        ctx.Db.PlayerItem.Insert(
            new PlayerItem
            {
                Id = 0,
                Owner = owner,
                ItemDefId = item.Id,
                Quantity = 1,
                EquippedSlot = SlotFor(item),
                InventoryIndex = InventoryNone,
            }
        );

    public static void GiveToBag(ReducerContext ctx, Identity owner, uint itemDefId, int quantity)
    {
        if (BagCount(ctx, owner) >= BagCapacity)
        {
            throw new Exception("Bag is full.");
        }

        ctx.Db.PlayerItem.Insert(
            new PlayerItem
            {
                Id = 0,
                Owner = owner,
                ItemDefId = itemDefId,
                Quantity = quantity,
                EquippedSlot = EquipSlot.Bag,
                InventoryIndex = InventoryNone,
            }
        );
    }

    /// First free 3x3 cell. Not a reducer; future drops call this. Clients cannot.
    static bool TryAddItemToInventory(ReducerContext ctx, Identity owner, uint itemDefId)
    {
        var index = FirstFreeInventoryIndex(ctx, owner);
        if (index < 0)
        {
            return false;
        }

        ctx.Db.PlayerItem.Insert(
            new PlayerItem
            {
                Id = 0,
                Owner = owner,
                ItemDefId = itemDefId,
                Quantity = 1,
                EquippedSlot = EquipSlot.Inventory,
                InventoryIndex = (uint)index,
            }
        );
        return true;
    }

    static bool TryPlaceExistingItemInInventory(ReducerContext ctx, PlayerItem instance)
    {
        var index = FirstFreeInventoryIndex(ctx, instance.Owner);
        if (index < 0)
        {
            return false;
        }

        ctx.Db.PlayerItem.Id.Update(
            instance with
            {
                EquippedSlot = EquipSlot.Inventory,
                InventoryIndex = (uint)index,
            }
        );
        return true;
    }

    static int FirstFreeInventoryIndex(ReducerContext ctx, Identity owner)
    {
        var taken = new bool[InventoryCapacity];
        foreach (var item in ctx.Db.PlayerItem.Owner.Filter(owner))
        {
            if (item.EquippedSlot != EquipSlot.Inventory || item.InventoryIndex >= InventoryCapacity)
            {
                continue;
            }

            taken[item.InventoryIndex] = true;
        }

        for (var i = 0; i < InventoryCapacity; i++)
        {
            if (!taken[i])
            {
                return i;
            }
        }

        return -1;
    }

    public static int BagCount(ReducerContext ctx, Identity owner) =>
        ctx.Db.PlayerItem.Owner.Filter(owner).Count(i => i.EquippedSlot == EquipSlot.Bag);

    public static void GrantUnlockedSkills(
        ReducerContext ctx,
        ulong entityId,
        PlayerClass playerClass,
        uint characterLevel
    )
    {
        var level = characterLevel == 0 ? 1u : characterLevel;
        foreach (var skill in ctx.Db.SkillDef.Iter())
        {
            if (skill.IsEnemySkill || skill.ForClass != playerClass)
            {
                continue;
            }

            var required = skill.LevelRequired == 0 ? 1u : skill.LevelRequired;
            if (required > level || KnowsSkill(ctx, entityId, skill.Id))
            {
                continue;
            }

            ctx.Db.EntitySkill.Insert(
                new EntitySkill
                {
                    Id = 0,
                    EntityId = entityId,
                    SkillDefId = skill.Id,
                }
            );
        }
    }

    public static void GrantSkillsByName(ReducerContext ctx, ulong entityId, string[] names)
    {
        foreach (var name in names)
        {
            ctx.Db.EntitySkill.Insert(
                new EntitySkill
                {
                    Id = 0,
                    EntityId = entityId,
                    SkillDefId = RequireSkill(ctx, name).Id,
                }
            );
        }
    }
}
