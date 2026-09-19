using SpacetimeDB;

public static partial class Module
{
    public static void SeedCatalog(ReducerContext ctx)
    {
        SeedSkills(ctx);
        SeedItems(ctx);
    }

    static void SeedSkills(ReducerContext ctx)
    {
        InsertPlayerSkill(ctx, "Bash", PlayerClass.Warrior, 8, 5, 1, false, 0, 0, DamageType.Physical);
        InsertPlayerSkill(ctx, "Rush", PlayerClass.Warrior, 10, 3, 1, true, 0, 0, DamageType.Physical);
        InsertPlayerSkill(ctx, "Enrage", PlayerClass.Warrior, 12, 0, 0, false, 2, 0, DamageType.Physical);
        InsertPlayerSkill(ctx, "Cleave", PlayerClass.Warrior, 14, 7, 3, false, 0, 0, DamageType.Physical);

        InsertPlayerSkill(ctx, "Aimed Shot", PlayerClass.Archer, 8, 5, 1, false, 0, 0, DamageType.Physical);
        InsertPlayerSkill(ctx, "Quickdraw", PlayerClass.Archer, 10, 3, 1, true, 0, 0, DamageType.Physical);
        InsertPlayerSkill(ctx, "Rain of Arrows", PlayerClass.Archer, 14, 4, 3, false, 0, 0, DamageType.Physical);

        InsertPlayerSkill(ctx, "Spark", PlayerClass.Mage, 12, 5, 1, false, 0, 0, DamageType.Magical);
        InsertPlayerSkill(ctx, "Arcane Pulse", PlayerClass.Mage, 16, 8, 1, false, 0, 0, DamageType.Magical);
        InsertPlayerSkill(ctx, "Frost Nova", PlayerClass.Mage, 18, 4, 3, false, 0, 0, DamageType.Magical);

        InsertPlayerSkill(ctx, "Stab", PlayerClass.Rogue, 8, 5, 1, false, 0, 0, DamageType.Physical);
        InsertPlayerSkill(ctx, "Ambush", PlayerClass.Rogue, 10, 3, 1, true, 0, 0, DamageType.Physical);
        InsertPlayerSkill(ctx, "Fan of Knives", PlayerClass.Rogue, 14, 4, 3, false, 0, 0, DamageType.Physical);

        InsertEnemySkill(ctx, "Rusty Slash", 6, 8, 1, 0, DamageType.Physical);
        InsertEnemySkill(ctx, "Howl", 5, 0, 0, 2, DamageType.Physical);
        InsertEnemySkill(ctx, "Boulder Smash", 10, 12, 1, 0, DamageType.Physical);
        InsertEnemySkill(ctx, "Shadow Volley", 8, 6, 3, 0, DamageType.Magical);
    }

