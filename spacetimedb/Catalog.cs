using SpacetimeDB;

public static partial class Module
{
    public static void SeedCatalog(ReducerContext ctx)
    {
        SeedSkills(ctx);
        SeedItems(ctx);
    }

    private static void SeedSkills(ReducerContext ctx)
    {
        // Warrior: first three are starters. One more skill every 10 floors.
        InsertPlayerSkill(ctx, "Bash", PlayerClass.Warrior, 0, 8, 5, 1, false, 0, 0, DamageType.Physical);
        InsertPlayerSkill(ctx, "Rush", PlayerClass.Warrior, 0, 10, 3, 1, true, 0, 0, DamageType.Physical);
        InsertPlayerSkill(ctx, "Enrage", PlayerClass.Warrior, 0, 12, 0, 0, false, 2, 0, DamageType.Physical);
        InsertPlayerSkill(ctx, "Berserker Rage", PlayerClass.Warrior, 10, 18, 0, 0, false, 4, 1, DamageType.Physical);
        InsertPlayerSkill(ctx, "Cleave", PlayerClass.Warrior, 20, 14, 7, 3, false, 0, 0, DamageType.Physical);
        InsertPlayerSkill(ctx, "Bludgeon", PlayerClass.Warrior, 30, 20, 30, 1, false, 0, 0, DamageType.Physical);

        InsertPlayerSkill(ctx, "Aimed Shot", PlayerClass.Archer, 0, 8, 5, 1, false, 0, 0, DamageType.Physical);
        InsertPlayerSkill(ctx, "Quickdraw", PlayerClass.Archer, 0, 10, 3, 1, true, 0, 0, DamageType.Physical);
        InsertPlayerSkill(ctx, "Rain of Arrows", PlayerClass.Archer, 0, 14, 4, 3, false, 0, 0, DamageType.Physical);
        InsertPlayerSkill(ctx, "Piercing Shot", PlayerClass.Archer, 10, 12, 12, 1, false, 0, 0, DamageType.Physical);
        InsertPlayerSkill(ctx, "Volley", PlayerClass.Archer, 20, 16, 8, 3, false, 0, 0, DamageType.Physical);
        InsertPlayerSkill(ctx, "Snipe", PlayerClass.Archer, 30, 22, 28, 1, false, 0, 0, DamageType.Physical);

        InsertPlayerSkill(ctx, "Spark", PlayerClass.Mage, 0, 12, 5, 1, false, 0, 0, DamageType.Magical);
        InsertPlayerSkill(ctx, "Arcane Pulse", PlayerClass.Mage, 0, 16, 8, 1, false, 0, 0, DamageType.Magical);
        InsertPlayerSkill(ctx, "Frost Nova", PlayerClass.Mage, 0, 18, 4, 3, false, 0, 0, DamageType.Magical);
        InsertPlayerSkill(ctx, "Fireball", PlayerClass.Mage, 10, 20, 14, 1, false, 0, 0, DamageType.Magical);
        InsertPlayerSkill(ctx, "Blizzard", PlayerClass.Mage, 20, 22, 8, 3, false, 0, 0, DamageType.Magical);
        InsertPlayerSkill(ctx, "Meteor", PlayerClass.Mage, 30, 28, 32, 1, false, 0, 0, DamageType.Magical);

        InsertPlayerSkill(ctx, "Stab", PlayerClass.Rogue, 0, 8, 5, 1, false, 0, 0, DamageType.Physical);
        InsertPlayerSkill(ctx, "Ambush", PlayerClass.Rogue, 0, 10, 3, 1, true, 0, 0, DamageType.Physical);
        InsertPlayerSkill(ctx, "Fan of Knives", PlayerClass.Rogue, 0, 14, 4, 3, false, 0, 0, DamageType.Physical);
        InsertPlayerSkill(ctx, "Backstab", PlayerClass.Rogue, 10, 12, 12, 1, false, 0, 0, DamageType.Physical);
        InsertPlayerSkill(ctx, "Shadowstep", PlayerClass.Rogue, 20, 16, 8, 1, true, 0, 0, DamageType.Physical);
        InsertPlayerSkill(ctx, "Assassinate", PlayerClass.Rogue, 30, 22, 28, 1, false, 0, 0, DamageType.Physical);

        InsertEnemySkill(ctx, "Strike", Biome.Forest, 0, 4, 1, DamageType.Physical);
        InsertEnemySkill(ctx, "Howl", Biome.Forest, 5, 0, 0, DamageType.Physical);
        InsertEnemySkill(ctx, "Tidal Slash", Biome.Ocean, 0, 6, 1, DamageType.Physical);
        InsertEnemySkill(ctx, "Riptide", Biome.Ocean, 8, 5, 3, DamageType.Magical);
        InsertEnemySkill(ctx, "Hellfire", Biome.Hell, 0, 7, 1, DamageType.Magical);
        InsertEnemySkill(ctx, "Infernal Burst", Biome.Hell, 10, 6, 3, DamageType.Magical);
        InsertEnemySkill(ctx, "Crushing Blow", Biome.Forest, 0, 15, 1, DamageType.Physical);
    }

