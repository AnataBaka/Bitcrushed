using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Type]
    public enum PlayerClass
    {
        Warrior,
        Archer,
        Mage,
        Rogue,
    }

    [SpacetimeDB.Type]
    public enum GamePhase
    {
        Waiting,
        Combat,
        FloorClear,
        Defeat,
    }

    [SpacetimeDB.Type]
    public enum Biome
    {
        Forest,
        Ocean,
        Hell,
    }

    [SpacetimeDB.Type]
    public enum WeaponType
    {
        None,
        Sword,
        Bow,
        Staff,
        Dagger,
    }

    [SpacetimeDB.Type]
    public enum ItemKind
    {
        Weapon,
        Armor,
        Consumable,
    }

    [SpacetimeDB.Type]
    public enum ArmorSlot
    {
        None,
        Helmet,
        Chestplate,
        Leggings,
        Boots,
    }

    [SpacetimeDB.Type]
    public enum EquipSlot
    {
        Bag,
        Weapon,
        Helmet,
        Chestplate,
        Leggings,
        Boots,
    }

    [SpacetimeDB.Type]
    public enum CombatActionType
    {
        Attack,
        Spell,
        Defend,
        Item,
    }

    [SpacetimeDB.Type]
    public enum CombatantKind
    {
        Player,
        Enemy,
    }

    [SpacetimeDB.Type]
    public enum StatType
    {
        Strength,
        Dexterity,
        Intelligence,
        Speed,
    }

    [SpacetimeDB.Type]
    public enum DamageType
    {
        Physical,
        Magical,
    }

    [SpacetimeDB.Table(Accessor = "GameSession", Public = true)]
    public partial struct GameSession
    {
        [PrimaryKey]
        public uint Id;
        public uint PlayerCount;
        public uint MaxPlayers;
        public GamePhase Phase;
        public uint Floor;
        public Biome Biome;
        public bool IsBossFloor;
        public uint RoundNumber;
        public uint TurnNumber;
        public CombatantKind ActiveKind;
        public uint ActiveCombatantId;
    }

    [SpacetimeDB.Table(Accessor = "Player", Public = true)]
    public partial struct Player
    {
        [PrimaryKey]
        public Identity Identity;
        [Unique]
        public uint Slot;
        public bool Online;
        public PlayerClass Class;
        public string Name;
        public uint SpriteId;
        public uint Level;
        public uint Xp;
        public uint UnspentStatPoints;
        public uint MaxHealth;
        public uint CurrHealth;
        public uint MaxMana;
        public uint CurrMana;
        public uint Speed;
        public uint Strength;
        public uint Dexterity;
        public uint Intelligence;
        public uint BaseSpeed;
        public uint BaseStrength;
        public uint BaseDexterity;
        public uint BaseIntelligence;
        public uint Atk;
        public uint BaseDefense;
        public uint Defense;
        public uint StrengthBuff;
        public uint NextTurnStrengthBonus;
        public uint NextTurnSpeedOverride;
        public bool GoFirstNextRound;
        public bool IsDefending;
        public bool Alive;
        public uint EquippedWeaponDefId;
        public uint EquippedHelmetDefId;
        public uint EquippedChestplateDefId;
        public uint EquippedLeggingsDefId;
        public uint EquippedBootsDefId;
    }

    [SpacetimeDB.Table(Accessor = "SkillDef", Public = true)]
    public partial struct SkillDef
    {
        [PrimaryKey]
        [AutoInc]
        public uint Id;
        public string Name;
        public bool IsEnemySkill;
        public PlayerClass Class;
        public Biome Biome;
        public uint UnlockFloor;
        public uint ManaCost;
        public uint BaseDamage;
        public uint TargetCount;
        public bool AlwaysGoFirst;
        public uint NextTurnStrengthBonus;
        public uint NextTurnSpeedOverride;
        public DamageType DamageType;
    }

    [SpacetimeDB.Table(Accessor = "PlayerSkill", Public = true)]
    public partial struct PlayerSkill
    {
        [PrimaryKey]
        [AutoInc]
        public uint Id;
        [SpacetimeDB.Index.BTree]
        public Identity Owner;
        public uint SkillDefId;
        public bool Unlocked;
    }

    [SpacetimeDB.Table(Accessor = "ItemDef", Public = true)]
    public partial struct ItemDef
    {
        [PrimaryKey]
        [AutoInc]
        public uint Id;
        public string Name;
        public ItemKind Kind;
        public WeaponType WeaponType;
        public ArmorSlot ArmorSlot;
        public uint AtkBonus;
        public uint StrengthBonus;
        public uint DexterityBonus;
        public uint IntelligenceBonus;
        public uint SpeedBonus;
        public uint MaxHealthBonus;
        public uint MaxManaBonus;
        public uint HealAmount;
        public uint ManaRestoreAmount;
        public uint SpriteId;
    }

    [SpacetimeDB.Table(Accessor = "PlayerItem", Public = true)]
    public partial struct PlayerItem
    {
        [PrimaryKey]
        [AutoInc]
        public uint Id;
        [SpacetimeDB.Index.BTree]
        public Identity Owner;
        public uint ItemDefId;
        public uint Quantity;
        public EquipSlot EquippedSlot;
        public uint HealthPips;
    }

    [SpacetimeDB.Table(Accessor = "Enemy", Public = true)]
    public partial struct Enemy
    {
        [PrimaryKey]
        [AutoInc]
        public uint Id;
        public uint Slot;
        public string Name;
        public uint SpriteId;
        public Biome Biome;
        public string TraitName;
        public bool IsBoss;
        public uint Floor;
        public uint MaxHealth;
        public uint CurrHealth;
        public uint MaxMana;
        public uint CurrMana;
        public uint Speed;
        public uint Strength;
        public uint Dexterity;
        public uint Intelligence;
        public uint Atk;
        public uint Defense;
        public uint StrengthBuff;
        public uint NextTurnStrengthBonus;
        public uint NextTurnSpeedOverride;
        public bool GoFirstNextRound;
        public bool Alive;
    }

    [SpacetimeDB.Table(Accessor = "EnemySkill", Public = true)]
    public partial struct EnemySkill
    {
        [PrimaryKey]
        [AutoInc]
        public uint Id;
        [SpacetimeDB.Index.BTree]
        public uint EnemyId;
        public uint SkillDefId;
    }

    [SpacetimeDB.Table(Accessor = "TurnOrder", Public = true)]
    public partial struct TurnOrder
    {
        [PrimaryKey]
        [AutoInc]
        public uint Id;
        public uint Sequence;
        public CombatantKind Kind;
        public uint CombatantId;
        public uint Speed;
        public bool HasActed;
        public bool IsRush;
    }

    [SpacetimeDB.Table(Accessor = "CombatEvent", Public = true)]
    public partial struct CombatEvent
    {
        [PrimaryKey]
        [AutoInc]
        public uint Id;
        public uint RoundNumber;
        public uint TurnNumber;
        public CombatantKind ActorKind;
        public uint ActorId;
        public CombatantKind TargetKind;
        public uint TargetId;
        public CombatActionType ActionType;
        public string SkillName;
        public uint Damage;
        public uint Healing;
        public bool Dodged;
        public bool Clashed;
        public string Message;
    }
}
