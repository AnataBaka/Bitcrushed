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
    }

    // ----------------------------------------------------------------- skills

    static void SeedSkills(ReducerContext ctx)
    {
        // Every class gets one heavy single target hit, one area attack and one
        // cheap trick, so the spell menu is worth opening for all four rolls.
        AddPlayerSkill(ctx, "Cleaving Strike", PlayerClass.Warrior, 5, 10, 1, DamageType.Physical);
        AddPlayerSkill(ctx, "Cleave", PlayerClass.Warrior, 14, 7, 3, DamageType.Physical);
        AddBuffSkill(ctx, "Enrage", PlayerClass.Warrior, 12, strengthBonus: 4);

        AddPlayerSkill(ctx, "Fireball", PlayerClass.Mage, 18, 25, 1, DamageType.Magical);
        AddPlayerSkill(ctx, "Frost Nova", PlayerClass.Mage, 16, 10, 3, DamageType.Magical);
        AddPlayerSkill(ctx, "Arcane Pulse", PlayerClass.Mage, 10, 12, 1, DamageType.Magical);

        AddPlayerSkill(ctx, "Backstab", PlayerClass.Rogue, 10, 14, 1, DamageType.Physical);
        AddPlayerSkill(ctx, "Fan of Knives", PlayerClass.Rogue, 13, 8, 3, DamageType.Physical);
        AddRushSkill(ctx, "Ambush", PlayerClass.Rogue, 8, 6);

        AddPlayerSkill(ctx, "Piercing Arrow", PlayerClass.Archer, 9, 13, 1, DamageType.Physical);
        AddPlayerSkill(ctx, "Rain of Arrows", PlayerClass.Archer, 13, 8, 3, DamageType.Physical);
        AddRushSkill(ctx, "Quickdraw", PlayerClass.Archer, 8, 5);

        AddEnemySkill(ctx, "Rusty Slash", 6, 8, 1, DamageType.Physical);
        AddEnemySkill(ctx, "Whirling Rust", 14, 4, 3, DamageType.Physical);
        AddEnemySkill(ctx, "Boulder Smash", 10, 12, 1, DamageType.Physical);
        AddEnemySkill(ctx, "Tremor", 18, 5, 3, DamageType.Physical);
        AddEnemySkill(ctx, "Hex Bolt", 8, 10, 1, DamageType.Magical);
        AddEnemySkill(ctx, "Bite", 5, 7, 1, DamageType.Physical);
        AddEnemySkill(ctx, "Bone Slash", 7, 8, 1, DamageType.Physical);
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
        DamageType damageType
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
            }
        );

    /// Deals no damage; buys a Strength spike on the caster's next turn.
    static void AddBuffSkill(
        ReducerContext ctx,
        string name,
        PlayerClass forClass,
        int manaCost,
        int strengthBonus
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
                NextTurnStrengthBonus = strengthBonus,
                DamageType = DamageType.Physical,
            }
        );

    /// Light hit that also jumps the caster to the front of the next round.
    static void AddRushSkill(
        ReducerContext ctx,
        string name,
        PlayerClass forClass,
        int manaCost,
        int baseDamage
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
                TargetCount = 1,
                AlwaysGoFirst = true,
                NextTurnStrengthBonus = 0,
                DamageType = DamageType.Physical,
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
                ForClass = PlayerClass.Warrior,
                ManaCost = manaCost,
                BaseDamage = baseDamage,
                TargetCount = targetCount,
                AlwaysGoFirst = false,
                NextTurnStrengthBonus = 0,
                DamageType = damageType,
            }
        );

    // ------------------------------------------------------------------ items

    static void SeedItems(ReducerContext ctx)
    {
        AddWeapon(ctx, "Iron Sword", "SWD", WeaponType.Sword, atk: 12, strength: 1);
        AddWeapon(ctx, "Oak Staff", "STF", WeaponType.Staff, atk: 6, intelligence: 2);
        AddWeapon(ctx, "Twin Daggers", "DGR", WeaponType.Dagger, atk: 9, speed: 2);
        AddWeapon(ctx, "Hunting Bow", "BOW", WeaponType.Bow, atk: 10, dexterity: 2);

        AddArmor(ctx, "Leather Helm", "HLM", ArmorSlot.Helmet, defense: 1, maxHp: 6);
        AddArmor(ctx, "Leather Vest", "CHS", ArmorSlot.Chestplate, defense: 2, maxHp: 10);
        AddArmor(ctx, "Leather Greaves", "LEG", ArmorSlot.Leggings, defense: 1, maxHp: 6, speed: 1);
        AddArmor(ctx, "Leather Boots", "BTS", ArmorSlot.Boots, defense: 1, speed: 1);
        AddArmor(
            ctx,
            "Mage Robes",
            "ROB",
            ArmorSlot.Chestplate,
            defense: 1,
            maxMana: 20,
            intelligence: 1
        );

        AddConsumable(ctx, "Health Potion", "HPT", heal: 30, mana: 0);
        AddConsumable(ctx, "Mana Potion", "MPT", heal: 0, mana: 30);
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
                ArmorSlot = ArmorSlot.None,
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
            }
        );

    static void AddArmor(
        ReducerContext ctx,
        string name,
        string shortName,
        ArmorSlot armorSlot,
        int defense,
        int maxHp = 0,
        int maxMana = 0,
        int speed = 0,
        int intelligence = 0
    ) =>
        ctx.Db.ItemDef.Insert(
            new ItemDef
            {
                Id = 0,
                Name = name,
                ShortName = shortName,
                Kind = ItemKind.Armor,
                WeaponType = WeaponType.None,
                ArmorSlot = armorSlot,
                AtkBonus = 0,
                DefenseBonus = defense,
                StrengthBonus = 0,
                DexterityBonus = 0,
                IntelligenceBonus = intelligence,
                SpeedBonus = speed,
                MaxHpBonus = maxHp,
                MaxManaBonus = maxMana,
                HealAmount = 0,
                ManaRestoreAmount = 0,
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
                ArmorSlot = ArmorSlot.None,
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
            }
        );

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

    /// Weapon and full armour set worn on arrival, potions in the bag. The class
    /// weapon is equipped immediately so the EQUIPMENT panel is never empty.
    public static void GiveStartingLoadout(
        ReducerContext ctx,
        Identity owner,
        Entity entity,
        PlayerClass playerClass
    )
    {
        var weapon = RequireItem(ctx, StarterWeaponName(playerClass));
        var chest = playerClass == PlayerClass.Mage
            ? RequireItem(ctx, "Mage Robes")
            : RequireItem(ctx, "Leather Vest");

        EquipFresh(ctx, owner, weapon);
        EquipFresh(ctx, owner, RequireItem(ctx, "Leather Helm"));
        EquipFresh(ctx, owner, chest);
        EquipFresh(ctx, owner, RequireItem(ctx, "Leather Greaves"));
        EquipFresh(ctx, owner, RequireItem(ctx, "Leather Boots"));

        GiveToBag(ctx, owner, RequireItem(ctx, "Health Potion").Id, 3);
        GiveToBag(ctx, owner, RequireItem(ctx, "Mana Potion").Id, 2);

        // A spare chest piece with a different stat line, so the bag always holds
        // something worth equipping.
        var spare = playerClass == PlayerClass.Mage
            ? RequireItem(ctx, "Leather Vest")
            : RequireItem(ctx, "Mage Robes");
        GiveToBag(ctx, owner, spare.Id, 1);

        GrantClassSkills(ctx, entity.EntityId, playerClass);
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
            }
        );
    }

    public static int BagCount(ReducerContext ctx, Identity owner) =>
        ctx.Db.PlayerItem.Owner.Filter(owner).Count(i => i.EquippedSlot == EquipSlot.Bag);

    static void GrantClassSkills(ReducerContext ctx, ulong entityId, PlayerClass playerClass)
    {
        foreach (var skill in ctx.Db.SkillDef.Iter())
        {
            if (skill.IsEnemySkill || skill.ForClass != playerClass)
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