    private static void SeedItems(ReducerContext ctx)
    {
        ctx.Db.ItemDef.Insert(new ItemDef
        {
            Id = 0,
            Name = "Starter Sword",
            Kind = ItemKind.Weapon,
            WeaponType = WeaponType.Sword,
            ArmorSlot = ArmorSlot.None,
            AtkBonus = 6,
            StrengthBonus = 1,
            SpriteId = 1,
        });
        ctx.Db.ItemDef.Insert(new ItemDef
        {
            Id = 0,
            Name = "Starter Bow",
            Kind = ItemKind.Weapon,
            WeaponType = WeaponType.Bow,
            ArmorSlot = ArmorSlot.None,
            AtkBonus = 5,
            DexterityBonus = 1,
            SpriteId = 2,
        });
        ctx.Db.ItemDef.Insert(new ItemDef
        {
            Id = 0,
            Name = "Starter Staff",
            Kind = ItemKind.Weapon,
            WeaponType = WeaponType.Staff,
            ArmorSlot = ArmorSlot.None,
            AtkBonus = 4,
            IntelligenceBonus = 1,
            SpriteId = 3,
        });
        ctx.Db.ItemDef.Insert(new ItemDef
        {
            Id = 0,
            Name = "Starter Dagger",
            Kind = ItemKind.Weapon,
            WeaponType = WeaponType.Dagger,
            ArmorSlot = ArmorSlot.None,
            AtkBonus = 5,
            SpeedBonus = 1,
            SpriteId = 4,
        });
        ctx.Db.ItemDef.Insert(new ItemDef
        {
            Id = 0,
            Name = "Leather Helm",
            Kind = ItemKind.Armor,
            WeaponType = WeaponType.None,
            ArmorSlot = ArmorSlot.Helmet,
            MaxHealthBonus = 6,
            SpriteId = 12,
        });
        ctx.Db.ItemDef.Insert(new ItemDef
        {
            Id = 0,
            Name = "Leather Vest",
            Kind = ItemKind.Armor,
            WeaponType = WeaponType.None,
            ArmorSlot = ArmorSlot.Chestplate,
            MaxHealthBonus = 10,
            SpriteId = 10,
        });
        ctx.Db.ItemDef.Insert(new ItemDef
        {
            Id = 0,
            Name = "Leather Leggings",
            Kind = ItemKind.Armor,
            WeaponType = WeaponType.None,
            ArmorSlot = ArmorSlot.Leggings,
            MaxHealthBonus = 6,
            SpeedBonus = 1,
            SpriteId = 13,
        });
        ctx.Db.ItemDef.Insert(new ItemDef
        {
            Id = 0,
            Name = "Leather Boots",
            Kind = ItemKind.Armor,
            WeaponType = WeaponType.None,
            ArmorSlot = ArmorSlot.Boots,
            SpeedBonus = 1,
            SpriteId = 14,
        });
        ctx.Db.ItemDef.Insert(new ItemDef
        {
            Id = 0,
            Name = "Mage Robes",
            Kind = ItemKind.Armor,
            WeaponType = WeaponType.None,
            ArmorSlot = ArmorSlot.Chestplate,
            MaxManaBonus = 20,
            IntelligenceBonus = 1,
            SpriteId = 11,
        });
        ctx.Db.ItemDef.Insert(new ItemDef
        {
            Id = 0,
            Name = "Health Potion",
            Kind = ItemKind.Consumable,
            WeaponType = WeaponType.None,
            ArmorSlot = ArmorSlot.None,
            HealAmount = 30,
            SpriteId = 20,
        });
        ctx.Db.ItemDef.Insert(new ItemDef
        {
            Id = 0,
            Name = "Mana Potion",
            Kind = ItemKind.Consumable,
            WeaponType = WeaponType.None,
            ArmorSlot = ArmorSlot.None,
            ManaRestoreAmount = 40,
            SpriteId = 21,
        });
    }