    static void SeedItems(ReducerContext ctx)
    {
        InsertItem(
            ctx,
            "Starter Sword",
            ItemKind.Weapon,
            WeaponType.Sword,
            ArmorSlot.None,
            atk: 6,
            str: 1,
            sprite: 1
        );
        InsertItem(
            ctx,
            "Starter Bow",
            ItemKind.Weapon,
            WeaponType.Bow,
            ArmorSlot.None,
            atk: 5,
            dex: 1,
            sprite: 2
        );
        InsertItem(
            ctx,
            "Starter Staff",
            ItemKind.Weapon,
            WeaponType.Staff,
            ArmorSlot.None,
            atk: 4,
            intel: 1,
            sprite: 3
        );
        InsertItem(
            ctx,
            "Starter Dagger",
            ItemKind.Weapon,
            WeaponType.Dagger,
            ArmorSlot.None,
            atk: 5,
            spd: 1,
            sprite: 4
        );
        InsertItem(
            ctx,
            "Leather Helm",
            ItemKind.Armor,
            WeaponType.None,
            ArmorSlot.Helmet,
            maxHp: 6,
            sprite: 12
        );
        InsertItem(
            ctx,
            "Leather Vest",
            ItemKind.Armor,
            WeaponType.None,
            ArmorSlot.Chestplate,
            maxHp: 10,
            sprite: 10
        );
        InsertItem(
            ctx,
            "Leather Leggings",
            ItemKind.Armor,
            WeaponType.None,
            ArmorSlot.Leggings,
            maxHp: 6,
            spd: 1,
            sprite: 13
        );
        InsertItem(
            ctx,
            "Leather Boots",
            ItemKind.Armor,
            WeaponType.None,
            ArmorSlot.Boots,
            spd: 1,
            sprite: 14
        );
        InsertItem(
            ctx,
            "Mage Robes",
            ItemKind.Armor,
            WeaponType.None,
            ArmorSlot.Chestplate,
            intel: 1,
            maxMp: 20,
            sprite: 11
        );
        InsertItem(
            ctx,
            "Health Potion",
            ItemKind.Consumable,
            WeaponType.None,
            ArmorSlot.None,
            heal: 30,
            sprite: 20
        );
        InsertItem(
            ctx,
            "Mana Potion",
            ItemKind.Consumable,
            WeaponType.None,
            ArmorSlot.None,
            mana: 40,
            sprite: 21
        );
    }

    static void InsertPlayerSkill(
        ReducerContext ctx,
        string name,
        PlayerClass classChoice,
        uint manaCost,
        uint baseDamage,
        uint targetCount,
        bool alwaysGoFirst,
        uint nextTurnStrengthBonus,
        uint nextTurnSpeedOverride,
        DamageType damageType
    )
    {
        ctx.Db.SkillDef.Insert(
            new SkillDef
            {
                Id = 0,
                Name = name,
                IsEnemySkill = false,
                Class = classChoice,
                UnlockFloor = 0,
                ManaCost = manaCost,
                BaseDamage = baseDamage,
                TargetCount = targetCount,
                AlwaysGoFirst = alwaysGoFirst,
                NextTurnStrengthBonus = nextTurnStrengthBonus,
                NextTurnSpeedOverride = nextTurnSpeedOverride,
                DamageType = damageType,
            }
        );
    }

    static void InsertEnemySkill(
        ReducerContext ctx,
        string name,
        uint manaCost,
        uint baseDamage,
        uint targetCount,
        uint nextTurnStrengthBonus,
        DamageType damageType
    )
    {
        ctx.Db.SkillDef.Insert(
            new SkillDef
            {
                Id = 0,
                Name = name,
                IsEnemySkill = true,
                Class = PlayerClass.Warrior,
                UnlockFloor = 0,
                ManaCost = manaCost,
                BaseDamage = baseDamage,
                TargetCount = targetCount,
                AlwaysGoFirst = false,
                NextTurnStrengthBonus = nextTurnStrengthBonus,
                NextTurnSpeedOverride = 0,
                DamageType = damageType,
            }
        );
    }

    static void InsertItem(
        ReducerContext ctx,
        string name,
        ItemKind kind,
        WeaponType weaponType,
        ArmorSlot armorSlot,
        uint atk = 0,
        uint str = 0,
        uint dex = 0,
        uint intel = 0,
        uint spd = 0,
        uint maxHp = 0,
        uint maxMp = 0,
        uint heal = 0,
        uint mana = 0,
        uint sprite = 0
    )
    {
        ctx.Db.ItemDef.Insert(
            new ItemDef
            {
                Id = 0,
                Name = name,
                Kind = kind,
                WeaponType = weaponType,
                ArmorSlot = armorSlot,
                AtkBonus = atk,
                StrengthBonus = str,
                DexterityBonus = dex,
                IntelligenceBonus = intel,
                SpeedBonus = spd,
                MaxHealthBonus = maxHp,
                MaxManaBonus = maxMp,
                HealAmount = heal,
                ManaRestoreAmount = mana,
                SpriteId = sprite,
            }
        );
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
}
