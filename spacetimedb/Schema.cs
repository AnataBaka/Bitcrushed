using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Type]
    public enum PlayerClass
    {
        Warrior,
        Mage,
        Rogue,
        Archer,
    }

    [SpacetimeDB.Type]
    public enum Team
    {
        Players,
        Enemies,
    }

    [SpacetimeDB.Type]
    public enum BattlePhase
    {
        Waiting,
        InBattle,
        Victory,
        Defeat,
    }

    [SpacetimeDB.Type]
    public enum ItemKind
    {
        Weapon,
        Armor,
        Consumable,
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
        public BattlePhase Phase;
        public uint Round;
        public uint TurnIndex;
        public ulong ActiveEntityId;
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
        [Unique]
        public ulong EntityId;
        public uint BaseStrength;
        public uint BaseDexterity;
        public uint BaseIntelligence;
        public uint BaseSpeed;
        public uint EquippedWeaponDefId;
        public uint EquippedHelmetDefId;
        public uint EquippedChestplateDefId;
        public uint EquippedLeggingsDefId;
        public uint EquippedBootsDefId;
    }

    [SpacetimeDB.Table(Accessor = "Entity", Public = true)]
    public partial struct Entity
    {
        [PrimaryKey]
        [AutoInc]
        public ulong EntityId;
        public Team Faction;
        public uint Slot;
        public string Name;
        public string ClassName;
        public int MaxHp;
        public int Hp;
        public int MaxMana;
        public int Mana;
        public int Strength;
        public int Dexterity;
        public int Intelligence;
        public int Atk;
        public int Defense;
        public int Speed;
        public bool Alive;
        public uint StrengthBuff;
        public uint NextTurnStrengthBonus;
        public uint NextTurnSpeedOverride;
        public bool GoFirstNextRound;
    }

    [SpacetimeDB.Table(Accessor = "TurnOrder", Public = true)]
    public partial struct TurnOrder
    {
        [PrimaryKey]
        public uint Idx;
        public ulong EntityId;
        public uint Speed;
        public bool HasActed;
        public bool IsRush;
    }

    [SpacetimeDB.Table(Accessor = "BattleLog", Public = true)]
    public partial struct BattleLog
    {
        [PrimaryKey]
        [AutoInc]
        public ulong Id;
        public uint Round;
        public string Message;
        public Timestamp CreatedAt;
    }

    [SpacetimeDB.Table(
        Accessor = "EnemyTurnTimer",
        Scheduled = nameof(EnemyTurn),
        ScheduledAt = nameof(ScheduledAt)
    )]
    public partial struct EnemyTurnTimer
    {
        [PrimaryKey]
        [AutoInc]
        public ulong ScheduledId;
        public ScheduleAt ScheduledAt;
        public ulong EntityId;
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

    [SpacetimeDB.Table(Accessor = "EntitySkill", Public = true)]
    public partial struct EntitySkill
    {
        [PrimaryKey]
        [AutoInc]
        public uint Id;
        [SpacetimeDB.Index.BTree]
        public ulong EntityId;
        public uint SkillDefId;
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

    [SpacetimeDB.Table(Accessor = "CombatEvent", Public = true)]
    public partial struct CombatEvent
    {
        [PrimaryKey]
        [AutoInc]
        public uint Id;
        public uint Round;
        public ulong ActorEntityId;
        public ulong TargetEntityId;
        public CombatActionType ActionType;
        public string SkillName;
        public uint Damage;
        public uint Healing;
        public bool Dodged;
        public bool Clashed;
        public string Message;
    }
}