    private static void InsertPlayerSkill(
        ReducerContext ctx,
        string name,
        PlayerClass classChoice,
        uint unlockFloor,
        uint manaCost,
        uint baseDamage,
        uint targetCount,
        bool alwaysGoFirst,
        uint nextTurnStrengthBonus,
        uint nextTurnSpeedOverride,
        DamageType damageType)
    {
        ctx.Db.SkillDef.Insert(new SkillDef
        {
            Id = 0,
            Name = name,
            IsEnemySkill = false,
            Class = classChoice,
            Biome = Biome.Forest,
            UnlockFloor = unlockFloor,
            ManaCost = manaCost,
            BaseDamage = baseDamage,
            TargetCount = targetCount,
            AlwaysGoFirst = alwaysGoFirst,
            NextTurnStrengthBonus = nextTurnStrengthBonus,
            NextTurnSpeedOverride = nextTurnSpeedOverride,
            DamageType = damageType,
        });
    }

    private static void InsertEnemySkill(
        ReducerContext ctx,
        string name,
        Biome biome,
        uint manaCost,
        uint baseDamage,
        uint targetCount,
        DamageType damageType)
    {
        ctx.Db.SkillDef.Insert(new SkillDef
        {
            Id = 0,
            Name = name,
            IsEnemySkill = true,
            Class = PlayerClass.Warrior,
            Biome = biome,
            UnlockFloor = 0,
            ManaCost = manaCost,
            BaseDamage = baseDamage,
            TargetCount = targetCount,
            AlwaysGoFirst = false,
            NextTurnStrengthBonus = name == "Howl" ? 2u : 0u,
            NextTurnSpeedOverride = 0,
            DamageType = damageType,
        });
    }

    public static ItemDef RequireItemDefByName(ReducerContext ctx, string name)
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

    public static SkillDef? FindSkillDefByName(ReducerContext ctx, string name)
    {
        foreach (var skill in ctx.Db.SkillDef.Iter())
        {
            if (skill.Name == name)
            {
                return skill;
            }
        }

        return null;
    }

    public static string RandomPlayerName(Random rng, PlayerClass classChoice)
    {
        return classChoice switch
        {
            PlayerClass.Warrior => Pick(rng, new[] { "Brann", "Gareth", "Ingrid", "Rurik", "Helga" }),
            PlayerClass.Archer => Pick(rng, new[] { "Lyra", "Flint", "Sable", "Quinn", "Ash" }),
            PlayerClass.Mage => Pick(rng, new[] { "Aldric", "Nyx", "Vesper", "Elowen", "Orin" }),
            PlayerClass.Rogue => Pick(rng, new[] { "Kade", "Vex", "Rina", "Shade", "Nix" }),
            _ => "Adventurer",
        };
    }

    public static string RandomEnemyName(Random rng, Biome biome, bool boss)
    {
        if (boss)
        {
            return biome switch
            {
                Biome.Ocean => Pick(rng, new[] { "Leviathan", "Tide Tyrant", "Abyssal Queen" }),
                Biome.Hell => Pick(rng, new[] { "Demon Lord", "Ash Sovereign", "Infernal Warden" }),
                _ => Pick(rng, new[] { "Forest Alpha", "Elder Treant", "Grove Tyrant" }),
            };
        }

        return biome switch
        {
            Biome.Ocean => Pick(rng, new[] { "Shark", "Drowned", "Crab", "Serpent", "Siren" }),
            Biome.Hell => Pick(rng, new[] { "Imp", "Hellhound", "Wraith", "Cultist", "Ashling" }),
            _ => Pick(rng, new[] { "Wolf", "Bandit", "Boar", "Treant", "Wisp" }),
        };
    }
}
