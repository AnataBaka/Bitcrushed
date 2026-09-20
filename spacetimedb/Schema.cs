using SpacetimeDB;

public static partial class Module
{
    // ------------------------------------------------------------------- enums

    [SpacetimeDB.Type]
    public enum PlayerClass
    {
        Knight,
        Mage,
        Ninja,
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
        Amulet,
        Consumable,
    }

    [SpacetimeDB.Type]
    public enum WeaponType
    {
        None,
        Sword,
        Staff,
        Katana,
        Bow,
    }

    /// Where an owned item currently lives. Bag is potions only. Inventory is
    /// the 3x3 gear grid (see InventoryIndex). Weapon and Amulet are worn.
    [SpacetimeDB.Type]
    public enum EquipSlot
    {
        Bag,
        Weapon,
        Amulet,
        Inventory,
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

    [SpacetimeDB.Type]
    public enum WorldBiome
    {
        Plains,
        Caves,
        Volcano,
        Swamp,
        SnowyTundra,
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
        Aoe,
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
        /// 1-based battle stage. 0 in the lobby. Endless: no cap.
        [Default(0u)]
        public uint StageNumber;
        /// Set during StageTransition: true if the next beat is a rest stop.
        [Default(false)]
        public bool UpcomingRestStop;
        /// Living combatant that receives every enemy attack this round (Grand Undertaking).
        [Default(0ul)]
        public ulong AttackRedirectEntityId;
        public WorldBiome CurrentBiome;
        /// Biome of the next battle. Clients read this for the stage-cleared "Next:" line.
        public WorldBiome NextBiome;
        [Default(false)]
        public bool IsBossStage;
        [Default(false)]
        public bool BossLootGranted;
        /// True after this rest stop has already granted living players their amulet.
        [Default(false)]
        public bool RestAmuletGranted;
        [Default("")]
        public string StageClearNote;
    }

    /// One row per biome: display name, log phrasing, and backdrop colors.
    [SpacetimeDB.Table(Accessor = "BiomeDef", Public = true)]
    public partial struct BiomeDef
    {
        [PrimaryKey]
        public uint Id;
        public WorldBiome Kind;
        public string Name;
        /// Used in "Entering the Caves" / "Next: the Caves".
        public string TheName;
        public int BackTopR;
        public int BackTopG;
        public int BackTopB;
        public int BackBotR;
        public int BackBotG;
        public int BackBotB;
        [Default("")]
        public string VariantPrefix;
        public int TintR;
        public int TintG;
        public int TintB;
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
        /// Unspent character-level points. Accrues on level-up; spent via SpendStatPoint.
        [Default(0u)]
        public uint UnspentStatPoints;
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

        /// Enraged stacks: +1 damage per stack on attacks this turn.
        public int StrengthBuff;
        public int NextTurnStrengthBonus;
        /// Set by "always go first" skills; consumed when the next round is built.
        public bool GoFirstNextRound;

        public bool Alive;
        /// Free fallback action, so an out-of-mana combatant can always act.
        public string BasicAttackName;

        /// Burn: stack is damage per tick, count is turns remaining. Reapplying adds both.
        [Default(0)]
        public int BurnStack;
        [Default(0)]
        public int BurnCount;
        /// Weak: 10% less damage dealt per stack while active this round.
        [Default(0)]
        public int WeakStacks;
        [Default(0)]
        public int NextTurnWeak;
        /// Fragile: 10% more damage taken per stack while active this round.
        [Default(0)]
        public int FragileStacks;
        [Default(0)]
        public int NextTurnFragile;

        /// Speed used for this round's turn order and Ninja power comparisons.
        [Default(0)]
        public int CombatSpeed;
        /// Non-zero replaces Speed next round (Rush 99999, Gallant Pride 1).
        [Default(0)]
        public int NextTurnSpeedSet;
        [Default(0)]
        public int NextTurnSpeedDelta;

        [Default(0)]
        public int DodgeBonusPercent;
        [Default(0)]
        public int NextTurnDodgeBonus;
        /// Incoming hits below this damage are negated (Archer Evade / Ninja Focus Spirit).
        [Default(0)]
        public int EvadeThreshold;
        [Default(0)]
        public int EvadeFragileOnDodge;
        [Default(0)]
        public int EvadeStrengthOnDodge;
        [Default(false)]
        public bool HasDodged;
        /// Successful dodges/evades this battle. Grandshot scales off this, capped at 4.
        [Default(0)]
        public int DodgeCount;

        [Default(false)]
        public bool UsedAttackThisTurn;
        [Default(false)]
        public bool UsedAttackLastTurn;
        [Default(0)]
        public int NextAttackBonus;

        [Default(1)]
        public int MagicBulletStage;
        [Default(0)]
        public int SpearDiscount;
        [Default(0)]
        public int VerticalCutDiscount;
        [Default(false)]
        public bool FinishTheJobUsed;
        [Default(false)]
        public bool FinishTheJobStance;
        [Default(0)]
        public int FinishTheJobPower;
        [Default(false)]
        public bool SkipNextTurn;
        [Default(false)]
        public bool GrandUndertakingPending;
        /// Cosmetic biome variant prefix stored so every client shows the same name and tint.
        [Default("")]
        public string VariantPrefix;
        [Default(255)]
        public int TintR;
        [Default(255)]
        public int TintG;
        [Default(255)]
        public int TintB;
        [Default(false)]
        public bool IsBoss;
        [Default(0)]
        public int SkillCooldown;
        /// Dragonfly Charm: Strength is doubled for one turn after an ally dies.
        [Default(false)]
        public bool DoubleStrength;
        [Default(false)]
        public bool NextTurnDoubleStrength;
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
        /// 0 is the current or next actor. Living combatants are 0..n-1 in
        /// display order. Hidden or dead entries use TurnListHiddenPos.
        [Default(0u)]
        public uint DisplayPos;
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
        [Default(1u)]
        public uint LevelRequired;
        [Default(1)]
        public int HitCount;
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
        /// One-line special effect. Empty when the item is only a stat stick.
        [Default("")]
        public string Description;
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
        /// 0-8 when EquippedSlot is Inventory; otherwise InventoryNone (255).
        [Default(255u)]
        public uint InventoryIndex;
    }

    /// Data-driven boss loot. Later tiers can reuse or extend these rows.
    [SpacetimeDB.Table(Accessor = "BossDrop", Public = true)]
    public partial struct BossDrop
    {
        [PrimaryKey]
        [AutoInc]
        public uint Id;
        [SpacetimeDB.Index.BTree]
        public uint Tier;
        public uint ItemDefId;
    }

    /// Loot that did not fit in the 3x3. Delivered as soon as a cell frees.
    [SpacetimeDB.Table(Accessor = "PendingReward", Public = true)]
    public partial struct PendingReward
    {
        [PrimaryKey]
        [AutoInc]
        public ulong Id;
        [SpacetimeDB.Index.BTree]
        public Identity Owner;
        public uint ItemDefId;
    }
}
