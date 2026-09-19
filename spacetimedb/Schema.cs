using SpacetimeDB;

public static partial class Module
{
    // ------------------------------------------------------------------- enums

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
        RestStop,
        StageTransition,
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
        Staff,
        Dagger,
        Bow,
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

    /// Where an owned item currently lives. Bag means "carried, not worn".
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

    /// Lets the client pick an animation for a log line without parsing prose.
    [SpacetimeDB.Type]
    public enum LogKind
    {
        Info,
        TurnStart,
        Attack,
        Heal,
        Focus,
        Equip,
        Defeat,
    }

    // ------------------------------------------------------------------ tables

    /// Singleton row (Id == SessionId) describing the one and only battle.
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
        /// EntityId of whoever is acting right now; 0 when nobody is.
        public ulong ActiveEntityId;
        /// 1-based battle stage. 0 in the lobby. Cap is MaxStageCount in Rules.
        [Default(0u)]
        public uint StageNumber;
        /// Set during StageTransition: true if the next beat is a rest stop.
        [Default(false)]
        public bool UpcomingRestStop;
    }

    /// A seat in the party. Owns exactly one Entity row once the player joins.
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
        /// Lobby ready-up. Appended with a default so existing rows migrate.
        [Default(false)]
        public bool Ready;
        /// EXP-based character level. Starts at 1. Not the battle stage number.
        [Default(1u)]
        public uint CharacterLevel;
        [Default(0u)]
        public uint Xp;
    }

    /// Every combatant, player or enemy, lives here so turn order and damage
    /// only ever have to deal with one shape of row.
    [SpacetimeDB.Table(Accessor = "Entity", Public = true)]
    public partial struct Entity
    {
        [PrimaryKey]
        [AutoInc]
        public ulong EntityId;
        public Team Faction;
        /// Position within the team: 0..2 for players, 0..3 for enemies.
        public uint Slot;
        public string Name;
        public string ClassName;

        public int MaxHp;
        public int Hp;
        public int MaxMana;
        public int Mana;

        /// Rolled once on join, then never touched. Gear bonuses are layered on
        /// top of these to produce the effective stats below, so re-equipping
        /// never compounds.
        public int BaseStrength;
        public int BaseDexterity;
        public int BaseIntelligence;
        public int BaseSpeed;

        public int Strength;
        public int Dexterity;
        public int Intelligence;
        public int Speed;
        public int Atk;
        public int Defense;

        /// Applied for one turn by buff skills such as Enrage.
        public int StrengthBuff;
        public int NextTurnStrengthBonus;
        /// Set by "always go first" skills; consumed when the next round is built.
        public bool GoFirstNextRound;

        public bool Alive;
        /// Free fallback action, so an out-of-mana combatant can always act.
        public string BasicAttackName;
    }

    /// Rebuilt at the start of every round by BuildTurnOrder, fastest first.
    [SpacetimeDB.Table(Accessor = "TurnOrder", Public = true)]
    public partial struct TurnOrder
    {
        [PrimaryKey]
        public uint Idx;
        public ulong EntityId;
        public int Speed;
        public bool HasActed;
        /// True when a rush skill pushed this combatant to the front.
        public bool IsRush;
    }

    /// Append-only battle log. Clients sort by Id, which is monotonic. The
    /// actor/target/damage columns exist so the view can drive lunge and hit
    /// animations from the same rows it prints.
    [SpacetimeDB.Table(Accessor = "BattleLog", Public = true)]
    public partial struct BattleLog
    {
        [PrimaryKey]
        [AutoInc]
        public ulong Id;
        public uint Round;
        public string Message;
        public LogKind Kind;
        public ulong ActorEntityId;
        public ulong TargetEntityId;
        public int Damage;
        public int Healing;
        public Timestamp CreatedAt;
    }

    /// One-shot timer that drives a single enemy action.
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

    /// One-shot timer that advances StageTransition after the cleared-stage pause.
    [SpacetimeDB.Table(
        Accessor = "StageTransitionTimer",
        Scheduled = nameof(AdvanceStageTransition),
        ScheduledAt = nameof(ScheduledAt)
    )]
    public partial struct StageTransitionTimer
    {
        [PrimaryKey]
        [AutoInc]
        public ulong ScheduledId;
        public ScheduleAt ScheduledAt;
    }

    /// Catalog of every castable skill. Seeded once by Init.
    [SpacetimeDB.Table(Accessor = "SkillDef", Public = true)]
    public partial struct SkillDef
    {
        [PrimaryKey]
        [AutoInc]
        public uint Id;
        public string Name;
        public bool IsEnemySkill;
        /// Meaningless when IsEnemySkill is true.
        public PlayerClass ForClass;
        public int ManaCost;
        public int BaseDamage;
        /// 1 is single target, >1 is an area attack, 0 is a pure buff.
        public int TargetCount;
        public bool AlwaysGoFirst;
        public int NextTurnStrengthBonus;
        public DamageType DamageType;
    }

    /// Which skills a combatant can use. Covers players and enemies alike.
    [SpacetimeDB.Table(Accessor = "EntitySkill", Public = true)]
    public partial struct EntitySkill
    {
        [PrimaryKey]
        [AutoInc]
        public ulong Id;
        [SpacetimeDB.Index.BTree]
        public ulong EntityId;
        public uint SkillDefId;
    }

    /// Catalog of every item. Seeded once by Init.
    [SpacetimeDB.Table(Accessor = "ItemDef", Public = true)]
    public partial struct ItemDef
    {
        [PrimaryKey]
        [AutoInc]
        public uint Id;
        public string Name;
        /// Three-letter caption the HUD prints inside a slot.
        public string ShortName;
        public ItemKind Kind;
        public WeaponType WeaponType;
        public ArmorSlot ArmorSlot;
        public int AtkBonus;
        public int DefenseBonus;
        public int StrengthBonus;
        public int DexterityBonus;
        public int IntelligenceBonus;
        public int SpeedBonus;
        public int MaxHpBonus;
        public int MaxManaBonus;
        public int HealAmount;
        public int ManaRestoreAmount;
    }

    /// One stack of one item owned by one player, either worn or in the bag.
    [SpacetimeDB.Table(Accessor = "PlayerItem", Public = true)]
    public partial struct PlayerItem
    {
        [PrimaryKey]
        [AutoInc]
        public ulong Id;
        [SpacetimeDB.Index.BTree]
        public Identity Owner;
        public uint ItemDefId;
        public int Quantity;
        public EquipSlot EquippedSlot;
    }
}
