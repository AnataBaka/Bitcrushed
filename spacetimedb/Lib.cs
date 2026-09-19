using System;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;

public static partial class Module
{
    static readonly string[] PartyNames = { "Aria", "Bran", "Cael" };

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
        }
        else
        {
            Log.Info($"Client connected: {ctx.Sender}");
        }
    }

    [SpacetimeDB.Reducer(ReducerKind.ClientDisconnected)]
    public static void ClientDisconnected(ReducerContext ctx)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is Player player)
        {
            ctx.Db.Player.Identity.Update(player with { Online = false });
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

        if (session.PlayerCount >= session.MaxPlayers)
        {
            throw new Exception("Party is Full");
        }

        if (session.Phase != BattlePhase.Waiting)
        {
            throw new Exception("The battle has already started.");
        }

        var slot = FindFreeSlot(ctx);
        var playerClass = RollClass(ctx);
        var rolled = RollStarterStats(ctx.Rng, playerClass);
        var name = PartyNames[slot % (uint)PartyNames.Length];
        var className = ClassNameOf(playerClass);
        var maxHealth = ClassMaxHealth(playerClass);
        var maxMana = SaturatingAdd(ClassBaseMana(playerClass), rolled.Intelligence);

        var entity = ctx.Db.Entity.Insert(
            new Entity
            {
                EntityId = 0,
                Faction = Team.Players,
                Slot = slot,
                Name = name,
                ClassName = className,
                MaxHp = ToInt(maxHealth),
                Hp = ToInt(maxHealth),
                MaxMana = ToInt(maxMana),
                Mana = ToInt(maxMana),
                Strength = ToInt(rolled.Strength),
                Dexterity = ToInt(rolled.Dexterity),
                Intelligence = ToInt(rolled.Intelligence),
                Atk = 0,
                Defense = 0,
                Speed = ToInt(rolled.Speed),
                Alive = true,
                StrengthBuff = 0,
                NextTurnStrengthBonus = 0,
                NextTurnSpeedOverride = 0,
                GoFirstNextRound = false,
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
                BaseStrength = rolled.Strength,
                BaseDexterity = rolled.Dexterity,
                BaseIntelligence = rolled.Intelligence,
                BaseSpeed = rolled.Speed,
                EquippedWeaponDefId = 0,
                EquippedHelmetDefId = 0,
                EquippedChestplateDefId = 0,
                EquippedLeggingsDefId = 0,
                EquippedBootsDefId = 0,
            }
        );

        var weapon = RequireItemDefByName(ctx, StarterWeaponName(playerClass));
        GiveItem(ctx, ctx.Sender, weapon.Id, 1);
        EquipFromCatalog(ctx, ctx.Sender, weapon);
        GiveStarterArmor(ctx, ctx.Sender);
        GiveConsumable(ctx, ctx.Sender, "Health Potion", 2);
        GiveConsumable(ctx, ctx.Sender, "Mana Potion", 1);
        GrantClassSkills(ctx, ctx.Sender, playerClass);

        var playerCount = session.PlayerCount + 1;
        ctx.Db.GameSession.Id.Update(session with { PlayerCount = playerCount });
        AddLog(ctx, $"{name} the {className} joined the party.");

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

        DeletePlayerOwnedRows(ctx, player.Identity);
        ctx.Db.Entity.EntityId.Delete(player.EntityId);
        ctx.Db.Player.Identity.Delete(ctx.Sender);

        var playerCount = session.PlayerCount == 0 ? 0 : session.PlayerCount - 1;
        ctx.Db.GameSession.Id.Update(session with { PlayerCount = playerCount });
        AddLog(ctx, $"A player in slot {player.Slot} left the party.");
    }

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

    [SpacetimeDB.Reducer]
    public static void ResetStage(ReducerContext ctx)
    {
        ClearTimers(ctx);
        ClearTurnOrder(ctx);
        ClearCombatEvents(ctx);

        foreach (var skill in ctx.Db.EntitySkill.Iter().ToList())
        {
            ctx.Db.EntitySkill.Id.Delete(skill.Id);
        }

        foreach (var skill in ctx.Db.PlayerSkill.Iter().ToList())
        {
            ctx.Db.PlayerSkill.Id.Delete(skill.Id);
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
            }
        );
        AddLog(ctx, "Stage reset. Waiting for players.");
    }

    // ---------------------------------------------------------- player actions

    [SpacetimeDB.Reducer]
    public static void Attack(ReducerContext ctx, ulong targetEntityId, uint skillDefId)
    {
        var actor = RequireActingEntity(ctx);
        var player = RequirePlayer(ctx);
        RequireWeapon(player);

        if (!OwnsUnlockedSkill(ctx, player.Identity, skillDefId))
        {
            throw new Exception("Skill is not unlocked for this character.");
        }

        if (ctx.Db.SkillDef.Id.Find(skillDefId) is not SkillDef skill || skill.IsEnemySkill)
        {
            throw new Exception("Unknown skill.");
        }

        if (ToUInt(actor.Mana) < skill.ManaCost)
        {
            AddLog(ctx, $"Not enough mana. {actor.Name} needs {skill.ManaCost} mana for {skill.Name}.");
            return;
        }

        ctx.Db.Entity.EntityId.Update(actor with { Mana = actor.Mana - ToInt(skill.ManaCost) });
        actor = ctx.Db.Entity.EntityId.Find(actor.EntityId)!.Value;

        if (skill.AlwaysGoFirst)
        {
            ctx.Db.Entity.EntityId.Update(actor with { GoFirstNextRound = true });
            actor = ctx.Db.Entity.EntityId.Find(actor.EntityId)!.Value;
        }

        if (skill.NextTurnStrengthBonus > 0 || skill.NextTurnSpeedOverride > 0)
        {
            ctx.Db.Entity.EntityId.Update(
                actor with
                {
                    NextTurnStrengthBonus = SaturatingAdd(
                        actor.NextTurnStrengthBonus,
                        skill.NextTurnStrengthBonus
                    ),
                    NextTurnSpeedOverride =
                        skill.NextTurnSpeedOverride > 0
                            ? skill.NextTurnSpeedOverride
                            : actor.NextTurnSpeedOverride,
                }
            );
            actor = ctx.Db.Entity.EntityId.Find(actor.EntityId)!.Value;
            PushCombatEvent(
                ctx,
                actor.EntityId,
                actor.EntityId,
                CombatActionType.Spell,
                skill.Name,
                0,
                0,
                false,
                false,
                $"{actor.Name} used {skill.Name}."
            );
            AddLog(ctx, $"{actor.Name} used {skill.Name}.");
        }

        if (skill.TargetCount > 0 && skill.BaseDamage > 0)
        {
            foreach (var target in SelectEnemyTargets(ctx, targetEntityId, skill.TargetCount))
            {
                ApplyOutgoingDamage(ctx, actor, target, CombatActionType.Spell, skill.Name, skill.BaseDamage);
                actor = ctx.Db.Entity.EntityId.Find(actor.EntityId)!.Value;
            }
        }

        AdvanceTurn(ctx);
    }

    [SpacetimeDB.Reducer]
    public static void UseItem(ReducerContext ctx, uint itemInstanceId)
    {
        var actor = RequireActingEntity(ctx);
        var player = RequirePlayer(ctx);

        if (
            ctx.Db.PlayerItem.Id.Find(itemInstanceId) is not PlayerItem instance
            || instance.Owner != player.Identity
        )
        {
            throw new Exception("Item not in inventory.");
        }

        if (ctx.Db.ItemDef.Id.Find(instance.ItemDefId) is not ItemDef item || item.Kind != ItemKind.Consumable)
        {
            throw new Exception("That item cannot be used.");
        }

        var heal = item.HealAmount;
        var mana = item.ManaRestoreAmount;
        var newHealth = SaturatingAdd(ToUInt(actor.Hp), heal);
        if (newHealth > ToUInt(actor.MaxHp))
        {
            newHealth = ToUInt(actor.MaxHp);
        }

        var newMana = SaturatingAdd(ToUInt(actor.Mana), mana);
        if (newMana > ToUInt(actor.MaxMana))
        {
            newMana = ToUInt(actor.MaxMana);
        }

        var restoredHp = SaturatingSub(newHealth, ToUInt(actor.Hp));
        var restoredMp = SaturatingSub(newMana, ToUInt(actor.Mana));
        ctx.Db.Entity.EntityId.Update(
            actor with
            {
                Hp = ToInt(newHealth),
                Mana = ToInt(newMana),
            }
        );

        if (instance.Quantity <= 1)
        {
            ctx.Db.PlayerItem.Id.Delete(instance.Id);
        }
        else
        {
            ctx.Db.PlayerItem.Id.Update(instance with { Quantity = instance.Quantity - 1 });
        }

        PushCombatEvent(
            ctx,
            actor.EntityId,
            actor.EntityId,
            CombatActionType.Item,
            item.Name,
            0,
            restoredHp,
            false,
            false,
            $"{actor.Name} used {item.Name}."
        );
        AddLog(
            ctx,
            $"{actor.Name} uses {item.Name} (+{restoredHp} HP, +{restoredMp} MP)."
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

    [SpacetimeDB.Reducer]
    public static void EquipItem(ReducerContext ctx, uint itemInstanceId)
    {
        var player = RequirePlayer(ctx);
        if (
            ctx.Db.PlayerItem.Id.Find(itemInstanceId) is not PlayerItem instance
            || instance.Owner != ctx.Sender
        )
        {
            throw new Exception("Item not in inventory.");
        }

        if (ctx.Db.ItemDef.Id.Find(instance.ItemDefId) is not ItemDef item)
        {
            throw new Exception("Unknown item.");
        }

        if (item.Kind == ItemKind.Consumable)
        {
            throw new Exception("Consumables are used, not equipped.");
        }

        if (item.Kind == ItemKind.Weapon && item.WeaponType != ClassWeapon(player.Class))
        {
            throw new Exception($"{player.Class} can only equip a {ClassWeapon(player.Class)}.");
        }

        if (item.Kind == ItemKind.Armor && item.ArmorSlot == ArmorSlot.None)
        {
            throw new Exception("That armor has no slot.");
        }

        if (instance.EquippedSlot != EquipSlot.Bag)
        {
            UnequipOwnedItem(ctx, player.Identity, instance);
            return;
        }

        EquipOwnedItem(ctx, player.Identity, instance, item);
    }

    [SpacetimeDB.Reducer]
    public static void UnequipItem(ReducerContext ctx, uint itemInstanceId)
    {
        var player = RequirePlayer(ctx);
        if (
            ctx.Db.PlayerItem.Id.Find(itemInstanceId) is not PlayerItem instance
            || instance.Owner != ctx.Sender
        )
        {
            throw new Exception("Item not in inventory.");
        }

        UnequipOwnedItem(ctx, player.Identity, instance);
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
        if (skill is SkillDef chosen && ToUInt(enemy.Mana) >= chosen.ManaCost)
        {
            ctx.Db.Entity.EntityId.Update(enemy with { Mana = enemy.Mana - ToInt(chosen.ManaCost) });
            enemy = ctx.Db.Entity.EntityId.Find(enemy.EntityId)!.Value;

            if (chosen.NextTurnStrengthBonus > 0)
            {
                ctx.Db.Entity.EntityId.Update(
                    enemy with
                    {
                        NextTurnStrengthBonus = SaturatingAdd(
                            enemy.NextTurnStrengthBonus,
                            chosen.NextTurnStrengthBonus
                        ),
                    }
                );
                enemy = ctx.Db.Entity.EntityId.Find(enemy.EntityId)!.Value;
                PushCombatEvent(
                    ctx,
                    enemy.EntityId,
                    enemy.EntityId,
                    CombatActionType.Spell,
                    chosen.Name,
                    0,
                    0,
                    false,
                    false,
                    $"{enemy.Name} used {chosen.Name}."
                );
                AddLog(ctx, $"{enemy.Name} used {chosen.Name}.");
                if (chosen.BaseDamage == 0)
                {
                    AdvanceTurn(ctx);
                    return;
                }
            }

            if (chosen.TargetCount > 0 && chosen.BaseDamage > 0)
            {
                var picked = chosen.TargetCount > 1
                    ? targets.Take((int)chosen.TargetCount).ToList()
                    : new List<Entity> { targets[ctx.Rng.Next(0, targets.Count)] };
                foreach (var target in picked)
                {
                    ApplyOutgoingDamage(
                        ctx,
                        enemy,
                        target,
                        CombatActionType.Spell,
                        chosen.Name,
                        chosen.BaseDamage
                    );
                    enemy = ctx.Db.Entity.EntityId.Find(enemy.EntityId)!.Value;
                }
            }
        }
        else if (ToUInt(enemy.Mana) < 6)
        {
            ApplyFocus(ctx, enemy);
        }
        else
        {
            var target = targets[ctx.Rng.Next(0, targets.Count)];
            ApplyOutgoingDamage(ctx, enemy, target, CombatActionType.Attack, "Attack", 4);
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
        SpawnEnemy(ctx, 0, "Goblin Raider", 90, 40, 6, 6, 4, 6, 7, "Rusty Slash", "Howl");
        SpawnEnemy(ctx, 1, "Cave Troll", 140, 50, 10, 2, 2, 8, 4, "Boulder Smash");
        SpawnEnemy(ctx, 2, "Shadow Wisp", 70, 60, 4, 8, 8, 5, 11, "Shadow Volley");
    }

    static void SpawnEnemy(
        ReducerContext ctx,
        uint slot,
        string name,
        uint hp,
        uint mana,
        uint strength,
        uint dexterity,
        uint intelligence,
        uint atk,
        uint speed,
        params string[] skillNames
    )
    {
        var entity = ctx.Db.Entity.Insert(
            new Entity
            {
                EntityId = 0,
                Faction = Team.Enemies,
                Slot = slot,
                Name = name,
                ClassName = "Enemy",
                MaxHp = ToInt(hp),
                Hp = ToInt(hp),
                MaxMana = ToInt(mana),
                Mana = ToInt(mana),
                Strength = ToInt(strength),
                Dexterity = ToInt(dexterity),
                Intelligence = ToInt(intelligence),
                Atk = ToInt(atk),
                Defense = 0,
                Speed = ToInt(speed),
                Alive = true,
                StrengthBuff = 0,
                NextTurnStrengthBonus = 0,
                NextTurnSpeedOverride = 0,
                GoFirstNextRound = false,
            }
        );

        foreach (var skillName in skillNames)
        {
            if (FindSkillDefByName(ctx, skillName) is SkillDef skill)
            {
                ctx.Db.EntitySkill.Insert(
                    new EntitySkill
                    {
                        Id = 0,
                        EntityId = entity.EntityId,
                        SkillDefId = skill.Id,
                    }
                );
            }
        }
    }

    static void BuildTurnOrder(ReducerContext ctx)
    {
        ClearTurnOrder(ctx);

        var entries = new List<(ulong EntityId, uint Speed, bool Rush)>();
        foreach (var entity in ctx.Db.Entity.Iter().ToList())
        {
            if (!entity.Alive)
            {
                continue;
            }

            var speed = entity.NextTurnSpeedOverride > 0
                ? entity.NextTurnSpeedOverride
                : ToUInt(entity.Speed);
            entries.Add((entity.EntityId, speed, entity.GoFirstNextRound));
            ctx.Db.Entity.EntityId.Update(
                entity with
                {
                    NextTurnSpeedOverride = 0,
                    GoFirstNextRound = false,
                }
            );
        }

        entries.Sort(
            (a, b) =>
            {
                var rush = b.Rush.CompareTo(a.Rush);
                if (rush != 0)
                {
                    return rush;
                }

                var speed = b.Speed.CompareTo(a.Speed);
                return speed != 0 ? speed : a.EntityId.CompareTo(b.EntityId);
            }
        );

        for (var i = 0; i < entries.Count; i++)
        {
            ctx.Db.TurnOrder.Insert(
                new TurnOrder
                {
                    Idx = (uint)i,
                    EntityId = entries[i].EntityId,
                    Speed = entries[i].Speed,
                    HasActed = false,
                    IsRush = entries[i].Rush,
                }
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

        if (ctx.Db.TurnOrder.Idx.Find(idx) is TurnOrder row)
        {
            ctx.Db.TurnOrder.Idx.Update(row with { HasActed = true });
        }

        if (entity.Faction == Team.Players)
        {
            StartPlayerTurn(ctx, entity);
            entity = ctx.Db.Entity.EntityId.Find(entity.EntityId)!.Value;
        }
        else if (entity.NextTurnStrengthBonus > 0)
        {
            ctx.Db.Entity.EntityId.Update(
                entity with
                {
                    StrengthBuff = entity.NextTurnStrengthBonus,
                    NextTurnStrengthBonus = 0,
                }
            );
            entity = ctx.Db.Entity.EntityId.Find(entity.EntityId)!.Value;
        }

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

    static void StartPlayerTurn(ReducerContext ctx, Entity entity)
    {
        var mana = SaturatingAdd(ToUInt(entity.Mana), MpRegenPerTurn);
        if (mana > ToUInt(entity.MaxMana))
        {
            mana = ToUInt(entity.MaxMana);
        }

        ctx.Db.Entity.EntityId.Update(
            entity with
            {
                Mana = ToInt(mana),
                StrengthBuff = entity.NextTurnStrengthBonus,
                NextTurnStrengthBonus = 0,
            }
        );
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

    static void ApplyOutgoingDamage(
        ReducerContext ctx,
        Entity attacker,
        Entity target,
        CombatActionType action,
        string skillName,
        uint skillBaseDamage
    )
    {
        if (!target.Alive)
        {
            return;
        }

        uint chrDamage;
        if (attacker.Faction == Team.Players && ctx.Db.Player.EntityId.Find(attacker.EntityId) is Player owner)
        {
            chrDamage = CharacterDamage(
                owner.Class,
                skillBaseDamage,
                ToUInt(attacker.Dexterity),
                ToUInt(attacker.Intelligence),
                ToUInt(attacker.Speed)
            );
        }
        else
        {
            chrDamage = skillBaseDamage;
        }

        var strength = EffectiveStrength(ToUInt(attacker.Strength), attacker.StrengthBuff);
        var incoming = DealtDamage(chrDamage, strength, ToUInt(attacker.Atk));
        var clashed = attacker.Faction == Team.Players && attacker.Speed > target.Speed;
        if (clashed)
        {
            incoming = SaturatingAdd(incoming, ClashDamageBonus);
        }

        var dodged = (uint)ctx.Rng.Next(1, 101) <= DodgeChance(ToUInt(target.Dexterity));
        var damage = dodged ? 0u : IncomingAfterDefense(incoming, ToUInt(target.Defense));

        var hp = SaturatingSub(ToUInt(target.Hp), damage);
        var alive = hp > 0;
        ctx.Db.Entity.EntityId.Update(target with { Hp = ToInt(hp), Alive = alive });

        var clashText = clashed ? " Clash!" : "";
        var result = dodged ? $"{target.Name} dodged." : $"{target.Name} took {damage} damage.";
        var message = $"{attacker.Name} used {skillName}. {result}{clashText}";
        PushCombatEvent(
            ctx,
            attacker.EntityId,
            target.EntityId,
            action,
            skillName,
            damage,
            0,
            dodged,
            clashed,
            message
        );
        AddLog(ctx, message);

        if (!alive)
        {
            AddLog(ctx, $"{target.Name} is defeated!");
        }
    }

    static void ApplyFocus(ReducerContext ctx, Entity actor)
    {
        var mana = SaturatingAdd(ToUInt(actor.Mana), FocusManaRecover);
        if (mana > ToUInt(actor.MaxMana))
        {
            mana = ToUInt(actor.MaxMana);
        }

        var restored = SaturatingSub(mana, ToUInt(actor.Mana));
        ctx.Db.Entity.EntityId.Update(actor with { Mana = ToInt(mana) });
        PushCombatEvent(
            ctx,
            actor.EntityId,
            actor.EntityId,
            CombatActionType.Defend,
            "Focus",
            0,
            restored,
            false,
            false,
            $"{actor.Name} focuses (+{FocusManaRecover} MP)."
        );
        AddLog(ctx, $"{actor.Name} focuses and restores {restored} mana ({mana}/{actor.MaxMana}).");
    }

    static List<Entity> SelectEnemyTargets(ReducerContext ctx, ulong preferredId, uint count)
    {
        var living = new List<Entity>();
        Entity? preferred = null;
        foreach (var enemy in LivingMembers(ctx, Team.Enemies))
        {
            if (enemy.EntityId == preferredId)
            {
                preferred = enemy;
            }
            else
            {
                living.Add(enemy);
            }
        }

        var selected = new List<Entity>();
        if (preferred is Entity chosen)
        {
            selected.Add(chosen);
        }

        foreach (var enemy in living)
        {
            if (selected.Count >= count)
            {
                break;
            }

            selected.Add(enemy);
        }

        if (selected.Count == 0)
        {
            throw new Exception("No living enemies to target.");
        }

        return selected;
    }

    static SkillDef? PickEnemySkill(ReducerContext ctx, Entity enemy)
    {
        var options = new List<SkillDef>();
        foreach (var owned in ctx.Db.EntitySkill.EntityId.Filter(enemy.EntityId))
        {
            if (ctx.Db.SkillDef.Id.Find(owned.SkillDefId) is SkillDef skill)
            {
                options.Add(skill);
            }
        }

        return options.Count == 0 ? null : options[ctx.Rng.Next(options.Count)];
    }

    // ----------------------------------------------------------- inventory

    static void EquipFromCatalog(ReducerContext ctx, Identity owner, ItemDef item)
    {
        foreach (var instance in ctx.Db.PlayerItem.Owner.Filter(owner))
        {
            if (instance.ItemDefId == item.Id && instance.EquippedSlot == EquipSlot.Bag)
            {
                EquipOwnedItem(ctx, owner, instance, item);
                return;
            }
        }

        throw new Exception($"{item.Name} is not in the bag.");
    }

    static void EquipOwnedItem(ReducerContext ctx, Identity owner, PlayerItem instance, ItemDef item)
    {
        if (ctx.Db.Player.Identity.Find(owner) is not Player player)
        {
            throw new Exception("Not in the party.");
        }

        var slot = EquipSlotFor(item);
        if (slot == EquipSlot.Bag)
        {
            throw new Exception("That item cannot be equipped.");
        }

        foreach (var other in ctx.Db.PlayerItem.Owner.Filter(owner).ToList())
        {
            if (other.Id != instance.Id && other.EquippedSlot == slot)
            {
                ctx.Db.PlayerItem.Id.Update(other with { EquippedSlot = EquipSlot.Bag });
            }
        }

        ctx.Db.PlayerItem.Id.Update(instance with { EquippedSlot = slot });
        ctx.Db.Player.Identity.Update(
            player with
            {
                EquippedWeaponDefId = slot == EquipSlot.Weapon ? item.Id : player.EquippedWeaponDefId,
                EquippedHelmetDefId = slot == EquipSlot.Helmet ? item.Id : player.EquippedHelmetDefId,
                EquippedChestplateDefId =
                    slot == EquipSlot.Chestplate ? item.Id : player.EquippedChestplateDefId,
                EquippedLeggingsDefId = slot == EquipSlot.Leggings ? item.Id : player.EquippedLeggingsDefId,
                EquippedBootsDefId = slot == EquipSlot.Boots ? item.Id : player.EquippedBootsDefId,
            }
        );
        RefreshDerivedStats(ctx, owner);
    }

    static void UnequipOwnedItem(ReducerContext ctx, Identity owner, PlayerItem instance)
    {
        if (instance.EquippedSlot == EquipSlot.Bag)
        {
            throw new Exception("That item is not equipped.");
        }

        if (BagCount(ctx, owner) >= BagCapacity)
        {
            throw new Exception("Inventory is full.");
        }

        if (ctx.Db.Player.Identity.Find(owner) is not Player player)
        {
            throw new Exception("Not in the party.");
        }

        var slot = instance.EquippedSlot;
        ctx.Db.PlayerItem.Id.Update(instance with { EquippedSlot = EquipSlot.Bag });
        ctx.Db.Player.Identity.Update(
            player with
            {
                EquippedWeaponDefId = slot == EquipSlot.Weapon ? 0 : player.EquippedWeaponDefId,
                EquippedHelmetDefId = slot == EquipSlot.Helmet ? 0 : player.EquippedHelmetDefId,
                EquippedChestplateDefId = slot == EquipSlot.Chestplate ? 0 : player.EquippedChestplateDefId,
                EquippedLeggingsDefId = slot == EquipSlot.Leggings ? 0 : player.EquippedLeggingsDefId,
                EquippedBootsDefId = slot == EquipSlot.Boots ? 0 : player.EquippedBootsDefId,
            }
        );
        RefreshDerivedStats(ctx, owner);
    }

    static EquipSlot EquipSlotFor(ItemDef item) =>
        item.Kind switch
        {
            ItemKind.Weapon => EquipSlot.Weapon,
            ItemKind.Armor => item.ArmorSlot switch
            {
                ArmorSlot.Helmet => EquipSlot.Helmet,
                ArmorSlot.Chestplate => EquipSlot.Chestplate,
                ArmorSlot.Leggings => EquipSlot.Leggings,
                ArmorSlot.Boots => EquipSlot.Boots,
                _ => EquipSlot.Bag,
            },
            _ => EquipSlot.Bag,
        };

    static uint BagCount(ReducerContext ctx, Identity owner)
    {
        uint count = 0;
        foreach (var item in ctx.Db.PlayerItem.Owner.Filter(owner))
        {
            if (item.EquippedSlot != EquipSlot.Bag)
            {
                continue;
            }

            if (ctx.Db.ItemDef.Id.Find(item.ItemDefId) is ItemDef def && def.Kind == ItemKind.Consumable)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    static void RefreshDerivedStats(ReducerContext ctx, Identity owner)
    {
        if (ctx.Db.Player.Identity.Find(owner) is not Player player)
        {
            throw new Exception("Not in the party.");
        }

        if (ctx.Db.Entity.EntityId.Find(player.EntityId) is not Entity entity)
        {
            throw new Exception("Your character is missing.");
        }

        var gear = EquippedGear(ctx, player);
        var pips = EquippedHealthPips(ctx, owner);
        var strength = ClampStat(SaturatingAdd(player.BaseStrength, GearStat(gear, StatType.Strength)));
        var dexterity = ClampStat(SaturatingAdd(player.BaseDexterity, GearStat(gear, StatType.Dexterity)));
        var intelligence = ClampStat(
            SaturatingAdd(player.BaseIntelligence, GearStat(gear, StatType.Intelligence))
        );
        var speed = ClampStat(SaturatingAdd(player.BaseSpeed, GearStat(gear, StatType.Speed)));
        var atk = 0u;
        var manaBonus = 0u;
        foreach (var piece in gear)
        {
            atk = SaturatingAdd(atk, piece.AtkBonus);
            manaBonus = SaturatingAdd(manaBonus, piece.MaxManaBonus);
        }

        var maxHealth = SaturatingAdd(ClassMaxHealth(player.Class), pips);
        var maxMana = SaturatingAdd(
            SaturatingAdd(ClassBaseMana(player.Class), intelligence),
            manaBonus
        );

        var currHealth = ToUInt(entity.Hp);
        if (currHealth > maxHealth)
        {
            currHealth = maxHealth;
        }

        var currMana = ToUInt(entity.Mana);
        if (maxMana > ToUInt(entity.MaxMana))
        {
            currMana = SaturatingAdd(currMana, SaturatingSub(maxMana, ToUInt(entity.MaxMana)));
        }
        else if (currMana > maxMana)
        {
            currMana = maxMana;
        }

        ctx.Db.Entity.EntityId.Update(
            entity with
            {
                Strength = ToInt(strength),
                Dexterity = ToInt(dexterity),
                Intelligence = ToInt(intelligence),
                Speed = ToInt(speed),
                Atk = ToInt(atk),
                MaxHp = ToInt(maxHealth),
                Hp = ToInt(currHealth),
                MaxMana = ToInt(maxMana),
                Mana = ToInt(currMana),
            }
        );
    }

    static List<ItemDef> EquippedGear(ReducerContext ctx, Player player)
    {
        var gear = new List<ItemDef>();
        AddGear(ctx, gear, player.EquippedWeaponDefId);
        AddGear(ctx, gear, player.EquippedHelmetDefId);
        AddGear(ctx, gear, player.EquippedChestplateDefId);
        AddGear(ctx, gear, player.EquippedLeggingsDefId);
        AddGear(ctx, gear, player.EquippedBootsDefId);
        return gear;
    }

    static void AddGear(ReducerContext ctx, List<ItemDef> gear, uint itemDefId)
    {
        if (itemDefId != 0 && ctx.Db.ItemDef.Id.Find(itemDefId) is ItemDef item)
        {
            gear.Add(item);
        }
    }

    static uint EquippedHealthPips(ReducerContext ctx, Identity owner)
    {
        uint pips = 0;
        foreach (var item in ctx.Db.PlayerItem.Owner.Filter(owner))
        {
            if (item.EquippedSlot != EquipSlot.Bag && item.EquippedSlot != EquipSlot.Weapon)
            {
                pips = SaturatingAdd(pips, item.HealthPips);
            }
        }

        return pips;
    }

    static void RequireWeapon(Player player)
    {
        if (player.EquippedWeaponDefId == 0)
        {
            throw new Exception("Equip a weapon to attack.");
        }
    }

    static uint GearStat(List<ItemDef> gear, StatType stat)
    {
        uint bonus = 0;
        foreach (var item in gear)
        {
            bonus = SaturatingAdd(bonus, StatBonus(item, stat));
        }

        return bonus;
    }

    static uint StatBonus(ItemDef item, StatType stat) =>
        stat switch
        {
            StatType.Strength => item.StrengthBonus,
            StatType.Dexterity => item.DexterityBonus,
            StatType.Intelligence => item.IntelligenceBonus,
            StatType.Speed => item.SpeedBonus,
            _ => 0,
        };

    static (uint Strength, uint Dexterity, uint Intelligence, uint Speed) RollStarterStats(
        Random rng,
        PlayerClass classChoice
    )
    {
        var main = RollInclusive(rng, MainStatMin, MainStatMax);
        var strength = RollInclusive(rng, OffStatMin, OffStatMax);
        var dexterity = RollInclusive(rng, OffStatMin, OffStatMax);
        var intelligence = RollInclusive(rng, OffStatMin, OffStatMax);
        var speed = RollInclusive(rng, OffStatMin, OffStatMax);
        switch (MainStat(classChoice))
        {
            case StatType.Strength:
                strength = main;
                break;
            case StatType.Dexterity:
                dexterity = main;
                break;
            case StatType.Intelligence:
                intelligence = main;
                break;
            default:
                speed = main;
                break;
        }

        return (strength, dexterity, intelligence, speed);
    }

    static void GrantClassSkills(ReducerContext ctx, Identity owner, PlayerClass classChoice)
    {
        foreach (var skill in ctx.Db.SkillDef.Iter())
        {
            if (skill.IsEnemySkill || skill.Class != classChoice)
            {
                continue;
            }

            ctx.Db.PlayerSkill.Insert(
                new PlayerSkill
                {
                    Id = 0,
                    Owner = owner,
                    SkillDefId = skill.Id,
                    Unlocked = skill.UnlockFloor == 0,
                }
            );
        }
    }

    static bool OwnsUnlockedSkill(ReducerContext ctx, Identity owner, uint skillDefId)
    {
        foreach (var owned in ctx.Db.PlayerSkill.Owner.Filter(owner))
        {
            if (owned.SkillDefId == skillDefId && owned.Unlocked)
            {
                return true;
            }
        }

        return false;
    }

    static PlayerClass RollClass(ReducerContext ctx)
    {
        var classes = new[]
        {
            PlayerClass.Warrior,
            PlayerClass.Archer,
            PlayerClass.Mage,
            PlayerClass.Rogue,
        };
        var mix =
            ctx.Timestamp.MicrosecondsSinceUnixEpoch
            ^ ctx.Rng.Next()
            ^ ctx.Rng.Next()
            ^ ctx.Sender.GetHashCode();
        return classes[(int)(mix & 3)];
    }

    static void GiveStarterArmor(ReducerContext ctx, Identity owner)
    {
        GiveItem(ctx, owner, RequireItemDefByName(ctx, "Leather Helm").Id, 1);
        GiveItem(ctx, owner, RequireItemDefByName(ctx, "Leather Vest").Id, 1);
        GiveItem(ctx, owner, RequireItemDefByName(ctx, "Leather Leggings").Id, 1);
        GiveItem(ctx, owner, RequireItemDefByName(ctx, "Leather Boots").Id, 1);
    }

    static void GiveConsumable(ReducerContext ctx, Identity owner, string name, uint quantity)
    {
        if (quantity == 0)
        {
            return;
        }

        GiveItem(ctx, owner, RequireItemDefByName(ctx, name).Id, quantity);
    }

    static void GiveItem(ReducerContext ctx, Identity owner, uint itemDefId, uint quantity)
    {
        if (ctx.Db.ItemDef.Id.Find(itemDefId) is not ItemDef def)
        {
            throw new Exception("Unknown item.");
        }

        var pips = 0u;
        if (def.Kind == ItemKind.Armor)
        {
            pips = RollArmorPips(ctx.Rng, def.ArmorSlot);
        }

        if (def.Kind != ItemKind.Consumable && BagCount(ctx, owner) >= BagCapacity)
        {
            throw new Exception("Inventory is full.");
        }

        ctx.Db.PlayerItem.Insert(
            new PlayerItem
            {
                Id = 0,
                Owner = owner,
                ItemDefId = itemDefId,
                Quantity = quantity,
                EquippedSlot = EquipSlot.Bag,
                HealthPips = pips,
            }
        );
    }

    static uint RollArmorPips(Random rng, ArmorSlot slot)
    {
        var cap = ArmorPipCap(slot);
        return cap == 0 ? 0 : RollInclusive(rng, ArmorPipMin, cap);
    }

    static string StarterWeaponName(PlayerClass classChoice) =>
        classChoice switch
        {
            PlayerClass.Warrior => "Starter Sword",
            PlayerClass.Archer => "Starter Bow",
            PlayerClass.Mage => "Starter Staff",
            PlayerClass.Rogue => "Starter Dagger",
            _ => "Starter Sword",
        };

    static void DeletePlayerOwnedRows(ReducerContext ctx, Identity owner)
    {
        foreach (var skill in ctx.Db.PlayerSkill.Owner.Filter(owner).ToList())
        {
            ctx.Db.PlayerSkill.Id.Delete(skill.Id);
        }

        foreach (var item in ctx.Db.PlayerItem.Owner.Filter(owner).ToList())
        {
            ctx.Db.PlayerItem.Id.Delete(item.Id);
        }
    }

    static void ClearTimers(ReducerContext ctx)
    {
        foreach (var timer in ctx.Db.EnemyTurnTimer.Iter().ToList())
        {
            ctx.Db.EnemyTurnTimer.ScheduledId.Delete(timer.ScheduledId);
        }
    }

    static void ClearTurnOrder(ReducerContext ctx)
    {
        foreach (var entry in ctx.Db.TurnOrder.Iter().ToList())
        {
            ctx.Db.TurnOrder.Idx.Delete(entry.Idx);
        }
    }

    static void ClearCombatEvents(ReducerContext ctx)
    {
        foreach (var row in ctx.Db.CombatEvent.Iter().ToList())
        {
            ctx.Db.CombatEvent.Id.Delete(row.Id);
        }
    }

    static void PushCombatEvent(
        ReducerContext ctx,
        ulong actorEntityId,
        ulong targetEntityId,
        CombatActionType action,
        string skillName,
        uint damage,
        uint healing,
        bool dodged,
        bool clashed,
        string message
    )
    {
        var session = RequireSession(ctx);
        ctx.Db.CombatEvent.Insert(
            new CombatEvent
            {
                Id = 0,
                Round = session.Round,
                ActorEntityId = actorEntityId,
                TargetEntityId = targetEntityId,
                ActionType = action,
                SkillName = skillName,
                Damage = damage,
                Healing = healing,
                Dodged = dodged,
                Clashed = clashed,
                Message = message,
            }
        );
    }

    // ---------------------------------------------------------------- helpers

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

    static Player RequirePlayer(ReducerContext ctx)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is Player player)
        {
            return player;
        }

        throw new Exception("You are not in the party.");
    }

    static List<Entity> LivingMembers(ReducerContext ctx, Team faction) =>
        ctx.Db.Entity.Iter().Where(e => e.Faction == faction && e.Alive).OrderBy(e => e.Slot).ToList();

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
        var round = ctx.Db.GameSession.Id.Find(SessionId) is GameSession session ? session.Round : 0u;

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
