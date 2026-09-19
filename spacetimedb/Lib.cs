using System;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;

public static partial class Module
{
    // ------------------------------------------------------------------ tuning

    public const uint SessionId = 1;
    public const uint MaxPartySize = 3;
    public const uint EnemyCount = 2;

    public const int StartingPotions = 3;
    public const int PotionHeal = 20;
    public const int FocusManaGain = 20;

    // Enemy actions are spaced out so the battle log stays readable.
    public const long EnemyTurnDelayMicros = 2_000_000;

    static readonly string[] PartyNames = { "Aria", "Bran", "Cael" };

    // ------------------------------------------------------------------- types

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
        HealthPotion,
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
        /// Position within the team: 0..2 for players, 0..1 for enemies.
        public uint Slot;
        public string Name;
        public string ClassName;

        public int MaxHp;
        public int Hp;
        public int MaxMana;
        public int Mana;
        public int Strength;
        public int Damage;
        public int Speed;
        public bool Alive;
        public int Potions;

        public string SkillName;
        public int SkillAtk;
        public int SkillManaCost;
    }

    /// Rebuilt at the start of every round by BuildTurnOrder.
    [SpacetimeDB.Table(Accessor = "TurnOrder", Public = true)]
    public partial struct TurnOrder
    {
        [PrimaryKey]
        public uint Idx;
        public ulong EntityId;
    }

    /// Append-only battle log. Clients sort by Id, which is monotonic.
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

    // --------------------------------------------------------------- templates

    static (
        string ClassName,
        int MaxHp,
        int MaxMana,
        int Strength,
        int Damage,
        int Speed,
        string SkillName,
        int SkillAtk,
        int SkillManaCost
    ) TemplateFor(PlayerClass playerClass) =>
        playerClass switch
        {
            PlayerClass.Warrior => ("Warrior", 120, 30, 12, 8, 5, "Cleaving Strike", 10, 5),
            PlayerClass.Mage => ("Mage", 70, 80, 3, 4, 6, "Fireball", 25, 18),
            PlayerClass.Rogue => ("Rogue", 85, 45, 8, 9, 12, "Backstab", 14, 10),
            PlayerClass.Archer => ("Archer", 90, 50, 7, 10, 9, "Piercing Arrow", 13, 9),
            _ => throw new Exception("Unknown class."),
        };

    static (
        string Name,
        int MaxHp,
        int MaxMana,
        int Strength,
        int Damage,
        int Speed,
        string SkillName,
        int SkillAtk,
        int SkillManaCost
    ) EnemyTemplate(uint slot) =>
        slot switch
        {
            0 => ("Goblin Raider", 180, 40, 6, 6, 7, "Rusty Slash", 8, 6),
            _ => ("Cave Troll", 240, 50, 10, 8, 4, "Boulder Smash", 12, 10),
        };

    // -------------------------------------------------------------- lifecycle

    [SpacetimeDB.Reducer(ReducerKind.Init)]
    public static void Init(ReducerContext ctx)
    {
        ctx.Db.GameSession.Insert(
            new GameSession
            {
                Id = SessionId,
                PlayerCount = 0,
                MaxPlayers = MaxPartySize,
                Phase = BattlePhase.Waiting,
                Round = 0,
                TurnIndex = 0,
                ActiveEntityId = 0,
            }
        );
        Log.Info("Testing Fight Stage initialized.");
    }

    [SpacetimeDB.Reducer(ReducerKind.ClientConnected)]
    public static void ClientConnected(ReducerContext ctx)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is Player player)
        {
            ctx.Db.Player.Identity.Update(player with { Online = true });
            Log.Info($"Player in slot {player.Slot} reconnected.");
        }
        else
        {
            Log.Info($"Client connected: {ctx.Sender}");
        }
    }

    [SpacetimeDB.Reducer(ReducerKind.ClientDisconnected)]
    public static void ClientDisconnected(ReducerContext ctx)
    {
        // Mark the sender offline so a quick reconnect can resume mid-battle.
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is Player player)
        {
            ctx.Db.Player.Identity.Update(player with { Online = false });
        }

        // When the last connected player drops, wipe the stage so the next session
        // starts fresh instead of resuming an abandoned battle of offline characters.
        if (!ctx.Db.Player.Iter().Any(p => p.Online))
        {
            ResetStageState(ctx);
        }
    }

    // ------------------------------------------------------------------ lobby

    [SpacetimeDB.Reducer]
    public static void JoinGame(ReducerContext ctx)
    {
        var session = RequireSession(ctx);

        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not null)
        {
            throw new Exception("Already joined the party.");
        }

        // Checked before the phase so the 4th joiner always hears why.
        if (session.PlayerCount >= session.MaxPlayers)
        {
            throw new Exception("Party is Full");
        }

        if (session.Phase != BattlePhase.Waiting)
        {
            throw new Exception("The battle has already started.");
        }

        var slot = FindFreeSlot(ctx);
        var playerClass = (PlayerClass)ctx.Rng.Next(0, 4);
        var template = TemplateFor(playerClass);
        var name = PartyNames[slot % (uint)PartyNames.Length];

        var entity = ctx.Db.Entity.Insert(
            new Entity
            {
                EntityId = 0,
                Faction = Team.Players,
                Slot = slot,
                Name = name,
                ClassName = template.ClassName,
                MaxHp = template.MaxHp,
                Hp = template.MaxHp,
                MaxMana = template.MaxMana,
                Mana = template.MaxMana,
                Strength = template.Strength,
                Damage = template.Damage,
                Speed = template.Speed,
                Alive = true,
                Potions = StartingPotions,
                SkillName = template.SkillName,
                SkillAtk = template.SkillAtk,
                SkillManaCost = template.SkillManaCost,
            }
        );

        ctx.Db.Player.Insert(
            new Player
            {
                Identity = ctx.Sender,
                Slot = slot,
                Online = true,
                Class = playerClass,
                EntityId = entity.EntityId,
            }
        );

        var playerCount = session.PlayerCount + 1;
        ctx.Db.GameSession.Id.Update(session with { PlayerCount = playerCount });
        AddLog(ctx, $"{name} the {template.ClassName} joined the party.");

        if (playerCount >= MaxPartySize)
        {
            BeginBattle(ctx);
        }
    }

    [SpacetimeDB.Reducer]
    public static void LeaveGame(ReducerContext ctx)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player)
        {
            throw new Exception("Not in the party.");
        }

        var session = RequireSession(ctx);
        if (session.Phase == BattlePhase.InBattle)
        {
            throw new Exception("Cannot leave during a battle.");
        }

        ctx.Db.Entity.EntityId.Delete(player.EntityId);
        ctx.Db.Player.Identity.Delete(ctx.Sender);

        var playerCount = session.PlayerCount == 0 ? 0 : session.PlayerCount - 1;
        ctx.Db.GameSession.Id.Update(session with { PlayerCount = playerCount });
        AddLog(ctx, $"A player in slot {player.Slot} left the party.");
    }

    /// Starts the fight without a full party. Handy for testing with 1-2 clients.
    [SpacetimeDB.Reducer]
    public static void StartBattle(ReducerContext ctx)
    {
        var session = RequireSession(ctx);
        if (session.Phase != BattlePhase.Waiting)
        {
            throw new Exception("The battle has already started.");
        }

        if (session.PlayerCount == 0)
        {
            throw new Exception("At least one player must join first.");
        }

        BeginBattle(ctx);
    }

    /// Wipes the stage back to an empty lobby. Testing affordance only.
    [SpacetimeDB.Reducer]
    public static void ResetStage(ReducerContext ctx) => ResetStageState(ctx);

    /// Clears timers, turn order, entities, players, and battle log, then returns
    /// the session to Waiting. Shared by the manual ResetStage reducer and the
    /// auto-reset that fires when every player disconnects.
    static void ResetStageState(ReducerContext ctx)
    {
        foreach (var timer in ctx.Db.EnemyTurnTimer.Iter().ToList())
        {
            ctx.Db.EnemyTurnTimer.ScheduledId.Delete(timer.ScheduledId);
        }

        foreach (var entry in ctx.Db.TurnOrder.Iter().ToList())
        {
            ctx.Db.TurnOrder.Idx.Delete(entry.Idx);
        }

        foreach (var entity in ctx.Db.Entity.Iter().ToList())
        {
            ctx.Db.Entity.EntityId.Delete(entity.EntityId);
        }

        foreach (var player in ctx.Db.Player.Iter().ToList())
        {
            ctx.Db.Player.Identity.Delete(player.Identity);
        }

        foreach (var line in ctx.Db.BattleLog.Iter().ToList())
        {
            ctx.Db.BattleLog.Id.Delete(line.Id);
        }

        var session = RequireSession(ctx);
        ctx.Db.GameSession.Id.Update(
            session with
            {
                PlayerCount = 0,
                Phase = BattlePhase.Waiting,
                Round = 0,
                TurnIndex = 0,
                ActiveEntityId = 0,
            }
        );
        AddLog(ctx, "Stage reset. Waiting for players.");
    }

    // ---------------------------------------------------------- player actions

    [SpacetimeDB.Reducer]
    public static void Attack(ReducerContext ctx, ulong targetEntityId)
    {
        var attacker = RequireActingEntity(ctx);

        if (ctx.Db.Entity.EntityId.Find(targetEntityId) is not Entity target)
        {
            throw new Exception("That target does not exist.");
        }

        if (target.Faction == attacker.Faction)
        {
            throw new Exception("You cannot attack your own team.");
        }

        if (!target.Alive)
        {
            AddLog(ctx, $"{target.Name} is already defeated.");
            return;
        }

        if (attacker.Mana < attacker.SkillManaCost)
        {
            AddLog(
                ctx,
                $"Not enough mana. {attacker.Name} needs {attacker.SkillManaCost} mana for {attacker.SkillName}."
            );
            return;
        }

        ResolveAttack(ctx, attacker, target);
        AdvanceTurn(ctx);
    }

    [SpacetimeDB.Reducer]
    public static void UseItem(ReducerContext ctx, ItemKind item)
    {
        var actor = RequireActingEntity(ctx);

        if (item != ItemKind.HealthPotion)
        {
            throw new Exception("Unknown item.");
        }

        if (actor.Potions <= 0)
        {
            AddLog(ctx, $"{actor.Name} has no Health Potions left.");
            return;
        }

        var healed = Math.Min(actor.MaxHp, actor.Hp + PotionHeal);
        var restored = healed - actor.Hp;
        ctx.Db.Entity.EntityId.Update(actor with { Hp = healed, Potions = actor.Potions - 1 });
        AddLog(
            ctx,
            $"{actor.Name} drinks a Health Potion and restores {restored} HP ({healed}/{actor.MaxHp})."
        );
        AdvanceTurn(ctx);
    }

    [SpacetimeDB.Reducer]
    public static void Focus(ReducerContext ctx)
    {
        var actor = RequireActingEntity(ctx);
        ApplyFocus(ctx, actor);
        AdvanceTurn(ctx);
    }

    // ------------------------------------------------------------- enemy turns

    [SpacetimeDB.Reducer]
    public static void EnemyTurn(ReducerContext ctx, EnemyTurnTimer timer)
    {
        if (ctx.Sender != ctx.DatabaseIdentity)
        {
            throw new Exception("EnemyTurn may only be invoked by the scheduler.");
        }

        var session = RequireSession(ctx);
        if (session.Phase != BattlePhase.InBattle || session.ActiveEntityId != timer.EntityId)
        {
            return;
        }

        if (ctx.Db.Entity.EntityId.Find(timer.EntityId) is not Entity enemy || !enemy.Alive)
        {
            AdvanceTurn(ctx);
            return;
        }

        var targets = LivingMembers(ctx, Team.Players);
        if (targets.Count == 0)
        {
            AdvanceTurn(ctx);
            return;
        }

        if (enemy.Mana >= enemy.SkillManaCost)
        {
            var target = targets[ctx.Rng.Next(0, targets.Count)];
            ResolveAttack(ctx, enemy, target);
        }
        else
        {
            ApplyFocus(ctx, enemy);
        }

        AdvanceTurn(ctx);
    }

    // ------------------------------------------------------------ battle rules

    static void BeginBattle(ReducerContext ctx)
    {
        SpawnEnemies(ctx);

        var session = RequireSession(ctx);
        ctx.Db.GameSession.Id.Update(
            session with
            {
                Phase = BattlePhase.InBattle,
                Round = 1,
                TurnIndex = 0,
                ActiveEntityId = 0,
            }
        );

        AddLog(ctx, $"{EnemyCount} enemies appeared!");
        BuildTurnOrder(ctx);

        if (EndBattleIfOver(ctx))
        {
            return;
        }

        if (
            ctx.Db.TurnOrder.Idx.Find(0u) is TurnOrder first
            && ctx.Db.Entity.EntityId.Find(first.EntityId) is Entity entity
        )
        {
            SetActive(ctx, 1, 0, entity);
        }
    }

    static void SpawnEnemies(ReducerContext ctx)
    {
        for (uint slot = 0; slot < EnemyCount; slot++)
        {
            var template = EnemyTemplate(slot);
            ctx.Db.Entity.Insert(
                new Entity
                {
                    EntityId = 0,
                    Faction = Team.Enemies,
                    Slot = slot,
                    Name = template.Name,
                    ClassName = "Enemy",
                    MaxHp = template.MaxHp,
                    Hp = template.MaxHp,
                    MaxMana = template.MaxMana,
                    Mana = template.MaxMana,
                    Strength = template.Strength,
                    Damage = template.Damage,
                    Speed = template.Speed,
                    Alive = true,
                    Potions = 0,
                    SkillName = template.SkillName,
                    SkillAtk = template.SkillAtk,
                    SkillManaCost = template.SkillManaCost,
                }
            );
        }
    }

    /// The single place that decides ordering. Today: players by slot, then
    /// enemies by slot. Swap the comparer here to order by Speed descending.
    static void BuildTurnOrder(ReducerContext ctx)
    {
        foreach (var entry in ctx.Db.TurnOrder.Iter().ToList())
        {
            ctx.Db.TurnOrder.Idx.Delete(entry.Idx);
        }

        var ordered = ctx
            .Db.Entity.Iter()
            .Where(e => e.Alive)
            .OrderBy(e => e.Faction == Team.Players ? 0 : 1)
            .ThenBy(e => e.Slot)
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            ctx.Db.TurnOrder.Insert(
                new TurnOrder { Idx = (uint)i, EntityId = ordered[i].EntityId }
            );
        }
    }

    static void AdvanceTurn(ReducerContext ctx)
    {
        if (EndBattleIfOver(ctx))
        {
            return;
        }

        var session = RequireSession(ctx);
        var round = session.Round;
        var idx = session.TurnIndex + 1;

        // Bounded so a logic slip can never hang the reducer.
        for (var guard = 0; guard < 128; guard++)
        {
            if (idx >= (uint)ctx.Db.TurnOrder.Count)
            {
                round += 1;
                BuildTurnOrder(ctx);
                idx = 0;

                if (ctx.Db.TurnOrder.Count == 0)
                {
                    EndBattleIfOver(ctx);
                    return;
                }
            }

            if (
                ctx.Db.TurnOrder.Idx.Find(idx) is TurnOrder entry
                && ctx.Db.Entity.EntityId.Find(entry.EntityId) is Entity entity
                && entity.Alive
            )
            {
                SetActive(ctx, round, idx, entity);
                return;
            }

            idx += 1;
        }

        Log.Warn("AdvanceTurn gave up looking for a living entity.");
    }

    static void SetActive(ReducerContext ctx, uint round, uint idx, Entity entity)
    {
        var session = RequireSession(ctx);
        ctx.Db.GameSession.Id.Update(
            session with
            {
                Round = round,
                TurnIndex = idx,
                ActiveEntityId = entity.EntityId,
            }
        );

        AddLog(ctx, $"Round {round} - {entity.Name}'s turn.");

        if (entity.Faction == Team.Enemies)
        {
            ctx.Db.EnemyTurnTimer.Insert(
                new EnemyTurnTimer
                {
                    ScheduledId = 0,
                    ScheduledAt = new ScheduleAt.Time(
                        ctx.Timestamp + new TimeDuration(EnemyTurnDelayMicros)
                    ),
                    EntityId = entity.EntityId,
                }
            );
        }
    }

    static bool EndBattleIfOver(ReducerContext ctx)
    {
        var session = RequireSession(ctx);
        if (session.Phase != BattlePhase.InBattle)
        {
            return session.Phase is BattlePhase.Victory or BattlePhase.Defeat;
        }

        var playersAlive = LivingMembers(ctx, Team.Players).Count;
        var enemiesAlive = LivingMembers(ctx, Team.Enemies).Count;

        if (enemiesAlive == 0)
        {
            ctx.Db.GameSession.Id.Update(
                session with
                {
                    Phase = BattlePhase.Victory,
                    ActiveEntityId = 0,
                }
            );
            AddLog(ctx, "All enemies are defeated. Level Complete!");
            return true;
        }

        if (playersAlive == 0)
        {
            ctx.Db.GameSession.Id.Update(
                session with
                {
                    Phase = BattlePhase.Defeat,
                    ActiveEntityId = 0,
                }
            );
            AddLog(ctx, "The whole party has fallen. Defeat.");
            return true;
        }

        return false;
    }

    static void ResolveAttack(ReducerContext ctx, Entity attacker, Entity target)
    {
        var damage = attacker.Damage + attacker.Strength + attacker.SkillAtk;
        if (damage < 0)
        {
            damage = 0;
        }

        var hp = target.Hp - damage;
        if (hp < 0)
        {
            hp = 0;
        }

        var alive = hp > 0;

        ctx.Db.Entity.EntityId.Update(target with { Hp = hp, Alive = alive });
        ctx.Db.Entity.EntityId.Update(
            attacker with
            {
                Mana = attacker.Mana - attacker.SkillManaCost,
            }
        );

        AddLog(
            ctx,
            $"{attacker.Name} uses {attacker.SkillName} on {target.Name} for {damage} damage."
        );

        if (!alive)
        {
            AddLog(ctx, $"{target.Name} is defeated!");
        }
    }

    static void ApplyFocus(ReducerContext ctx, Entity actor)
    {
        var mana = Math.Min(actor.MaxMana, actor.Mana + FocusManaGain);
        var restored = mana - actor.Mana;
        ctx.Db.Entity.EntityId.Update(actor with { Mana = mana });
        AddLog(
            ctx,
            $"{actor.Name} focuses and restores {restored} mana ({mana}/{actor.MaxMana})."
        );
    }

    // ---------------------------------------------------------------- helpers

    /// Every player action funnels through here: right phase, right caller,
    /// right turn, still alive.
    static Entity RequireActingEntity(ReducerContext ctx)
    {
        var session = RequireSession(ctx);
        if (session.Phase != BattlePhase.InBattle)
        {
            throw new Exception("The battle is not running.");
        }

        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player)
        {
            throw new Exception("You are not in the party.");
        }

        if (ctx.Db.Entity.EntityId.Find(player.EntityId) is not Entity entity)
        {
            throw new Exception("Your character is missing.");
        }

        if (!entity.Alive)
        {
            throw new Exception("You are defeated and cannot act.");
        }

        if (session.ActiveEntityId != entity.EntityId)
        {
            throw new Exception("It is not your turn.");
        }

        return entity;
    }

    static List<Entity> LivingMembers(ReducerContext ctx, Team faction) =>
        ctx.Db.Entity.Iter()
            .Where(e => e.Faction == faction && e.Alive)
            .OrderBy(e => e.Slot)
            .ToList();

    static GameSession RequireSession(ReducerContext ctx)
    {
        if (ctx.Db.GameSession.Id.Find(SessionId) is GameSession session)
        {
            return session;
        }

        throw new Exception("Game session is missing.");
    }

    static uint FindFreeSlot(ReducerContext ctx)
    {
        var taken = new bool[MaxPartySize];
        foreach (var player in ctx.Db.Player.Iter())
        {
            if (player.Slot < MaxPartySize)
            {
                taken[player.Slot] = true;
            }
        }

        for (uint slot = 0; slot < MaxPartySize; slot++)
        {
            if (!taken[slot])
            {
                return slot;
            }
        }

        throw new Exception("Party is Full");
    }

    static void AddLog(ReducerContext ctx, string message)
    {
        var round = ctx.Db.GameSession.Id.Find(SessionId) is GameSession session
            ? session.Round
            : 0u;

        ctx.Db.BattleLog.Insert(
            new BattleLog
            {
                Id = 0,
                Round = round,
                Message = message,
                CreatedAt = ctx.Timestamp,
            }
        );

        Log.Info(message);
    }
}
