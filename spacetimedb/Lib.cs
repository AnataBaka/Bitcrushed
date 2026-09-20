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
                CurrentBiome = WorldBiome.Plains,
                NextBiome = WorldBiome.Plains,
                IsBossStage = false,
                BossLootGranted = false,
                RestAmuletGranted = false,
                StageClearNote = "",
            }
        );

        SeedCatalog(ctx);
        EnsureSkillCatalog(ctx);
        EnsureItemCatalog(ctx);
        Log.Info("Testing Fight Stage initialized.");
    }

    [SpacetimeDB.Reducer(ReducerKind.ClientConnected)]
    public static void ClientConnected(ReducerContext ctx)
    {
        EnsureSkillCatalog(ctx);
        EnsureItemCatalog(ctx);
        EnsureBiomeDefs(ctx);
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
    public static void JoinGame(ReducerContext ctx, string requestedName)
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

        var name = ResolveJoinName(ctx, requestedName);
        var slot = FindFreeSlot(ctx);
        var rng = PerSenderRng(ctx);
        var playerClass = (PlayerClass)rng.Next(0, 4);
        var stats = RollStats(rng, playerClass);
        var className = ClassName(playerClass);
        var maxHp = ClassMaxHp(playerClass, 1);
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
                MagicBulletStage = 1,
                VariantPrefix = "",
                TintR = 255,
                TintG = 255,
                TintB = 255,
                IsBoss = false,
                SkillCooldown = 0,
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
                UnspentStatPoints = 0,
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
        DeletePendingRewards(ctx, player.Identity);
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

        foreach (var pending in ctx.Db.PendingReward.Iter().ToList())
        {
            ctx.Db.PendingReward.Id.Delete(pending.Id);
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
                AttackRedirectEntityId = 0,
                CurrentBiome = WorldBiome.Plains,
                NextBiome = WorldBiome.Plains,
                IsBossStage = false,
                BossLootGranted = false,
                RestAmuletGranted = false,
                StageClearNote = "",
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

        ResolveHit(ctx, attacker, target, attacker.BasicAttackName, 0, isSkill: false);
        MarkUsedAttack(ctx, attacker.EntityId);
        AdvanceTurn(ctx);
    }

    /// Spends mana and resolves the named class skill.
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

        ValidatePlayerSkill(ctx, caster, skill, targetEntityId);

        var manaCost = ApplySpellManaDiscount(
            EffectiveSkillManaCost(skill.Name, skill.ManaCost, caster),
            HasEmeraldPendant(ctx, caster)
        );
        if (caster.Mana < manaCost)
        {
            AddLog(
                ctx,
                $"Not enough mana. {caster.Name} needs {manaCost} mana for {skill.Name}."
            );
            return;
        }

        caster = SpendMana(ctx, caster, manaCost);
        ExecutePlayerSkill(ctx, caster, skill, targetEntityId);
        if (IsDamagingSkill(skill.Name))
        {
            MarkUsedAttack(ctx, caster.EntityId);
        }

        if (EndBattleIfOver(ctx))
        {
            return;
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
    public static void EquipItem(ReducerContext ctx, uint slotIndex)
    {
        var player = RequirePlayer(ctx);
        RejectEquipmentChangeInBattle(ctx);

        if (slotIndex >= InventoryCapacity)
        {
            throw new Exception("That inventory slot does not exist.");
        }

        if (InventoryAt(ctx, ctx.Sender, slotIndex) is not PlayerItem instance)
        {
            throw new Exception("That slot is empty.");
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
        if (slot is not (EquipSlot.Weapon or EquipSlot.Amulet))
        {
            throw new Exception($"{def.Name} has no equipment slot.");
        }

        PlayerItem? previous = null;
        foreach (var other in ctx.Db.PlayerItem.Owner.Filter(ctx.Sender).ToList())
        {
            if (other.Id != instance.Id && other.EquippedSlot == slot)
            {
                previous = other;
                break;
            }
        }

        ctx.Db.PlayerItem.Id.Update(
            instance with { EquippedSlot = slot, InventoryIndex = InventoryNone }
        );

        if (previous is PlayerItem worn)
        {
            ctx.Db.PlayerItem.Id.Update(
                worn with { EquippedSlot = EquipSlot.Inventory, InventoryIndex = slotIndex }
            );
        }

        RecomputeStats(ctx, ctx.Sender);

        var entity = ctx.Db.Entity.EntityId.Find(player.EntityId);
        AddLog(
            ctx,
            $"{entity?.Name ?? "Someone"} equips {def.Name}.",
            LogKind.Equip,
            player.EntityId,
            player.EntityId
        );
        DeliverPendingRewardsFor(ctx, ctx.Sender);
    }

    [SpacetimeDB.Reducer]
    public static void DropItem(ReducerContext ctx, uint slotIndex)
    {
        var player = RequirePlayer(ctx);
        RejectEquipmentChangeInBattle(ctx);

        if (slotIndex >= InventoryCapacity)
        {
            throw new Exception("That inventory slot does not exist.");
        }

        if (InventoryAt(ctx, ctx.Sender, slotIndex) is not PlayerItem instance)
        {
            throw new Exception("That slot is empty.");
        }

        var def = ctx.Db.ItemDef.Id.Find(instance.ItemDefId);
        ctx.Db.PlayerItem.Id.Delete(instance.Id);

        var entity = ctx.Db.Entity.EntityId.Find(player.EntityId);
        AddLog(
            ctx,
            $"{entity?.Name ?? "Someone"} drops {def?.Name ?? "an item"}.",
            LogKind.Equip,
            player.EntityId,
            player.EntityId
        );
        DeliverPendingRewardsFor(ctx, ctx.Sender);
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

        if (instance.EquippedSlot is not (EquipSlot.Weapon or EquipSlot.Amulet))
        {
            throw new Exception("That item is not equipped.");
        }

        if (!TryPlaceExistingItemInInventory(ctx, instance))
        {
            throw new Exception("Inventory is full.");
        }

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
        DeliverPendingRewardsFor(ctx, ctx.Sender);
    }

    static PlayerItem? InventoryAt(ReducerContext ctx, Identity owner, uint slotIndex)
    {
        foreach (var item in ctx.Db.PlayerItem.Owner.Filter(owner))
        {
            if (item.EquippedSlot == EquipSlot.Inventory && item.InventoryIndex == slotIndex)
            {
                return item;
            }
        }

        return null;
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
            if (instance.EquippedSlot is not (EquipSlot.Weapon or EquipSlot.Amulet))
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

        if (entity.FinishTheJobStance)
        {
            atk += 6;
        }

        if (entity.DoubleStrength)
        {
            strength *= 2;
        }

        var maxHp = ClassMaxHp(player.Class, player.CharacterLevel) + hpBonus;
        var intelMana = player.Class == PlayerClass.Mage
            ? MageManaFromIntelligence(intelligence)
            : intelligence;
        var maxMana = ClassMaxMana(player.Class) + manaBonus + intelMana;

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

        if (enemy.IsBoss)
        {
            TakeBossTurn(ctx, enemy, targets);
            return;
        }

        var skill = PickEnemySkill(ctx, enemy);
        if (skill is SkillDef chosen)
        {
            enemy = SpendMana(ctx, enemy, chosen.ManaCost);
            var picked = RedirectedOrChosenTargets(ctx, enemy, targets, chosen.TargetCount);
            foreach (var target in picked)
            {
                if (ctx.Db.Entity.EntityId.Find(target.EntityId) is Entity fresh && fresh.Alive)
                {
                    var current = ctx.Db.Entity.EntityId.Find(enemy.EntityId) ?? enemy;
                    ResolveHit(
                        ctx,
                        current,
                        fresh,
                        EnemyLoggedActionName(current, chosen.Name),
                        0,
                        isSkill: true
                    );
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

    /// Cooldown skills only. The boss never Focuses, even with leftover mana.
    static void TakeBossTurn(ReducerContext ctx, Entity enemy, List<Entity> livingPlayers)
    {
        var cooldown = Math.Max(0, enemy.SkillCooldown);
        if (cooldown > 0)
        {
            cooldown--;
        }

        var sweep = cooldown == 0;
        if (sweep)
        {
            cooldown = FlameSweepInterval;
        }

        ctx.Db.Entity.EntityId.Update(
            (ctx.Db.Entity.EntityId.Find(enemy.EntityId) ?? enemy) with { SkillCooldown = cooldown }
        );
        enemy = ctx.Db.Entity.EntityId.Find(enemy.EntityId) ?? enemy;

        if (sweep)
        {
            AddLog(
                ctx,
                $"{enemy.Name} uses {FlameSweepName}!",
                LogKind.Aoe,
                enemy.EntityId,
                0
            );
            foreach (var target in livingPlayers)
            {
                if (ctx.Db.Entity.EntityId.Find(target.EntityId) is not Entity fresh || !fresh.Alive)
                {
                    continue;
                }

                var current = ctx.Db.Entity.EntityId.Find(enemy.EntityId) ?? enemy;
                var hit = ResolveHit(ctx, current, fresh, FlameSweepName, 0, isSkill: true);
                if (
                    hit.Connected
                    && ctx.Db.Entity.EntityId.Find(fresh.EntityId) is Entity burned
                    && burned.Alive
                )
                {
                    ApplyBurn(ctx, burned.EntityId, FlameSweepBurnStack, FlameSweepBurnCount);
                }
            }
        }
        else
        {
            var picked = RedirectedOrChosenTargets(ctx, enemy, livingPlayers, 1);
            foreach (var target in picked)
            {
                if (ctx.Db.Entity.EntityId.Find(target.EntityId) is Entity fresh && fresh.Alive)
                {
                    var current = ctx.Db.Entity.EntityId.Find(enemy.EntityId) ?? enemy;
                    ResolveHit(ctx, current, fresh, enemy.BasicAttackName, 0, isSkill: false);
                }
            }
        }

        AdvanceTurn(ctx);
    }

    static List<Entity> RedirectedOrChosenTargets(
        ReducerContext ctx,
        Entity attacker,
        List<Entity> living,
        int count
    )
    {
        var session = RequireSession(ctx);
        if (
            session.AttackRedirectEntityId != 0
            && ctx.Db.Entity.EntityId.Find(session.AttackRedirectEntityId) is Entity redirected
            && redirected.Alive
            && redirected.Faction != attacker.Faction
        )
        {
            var hits = Math.Max(1, count);
            var forced = new List<Entity>(hits);
            for (var i = 0; i < hits; i++)
            {
                forced.Add(redirected);
            }

            return forced;
        }

        return ChooseEnemyTargets(ctx, living, count);
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
        var stage = DebugStartStage < 1 ? 1u : DebugStartStage;
        ApplyStageFields(ctx, session, stage);
        MaybeLogBiomeEntry(ctx, 0, stage);
        AddLog(ctx, $"Stage {stage} begins.");
        EnterBattle(ctx);
    }

    static void EnterBattle(ReducerContext ctx)
    {
        EnsureBiomeDefs(ctx);
        ClearReadyFlags(ctx);
        ClearEnemySide(ctx);
        PreparePlayersForStage(ctx);
        var session = RequireSession(ctx);
        if (session.IsBossStage)
        {
            SpawnBoss(ctx);
        }
        else
        {
            SpawnEnemies(ctx);
        }

        session = RequireSession(ctx);
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

        session = RequireSession(ctx);
        if (session.IsBossStage)
        {
            var boss = LivingMembers(ctx, Team.Enemies);
            var bossName = boss.Count > 0 ? boss[0].Name : "A boss";
            AddLog(ctx, $"{bossName} appears!");
        }
        else
        {
            var spawned = LivingMembers(ctx, Team.Enemies).Count;
            AddLog(ctx, $"{spawned} enemies appeared!");
        }
        BuildTurnOrder(ctx);

        DeliverPendingRewards(ctx);

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
        var previous = session.StageNumber;
        var next = previous + 1;
        ApplyStageFields(ctx, session, next);
        MaybeLogBiomeEntry(ctx, previous, next);
        AddLog(ctx, $"Stage {next} begins.");
        EnterBattle(ctx);
    }

    static void OnStageCleared(ReducerContext ctx)
    {
        var session = RequireSession(ctx);
        var cleared = session.StageNumber < 1 ? 1u : session.StageNumber;
        ClearEnemySide(ctx);
        AddLog(ctx, $"Stage {cleared} cleared.");
        GrantBossLoot(ctx, cleared);
        EnterStageTransition(ctx, ShouldEnterRestStop(cleared));
    }

    static void ApplyStageFields(ReducerContext ctx, GameSession session, uint stage)
    {
        var biome = BiomeOf(stage);
        ctx.Db.GameSession.Id.Update(
            session with
            {
                StageNumber = stage,
                CurrentBiome = biome,
                NextBiome = BiomeOf(stage + 1),
                IsBossStage = IsBossStageNumber(stage),
                BossLootGranted = false,
                RestAmuletGranted = false,
                StageClearNote = "",
            }
        );
    }

    static void MaybeLogBiomeEntry(ReducerContext ctx, uint previousStage, uint stage)
    {
        EnsureBiomeDefs(ctx);
        var biome = BiomeOf(stage);
        if (previousStage >= 1 && BiomeOf(previousStage) == biome)
        {
            return;
        }

        var theName = BiomeTheName(ctx, biome);
        AddLog(ctx, $"Entering {theName}");
    }

    static string BiomeTheName(ReducerContext ctx, WorldBiome biome)
    {
        if (FindBiomeDef(ctx, biome) is BiomeDef row && !string.IsNullOrEmpty(row.TheName))
        {
            return row.TheName;
        }

        return biome switch
        {
            WorldBiome.Volcano => "the Volcano",
            WorldBiome.Swamp => "the Swamp",
            WorldBiome.SnowyTundra => "the Snowy Tundra",
            _ => "the Plains",
        };
    }

    static BiomeDef? FindBiomeDef(ReducerContext ctx, WorldBiome biome)
    {
        foreach (var row in ctx.Db.BiomeDef.Iter())
        {
            if (row.Kind == biome)
            {
                return row;
            }
        }

        return null;
    }

    static void EnterStageTransition(ReducerContext ctx, bool upcomingRestStop)
    {
        ClearReadyFlags(ctx);
        ClearStageTransitionTimers(ctx);

        var session = RequireSession(ctx);
        var nextStage = (session.StageNumber < 1 ? 1u : session.StageNumber) + 1;
        ctx.Db.GameSession.Id.Update(
            session with
            {
                Phase = BattlePhase.StageTransition,
                Round = 0,
                TurnIndex = 0,
                ActiveEntityId = 0,
                UpcomingRestStop = upcomingRestStop,
                NextBiome = BiomeOf(nextStage),
            }
        );

        DeliverPendingRewards(ctx);

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
        var alreadyGranted = session.RestAmuletGranted;
        ctx.Db.GameSession.Id.Update(
            session with
            {
                Phase = BattlePhase.RestStop,
                Round = 0,
                TurnIndex = 0,
                ActiveEntityId = 0,
                UpcomingRestStop = false,
                RestAmuletGranted = true,
            }
        );
        AddLog(ctx, "Rest stop. HP and mana restored. Ready up to continue.");
        DeliverPendingRewards(ctx);

        foreach (var player in ctx.Db.Player.Iter())
        {
            if (ctx.Db.Entity.EntityId.Find(player.EntityId) is not Entity entity || !entity.Alive)
            {
                continue;
            }

            if (!alreadyGranted)
            {
                GrantRandomUnownedAmulet(ctx, player.Identity, entity.Name);
            }

            GrantRandomUnownedWeapon(ctx, player.Identity, player.Class, entity.Name);
        }
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
        var session = RequireSession(ctx);
        ctx.Db.GameSession.Id.Update(session with { AttackRedirectEntityId = 0 });

        foreach (var entity in ctx.Db.Entity.Iter().ToList())
        {
            if (entity.Faction != Team.Players)
            {
                continue;
            }

            var reset = entity with
            {
                Mana = entity.Alive ? entity.MaxMana : entity.Mana,
                StrengthBuff = 0,
                NextTurnStrengthBonus = 0,
                GoFirstNextRound = false,
                BurnStack = 0,
                BurnCount = 0,
                WeakStacks = 0,
                NextTurnWeak = 0,
                FragileStacks = 0,
                NextTurnFragile = 0,
                CombatSpeed = 0,
                NextTurnSpeedSet = 0,
                NextTurnSpeedDelta = 0,
                DodgeBonusPercent = 0,
                NextTurnDodgeBonus = 0,
                EvadeThreshold = 0,
                EvadeFragileOnDodge = 0,
                EvadeStrengthOnDodge = 0,
                UsedAttackThisTurn = false,
                UsedAttackLastTurn = false,
                NextAttackBonus = 0,
                MagicBulletStage = 1,
                SpearDiscount = 0,
                VerticalCutDiscount = 0,
                FinishTheJobUsed = false,
                FinishTheJobStance = false,
                FinishTheJobPower = 0,
                HasDodged = false,
                DodgeCount = 0,
                SkipNextTurn = false,
                GrandUndertakingPending = false,
                DoubleStrength = false,
                NextTurnDoubleStrength = false,
            };
            ctx.Db.Entity.EntityId.Update(reset);
        }

        foreach (var player in ctx.Db.Player.Iter())
        {
            RecomputeStats(ctx, player.Identity);
        }
    }

    static void SpawnEnemies(ReducerContext ctx)
    {
        EnsureEnemyCatalog(ctx);
        EnsureSkillCatalog(ctx);
        var floor = CombatFloor(ctx);
        var players = PartyEncounterSize(ctx);
        var partyLevel = PartyCombatLevel(ctx);
        var pool = EnemyPool;
        var count = RollNonBossPackSize(ctx.Rng);
        var maxHp = EnemyHpForEncounter(floor, partyLevel, players, count);
        var atk = Math.Max(1, EnemyAtkForEncounter(floor, partyLevel, players));
        var defense = EnemyDefenseBaseline(EncounterScaleLevel(floor, partyLevel));

        var picks = new List<EnemyArchetype>(count);
        for (var i = 0; i < count; i++)
        {
            picks.Add(pool[ctx.Rng.Next(0, pool.Length)]);
        }

        var session = RequireSession(ctx);
        var biome = session.CurrentBiome;
        var def = FindBiomeDef(ctx, biome);
        var prefix = def?.VariantPrefix ?? "";
        var tintR = def?.TintR ?? 255;
        var tintG = def?.TintG ?? 255;
        var tintB = def?.TintB ?? 255;

        var copies = new Dictionary<string, int>();
        var rolls = new List<(EnemyArchetype Arch, string VisualKind, string Labeled)>(count);
        foreach (var pick in picks)
        {
            var visualKind = RollEnemyVisualKind(ctx);
            var labeled = VariantDisplayName(prefix, EnemyVisualDisplayName(visualKind));
            rolls.Add((pick, visualKind, labeled));
            copies.TryGetValue(labeled, out var n);
            copies[labeled] = n + 1;
        }

        var seen = new Dictionary<string, int>();
        for (uint slot = 0; slot < (uint)rolls.Count; slot++)
        {
            var roll = rolls[(int)slot];
            var arch = roll.Arch;
            var labeled = roll.Labeled;
            seen.TryGetValue(labeled, out var index);
            seen[labeled] = index + 1;
            var name = copies[labeled] <= 1
                ? labeled
                : $"{labeled} {(char)('A' + index)}";
            var maxMana = Math.Max(1, arch.MaxMana);
            var strength = Math.Max(1, arch.Strength);
            var dexterity = Math.Max(1, arch.Dexterity);
            var intelligence = Math.Max(1, arch.Intelligence);
            var speed = Math.Max(1, arch.Speed);

            var enemy = ctx.Db.Entity.Insert(
                new Entity
                {
                    EntityId = 0,
                    Faction = Team.Enemies,
                    Slot = slot,
                    Name = name,
                    ClassName = roll.VisualKind,
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
                    BasicAttackName = EnemyVisualBasicAttack(roll.VisualKind),
                    MagicBulletStage = 1,
                    VariantPrefix = prefix,
                    TintR = tintR,
                    TintG = tintG,
                    TintB = tintB,
                    IsBoss = false,
                    SkillCooldown = 0,
                }
            );

            GrantSkillsByName(ctx, enemy.EntityId, new[] { arch.SkillName });
        }
    }

    static void SpawnBoss(ReducerContext ctx)
    {
        var floor = CombatFloor(ctx);
        var players = PartyEncounterSize(ctx);
        var playerLevel = PartyCombatLevel(ctx);
        var maxHp = ClampStat(
            (double)EnemyHpForEncounter(floor, playerLevel, players, ReferencePackSize)
                * BossHpMultiplier,
            1
        );
        var atk = ClampStat(
            ScaleByBps(EnemyAtkForEncounter(floor, playerLevel, players), BossDamageMultiplierBps),
            1
        );
        var strength = ClampStat(
            ScaleByBps(EnemyStrengthForEncounter(floor, playerLevel), BossDamageMultiplierBps),
            1
        );
        var defense = ClampStat(
            EnemyDefenseBaseline(EncounterScaleLevel(floor, playerLevel)) + 2,
            0
        );

        var visualKind = RollEnemyVisualKind(ctx);
        var visualName = BossVisualDisplayName(visualKind);

        ctx.Db.Entity.Insert(
            new Entity
            {
                EntityId = 0,
                Faction = Team.Enemies,
                Slot = 0,
                Name = visualName,
                ClassName = visualKind,
                MaxHp = maxHp,
                Hp = maxHp,
                MaxMana = 1,
                Mana = 1,
                BaseStrength = strength,
                BaseDexterity = 2,
                BaseIntelligence = 1,
                BaseSpeed = 4,
                Strength = strength,
                Dexterity = 2,
                Intelligence = 1,
                Speed = 4,
                Atk = atk,
                Defense = defense,
                StrengthBuff = 0,
                NextTurnStrengthBonus = 0,
                GoFirstNextRound = false,
                Alive = true,
                BasicAttackName = BossBasicAttackName,
                MagicBulletStage = 1,
                VariantPrefix = "",
                TintR = 255,
                TintG = 255,
                TintB = 255,
                IsBoss = true,
                SkillCooldown = FlameSweepInterval,
            }
        );
    }

    static string RollEnemyVisualKind(ReducerContext ctx)
    {
        var kinds = EnemyVisualKinds;
        return kinds[ctx.Rng.Next(0, kinds.Length)];
    }

    static string VariantDisplayName(string prefix, string baseName)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return baseName;
        }

        return $"{prefix} {baseName}";
    }

    static uint CombatFloor(ReducerContext ctx)
    {
        var stage = RequireSession(ctx).StageNumber;
        return stage == 0 ? 1u : stage;
    }

    static uint PartyCombatLevel(ReducerContext ctx)
    {
        uint level = 1;
        foreach (var player in ctx.Db.Player.Iter())
        {
            if (!IsLivingPlayer(ctx, player))
            {
                continue;
            }

            var characterLevel = player.CharacterLevel == 0 ? 1u : player.CharacterLevel;
            if (characterLevel > level)
            {
                level = characterLevel;
            }
        }

        return level;
    }

    static int PartyEncounterSize(ReducerContext ctx)
    {
        var fighting = 0;
        foreach (var player in ctx.Db.Player.Iter())
        {
            if (player.Online && IsLivingPlayer(ctx, player))
            {
                fighting += 1;
            }
        }

        if (fighting > 0)
        {
            return fighting;
        }

        return Math.Max(1, LivingMembers(ctx, Team.Players).Count);
    }

    static int CurrentEnemyPackSize(ReducerContext ctx)
    {
        var count = 0;
        foreach (var entity in ctx.Db.Entity.Iter())
        {
            if (entity.Faction == Team.Enemies)
            {
                count++;
            }
        }

        return Math.Max(1, count);
    }

    /// Fastest combatant first. Rush / Grand Undertaking jump the queue for one round.
    static void BuildTurnOrder(ReducerContext ctx)
    {
        foreach (var entry in ctx.Db.TurnOrder.Iter().ToList())
        {
            ctx.Db.TurnOrder.Idx.Delete(entry.Idx);
        }

        ResolvePendingGrandUndertakings(ctx);
        if (EndBattleIfOver(ctx))
        {
            return;
        }

        RefreshRoundStatuses(ctx);

        var ordered = ctx
            .Db.Entity.Iter()
            .Where(e => e.Alive)
            .OrderByDescending(e => HasForcedFirstSpeed(e))
            .ThenByDescending(e => HasKnightHighPriority(ctx, e))
            .ThenByDescending(e => EffectiveSpeed(e))
            .ThenBy(e => e.Faction == Team.Players ? ClassTurnPriority(ClassOf(ctx, e)) : 2)
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
                    Speed = EffectiveSpeed(ordered[i]),
                    HasActed = false,
                    IsRush = ordered[i].GoFirstNextRound || ordered[i].NextTurnSpeedSet >= RushNextTurnSpeed,
                    DisplayPos = (uint)i,
                }
            );
        }

        foreach (var entity in ordered)
        {
            if (
                entity.GoFirstNextRound
                || entity.NextTurnSpeedSet != 0
                || entity.NextTurnSpeedDelta != 0
            )
            {
                ctx.Db.Entity.EntityId.Update(
                    entity with
                    {
                        GoFirstNextRound = false,
                        NextTurnSpeedSet = 0,
                        NextTurnSpeedDelta = 0,
                    }
                );
            }
        }
    }

    static int PendingCombatSpeed(Entity entity) =>
        entity.NextTurnSpeedSet != 0 ? entity.NextTurnSpeedSet : entity.Speed + entity.NextTurnSpeedDelta;

    /// Knights act first unless Rush / Ninja first-action jumped the queue, or
    /// Gallant Pride set their Speed to 1.
    static bool HasKnightHighPriority(ReducerContext ctx, Entity entity) =>
        entity.Faction == Team.Players
        && ClassOf(ctx, entity) == PlayerClass.Knight
        && !HasForcedFirstSpeed(entity)
        && EffectiveSpeed(entity) > 1;

    static void RefreshRoundStatuses(ReducerContext ctx)
    {
        var maxEnemySpeed = 0;
        var hasEnemy = false;
        foreach (var entity in ctx.Db.Entity.Iter())
        {
            if (entity.Faction != Team.Enemies || !entity.Alive)
            {
                continue;
            }

            if (!hasEnemy || entity.Speed > maxEnemySpeed)
            {
                maxEnemySpeed = entity.Speed;
                hasEnemy = true;
            }
        }

        foreach (var entity in ctx.Db.Entity.Iter().ToList())
        {
            var speed = PendingCombatSpeed(entity);
            if (
                hasEnemy
                && entity.Faction == Team.Players
                && entity.Alive
                && ClassOf(ctx, entity) == PlayerClass.Ninja
                && entity.Speed >= maxEnemySpeed + NinjaSpeedLeadForFirstAction
            )
            {
                speed = NinjaGuaranteedFirstSpeed;
            }

            ctx.Db.Entity.EntityId.Update(
                entity with
                {
                    CombatSpeed = speed,
                    FragileStacks = entity.NextTurnFragile,
                    NextTurnFragile = 0,
                    WeakStacks = entity.NextTurnWeak,
                    NextTurnWeak = 0,
                    DodgeBonusPercent = entity.NextTurnDodgeBonus,
                    NextTurnDodgeBonus = 0,
                }
            );
        }
    }

    static void ResolvePendingGrandUndertakings(ReducerContext ctx)
    {
        foreach (var entity in ctx.Db.Entity.Iter().ToList())
        {
            if (!entity.GrandUndertakingPending)
            {
                continue;
            }

            ResolveGrandUndertaking(ctx, entity);
        }
    }

    static void AdvanceTurn(ReducerContext ctx)
    {
        var session = RequireSession(ctx);
        if (session.ActiveEntityId != 0)
        {
            TickBurn(ctx, session.ActiveEntityId);
        }

        MarkActed(ctx);

        if (EndBattleIfOver(ctx))
        {
            return;
        }

        session = RequireSession(ctx);
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
        var stancePower = entity.FinishTheJobStance
            ? Math.Min(FinishTheJobPowerCap, entity.FinishTheJobPower + 8)
            : entity.FinishTheJobPower;
        var doubleStrength = entity.NextTurnDoubleStrength;
        var refreshed = entity with
        {
            Mana = Math.Min(entity.MaxMana, entity.Mana + ManaRegenPerTurn),
            StrengthBuff = entity.NextTurnStrengthBonus,
            NextTurnStrengthBonus = 0,
            UsedAttackLastTurn = entity.UsedAttackThisTurn,
            UsedAttackThisTurn = false,
            EvadeThreshold = 0,
            EvadeFragileOnDodge = 0,
            EvadeStrengthOnDodge = 0,
            FinishTheJobPower = stancePower,
            DoubleStrength = doubleStrength,
            NextTurnDoubleStrength = false,
        };
        ctx.Db.Entity.EntityId.Update(refreshed);
        if (doubleStrength != entity.DoubleStrength && TryPlayerIdentity(ctx, refreshed.EntityId, out var owner))
        {
            RecomputeStats(ctx, owner);
            refreshed = ctx.Db.Entity.EntityId.Find(refreshed.EntityId) ?? refreshed;
            if (doubleStrength)
            {
                AddLog(
                    ctx,
                    $"{refreshed.Name}'s Dragonfly Charm doubles Strength this turn.",
                    LogKind.Focus,
                    refreshed.EntityId,
                    refreshed.EntityId
                );
            }
        }

        var session = RequireSession(ctx);
        ctx.Db.GameSession.Id.Update(
            session with
            {
                Round = round,
                TurnIndex = idx,
                ActiveEntityId = refreshed.EntityId,
            }
        );

        if (refreshed.SkipNextTurn)
        {
            ctx.Db.Entity.EntityId.Update(refreshed with { SkipNextTurn = false });
            AddLog(
                ctx,
                $"Round {round} - {refreshed.Name} cannot act.",
                LogKind.TurnStart,
                refreshed.EntityId,
                refreshed.EntityId
            );
            AdvanceTurn(ctx);
            return;
        }

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

        RefreshTurnDisplay(ctx);
    }

    /// Puts the current actor at DisplayPos 0, then the rest of this round,
    /// then combatants who already acted (their Idx order, which is next-round
    /// speed order). Dead rows are hidden.
    static void RefreshTurnDisplay(ReducerContext ctx)
    {
        var session = RequireSession(ctx);
        var living = new List<TurnOrder>();
        foreach (var entry in ctx.Db.TurnOrder.Iter().OrderBy(t => t.Idx))
        {
            if (ctx.Db.Entity.EntityId.Find(entry.EntityId) is Entity entity && entity.Alive)
            {
                living.Add(entry);
            }
        }

        var start = 0;
        if (session.ActiveEntityId != 0)
        {
            for (var i = 0; i < living.Count; i++)
            {
                if (living[i].EntityId == session.ActiveEntityId)
                {
                    start = i;
                    break;
                }
            }
        }

        foreach (var entry in ctx.Db.TurnOrder.Iter().ToList())
        {
            var pos = TurnListHiddenPos;
            for (var i = 0; i < living.Count; i++)
            {
                var rotated = living[(start + i) % living.Count];
                if (rotated.Idx == entry.Idx)
                {
                    pos = (uint)i;
                    break;
                }
            }

            if (entry.DisplayPos != pos)
            {
                ctx.Db.TurnOrder.Idx.Update(entry with { DisplayPos = pos });
            }
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
                    AttackRedirectEntityId = 0,
                }
            );
            AddLog(ctx, "The whole party has fallen. Defeat.");
            return true;
        }

        return false;
    }

    readonly struct HitResult
    {
        public HitResult(bool connected, int damage, bool killed)
        {
            Connected = connected;
            Damage = damage;
            Killed = killed;
        }

        public bool Connected { get; }
        public int Damage { get; }
        public bool Killed { get; }
    }

    /// The one place damage is computed, for players and enemies alike.
    static HitResult ResolveHit(
        ReducerContext ctx,
        Entity attacker,
        Entity target,
        string actionName,
        int skillBaseDamage,
        bool isSkill = false,
        bool bludgeonFragile = false
    )
    {
        attacker = ctx.Db.Entity.EntityId.Find(attacker.EntityId) ?? attacker;
        target = ctx.Db.Entity.EntityId.Find(target.EntityId) ?? target;
        if (!target.Alive)
        {
            return default;
        }

        var attackerClass = ClassOf(ctx, attacker);
        var power = skillBaseDamage + attacker.StrengthBuff + attacker.NextAttackBonus;
        if (isSkill && attacker.Faction == Team.Players && attackerClass == PlayerClass.Ninja)
        {
            power += NinjaSpeedPowerBonus(attacker.Speed, target.Speed);
            if (attacker.FinishTheJobStance)
            {
                power += 2 + attacker.FinishTheJobPower;
            }
        }

        if (attacker.Faction == Team.Players)
        {
            power += ClassPassiveDamage(
                attackerClass,
                attacker.Strength,
                attacker.Dexterity,
                attacker.Intelligence,
                attacker.BaseSpeed,
                isSpell: attackerClass == PlayerClass.Mage && isSkill
            );
        }

        var raw = DealtDamage(power, attacker.Atk);
        raw = ApplyWeak(raw, attacker.WeakStacks);
        var afterArmor = AfterDefense(raw, target.Defense);
        if (attacker.Faction == Team.Enemies && raw > 0)
        {
            afterArmor = Math.Max(1, afterArmor);
        }

        raw = afterArmor;

        if (attacker.NextAttackBonus != 0)
        {
            ctx.Db.Entity.EntityId.Update(attacker with { NextAttackBonus = 0 });
            attacker = ctx.Db.Entity.EntityId.Find(attacker.EntityId) ?? attacker;
        }

        var targetClass = ClassOf(ctx, target);
        var dodgeBps = DodgeChanceBps(
            target.Dexterity,
            target.Faction,
            target.DodgeBonusPercent + EquippedDodgeBonusPercent(ctx, target),
            targetClass == PlayerClass.Archer
        );
        var dodged = RollDodge(ctx.Rng, dodgeBps);
        var evaded = !dodged && target.EvadeThreshold > 0 && raw < target.EvadeThreshold;
        var connected = !dodged && !evaded;
        var crit = false;
        var damage = connected ? raw : 0;
        if (
            connected
            && attackerClass == PlayerClass.Ninja
            && RollCrit(ctx.Rng, NinjaCritChanceBps(attacker.Speed))
        )
        {
            crit = true;
            damage *= 2;
        }

        if (connected)
        {
            if (bludgeonFragile && target.FragileStacks > 0)
            {
                damage = ApplyBludgeonFragile(damage);
            }
            else
            {
                damage = ApplyFragile(damage, target.FragileStacks);
            }

            damage = ApplyReceivedDamageReduction(
                damage,
                HasAmulet(ctx, target, AmuletNames.GuardiansPendant)
            );
        }

        var hp = Math.Max(0, target.Hp - damage);
        var alive = hp > 0;
        var wasAlive = target.Alive;
        var tallyDodge =
            (dodged || evaded)
            && target.Faction == Team.Players
            && targetClass == PlayerClass.Archer;
        ctx.Db.Entity.EntityId.Update(
            target with
            {
                Hp = hp,
                Alive = alive,
                HasDodged = target.HasDodged || dodged || evaded,
                DodgeCount = target.DodgeCount + (tallyDodge ? 1 : 0),
            }
        );

        if (dodged || evaded)
        {
            OnSuccessfulDodge(ctx, attacker, target, evaded);
            var reason = evaded ? "evades" : "dodges";
            AddLog(
                ctx,
                $"{attacker.Name} uses {actionName} on {target.Name} but {target.Name} {reason}.",
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
            if (crit)
            {
                AddLog(
                    ctx,
                    "CRITICAL HIT!",
                    LogKind.Attack,
                    attacker.EntityId,
                    target.EntityId,
                    damage
                );
            }

            ApplyAmuletOnConnectedHit(ctx, attacker, target, damage);
            ApplyAmuletOnDamageTaken(ctx, attacker, target, damage);
        }

        if (wasAlive && !alive)
        {
            HandleDefeat(ctx, attacker, target);
        }

        return new HitResult(connected, damage, wasAlive && !alive);
    }

    static void OnSuccessfulDodge(ReducerContext ctx, Entity attacker, Entity target, bool evaded)
    {
        var freshTarget = ctx.Db.Entity.EntityId.Find(target.EntityId) ?? target;
        if (freshTarget.EvadeFragileOnDodge > 0)
        {
            QueueFragile(ctx, attacker.EntityId, freshTarget.EvadeFragileOnDodge);
        }

        if (freshTarget.EvadeStrengthOnDodge > 0)
        {
            QueueStrength(ctx, freshTarget.EntityId, freshTarget.EvadeStrengthOnDodge);
            AddLog(
                ctx,
                $"{freshTarget.Name} gains {freshTarget.EvadeStrengthOnDodge} Enraged next turn.",
                LogKind.Focus,
                freshTarget.EntityId,
                freshTarget.EntityId
            );
        }

        _ = evaded;
    }

    static void HandleDefeat(ReducerContext ctx, Entity attacker, Entity target)
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
            var stage = session.StageNumber < 1 ? 1u : session.StageNumber;
            GrantKillXp(ctx, stage, target.IsBoss);
        }

        ApplyAmuletOnKill(ctx, attacker);
        ApplyAmuletOnAllyDefeat(ctx, target);
        RefreshTurnDisplay(ctx);
    }

    static void ApplyPercentMaxHpDamage(
        ReducerContext ctx,
        Entity attacker,
        Entity target,
        int maxHpBps,
        string actionName
    )
    {
        target = ctx.Db.Entity.EntityId.Find(target.EntityId) ?? target;
        if (!target.Alive)
        {
            return;
        }

        var damage = ApplyReceivedDamageReduction(
            ApplyFragile(ScaleByBps(target.MaxHp, maxHpBps), target.FragileStacks),
            HasAmulet(ctx, target, AmuletNames.GuardiansPendant)
        );
        var hp = Math.Max(0, target.Hp - damage);
        var alive = hp > 0;
        ctx.Db.Entity.EntityId.Update(target with { Hp = hp, Alive = alive });
        AddLog(
            ctx,
            $"{actionName} hits {target.Name} for {damage} damage.",
            LogKind.Attack,
            attacker.EntityId,
            target.EntityId,
            damage
        );

        if (!alive)
        {
            HandleDefeat(ctx, attacker, target);
        }

        ApplyAmuletOnDamageTaken(ctx, attacker, target, damage);
    }

    static void TickBurn(ReducerContext ctx, ulong entityId)
    {
        if (ctx.Db.Entity.EntityId.Find(entityId) is not Entity entity || !entity.Alive)
        {
            return;
        }

        if (entity.BurnStack <= 0 || entity.BurnCount <= 0)
        {
            return;
        }

        var damage = ApplyReceivedDamageReduction(
            ApplyFragile(entity.BurnStack, entity.FragileStacks),
            HasAmulet(ctx, entity, AmuletNames.GuardiansPendant)
        );
        var hp = Math.Max(0, entity.Hp - damage);
        var alive = hp > 0;
        var count = entity.BurnCount - 1;
        ctx.Db.Entity.EntityId.Update(
            entity with
            {
                Hp = hp,
                Alive = alive,
                BurnCount = count,
                BurnStack = count > 0 ? entity.BurnStack : 0,
            }
        );
        AddLog(
            ctx,
            $"{entity.Name} takes {damage} burn damage.",
            LogKind.Attack,
            entity.EntityId,
            entity.EntityId,
            damage
        );

        if (!alive)
        {
            HandleDefeat(ctx, entity, entity);
        }
    }

    static void KillEntity(ReducerContext ctx, Entity entity, string message)
    {
        entity = ctx.Db.Entity.EntityId.Find(entity.EntityId) ?? entity;
        if (!entity.Alive)
        {
            return;
        }

        ctx.Db.Entity.EntityId.Update(entity with { Hp = 0, Alive = false });
        AddLog(ctx, message, LogKind.Defeat, entity.EntityId, entity.EntityId);
        RefreshTurnDisplay(ctx);
    }

    static void MarkUsedAttack(ReducerContext ctx, ulong entityId)
    {
        if (ctx.Db.Entity.EntityId.Find(entityId) is Entity entity)
        {
            ctx.Db.Entity.EntityId.Update(entity with { UsedAttackThisTurn = true });
        }
    }

    static void ApplyFocus(ReducerContext ctx, Entity actor)
    {
        var mana = Math.Min(actor.MaxMana, actor.Mana + FocusManaFor(HasAmulet(ctx, actor, AmuletNames.AmethystSash)));
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
    static void GrantKillXp(ReducerContext ctx, uint stage, bool bossKill)
    {
        var xp = KillXp(stage);
        if (bossKill)
        {
            xp = SaturatingMul(xp, BossExpMultiplier);
        }
        else
        {
            var pack = CurrentEnemyPackSize(ctx);
            xp = (uint)Math.Max(1L, ((long)xp * ReferencePackSize) / pack);
        }
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
                UnspentStatPoints = SaturatingAdd(
                    player.UnspentStatPoints,
                    SaturatingMul(StatPointsPerLevel, (uint)reached.Count)
                ),
            }
        );

        AddLog(ctx, $"{name} gained {xp} EXP.");

        if (reached.Count == 0)
        {
            return;
        }

        foreach (var newLevel in reached)
        {
            AddLog(ctx, $"{name} reached level {newLevel}!");
            Log.Info($"{name} reached level {newLevel}.");
        }

        GrantUnlockedSkills(ctx, player.EntityId, player.Class, level);
        RecomputeStats(ctx, player.Identity);
    }

    /// Debug: set the caller's character to level 999 and unlock every class skill.
    [SpacetimeDB.Reducer]
    public static void Cheat(ReducerContext ctx)
    {
        var player = RequirePlayer(ctx);
        if (ctx.Db.Entity.EntityId.Find(player.EntityId) is not Entity entity)
        {
            throw new Exception("Your character is missing.");
        }

        ctx.Db.Player.Identity.Update(
            player with
            {
                CharacterLevel = CheatCharacterLevel,
                Xp = 0,
            }
        );

        GrantUnlockedSkills(ctx, player.EntityId, player.Class, CheatCharacterLevel);
        GrantMissingAmulets(ctx, ctx.Sender);
        GrantMissingClassWeapons(ctx, ctx.Sender, player.Class);
        RecomputeStats(ctx, ctx.Sender);

        var fresh = ctx.Db.Entity.EntityId.Find(player.EntityId) ?? entity;
        AddLog(
            ctx,
            $"{fresh.Name} cheats to level {CheatCharacterLevel}. All skills, amulets, and class weapons unlocked.",
            LogKind.Focus,
            fresh.EntityId,
            fresh.EntityId
        );
    }

    /// Spend one unspent character-level point on a combat stat. Health and
    /// Mana are not spendable; only Strength, Speed, Intelligence, and Dexterity.
    [SpacetimeDB.Reducer]
    public static void SpendStatPoint(ReducerContext ctx, StatType stat)
    {
        var player = RequirePlayer(ctx);
        if (ctx.Db.Entity.EntityId.Find(player.EntityId) is not Entity entity)
        {
            throw new Exception("Your character is missing.");
        }

        if (!entity.Alive)
        {
            throw new Exception("Defeated players cannot spend stat points.");
        }

        if (player.UnspentStatPoints == 0)
        {
            throw new Exception("No unspent stat points.");
        }

        if (!IsSpendableStat(stat))
        {
            throw new Exception("That stat cannot be increased with points.");
        }

        var grown = stat switch
        {
            StatType.Strength => entity with { BaseStrength = entity.BaseStrength + StatPointStrength },
            StatType.Dexterity => entity with { BaseDexterity = entity.BaseDexterity + StatPointDexterity },
            StatType.Intelligence =>
                entity with { BaseIntelligence = entity.BaseIntelligence + StatPointIntelligence },
            StatType.Speed => entity with { BaseSpeed = entity.BaseSpeed + StatPointSpeed },
            _ => throw new Exception("That stat cannot be increased with points."),
        };

        ctx.Db.Player.Identity.Update(
            player with { UnspentStatPoints = player.UnspentStatPoints - 1 }
        );
        ctx.Db.Entity.EntityId.Update(grown);
        RecomputeStats(ctx, player.Identity);

        var label = stat switch
        {
            StatType.Strength => "Strength",
            StatType.Dexterity => "Dexterity",
            StatType.Intelligence => "Intelligence",
            _ => "Speed",
        };
        AddLog(ctx, $"{entity.Name} increased {label}.");
        Log.Info($"{entity.Name} increased {label}.");
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

    static PlayerClass ClassOf(ReducerContext ctx, Entity entity)
    {
        if (entity.Faction != Team.Players)
        {
            return PlayerClass.Knight;
        }

        foreach (var player in ctx.Db.Player.Iter())
        {
            if (player.EntityId == entity.EntityId)
            {
                return player.Class;
            }
        }

        return PlayerClass.Knight;
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

    static void DeletePendingRewards(ReducerContext ctx, Identity owner)
    {
        foreach (var pending in ctx.Db.PendingReward.Owner.Filter(owner).ToList())
        {
            ctx.Db.PendingReward.Id.Delete(pending.Id);
        }
    }

    static void GrantBossLoot(ReducerContext ctx, uint clearedStage)
    {
        var session = RequireSession(ctx);
        if (!IsBossStageNumber(clearedStage) || session.BossLootGranted)
        {
            return;
        }

        EnsureBossDrops(ctx);
        ctx.Db.GameSession.Id.Update(session with { BossLootGranted = true });

        var notes = new List<string>();
        foreach (var player in ctx.Db.Player.Iter().ToList())
        {
            if (ctx.Db.Entity.EntityId.Find(player.EntityId) is not Entity entity || !entity.Alive)
            {
                continue;
            }

            if (RollBossDrop(ctx, player.Class) is not ItemDef item)
            {
                continue;
            }

            var delivered = TryAddItemToInventory(ctx, player.Identity, item.Id);
            if (delivered)
            {
                AddLog(ctx, $"{entity.Name} found {item.Name}.");
                notes.Add($"{entity.Name} found {item.Name}");
            }
            else
            {
                ctx.Db.PendingReward.Insert(
                    new PendingReward
                    {
                        Id = 0,
                        Owner = player.Identity,
                        ItemDefId = item.Id,
                    }
                );
                AddLog(
                    ctx,
                    $"{entity.Name}'s inventory is full. {item.Name} waits for a free slot."
                );
                notes.Add($"{entity.Name}'s {item.Name} is waiting for a free slot");
            }
        }

        session = RequireSession(ctx);
        ctx.Db.GameSession.Id.Update(
            session with { StageClearNote = string.Join(". ", notes) }
        );
    }

    static ItemDef? RollBossDrop(ReducerContext ctx, PlayerClass playerClass)
    {
        var pool = new List<ItemDef>();
        foreach (var drop in ctx.Db.BossDrop.Tier.Filter(FirstBossDropTier))
        {
            if (ctx.Db.ItemDef.Id.Find(drop.ItemDefId) is not ItemDef item)
            {
                continue;
            }

            if (item.Kind == ItemKind.Amulet)
            {
                pool.Add(item);
                continue;
            }

            if (item.Kind == ItemKind.Weapon && item.WeaponType == ClassWeapon(playerClass))
            {
                pool.Add(item);
            }
        }

        if (pool.Count == 0)
        {
            return null;
        }

        return pool[ctx.Rng.Next(0, pool.Count)];
    }

    static void DeliverPendingRewards(ReducerContext ctx)
    {
        foreach (var player in ctx.Db.Player.Iter().ToList())
        {
            DeliverPendingRewardsFor(ctx, player.Identity);
        }
    }

    static void DeliverPendingRewardsFor(ReducerContext ctx, Identity owner)
    {
        foreach (var pending in ctx.Db.PendingReward.Owner.Filter(owner).ToList())
        {
            if (!TryAddItemToInventory(ctx, owner, pending.ItemDefId))
            {
                return;
            }

            ctx.Db.PendingReward.Id.Delete(pending.Id);
            var itemName = ctx.Db.ItemDef.Id.Find(pending.ItemDefId)?.Name ?? "an item";
            var ownerName = "Someone";
            if (ctx.Db.Player.Identity.Find(owner) is Player player
                && ctx.Db.Entity.EntityId.Find(player.EntityId) is Entity entity)
            {
                ownerName = entity.Name;
            }

            AddLog(ctx, $"{ownerName} claimed {itemName}.");
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
