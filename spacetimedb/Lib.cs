using System;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;

/// Reducers and battle flow. Every rule lives on the server; the Unity client
/// only renders tables and calls these reducers.
public static partial class Module
{
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
                StageNumber = 0,
                UpcomingRestStop = false,
            }
        );

        SeedCatalog(ctx);
        Log.Info("Testing Fight Stage initialized.");
    }

    [SpacetimeDB.Reducer(ReducerKind.ClientConnected)]
    public static void ClientConnected(ReducerContext ctx)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is Player player)
        {
            ctx.Db.Player.Identity.Update(player with { Online = true });
            Log.Info($"Player in slot {player.Slot} reconnected.");
            TryStartIfAllReady(ctx);
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
            return;
        }

        TryStartIfAllReady(ctx);
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
        var rng = PerSenderRng(ctx);
        var playerClass = (PlayerClass)rng.Next(0, 4);
        var stats = RollStats(rng, playerClass);
        var className = ClassName(playerClass);
        var name = PartyName(slot);
        var maxHp = ClassMaxHp(playerClass);
        var maxMana = ClassMaxMana(playerClass);

        var entity = ctx.Db.Entity.Insert(
            new Entity
            {
                EntityId = 0,
                Faction = Team.Players,
                Slot = slot,
                Name = name,
                ClassName = className,
                MaxHp = maxHp,
                Hp = maxHp,
                MaxMana = maxMana,
                Mana = maxMana,
                BaseStrength = stats.Strength,
                BaseDexterity = stats.Dexterity,
                BaseIntelligence = stats.Intelligence,
                BaseSpeed = stats.Speed,
                Strength = stats.Strength,
                Dexterity = stats.Dexterity,
                Intelligence = stats.Intelligence,
                Speed = stats.Speed,
                Atk = 0,
                Defense = 0,
                StrengthBuff = 0,
                NextTurnStrengthBonus = 0,
                GoFirstNextRound = false,
                Alive = true,
                BasicAttackName = BasicAttackName(playerClass),
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
                Ready = false,
                CharacterLevel = 1,
                Xp = 0,
            }
        );

        GiveStartingLoadout(ctx, ctx.Sender, entity, playerClass);

        var playerCount = session.PlayerCount + 1;
        ctx.Db.GameSession.Id.Update(session with { PlayerCount = playerCount });
        AddLog(ctx, $"{name} the {className} joined the party.");

        var rolled = ctx.Db.Entity.EntityId.Find(entity.EntityId) ?? entity;
        AddLog(
            ctx,
            $"{name} rolled STR {rolled.Strength} / DEX {rolled.Dexterity} / INT {rolled.Intelligence} / SPD {rolled.Speed}."
        );

        TryStartIfAllReady(ctx);
    }

    [SpacetimeDB.Reducer]
    public static void LeaveGame(ReducerContext ctx)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player)
        {
            return;
        }

        RemovePlayerFromRun(ctx, player);
    }

    /// Deletes a party member and everything tied to their seat. Shared by the
    /// explicit LeaveGame reducer. Disconnect keeps the row so a reconnect can
    /// resume; only LeaveGame removes the character from the run.
    static void RemovePlayerFromRun(ReducerContext ctx, Player player)
    {
        var session = RequireSession(ctx);
        var entityId = player.EntityId;
        var name = ctx.Db.Entity.EntityId.Find(entityId) is Entity entity
            ? entity.Name
            : $"slot {player.Slot}";
        var inBattle = session.Phase == BattlePhase.InBattle;
        var wasActive = inBattle && session.ActiveEntityId == entityId;
        var previousTurnIndex = session.TurnIndex;
        var previousActiveId = session.ActiveEntityId;

        if (wasActive)
        {
            ctx.Db.GameSession.Id.Update(session with { ActiveEntityId = 0 });
        }

        DeleteEntitySkills(ctx, entityId);
        DeleteOwnedItems(ctx, player.Identity);
        RemoveTurnOrderEntry(ctx, entityId);
        ctx.Db.Entity.EntityId.Delete(entityId);
        ctx.Db.Player.Identity.Delete(player.Identity);

        session = RequireSession(ctx);
        var playerCount = (uint)ctx.Db.Player.Count;
        ctx.Db.GameSession.Id.Update(session with { PlayerCount = playerCount });
        AddLog(ctx, $"{name} left the party.");

        if (playerCount == 0)
        {
            ResetStageState(ctx);
            return;
        }

        session = RequireSession(ctx);
        if (session.Phase == BattlePhase.InBattle)
        {
            if (LivingMembers(ctx, Team.Players).Count == 0)
            {
                EndBattleIfOver(ctx);
                return;
            }

            ContinueTurnsAfterRemoval(ctx, wasActive, previousTurnIndex, previousActiveId);
            return;
        }

        TryStartIfAllReady(ctx);
    }

    static void RemoveTurnOrderEntry(ReducerContext ctx, ulong entityId)
    {
        var remaining = ctx
            .Db.TurnOrder.Iter()
            .OrderBy(t => t.Idx)
            .Where(t => t.EntityId != entityId)
            .ToList();

        foreach (var entry in ctx.Db.TurnOrder.Iter().ToList())
        {
            ctx.Db.TurnOrder.Idx.Delete(entry.Idx);
        }

        for (var i = 0; i < remaining.Count; i++)
        {
            var row = remaining[i];
            ctx.Db.TurnOrder.Insert(row with { Idx = (uint)i });
        }
    }

    static void ContinueTurnsAfterRemoval(
        ReducerContext ctx,
        bool wasActive,
        uint previousTurnIndex,
        ulong previousActiveId
    )
    {
        if (EndBattleIfOver(ctx))
        {
            return;
        }

        var session = RequireSession(ctx);
        if (session.Phase != BattlePhase.InBattle)
        {
            return;
        }

        if (wasActive)
        {
            FindNextActor(ctx, session.Round, previousTurnIndex);
            return;
        }

        foreach (var entry in ctx.Db.TurnOrder.Iter())
        {
            if (entry.EntityId == previousActiveId)
            {
                ctx.Db.GameSession.Id.Update(session with { TurnIndex = entry.Idx });
                return;
            }
        }

        FindNextActor(ctx, session.Round, previousTurnIndex);
    }

    /// Ready-up for the lobby and rest stop. A player may un-ready until the
    /// next battle actually starts.
    [SpacetimeDB.Reducer]
    public static void SetReady(ReducerContext ctx, bool ready)
    {
        var session = RequireSession(ctx);
        if (!IsReadyUpPhase(session.Phase))
        {
            throw new Exception("Ready state can only change in the lobby or at a rest stop.");
        }

        var player = RequirePlayer(ctx);
        if (session.Phase == BattlePhase.RestStop && !IsLivingPlayer(ctx, player))
        {
            throw new Exception("Defeated players cannot ready up.");
        }

        if (player.Ready == ready)
        {
            return;
        }

        ctx.Db.Player.Identity.Update(player with { Ready = ready });
        TryStartIfAllReady(ctx);
    }

    /// Wipes the stage back to an empty lobby. Testing affordance only.
    [SpacetimeDB.Reducer]
    public static void ResetStage(ReducerContext ctx) => ResetStageState(ctx);

    /// Clears timers, turn order, entities, players, items, skills, and battle
    /// log, then returns the session to Waiting. Shared by the manual ResetStage
    /// reducer and the auto-reset that fires when every player disconnects.
    static void ResetStageState(ReducerContext ctx)
    {
        foreach (var timer in ctx.Db.EnemyTurnTimer.Iter().ToList())
        {
            ctx.Db.EnemyTurnTimer.ScheduledId.Delete(timer.ScheduledId);
        }

        ClearStageTransitionTimers(ctx);

        foreach (var entry in ctx.Db.TurnOrder.Iter().ToList())
        {
            ctx.Db.TurnOrder.Idx.Delete(entry.Idx);
        }

        foreach (var skill in ctx.Db.EntitySkill.Iter().ToList())
        {
            ctx.Db.EntitySkill.Id.Delete(skill.Id);
        }

        foreach (var item in ctx.Db.PlayerItem.Iter().ToList())
        {
            ctx.Db.PlayerItem.Id.Delete(item.Id);
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
                StageNumber = 0,
                UpcomingRestStop = false,
            }
        );
        AddLog(ctx, "Stage reset. Waiting for players.");
    }

    // ---------------------------------------------------------- player actions

    /// Free weapon swing. Requires an equipped weapon, matching Anthony's attack gate.
    [SpacetimeDB.Reducer]
    public static void Attack(ReducerContext ctx, ulong targetEntityId)
    {
        var attacker = RequireActingEntity(ctx);
        if (!HasEquippedWeapon(ctx, ctx.Sender))
        {
            throw new Exception("Equip a weapon to attack.");
        }

        var target = RequireEnemyOf(ctx, attacker, targetEntityId);

        ResolveHit(ctx, attacker, target, attacker.BasicAttackName, 0);
        AdvanceTurn(ctx);
    }

    /// Spends mana. Hits SkillDef.TargetCount enemies, or buffs when TargetCount is 0.
    [SpacetimeDB.Reducer]
    public static void CastSkill(ReducerContext ctx, uint skillDefId, ulong targetEntityId)
    {
        var caster = RequireActingEntity(ctx);

        if (ctx.Db.SkillDef.Id.Find(skillDefId) is not SkillDef skill || skill.IsEnemySkill)
        {
            throw new Exception("Unknown skill.");
        }

        if (!KnowsSkill(ctx, caster.EntityId, skillDefId))
        {
            throw new Exception("Your character has not learned that skill.");
        }

        if (caster.Mana < skill.ManaCost)
        {
            AddLog(
                ctx,
                $"Not enough mana. {caster.Name} needs {skill.ManaCost} mana for {skill.Name}."
            );
            return;
        }

        // A pure buff still needs an enemy to exist, but never needs a target.
        if (skill.TargetCount > 0)
        {
            RequireEnemyOf(ctx, caster, targetEntityId);
        }

        caster = SpendMana(ctx, caster, skill.ManaCost);
        ApplySkillSelfEffects(ctx, ref caster, skill);

        if (skill.TargetCount == 0 || skill.BaseDamage == 0)
        {
            AdvanceTurn(ctx);
            return;
        }

        var targets = SelectTargets(ctx, caster, targetEntityId, skill.TargetCount);
        foreach (var target in targets)
        {
            // Earlier hits in the volley can finish a target off.
            if (ctx.Db.Entity.EntityId.Find(target.EntityId) is Entity fresh && fresh.Alive)
            {
                var current = ctx.Db.Entity.EntityId.Find(caster.EntityId) ?? caster;
                ResolveHit(ctx, current, fresh, skill.Name, skill.BaseDamage);
            }
        }

        AdvanceTurn(ctx);
    }

    [SpacetimeDB.Reducer]
    public static void UseItem(ReducerContext ctx, ulong playerItemId)
    {
        var actor = RequireActingEntity(ctx);

        if (
            ctx.Db.PlayerItem.Id.Find(playerItemId) is not PlayerItem instance
            || instance.Owner != ctx.Sender
        )
        {
            throw new Exception("That item is not in your bag.");
        }

        if (ctx.Db.ItemDef.Id.Find(instance.ItemDefId) is not ItemDef def)
        {
            throw new Exception("Unknown item.");
        }

        if (def.Kind != ItemKind.Consumable)
        {
            throw new Exception($"{def.Name} is worn, not drunk.");
        }

        if (instance.Quantity <= 0)
        {
            AddLog(ctx, $"{actor.Name} has no {def.Name} left.");
            return;
        }

        var hp = Math.Min(actor.MaxHp, actor.Hp + def.HealAmount);
        var mana = Math.Min(actor.MaxMana, actor.Mana + def.ManaRestoreAmount);
        var healed = hp - actor.Hp;
        var restored = mana - actor.Mana;

        ctx.Db.Entity.EntityId.Update(actor with { Hp = hp, Mana = mana });
        ConsumeOne(ctx, instance);

        var effect = def.HealAmount > 0
            ? $"restores {healed} HP ({hp}/{actor.MaxHp})"
            : $"restores {restored} mana ({mana}/{actor.MaxMana})";
        AddLog(
            ctx,
            $"{actor.Name} drinks a {def.Name} and {effect}.",
            LogKind.Heal,
            actor.EntityId,
            actor.EntityId,
            healing: Math.Max(healed, restored)
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

    // ------------------------------------------------------------- inventory

    /// Equipping is allowed outside your turn: it is loadout management, not an action.
    [SpacetimeDB.Reducer]
    public static void EquipItem(ReducerContext ctx, ulong playerItemId)
    {
        var player = RequirePlayer(ctx);
        RejectEquipmentChangeInBattle(ctx);

        if (
            ctx.Db.PlayerItem.Id.Find(playerItemId) is not PlayerItem instance
            || instance.Owner != ctx.Sender
        )
        {
            throw new Exception("That item is not in your bag.");
        }

        if (ctx.Db.ItemDef.Id.Find(instance.ItemDefId) is not ItemDef def)
        {
            throw new Exception("Unknown item.");
        }

        if (def.Kind == ItemKind.Consumable)
        {
            throw new Exception("Potions are drunk, not worn.");
        }

        if (def.Kind == ItemKind.Weapon && def.WeaponType != ClassWeapon(player.Class))
        {
            throw new Exception($"A {ClassName(player.Class)} cannot wield a {def.Name}.");
        }

        var slot = SlotFor(def);
        if (slot == EquipSlot.Bag)
        {
            throw new Exception($"{def.Name} has no equipment slot.");
        }

        if (instance.EquippedSlot != EquipSlot.Bag)
        {
            throw new Exception($"{def.Name} is already equipped.");
        }

        // Whatever is in that slot goes back to the bag, so nothing is destroyed.
        foreach (var other in ctx.Db.PlayerItem.Owner.Filter(ctx.Sender).ToList())
        {
            if (other.Id != instance.Id && other.EquippedSlot == slot)
            {
                ctx.Db.PlayerItem.Id.Update(other with { EquippedSlot = EquipSlot.Bag });
            }
        }

        ctx.Db.PlayerItem.Id.Update(instance with { EquippedSlot = slot });
        RecomputeStats(ctx, ctx.Sender);

        var entity = ctx.Db.Entity.EntityId.Find(player.EntityId);
        AddLog(
            ctx,
            $"{entity?.Name ?? "Someone"} equips {def.Name}.",
            LogKind.Equip,
            player.EntityId,
            player.EntityId
        );
    }

    [SpacetimeDB.Reducer]
    public static void UnequipItem(ReducerContext ctx, ulong playerItemId)
    {
        var player = RequirePlayer(ctx);
        RejectEquipmentChangeInBattle(ctx);

        if (
            ctx.Db.PlayerItem.Id.Find(playerItemId) is not PlayerItem instance
            || instance.Owner != ctx.Sender
        )
        {
            throw new Exception("That item is not yours.");
        }

        if (instance.EquippedSlot == EquipSlot.Bag)
        {
            throw new Exception("That item is not equipped.");
        }

        if (BagCount(ctx, ctx.Sender) >= BagCapacity)
        {
            throw new Exception("Bag is full.");
        }

        ctx.Db.PlayerItem.Id.Update(instance with { EquippedSlot = EquipSlot.Bag });
        RecomputeStats(ctx, ctx.Sender);

        var def = ctx.Db.ItemDef.Id.Find(instance.ItemDefId);
        var entity = ctx.Db.Entity.EntityId.Find(player.EntityId);
        AddLog(
            ctx,
            $"{entity?.Name ?? "Someone"} stows {def?.Name ?? "an item"}.",
            LogKind.Equip,
            player.EntityId,
            player.EntityId
        );
    }

    /// Rebuilds effective stats from base rolls plus everything worn. Called after
    /// every equip change so gear bonuses can never stack twice.
    public static void RecomputeStats(ReducerContext ctx, Identity owner)
    {
        if (ctx.Db.Player.Identity.Find(owner) is not Player player)
        {
            return;
        }

        if (ctx.Db.Entity.EntityId.Find(player.EntityId) is not Entity entity)
        {
            return;
        }

        var atk = 0;
        var defense = 0;
        var strength = entity.BaseStrength;
        var dexterity = entity.BaseDexterity;
        var intelligence = entity.BaseIntelligence;
        var speed = entity.BaseSpeed;
        var hpBonus = 0;
        var manaBonus = 0;

        foreach (var instance in ctx.Db.PlayerItem.Owner.Filter(owner))
        {
            if (instance.EquippedSlot == EquipSlot.Bag)
            {
                continue;
            }

            if (ctx.Db.ItemDef.Id.Find(instance.ItemDefId) is not ItemDef def)
            {
                continue;
            }

            atk += def.AtkBonus;
            defense += def.DefenseBonus;
            strength += def.StrengthBonus;
            dexterity += def.DexterityBonus;
            intelligence += def.IntelligenceBonus;
            speed += def.SpeedBonus;
            hpBonus += def.MaxHpBonus;
            manaBonus += def.MaxManaBonus;
        }

        var maxHp = ClassMaxHp(player.Class) + hpBonus;
        var maxMana = ClassMaxMana(player.Class) + manaBonus + intelligence;

        // Gaining max HP grants the difference; losing it only ever clamps.
        var hp = entity.Hp + Math.Max(0, maxHp - entity.MaxHp);
        var mana = entity.Mana + Math.Max(0, maxMana - entity.MaxMana);

        ctx.Db.Entity.EntityId.Update(
            entity with
            {
                Strength = strength,
                Dexterity = dexterity,
                Intelligence = intelligence,
                Speed = speed,
                Atk = atk,
                Defense = defense,
                MaxHp = maxHp,
                Hp = Math.Clamp(hp, entity.Alive ? 1 : 0, maxHp),
                MaxMana = maxMana,
                Mana = Math.Clamp(mana, 0, maxMana),
            }
        );
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

        var skill = PickEnemySkill(ctx, enemy);
        if (skill is SkillDef chosen)
        {
            enemy = SpendMana(ctx, enemy, chosen.ManaCost);
            var picked = ChooseEnemyTargets(ctx, targets, chosen.TargetCount);
            foreach (var target in picked)
            {
                if (ctx.Db.Entity.EntityId.Find(target.EntityId) is Entity fresh && fresh.Alive)
                {
                    var current = ctx.Db.Entity.EntityId.Find(enemy.EntityId) ?? enemy;
                    ResolveHit(ctx, current, fresh, chosen.Name, chosen.BaseDamage);
                }
            }
        }
        else
        {
            // Out of mana for everything it knows, so it swings and recovers.
            ApplyFocus(ctx, enemy);
        }

        AdvanceTurn(ctx);
    }

    static SkillDef? PickEnemySkill(ReducerContext ctx, Entity enemy)
    {
        var affordable = new List<SkillDef>();
        foreach (var known in ctx.Db.EntitySkill.EntityId.Filter(enemy.EntityId))
        {
            if (
                ctx.Db.SkillDef.Id.Find(known.SkillDefId) is SkillDef skill
                && skill.ManaCost <= enemy.Mana
            )
            {
                affordable.Add(skill);
            }
        }

        if (affordable.Count == 0)
        {
            return null;
        }

        affordable.Sort((a, b) => a.Id.CompareTo(b.Id));
        return affordable[ctx.Rng.Next(0, affordable.Count)];
    }

    static List<Entity> ChooseEnemyTargets(ReducerContext ctx, List<Entity> living, int count)
    {
        if (count <= 1)
        {
            return new List<Entity> { living[ctx.Rng.Next(0, living.Count)] };
        }

        return living.OrderBy(e => e.Slot).Take(count).ToList();
    }

    // ------------------------------------------------------------ battle rules

    /// Starts the next fight once every living connected player is ready.
    /// Offline seats and corpses do not count, so a leave or disconnect can
    /// unblock the rest. Never starts a stage with an empty party.
    static void TryStartIfAllReady(ReducerContext ctx)
    {
        var session = RequireSession(ctx);
        if (!IsReadyUpPhase(session.Phase) || ctx.Db.Player.Count == 0)
        {
            return;
        }

        var living = ctx.Db.Player.Iter().Where(p => p.Online && IsLivingPlayer(ctx, p)).ToList();
        if (living.Count == 0 || living.Any(p => !p.Ready))
        {
            return;
        }

        if (session.Phase == BattlePhase.RestStop)
        {
            BeginNextStage(ctx);
            return;
        }

        StartRun(ctx);
    }

    /// Lobby and rest stop share ready-up and loadout. Every other phase is locked.
    static bool IsReadyUpPhase(BattlePhase phase) =>
        phase is BattlePhase.Waiting or BattlePhase.RestStop;

    static bool EquipmentChangesAllowed(BattlePhase phase) => IsReadyUpPhase(phase);

    static void RejectEquipmentChangeInBattle(ReducerContext ctx)
    {
        var session = RequireSession(ctx);
        if (!EquipmentChangesAllowed(session.Phase))
        {
            throw new Exception("Cannot change equipment during battle.");
        }

        if (!IsLivingPlayer(ctx, RequirePlayer(ctx)))
        {
            throw new Exception("Defeated players cannot change equipment.");
        }
    }

    static void StartRun(ReducerContext ctx)
    {
        if (LivingMembers(ctx, Team.Players).Count == 0)
        {
            return;
        }

        var session = RequireSession(ctx);
        ctx.Db.GameSession.Id.Update(session with { StageNumber = 1 });
        AddLog(ctx, "Stage 1 begins.");
        EnterBattle(ctx);
    }

    static void EnterBattle(ReducerContext ctx)
    {
        ClearReadyFlags(ctx);
        ClearEnemySide(ctx);
        PreparePlayersForStage(ctx);
        SpawnEnemies(ctx);

        var session = RequireSession(ctx);
        ctx.Db.GameSession.Id.Update(
            session with
            {
                Phase = BattlePhase.InBattle,
                Round = 1,
                TurnIndex = 0,
                ActiveEntityId = 0,
                UpcomingRestStop = false,
            }
        );

        var spawned = LivingMembers(ctx, Team.Enemies).Count;
        AddLog(ctx, $"{spawned} enemies appeared!");
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

    static void BeginNextStage(ReducerContext ctx)
    {
        if (LivingMembers(ctx, Team.Players).Count == 0)
        {
            return;
        }

        var session = RequireSession(ctx);
        var next = session.StageNumber + 1;
        ctx.Db.GameSession.Id.Update(session with { StageNumber = next });
        AddLog(ctx, $"Stage {next} begins.");
        EnterBattle(ctx);
    }

    static void OnStageCleared(ReducerContext ctx)
    {
        var session = RequireSession(ctx);
        var cleared = session.StageNumber < 1 ? 1u : session.StageNumber;
        ClearEnemySide(ctx);
        AddLog(ctx, $"Stage {cleared} cleared.");

        if (cleared >= MaxStageCount)
        {
            ClearReadyFlags(ctx);
            ctx.Db.GameSession.Id.Update(
                session with
                {
                    Phase = BattlePhase.Victory,
                    ActiveEntityId = 0,
                    UpcomingRestStop = false,
                }
            );
            AddLog(ctx, "The party is victorious!");
            return;
        }

        EnterStageTransition(ctx, ShouldEnterRestStop(cleared));
    }

    static bool ShouldEnterRestStop(uint clearedStage) =>
        RestStopEvery > 0 && clearedStage < MaxStageCount && clearedStage % RestStopEvery == 0;

    static void EnterStageTransition(ReducerContext ctx, bool upcomingRestStop)
    {
        ClearReadyFlags(ctx);
        ClearStageTransitionTimers(ctx);

        var session = RequireSession(ctx);
        ctx.Db.GameSession.Id.Update(
            session with
            {
                Phase = BattlePhase.StageTransition,
                Round = 0,
                TurnIndex = 0,
                ActiveEntityId = 0,
                UpcomingRestStop = upcomingRestStop,
            }
        );

        ctx.Db.StageTransitionTimer.Insert(
            new StageTransitionTimer
            {
                ScheduledId = 0,
                ScheduledAt = new ScheduleAt.Time(
                    ctx.Timestamp + new TimeDuration(StageTransitionDelayMicros)
                ),
            }
        );
    }

    [SpacetimeDB.Reducer]
    public static void AdvanceStageTransition(ReducerContext ctx, StageTransitionTimer timer)
    {
        if (ctx.Sender != ctx.DatabaseIdentity)
        {
            throw new Exception("AdvanceStageTransition may only be invoked by the scheduler.");
        }

        var session = RequireSession(ctx);
        if (session.Phase != BattlePhase.StageTransition)
        {
            return;
        }

        if (session.UpcomingRestStop)
        {
            EnterRestStop(ctx);
            return;
        }

        BeginNextStage(ctx);
    }

    static void ClearStageTransitionTimers(ReducerContext ctx)
    {
        foreach (var timer in ctx.Db.StageTransitionTimer.Iter().ToList())
        {
            ctx.Db.StageTransitionTimer.ScheduledId.Delete(timer.ScheduledId);
        }
    }

    static void EnterRestStop(ReducerContext ctx)
    {
        ClearReadyFlags(ctx);
        RestorePartyAtRest(ctx);

        var session = RequireSession(ctx);
        ctx.Db.GameSession.Id.Update(
            session with
            {
                Phase = BattlePhase.RestStop,
                Round = 0,
                TurnIndex = 0,
                ActiveEntityId = 0,
                UpcomingRestStop = false,
            }
        );
        AddLog(ctx, "Rest stop. HP and mana restored. Ready up to continue.");
    }

    static void RestorePartyAtRest(ReducerContext ctx)
    {
        foreach (var entity in ctx.Db.Entity.Iter().ToList())
        {
            if (entity.Faction != Team.Players || !entity.Alive)
            {
                continue;
            }

            ctx.Db.Entity.EntityId.Update(
                entity with
                {
                    Hp = entity.MaxHp,
                    Mana = entity.MaxMana,
                }
            );
        }
    }

    static void ClearReadyFlags(ReducerContext ctx)
    {
        foreach (var player in ctx.Db.Player.Iter().ToList())
        {
            if (!player.Ready)
            {
                continue;
            }

            ctx.Db.Player.Identity.Update(player with { Ready = false });
        }
    }

    static void ClearEnemySide(ReducerContext ctx)
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
            if (entity.Faction != Team.Enemies)
            {
                continue;
            }

            DeleteEntitySkills(ctx, entity.EntityId);
            ctx.Db.Entity.EntityId.Delete(entity.EntityId);
        }
    }

    static void PreparePlayersForStage(ReducerContext ctx)
    {
        foreach (var entity in ctx.Db.Entity.Iter().ToList())
        {
            if (entity.Faction != Team.Players || !entity.Alive)
            {
                continue;
            }

            ctx.Db.Entity.EntityId.Update(entity with { Mana = entity.MaxMana });
        }
    }

    static void SpawnEnemies(ReducerContext ctx)
    {
        EnsureEnemyCatalog(ctx);
        var session = RequireSession(ctx);
        var stage = session.StageNumber < 1 ? 1u : session.StageNumber;
        var pool = EnemyPool;
        var count = ctx.Rng.Next(1, (int)MaxEnemySlots + 1);
        var vitalityBps = PackVitalityBps(count);
        var powerBps = PackPowerBps(count);

        for (uint slot = 0; slot < (uint)count; slot++)
        {
            var arch = pool[ctx.Rng.Next(0, pool.Length)];
            var maxHp = ScaleByBps(ApplyStageScale(arch.MaxHp, stage), vitalityBps);
            var maxMana = ScaleByBps(ApplyStageScale(arch.MaxMana, stage), vitalityBps);
            var strength = ScaleByBps(ApplyStageScale(arch.Strength, stage), powerBps);
            var dexterity = ScaleByBps(ApplyStageScale(arch.Dexterity, stage), powerBps);
            var intelligence = ScaleByBps(ApplyStageScale(arch.Intelligence, stage), powerBps);
            var speed = ScaleByBps(ApplyStageScale(arch.Speed, stage), powerBps);
            var atk = ScaleByBps(ApplyStageScale(arch.Atk, stage), powerBps);
            var defense = ScaleByBps(ApplyStageScale(Math.Max(0, arch.Defense), stage), powerBps);
            if (arch.Defense == 0)
            {
                defense = 0;
            }

            var enemy = ctx.Db.Entity.Insert(
                new Entity
                {
                    EntityId = 0,
                    Faction = Team.Enemies,
                    Slot = slot,
                    Name = arch.Name,
                    ClassName = arch.Kind,
                    MaxHp = maxHp,
                    Hp = maxHp,
                    MaxMana = maxMana,
                    Mana = maxMana,
                    BaseStrength = strength,
                    BaseDexterity = dexterity,
                    BaseIntelligence = intelligence,
                    BaseSpeed = speed,
                    Strength = strength,
                    Dexterity = dexterity,
                    Intelligence = intelligence,
                    Speed = speed,
                    Atk = atk,
                    Defense = defense,
                    StrengthBuff = 0,
                    NextTurnStrengthBonus = 0,
                    GoFirstNextRound = false,
                    Alive = true,
                    BasicAttackName = arch.BasicAttackName,
                }
            );

            GrantSkillsByName(ctx, enemy.EntityId, new[] { arch.SkillName });
        }
    }

    /// Fastest combatant first. Rush skills jump the queue for exactly one round.
    static void BuildTurnOrder(ReducerContext ctx)
    {
        foreach (var entry in ctx.Db.TurnOrder.Iter().ToList())
        {
            ctx.Db.TurnOrder.Idx.Delete(entry.Idx);
        }

        var ordered = ctx
            .Db.Entity.Iter()
            .Where(e => e.Alive)
            .OrderByDescending(e => e.GoFirstNextRound)
            .ThenByDescending(e => e.Speed)
            .ThenBy(e => e.Faction == Team.Players ? 0 : 1)
            .ThenBy(e => e.Slot)
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            ctx.Db.TurnOrder.Insert(
                new TurnOrder
                {
                    Idx = (uint)i,
                    EntityId = ordered[i].EntityId,
                    Speed = ordered[i].Speed,
                    HasActed = false,
                    IsRush = ordered[i].GoFirstNextRound,
                }
            );
        }

        // The rush flag is spent by being placed, so it never carries over.
        foreach (var entity in ordered)
        {
            if (entity.GoFirstNextRound)
            {
                ctx.Db.Entity.EntityId.Update(entity with { GoFirstNextRound = false });
            }
        }
    }

    static void AdvanceTurn(ReducerContext ctx)
    {
        MarkActed(ctx);

        if (EndBattleIfOver(ctx))
        {
            return;
        }

        var session = RequireSession(ctx);
        FindNextActor(ctx, session.Round, session.TurnIndex + 1);
    }

    static void FindNextActor(ReducerContext ctx, uint round, uint idx)
    {
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

    static void MarkActed(ReducerContext ctx)
    {
        var session = RequireSession(ctx);
        if (
            session.ActiveEntityId != 0
            && ctx.Db.TurnOrder.Idx.Find(session.TurnIndex) is TurnOrder entry
            && entry.EntityId == session.ActiveEntityId
        )
        {
            ctx.Db.TurnOrder.Idx.Update(entry with { HasActed = true });
        }
    }

    static void SetActive(ReducerContext ctx, uint round, uint idx, Entity entity)
    {
        // Mana trickles back and one-turn buffs land right before the turn starts.
        var refreshed = entity with
        {
            Mana = Math.Min(entity.MaxMana, entity.Mana + ManaRegenPerTurn),
            StrengthBuff = entity.NextTurnStrengthBonus,
            NextTurnStrengthBonus = 0,
        };
        ctx.Db.Entity.EntityId.Update(refreshed);

        var session = RequireSession(ctx);
        ctx.Db.GameSession.Id.Update(
            session with
            {
                Round = round,
                TurnIndex = idx,
                ActiveEntityId = refreshed.EntityId,
            }
        );

        AddLog(
            ctx,
            $"Round {round} - {refreshed.Name}'s turn.",
            LogKind.TurnStart,
            refreshed.EntityId,
            refreshed.EntityId
        );

        if (refreshed.Faction == Team.Enemies)
        {
            ctx.Db.EnemyTurnTimer.Insert(
                new EnemyTurnTimer
                {
                    ScheduledId = 0,
                    ScheduledAt = new ScheduleAt.Time(
                        ctx.Timestamp + new TimeDuration(EnemyTurnDelayMicros)
                    ),
                    EntityId = refreshed.EntityId,
                }
            );
        }
    }

    static bool EndBattleIfOver(ReducerContext ctx)
    {
        var session = RequireSession(ctx);
        if (session.Phase != BattlePhase.InBattle)
        {
            return session.Phase is BattlePhase.Victory or BattlePhase.Defeat or BattlePhase.StageTransition;
        }

        var playersAlive = LivingMembers(ctx, Team.Players).Count;
        var enemiesAlive = LivingMembers(ctx, Team.Enemies).Count;

        if (enemiesAlive == 0)
        {
            OnStageCleared(ctx);
            return true;
        }

        if (playersAlive == 0)
        {
            ClearReadyFlags(ctx);
            ctx.Db.GameSession.Id.Update(
                session with
                {
                    Phase = BattlePhase.Defeat,
                    ActiveEntityId = 0,
                    UpcomingRestStop = false,
                }
            );
            AddLog(ctx, "The whole party has fallen. Defeat.");
            return true;
        }

        return false;
    }

    /// The one place damage is computed, for players and enemies alike.
    static void ResolveHit(
        ReducerContext ctx,
        Entity attacker,
        Entity target,
        string actionName,
        int skillBaseDamage
    )
    {
        var attackerClass = ClassOf(ctx, attacker);
        var characterDamage =
            skillBaseDamage
            + ClassDamageStat(
                attackerClass,
                attacker.Dexterity,
                attacker.Intelligence,
                attacker.Speed
            );

        var strength = attacker.Strength + attacker.StrengthBuff;
        var raw = DealtDamage(characterDamage, strength, attacker.Atk);

        var dodged = ctx.Rng.Next(1, 101) <= DodgeChance(target.Dexterity, target.Faction);
        var damage = dodged ? 0 : AfterDefense(raw, target.Defense);

        var hp = Math.Max(0, target.Hp - damage);
        var alive = hp > 0;
        ctx.Db.Entity.EntityId.Update(target with { Hp = hp, Alive = alive });

        if (dodged)
        {
            AddLog(
                ctx,
                $"{attacker.Name} uses {actionName} on {target.Name} but {target.Name} dodges.",
                LogKind.Attack,
                attacker.EntityId,
                target.EntityId
            );
        }
        else
        {
            AddLog(
                ctx,
                $"{attacker.Name} uses {actionName} on {target.Name} for {damage} damage.",
                LogKind.Attack,
                attacker.EntityId,
                target.EntityId,
                damage
            );
        }

        if (!alive)
        {
            AddLog(
                ctx,
                $"{target.Name} is defeated!",
                LogKind.Defeat,
                attacker.EntityId,
                target.EntityId
            );

            if (target.Faction == Team.Enemies)
            {
                var session = RequireSession(ctx);
                GrantKillXp(ctx, session.StageNumber < 1 ? 1u : session.StageNumber);
            }
        }
    }

    static void ApplySkillSelfEffects(ReducerContext ctx, ref Entity caster, SkillDef skill)
    {
        if (!skill.AlwaysGoFirst && skill.NextTurnStrengthBonus == 0)
        {
            return;
        }

        var updated = caster with
        {
            GoFirstNextRound = caster.GoFirstNextRound || skill.AlwaysGoFirst,
            NextTurnStrengthBonus = caster.NextTurnStrengthBonus + skill.NextTurnStrengthBonus,
        };
        ctx.Db.Entity.EntityId.Update(updated);
        caster = updated;

        if (skill.NextTurnStrengthBonus > 0)
        {
            AddLog(
                ctx,
                $"{caster.Name} uses {skill.Name} and gains +{skill.NextTurnStrengthBonus} strength next turn.",
                LogKind.Focus,
                caster.EntityId,
                caster.EntityId
            );
        }
        else
        {
            AddLog(
                ctx,
                $"{caster.Name} uses {skill.Name} and will strike first next round.",
                LogKind.Focus,
                caster.EntityId,
                caster.EntityId
            );
        }
    }

    static void ApplyFocus(ReducerContext ctx, Entity actor)
    {
        var mana = Math.Min(actor.MaxMana, actor.Mana + FocusManaGain);
        var restored = mana - actor.Mana;
        ctx.Db.Entity.EntityId.Update(actor with { Mana = mana });
        AddLog(
            ctx,
            $"{actor.Name} focuses and restores {restored} mana ({mana}/{actor.MaxMana}).",
            LogKind.Focus,
            actor.EntityId,
            actor.EntityId
        );
    }

    /// Every living party member receives kill EXP. Dead players sit this one out.
    static void GrantKillXp(ReducerContext ctx, uint stage)
    {
        var xp = KillXp(stage);
        foreach (var player in ctx.Db.Player.Iter().ToList())
        {
            if (ctx.Db.Entity.EntityId.Find(player.EntityId) is not Entity entity || !entity.Alive)
            {
                continue;
            }

            ApplyXp(ctx, player, entity.Name, xp);
        }
    }

    static void ApplyXp(ReducerContext ctx, Player player, string name, uint xp)
    {
        if (xp == 0)
        {
            return;
        }

        var currentXp = SaturatingAdd(player.Xp, xp);
        var level = player.CharacterLevel == 0 ? 1u : player.CharacterLevel;
        var reached = new List<uint>();
        while (true)
        {
            var need = XpToNextLevel(level);
            if (need == 0 || currentXp < need)
            {
                break;
            }

            currentXp = SaturatingSub(currentXp, need);
            level += 1;
            reached.Add(level);
        }

        ctx.Db.Player.Identity.Update(
            player with
            {
                Xp = currentXp,
                CharacterLevel = level,
            }
        );

        AddLog(ctx, $"{name} gained {xp} EXP.");

        if (reached.Count == 0)
        {
            return;
        }

        GrowMainStat(ctx, player, (int)(StatPointsPerLevel * (uint)reached.Count));
        foreach (var newLevel in reached)
        {
            AddLog(ctx, $"{name} reached level {newLevel}!");
        }
    }

    /// Anthony granted 2 unspent points per level. This branch has no allocate UI,
    /// so those points land on the class main stat and then gear is recomputed.
    static void GrowMainStat(ReducerContext ctx, Player player, int points)
    {
        if (points <= 0 || ctx.Db.Entity.EntityId.Find(player.EntityId) is not Entity entity)
        {
            return;
        }

        var grown = MainStat(player.Class) switch
        {
            StatType.Strength => entity with { BaseStrength = entity.BaseStrength + points },
            StatType.Dexterity => entity with { BaseDexterity = entity.BaseDexterity + points },
            StatType.Intelligence =>
                entity with { BaseIntelligence = entity.BaseIntelligence + points },
            _ => entity with { BaseSpeed = entity.BaseSpeed + points },
        };
        ctx.Db.Entity.EntityId.Update(grown);
        RecomputeStats(ctx, player.Identity);
    }

    // ---------------------------------------------------------------- helpers

    static Entity SpendMana(ReducerContext ctx, Entity entity, int cost)
    {
        if (cost <= 0)
        {
            return entity;
        }

        var updated = entity with { Mana = Math.Max(0, entity.Mana - cost) };
        ctx.Db.Entity.EntityId.Update(updated);
        return updated;
    }

    /// The preferred target first, then the rest of the living enemy line.
    static List<Entity> SelectTargets(
        ReducerContext ctx,
        Entity attacker,
        ulong preferredId,
        int count
    )
    {
        var enemyTeam = attacker.Faction == Team.Players ? Team.Enemies : Team.Players;
        var living = LivingMembers(ctx, enemyTeam);
        var selected = new List<Entity>();

        foreach (var candidate in living)
        {
            if (candidate.EntityId == preferredId)
            {
                selected.Add(candidate);
                break;
            }
        }

        foreach (var candidate in living)
        {
            if (selected.Count >= count)
            {
                break;
            }

            if (candidate.EntityId != preferredId)
            {
                selected.Add(candidate);
            }
        }

        return selected;
    }

    static PlayerClass ClassOf(ReducerContext ctx, Entity entity)
    {
        if (entity.Faction != Team.Players)
        {
            return PlayerClass.Warrior;
        }

        foreach (var player in ctx.Db.Player.Iter())
        {
            if (player.EntityId == entity.EntityId)
            {
                return player.Class;
            }
        }

        return PlayerClass.Warrior;
    }

    static bool KnowsSkill(ReducerContext ctx, ulong entityId, uint skillDefId)
    {
        foreach (var known in ctx.Db.EntitySkill.EntityId.Filter(entityId))
        {
            if (known.SkillDefId == skillDefId)
            {
                return true;
            }
        }

        return false;
    }

    static void ConsumeOne(ReducerContext ctx, PlayerItem instance)
    {
        if (instance.Quantity <= 1)
        {
            ctx.Db.PlayerItem.Id.Delete(instance.Id);
            return;
        }

        ctx.Db.PlayerItem.Id.Update(instance with { Quantity = instance.Quantity - 1 });
    }

    static void DeleteEntitySkills(ReducerContext ctx, ulong entityId)
    {
        foreach (var known in ctx.Db.EntitySkill.EntityId.Filter(entityId).ToList())
        {
            ctx.Db.EntitySkill.Id.Delete(known.Id);
        }
    }

    static void DeleteOwnedItems(ReducerContext ctx, Identity owner)
    {
        foreach (var item in ctx.Db.PlayerItem.Owner.Filter(owner).ToList())
        {
            ctx.Db.PlayerItem.Id.Delete(item.Id);
        }
    }

    /// Every player action funnels through here: right phase, right caller,
    /// right turn, still alive.
    static Entity RequireActingEntity(ReducerContext ctx)
    {
        var session = RequireSession(ctx);
        if (session.Phase != BattlePhase.InBattle)
        {
            throw new Exception("The battle is not running.");
        }

        var player = RequirePlayer(ctx);

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

    static Entity RequireEnemyOf(ReducerContext ctx, Entity attacker, ulong targetEntityId)
    {
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
            throw new Exception("That target is already defeated.");
        }

        return target;
    }

    static Player RequirePlayer(ReducerContext ctx)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is Player player)
        {
            return player;
        }

        throw new Exception("You are not in the party.");
    }

    static bool HasEquippedWeapon(ReducerContext ctx, Identity owner)
    {
        foreach (var item in ctx.Db.PlayerItem.Owner.Filter(owner))
        {
            if (item.EquippedSlot == EquipSlot.Weapon)
            {
                return true;
            }
        }

        return false;
    }

    static List<Entity> LivingMembers(ReducerContext ctx, Team faction) =>
        ctx.Db.Entity.Iter()
            .Where(e => e.Faction == faction && e.Alive)
            .OrderBy(e => e.Slot)
            .ToList();

    static bool IsLivingPlayer(ReducerContext ctx, Player player) =>
        ctx.Db.Entity.EntityId.Find(player.EntityId) is Entity entity && entity.Alive;

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

    static void AddLog(
        ReducerContext ctx,
        string message,
        LogKind kind = LogKind.Info,
        ulong actorEntityId = 0,
        ulong targetEntityId = 0,
        int damage = 0,
        int healing = 0
    )
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
                Kind = kind,
                ActorEntityId = actorEntityId,
                TargetEntityId = targetEntityId,
                Damage = damage,
                Healing = healing,
                CreatedAt = ctx.Timestamp,
            }
        );

        Log.Info(message);
    }
}
