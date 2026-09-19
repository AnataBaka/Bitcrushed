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
            100,
            5,
            1,
            DamageType.Physical,
            30,
            hitCount: 9
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
        AddPlayerSkill(ctx, SkillNames.Snipe, PlayerClass.Archer, 50, 30, 1, DamageType.Physical, 12);
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
            100,
            30,
            1,
            DamageType.Physical,
            30
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
        AddPlayerSkill(
            ctx,
            SkillNames.Necromancy,
            PlayerClass.Mage,
            150,
            0,
            1,
            DamageType.Magical,
            35
        );

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
            40,
            42,
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
        AddWeapon(ctx, "Wooden Cane", "WCN", WeaponType.Staff, atk: 6, intelligence: 2);
        AddWeapon(ctx, "Rusted Katana", "KTN", WeaponType.Katana, atk: 9, speed: 2);
        AddWeapon(ctx, "Weathered Bow", "WBW", WeaponType.Bow, atk: 10, dexterity: 2);
        AddAmulet(ctx, "Health Amulet", "HPA", maxHp: 10);

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

    static void AddAmulet(ReducerContext ctx, string name, string shortName, int maxHp) =>
        ctx.Db.ItemDef.Insert(
            new ItemDef
            {
                Id = 0,
                Name = name,
                ShortName = shortName,
                Kind = ItemKind.Amulet,
                WeaponType = WeaponType.None,
                ArmorSlot = ArmorSlot.None,
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

    /// Class base weapon worn, Amulet empty, 3x3 inventory empty. Potions stay
    /// in the action-menu bag and are not part of inventory.
    public static void GiveStartingLoadout(
        ReducerContext ctx,
        Identity owner,
        Entity entity,
        PlayerClass playerClass
    )
    {
        EquipFresh(ctx, owner, RequireItem(ctx, StarterWeaponName(playerClass)));

        GiveToBag(ctx, owner, RequireItem(ctx, "Health Potion").Id, 3);
        GiveToBag(ctx, owner, RequireItem(ctx, "Mana Potion").Id, 2);

        if (SeedTestItems)
        {
            TryAddItemToInventory(ctx, owner, RequireItem(ctx, "Health Amulet").Id);
            TryAddItemToInventory(ctx, owner, RequireItem(ctx, "Chipped Sword").Id);
            TryAddItemToInventory(ctx, owner, RequireItem(ctx, "Weathered Bow").Id);
            TryAddItemToInventory(ctx, owner, RequireItem(ctx, "Wooden Cane").Id);
            TryAddItemToInventory(ctx, owner, RequireItem(ctx, "Rusted Katana").Id);
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
