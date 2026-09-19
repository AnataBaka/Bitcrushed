using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Reducer(ReducerKind.Init)]
    public static void Init(ReducerContext ctx)
    {
        ctx.Db.GameSession.Insert(new GameSession
        {
            Id = SessionId,
            PlayerCount = 0,
            MaxPlayers = MaxPartySize,
            Phase = GamePhase.Waiting,
            Floor = 0,
            Biome = Biome.Forest,
            IsBossFloor = false,
            RoundNumber = 0,
            TurnNumber = 0,
            ActiveKind = CombatantKind.Player,
            ActiveCombatantId = 0,
        });
        SeedCatalog(ctx);
        Log.Info("Initialized tower session, skills, and items.");
    }

    [SpacetimeDB.Reducer(ReducerKind.ClientConnected)]
    public static void ClientConnected(ReducerContext ctx)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is Player player)
        {
            ctx.Db.Player.Identity.Update(player with { Online = true });
            Log.Info($"{player.Name} in slot {player.Slot} reconnected.");
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
            Log.Info($"{player.Name} in slot {player.Slot} went offline.");
        }
    }

    [SpacetimeDB.Reducer]
    public static void JoinGame(ReducerContext ctx)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not null)
        {
            throw new Exception("Already joined the party.");
        }

        var session = RequireSession(ctx);
        if (session.Phase != GamePhase.Waiting)
        {
            throw new Exception("Cannot join after the run has started.");
        }

        if (session.PlayerCount >= session.MaxPlayers)
        {
            throw new Exception("Party is full.");
        }

        var classChoice = (PlayerClass)ctx.Rng.Next(0, 4);
        var slot = FindFreeSlot(ctx);
        var rolled = RollStarterStats(ctx.Rng, classChoice);
        var weapon = RequireItemDefByName(ctx, StarterWeaponName(classChoice));
        var healthPotion = RequireItemDefByName(ctx, "Health Potion");

        var maxHealth = ClassMaxHealth(classChoice);
        var maxMana = SaturatingAdd(ClassBaseMana(classChoice), rolled.Intelligence);
        var player = ctx.Db.Player.Insert(new Player
        {
            Identity = ctx.Sender,
            Slot = slot,
            Online = true,
            Class = classChoice,
            Name = RandomPlayerName(ctx.Rng, classChoice),
            SpriteId = RollInclusive(ctx.Rng, 0, SpriteVariantCount - 1),
            Level = 1,
            Xp = 0,
            UnspentStatPoints = 0,
            MaxHealth = maxHealth,
            CurrHealth = maxHealth,
            MaxMana = maxMana,
            CurrMana = maxMana,
            Speed = rolled.Speed,
            Strength = rolled.Strength,
            Dexterity = rolled.Dexterity,
            Intelligence = rolled.Intelligence,
            BaseSpeed = rolled.Speed,
            BaseStrength = rolled.Strength,
            BaseDexterity = rolled.Dexterity,
            BaseIntelligence = rolled.Intelligence,
            Atk = 0,
            BaseDefense = 0,
            Defense = 0,
            StrengthBuff = 0,
            NextTurnStrengthBonus = 0,
            NextTurnSpeedOverride = 0,
            GoFirstNextRound = false,
            IsDefending = false,
            Alive = true,
            EquippedWeaponDefId = 0,
            EquippedArmorDefId = 0,
        });

        GiveItem(ctx, player.Identity, weapon.Id, 1);
        GiveItem(ctx, player.Identity, healthPotion.Id, 1);
        EquipFromCatalog(ctx, player.Identity, weapon);
        GrantClassSkills(ctx, player.Identity, classChoice, 0);

        var playerCount = session.PlayerCount + 1;
        ctx.Db.GameSession.Id.Update(session with { PlayerCount = playerCount });
        Log.Info($"Player joined as {player.Name} the {classChoice} in slot {slot}.");
    }

    [SpacetimeDB.Reducer]
    public static void LeaveGame(ReducerContext ctx)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player)
        {
            throw new Exception("Not in the party.");
        }

        var wasActive = IsActiveCombatant(ctx, CombatantKind.Player, player.Slot);
        DeletePlayerOwnedRows(ctx, player.Identity);
        ctx.Db.Player.Identity.Delete(ctx.Sender);

        var session = RequireSession(ctx);
        var playerCount = SaturatingSub(session.PlayerCount, 1);
        if (playerCount == 0)
        {
            ResetRun(ctx, session with { PlayerCount = 0 });
            Log.Info($"Player left slot {player.Slot}. Party empty, run reset.");
            return;
        }

        ctx.Db.GameSession.Id.Update(session with { PlayerCount = playerCount });
        if (session.Phase == GamePhase.Combat && wasActive)
        {
            AdvanceAfterAction(ctx);
        }

        Log.Info($"Player left slot {player.Slot}.");
    }

    [SpacetimeDB.Reducer]
    public static void StartRun(ReducerContext ctx)
    {
        RequirePlayer(ctx);
        var session = RequireSession(ctx);
        if (session.Phase != GamePhase.Waiting)
        {
            throw new Exception("A run is already in progress.");
        }

        if (session.PlayerCount == 0)
        {
            throw new Exception("Need at least one player to start.");
        }

        BeginFloor(ctx, 1);
        Log.Info("Tower run started on floor 1.");
    }

    [SpacetimeDB.Reducer]
    public static void ResetEncounter(ReducerContext ctx)
    {
        RequirePlayer(ctx);
        foreach (var player in ctx.Db.Player.Iter().ToList())
        {
            RerollPlayer(ctx, player);
        }

        BeginFloor(ctx, 1);
        Log.Info("Test encounter reset with a random class and full HP.");
    }

    [SpacetimeDB.Reducer]
    public static void AdvanceFloor(ReducerContext ctx)
    {
        RequirePlayer(ctx);
        var session = RequireSession(ctx);
        if (session.Phase != GamePhase.FloorClear)
        {
            throw new Exception("The current floor is not cleared.");
        }

        BeginFloor(ctx, session.Floor + 1);
        Log.Info($"Advanced to floor {session.Floor + 1}.");
    }

    [SpacetimeDB.Reducer]
    public static void AllocateStat(ReducerContext ctx, StatType stat, uint points)
    {
        if (points == 0)
        {
            throw new Exception("Allocate at least 1 point.");
        }

        var player = RequirePlayer(ctx);
        if (player.UnspentStatPoints < points)
        {
            throw new Exception("Not enough unspent stat points.");
        }

        var strength = player.BaseStrength;
        var dexterity = player.BaseDexterity;
        var intelligence = player.BaseIntelligence;
        var speed = player.BaseSpeed;
        switch (stat)
        {
            case StatType.Strength:
                strength = ClampStat(SaturatingAdd(strength, points));
                break;
            case StatType.Dexterity:
                dexterity = ClampStat(SaturatingAdd(dexterity, points));
                break;
            case StatType.Intelligence:
                intelligence = ClampStat(SaturatingAdd(intelligence, points));
                break;
            case StatType.Speed:
                speed = ClampStat(SaturatingAdd(speed, points));
                break;
            default:
                throw new Exception("Unknown stat.");
        }

        ctx.Db.Player.Identity.Update(player with
        {
            UnspentStatPoints = SaturatingSub(player.UnspentStatPoints, points),
            BaseStrength = strength,
            BaseDexterity = dexterity,
            BaseIntelligence = intelligence,
            BaseSpeed = speed,
        });
        RefreshDerivedStats(ctx, ctx.Sender);
    }

    [SpacetimeDB.Reducer]
    public static void EquipItem(ReducerContext ctx, uint itemInstanceId)
    {
        var player = RequirePlayer(ctx);
        var session = RequireSession(ctx);
        if (session.Phase == GamePhase.Combat)
        {
            throw new Exception("Cannot equip during combat.");
        }

        if (ctx.Db.PlayerItem.Id.Find(itemInstanceId) is not PlayerItem instance || instance.Owner != ctx.Sender)
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

        EquipFromCatalog(ctx, player.Identity, item);
    }

    [SpacetimeDB.Reducer]
    public static void SubmitAction(ReducerContext ctx, CombatActionType action, uint skillDefId, uint targetEnemyId, uint itemInstanceId)
    {
        var player = RequirePlayer(ctx);
        var session = RequireSession(ctx);
        if (session.Phase != GamePhase.Combat)
        {
            throw new Exception("Not in combat.");
        }

        if (!player.Alive)
        {
            throw new Exception("This character is down.");
        }

        if (session.ActiveKind != CombatantKind.Player || session.ActiveCombatantId != player.Slot)
        {
            throw new Exception("Not this character's turn.");
        }

        switch (action)
        {
            case CombatActionType.Attack:
                ResolvePlayerAttack(ctx, player, targetEnemyId);
                break;
            case CombatActionType.Spell:
                ResolvePlayerSkill(ctx, player, skillDefId, targetEnemyId);
                break;
            case CombatActionType.Defend:
                ResolvePlayerDefend(ctx, player);
                break;
            case CombatActionType.Item:
                ResolvePlayerItem(ctx, player, itemInstanceId);
                break;
            default:
                throw new Exception("Unknown action.");
        }

        MarkActed(ctx, CombatantKind.Player, player.Slot);
        AdvanceAfterAction(ctx);
    }

    private static void BeginFloor(ReducerContext ctx, uint floor)
    {
        ClearEncounter(ctx);
        foreach (var player in ctx.Db.Player.Iter().ToList())
        {
            UnlockClassSkills(ctx, player.Identity, player.Class, floor);
            ctx.Db.Player.Identity.Update(player with
            {
                StrengthBuff = 0,
                NextTurnStrengthBonus = 0,
                NextTurnSpeedOverride = 0,
                GoFirstNextRound = false,
                IsDefending = false,
                Defense = player.BaseDefense,
                Alive = player.CurrHealth > 0,
            });
        }

        var biome = BiomeForFloor(floor);
        var boss = IsBossFloor(floor);
        var session = RequireSession(ctx);
        ctx.Db.GameSession.Id.Update(session with
        {
            Phase = GamePhase.Combat,
            Floor = floor,
            Biome = biome,
            IsBossFloor = boss,
            RoundNumber = 1,
            TurnNumber = 0,
            ActiveKind = CombatantKind.Player,
            ActiveCombatantId = 0,
        });

        SpawnEncounter(ctx, floor, biome, boss);
        BuildTurnOrder(ctx);
        var first = NextUnacted(ctx);
        if (first is TurnOrder row)
        {
            SetActive(ctx, row);
            StartCurrentTurn(ctx);
        }
    }

    private static void SpawnEncounter(ReducerContext ctx, uint floor, Biome biome, bool boss)
    {
        var enemyCount = MaxPartySize;
        for (uint slot = 0; slot < enemyCount; slot++)
        {
            var isBoss = boss && slot == 0;
            var speed = SaturatingAdd(RollInclusive(ctx.Rng, 2, 6), TraitSpeedBonus(biome));
            var strength = ScaledEnemyStrength(RollInclusive(ctx.Rng, 2, 5), floor, isBoss);
            var dexterity = SaturatingAdd(RollInclusive(ctx.Rng, 1, 4), TraitDexterityBonus(biome));
            var intelligence = RollInclusive(ctx.Rng, 1, 3);
            var atk = SaturatingAdd(ScaledEnemyAtk(RollInclusive(ctx.Rng, 3, 6), floor, isBoss), TraitAtkBonus(biome));
            var maxHealth = ScaledEnemyHealth(isBoss ? 80u : 28u, floor, isBoss);
            var maxMana = isBoss ? 40u : 20u;
            var enemy = ctx.Db.Enemy.Insert(new Enemy
            {
                Id = 0,
                Slot = slot,
                Name = RandomEnemyName(ctx.Rng, biome, isBoss),
                SpriteId = isBoss ? 100 + (uint)biome : RollInclusive(ctx.Rng, 0, 5),
                Biome = biome,
                TraitName = TraitName(biome),
                IsBoss = isBoss,
                Floor = floor,
                MaxHealth = maxHealth,
                CurrHealth = maxHealth,
                MaxMana = maxMana,
                CurrMana = maxMana,
                Speed = speed,
                Strength = strength,
                Dexterity = dexterity,
                Intelligence = intelligence,
                Atk = atk,
                Defense = isBoss ? 4u : 1u,
                StrengthBuff = 0,
                NextTurnStrengthBonus = 0,
                NextTurnSpeedOverride = 0,
                GoFirstNextRound = false,
                Alive = true,
            });

            AttachEnemySkills(ctx, enemy.Id, biome, isBoss);
        }
    }

    private static void AttachEnemySkills(ReducerContext ctx, uint enemyId, Biome biome, bool boss)
    {
        var names = boss
            ? new[] { "Strike", "Crushing Blow" }
            : biome switch
            {
                Biome.Ocean => new[] { "Strike", "Tidal Slash", "Riptide" },
                Biome.Hell => new[] { "Strike", "Hellfire", "Infernal Burst" },
                _ => new[] { "Strike", "Howl" },
            };

        foreach (var name in names)
        {
            if (FindSkillDefByName(ctx, name) is SkillDef skill)
            {
                ctx.Db.EnemySkill.Insert(new EnemySkill
                {
                    Id = 0,
                    EnemyId = enemyId,
                    SkillDefId = skill.Id,
                });
            }
        }
    }

    private static void ResolvePlayerAttack(ReducerContext ctx, Player player, uint targetEnemyId)
    {
        var enemy = RequireLivingEnemy(ctx, targetEnemyId);
        ApplyOutgoingDamage(
            ctx,
            CombatantKind.Player,
            player.Slot,
            player.Name,
            CombatActionType.Attack,
            "Attack",
            0,
            EffectiveStrength(player.Strength, player.StrengthBuff),
            player.Atk,
            player.Speed,
            CombatantKind.Enemy,
            enemy.Id,
            enemy.Name,
            enemy.Dexterity,
            enemy.Defense,
            enemy.Speed);
    }

    private static void ResolvePlayerSkill(ReducerContext ctx, Player player, uint skillDefId, uint targetEnemyId)
    {
        if (!OwnsUnlockedSkill(ctx, player.Identity, skillDefId))
        {
            throw new Exception("Skill is not unlocked for this character.");
        }

        if (ctx.Db.SkillDef.Id.Find(skillDefId) is not SkillDef skill || skill.IsEnemySkill)
        {
            throw new Exception("Unknown skill.");
        }

        if (player.CurrMana < skill.ManaCost)
        {
            throw new Exception("Not enough mana.");
        }

        ctx.Db.Player.Identity.Update(player with { CurrMana = SaturatingSub(player.CurrMana, skill.ManaCost) });
        player = ctx.Db.Player.Identity.Find(player.Identity)!.Value;

        if (skill.AlwaysGoFirst)
        {
            ctx.Db.Player.Identity.Update(player with { GoFirstNextRound = true });
            player = ctx.Db.Player.Identity.Find(player.Identity)!.Value;
        }

        if (skill.NextTurnStrengthBonus > 0 || skill.NextTurnSpeedOverride > 0)
        {
            ctx.Db.Player.Identity.Update(player with
            {
                NextTurnStrengthBonus = SaturatingAdd(player.NextTurnStrengthBonus, skill.NextTurnStrengthBonus),
                NextTurnSpeedOverride = skill.NextTurnSpeedOverride > 0 ? skill.NextTurnSpeedOverride : player.NextTurnSpeedOverride,
            });
            player = ctx.Db.Player.Identity.Find(player.Identity)!.Value;
            PushCombatEvent(
                ctx,
                CombatantKind.Player,
                player.Slot,
                CombatantKind.Player,
                player.Slot,
                CombatActionType.Spell,
                skill.Name,
                0,
                0,
                false,
                false,
                $"{player.Name} used {skill.Name}.");
        }

        if (skill.TargetCount == 0 || skill.BaseDamage == 0)
        {
            return;
        }

        foreach (var enemy in SelectEnemyTargets(ctx, targetEnemyId, skill.TargetCount))
        {
            ApplyOutgoingDamage(
                ctx,
                CombatantKind.Player,
                player.Slot,
                player.Name,
                CombatActionType.Spell,
                skill.Name,
                skill.BaseDamage,
                EffectiveStrength(player.Strength, player.StrengthBuff),
                player.Atk,
                player.Speed,
                CombatantKind.Enemy,
                enemy.Id,
                enemy.Name,
                enemy.Dexterity,
                enemy.Defense,
                enemy.Speed);
        }
    }

    private static void ResolvePlayerDefend(ReducerContext ctx, Player player)
    {
        var mana = player.CurrMana + FocusManaRecover;
        if (mana > player.MaxMana)
        {
            mana = player.MaxMana;
        }

        ctx.Db.Player.Identity.Update(player with
        {
            IsDefending = false,
            Defense = player.BaseDefense,
            CurrMana = mana,
        });
        PushCombatEvent(
            ctx,
            CombatantKind.Player,
            player.Slot,
            CombatantKind.Player,
            player.Slot,
            CombatActionType.Defend,
            "Focus",
            0,
            FocusManaRecover,
            false,
            false,
            $"{player.Name} focuses (+{FocusManaRecover} MP).");
    }

    private static void ResolvePlayerItem(ReducerContext ctx, Player player, uint itemInstanceId)
    {
        if (ctx.Db.PlayerItem.Id.Find(itemInstanceId) is not PlayerItem instance || instance.Owner != player.Identity)
        {
            throw new Exception("Item not in inventory.");
        }

        if (ctx.Db.ItemDef.Id.Find(instance.ItemDefId) is not ItemDef item || item.Kind != ItemKind.Consumable)
        {
            throw new Exception("That item cannot be used.");
        }

        if (item.Name != "Health Potion")
        {
            throw new Exception("Only health potions can be used right now.");
        }

        var heal = item.HealAmount;
        var mana = item.ManaRestoreAmount;
        var newHealth = player.CurrHealth + heal;
        if (newHealth > player.MaxHealth)
        {
            newHealth = player.MaxHealth;
        }

        var newMana = player.CurrMana + mana;
        if (newMana > player.MaxMana)
        {
            newMana = player.MaxMana;
        }

        ctx.Db.Player.Identity.Update(player with
        {
            CurrHealth = newHealth,
            CurrMana = newMana,
        });

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
            CombatantKind.Player,
            player.Slot,
            CombatantKind.Player,
            player.Slot,
            CombatActionType.Item,
            item.Name,
            0,
            heal,
            false,
            false,
            $"{player.Name} used {item.Name}.");
    }

    private static void ResolveEnemyTurn(ReducerContext ctx, Enemy enemy)
    {
        var livingPlayers = LivingPlayers(ctx);
        if (livingPlayers.Count == 0)
        {
            return;
        }

        var target = livingPlayers[ctx.Rng.Next(livingPlayers.Count)];
        var skill = PickEnemySkill(ctx, enemy);
        if (skill is SkillDef chosen)
        {
            if (enemy.CurrMana >= chosen.ManaCost)
            {
                ctx.Db.Enemy.Id.Update(enemy with { CurrMana = SaturatingSub(enemy.CurrMana, chosen.ManaCost) });
                enemy = ctx.Db.Enemy.Id.Find(enemy.Id)!.Value;
                if (chosen.NextTurnStrengthBonus > 0)
                {
                    ctx.Db.Enemy.Id.Update(enemy with
                    {
                        NextTurnStrengthBonus = SaturatingAdd(enemy.NextTurnStrengthBonus, chosen.NextTurnStrengthBonus),
                    });
                    enemy = ctx.Db.Enemy.Id.Find(enemy.Id)!.Value;
                    PushCombatEvent(
                        ctx,
                        CombatantKind.Enemy,
                        enemy.Id,
                        CombatantKind.Enemy,
                        enemy.Id,
                        CombatActionType.Spell,
                        chosen.Name,
                        0,
                        0,
                        false,
                        false,
                        $"{enemy.Name} used {chosen.Name}.");
                    if (chosen.BaseDamage == 0)
                    {
                        return;
                    }
                }

                var targets = chosen.TargetCount > 1 ? livingPlayers.Take((int)chosen.TargetCount).ToList() : new List<Player> { target };
                foreach (var player in targets)
                {
                    ApplyOutgoingDamage(
                        ctx,
                        CombatantKind.Enemy,
                        enemy.Id,
                        enemy.Name,
                        CombatActionType.Spell,
                        chosen.Name,
                        chosen.BaseDamage,
                        EffectiveStrength(enemy.Strength, enemy.StrengthBuff),
                        enemy.Atk,
                        enemy.Speed,
                        CombatantKind.Player,
                        player.Slot,
                        player.Name,
                        player.Dexterity,
                        player.Defense,
                        player.Speed);
                }

                return;
            }
        }

        ApplyOutgoingDamage(
            ctx,
            CombatantKind.Enemy,
            enemy.Id,
            enemy.Name,
            CombatActionType.Attack,
            "Attack",
            0,
            EffectiveStrength(enemy.Strength, enemy.StrengthBuff),
            enemy.Atk,
            enemy.Speed,
            CombatantKind.Player,
            target.Slot,
            target.Name,
            target.Dexterity,
            target.Defense,
            target.Speed);
    }

    private static SkillDef? PickEnemySkill(ReducerContext ctx, Enemy enemy)
    {
        var options = new List<SkillDef>();
        foreach (var owned in ctx.Db.EnemySkill.EnemyId.Filter(enemy.Id))
        {
            if (ctx.Db.SkillDef.Id.Find(owned.SkillDefId) is SkillDef skill)
            {
                options.Add(skill);
            }
        }

        if (options.Count == 0)
        {
            return null;
        }

        return options[ctx.Rng.Next(options.Count)];
    }

    private static void ApplyOutgoingDamage(
        ReducerContext ctx,
        CombatantKind actorKind,
        uint actorId,
        string actorName,
        CombatActionType action,
        string skillName,
        uint skillBaseDamage,
        uint strength,
        uint atk,
        uint actorSpeed,
        CombatantKind targetKind,
        uint targetId,
        string targetName,
        uint targetDexterity,
        uint targetDefense,
        uint targetSpeed)
    {
        uint chrDamage;
        if (actorKind == CombatantKind.Player && ctx.Db.Player.Slot.Find(actorId) is Player attacker)
        {
            chrDamage = CharacterDamage(attacker.Class, skillBaseDamage, attacker.Dexterity, attacker.Intelligence, attacker.Speed);
        }
        else
        {
            // Enemies: CHR DAMAGE is the skill/attack value. Strength and ATK are added by DealtDamage.
            chrDamage = skillBaseDamage;
        }

        var incoming = DealtDamage(chrDamage, strength, atk);
        var clashed = actorKind == CombatantKind.Player && actorSpeed > targetSpeed;
        if (clashed)
        {
            incoming = SaturatingAdd(incoming, ClashDamageBonus);
        }

        var dodged = (uint)ctx.Rng.Next(1, 101) <= DodgeChance(targetDexterity);
        var damage = dodged ? 0u : IncomingAfterDefense(incoming, targetDefense);
        DealToTarget(ctx, targetKind, targetId, damage);

        var clashText = clashed ? " Clash!" : "";
        var result = dodged ? $"{targetName} dodged." : $"{targetName} took {damage} damage.";
        PushCombatEvent(
            ctx,
            actorKind,
            actorId,
            targetKind,
            targetId,
            action,
            skillName,
            damage,
            0,
            dodged,
            clashed,
            $"{actorName} used {skillName}. {result}{clashText}");
    }

    private static void DealToTarget(ReducerContext ctx, CombatantKind kind, uint id, uint damage)
    {
        if (kind == CombatantKind.Enemy)
        {
            if (ctx.Db.Enemy.Id.Find(id) is not Enemy enemy || !enemy.Alive)
            {
                return;
            }

            var hp = SaturatingSub(enemy.CurrHealth, damage);
            var alive = hp > 0;
            ctx.Db.Enemy.Id.Update(enemy with { CurrHealth = hp, Alive = alive });
            if (!alive)
            {
                GrantKillXp(ctx, enemy.Floor);
                Log.Info($"{enemy.Name} was defeated.");
            }

            return;
        }

        foreach (var player in ctx.Db.Player.Iter().ToList())
        {
            if (player.Slot != id || !player.Alive)
            {
                continue;
            }

            var hp = SaturatingSub(player.CurrHealth, damage);
            ctx.Db.Player.Identity.Update(player with { CurrHealth = hp, Alive = hp > 0 });
            if (hp == 0)
            {
                Log.Info($"{player.Name} was downed.");
            }

            return;
        }
    }

    private static void GrantKillXp(ReducerContext ctx, uint floor)
    {
        var xp = KillXp(floor);
        foreach (var player in ctx.Db.Player.Iter().ToList())
        {
            if (!player.Alive)
            {
                continue;
            }

            ApplyXp(ctx, player, xp);
        }
    }

    private static void ApplyXp(ReducerContext ctx, Player player, uint xp)
    {
        var currentXp = SaturatingAdd(player.Xp, xp);
        var level = player.Level;
        var unspent = player.UnspentStatPoints;
        while (currentXp >= XpToNextLevel(level))
        {
            currentXp = SaturatingSub(currentXp, XpToNextLevel(level));
            level += 1;
            unspent = SaturatingAdd(unspent, StatPointsPerLevel);
        }

        ctx.Db.Player.Identity.Update(player with
        {
            Xp = currentXp,
            Level = level,
            UnspentStatPoints = unspent,
        });
    }

    private static void AdvanceAfterAction(ReducerContext ctx)
    {
        if (CheckCombatEnd(ctx))
        {
            return;
        }

        var next = NextUnacted(ctx);
        if (next is null)
        {
            var session = RequireSession(ctx);
            ctx.Db.GameSession.Id.Update(session with { RoundNumber = session.RoundNumber + 1 });
            BuildTurnOrder(ctx);
            next = NextUnacted(ctx);
        }

        if (next is null)
        {
            return;
        }

        SetActive(ctx, next.Value);
        StartCurrentTurn(ctx);
    }

    private static void StartCurrentTurn(ReducerContext ctx)
    {
        var session = RequireSession(ctx);
        ctx.Db.GameSession.Id.Update(session with { TurnNumber = session.TurnNumber + 1 });
        session = RequireSession(ctx);

        if (session.ActiveKind == CombatantKind.Player)
        {
            if (ctx.Db.Player.Slot.Find(session.ActiveCombatantId) is not Player player || !player.Alive)
            {
                MarkActed(ctx, CombatantKind.Player, session.ActiveCombatantId);
                AdvanceAfterAction(ctx);
                return;
            }

            var mana = SaturatingAdd(player.CurrMana, MpRegenPerTurn);
            if (mana > player.MaxMana)
            {
                mana = player.MaxMana;
            }

            ctx.Db.Player.Identity.Update(player with
            {
                CurrMana = mana,
                StrengthBuff = player.NextTurnStrengthBonus,
                NextTurnStrengthBonus = 0,
                IsDefending = false,
                Defense = player.BaseDefense,
            });
            return;
        }

        if (ctx.Db.Enemy.Id.Find(session.ActiveCombatantId) is not Enemy enemy || !enemy.Alive)
        {
            MarkActed(ctx, CombatantKind.Enemy, session.ActiveCombatantId);
            AdvanceAfterAction(ctx);
            return;
        }

        var enemyMana = SaturatingAdd(enemy.CurrMana, MpRegenPerTurn);
        if (enemyMana > enemy.MaxMana)
        {
            enemyMana = enemy.MaxMana;
        }

        ctx.Db.Enemy.Id.Update(enemy with
        {
            CurrMana = enemyMana,
            StrengthBuff = enemy.NextTurnStrengthBonus,
            NextTurnStrengthBonus = 0,
        });
        enemy = ctx.Db.Enemy.Id.Find(enemy.Id)!.Value;
        ResolveEnemyTurn(ctx, enemy);
        MarkActed(ctx, CombatantKind.Enemy, enemy.Id);
        AdvanceAfterAction(ctx);
    }

    private static bool CheckCombatEnd(ReducerContext ctx)
    {
        var anyPlayerAlive = false;
        foreach (var player in ctx.Db.Player.Iter())
        {
            if (player.Alive)
            {
                anyPlayerAlive = true;
                break;
            }
        }

        if (!anyPlayerAlive)
        {
            var session = RequireSession(ctx);
            ctx.Db.GameSession.Id.Update(session with { Phase = GamePhase.Defeat });
            PushCombatEvent(ctx, CombatantKind.Player, 0, CombatantKind.Player, 0, CombatActionType.Defend, "", 0, 0, false, false, "The party was defeated.");
            return true;
        }

        var anyEnemyAlive = false;
        foreach (var enemy in ctx.Db.Enemy.Iter())
        {
            if (enemy.Alive)
            {
                anyEnemyAlive = true;
                break;
            }
        }

        if (!anyEnemyAlive)
        {
            var session = RequireSession(ctx);
            ctx.Db.GameSession.Id.Update(session with { Phase = GamePhase.FloorClear });
            PushCombatEvent(ctx, CombatantKind.Player, 0, CombatantKind.Player, 0, CombatActionType.Defend, "", 0, 0, false, false, $"Floor {session.Floor} cleared.");
            return true;
        }

        return false;
    }

    private static void BuildTurnOrder(ReducerContext ctx)
    {
        foreach (var row in ctx.Db.TurnOrder.Iter().ToList())
        {
            ctx.Db.TurnOrder.Id.Delete(row.Id);
        }

        var entries = new List<(CombatantKind Kind, uint Id, uint Speed, bool Rush)>();
        foreach (var player in ctx.Db.Player.Iter().ToList())
        {
            if (!player.Alive)
            {
                continue;
            }

            var speed = player.NextTurnSpeedOverride > 0 ? player.NextTurnSpeedOverride : player.Speed;
            entries.Add((CombatantKind.Player, player.Slot, speed, player.GoFirstNextRound));
            ctx.Db.Player.Identity.Update(player with
            {
                NextTurnSpeedOverride = 0,
                GoFirstNextRound = false,
            });
        }

        foreach (var enemy in ctx.Db.Enemy.Iter().ToList())
        {
            if (!enemy.Alive)
            {
                continue;
            }

            var speed = enemy.NextTurnSpeedOverride > 0 ? enemy.NextTurnSpeedOverride : enemy.Speed;
            entries.Add((CombatantKind.Enemy, enemy.Id, speed, enemy.GoFirstNextRound));
            ctx.Db.Enemy.Id.Update(enemy with
            {
                NextTurnSpeedOverride = 0,
                GoFirstNextRound = false,
            });
        }

        entries.Sort((a, b) =>
        {
            var rush = b.Rush.CompareTo(a.Rush);
            if (rush != 0)
            {
                return rush;
            }

            var speed = b.Speed.CompareTo(a.Speed);
            return speed != 0 ? speed : a.Id.CompareTo(b.Id);
        });

        uint sequence = 0;
        foreach (var entry in entries)
        {
            ctx.Db.TurnOrder.Insert(new TurnOrder
            {
                Id = 0,
                Sequence = sequence,
                Kind = entry.Kind,
                CombatantId = entry.Id,
                Speed = entry.Speed,
                HasActed = false,
                IsRush = entry.Rush,
            });
            sequence++;
        }
    }

    private static TurnOrder? NextUnacted(ReducerContext ctx)
    {
        TurnOrder? best = null;
        foreach (var row in ctx.Db.TurnOrder.Iter())
        {
            if (row.HasActed || !IsAlive(ctx, row.Kind, row.CombatantId))
            {
                continue;
            }

            if (best is null || row.Sequence < best.Value.Sequence)
            {
                best = row;
            }
        }

        return best;
    }

    private static void SetActive(ReducerContext ctx, TurnOrder row)
    {
        var session = RequireSession(ctx);
        ctx.Db.GameSession.Id.Update(session with
        {
            ActiveKind = row.Kind,
            ActiveCombatantId = row.CombatantId,
        });
    }

    private static void MarkActed(ReducerContext ctx, CombatantKind kind, uint combatantId)
    {
        foreach (var row in ctx.Db.TurnOrder.Iter().ToList())
        {
            if (row.Kind == kind && row.CombatantId == combatantId)
            {
                ctx.Db.TurnOrder.Id.Update(row with { HasActed = true });
            }
        }
    }

    private static bool IsAlive(ReducerContext ctx, CombatantKind kind, uint id)
    {
        if (kind == CombatantKind.Player)
        {
            return ctx.Db.Player.Slot.Find(id) is Player player && player.Alive;
        }

        return ctx.Db.Enemy.Id.Find(id) is Enemy enemy && enemy.Alive;
    }

    private static bool IsActiveCombatant(ReducerContext ctx, CombatantKind kind, uint id)
    {
        var session = RequireSession(ctx);
        return session.Phase == GamePhase.Combat && session.ActiveKind == kind && session.ActiveCombatantId == id;
    }

    private static List<Player> LivingPlayers(ReducerContext ctx)
    {
        var living = new List<Player>();
        foreach (var player in ctx.Db.Player.Iter())
        {
            if (player.Alive)
            {
                living.Add(player);
            }
        }

        return living;
    }

    private static List<Enemy> SelectEnemyTargets(ReducerContext ctx, uint preferredId, uint count)
    {
        var living = new List<Enemy>();
        Enemy? preferred = null;
        foreach (var enemy in ctx.Db.Enemy.Iter())
        {
            if (!enemy.Alive)
            {
                continue;
            }

            if (enemy.Id == preferredId)
            {
                preferred = enemy;
            }
            else
            {
                living.Add(enemy);
            }
        }

        var selected = new List<Enemy>();
        if (preferred is Enemy chosen)
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

    private static Enemy RequireLivingEnemy(ReducerContext ctx, uint enemyId)
    {
        if (ctx.Db.Enemy.Id.Find(enemyId) is Enemy enemy && enemy.Alive)
        {
            return enemy;
        }

        throw new Exception("Select a living enemy.");
    }

    private static void PushCombatEvent(
        ReducerContext ctx,
        CombatantKind actorKind,
        uint actorId,
        CombatantKind targetKind,
        uint targetId,
        CombatActionType action,
        string skillName,
        uint damage,
        uint healing,
        bool dodged,
        bool clashed,
        string message)
    {
        var session = RequireSession(ctx);
        ctx.Db.CombatEvent.Insert(new CombatEvent
        {
            Id = 0,
            RoundNumber = session.RoundNumber,
            TurnNumber = session.TurnNumber,
            ActorKind = actorKind,
            ActorId = actorId,
            TargetKind = targetKind,
            TargetId = targetId,
            ActionType = action,
            SkillName = skillName,
            Damage = damage,
            Healing = healing,
            Dodged = dodged,
            Clashed = clashed,
            Message = message,
        });
    }

    private static void EquipFromCatalog(ReducerContext ctx, Identity owner, ItemDef item)
    {
        if (ctx.Db.Player.Identity.Find(owner) is not Player player)
        {
            throw new Exception("Not in the party.");
        }

        var weaponId = item.Kind == ItemKind.Weapon ? item.Id : player.EquippedWeaponDefId;
        var armorId = item.Kind == ItemKind.Armor ? item.Id : player.EquippedArmorDefId;
        ctx.Db.Player.Identity.Update(player with
        {
            EquippedWeaponDefId = weaponId,
            EquippedArmorDefId = armorId,
        });
        RefreshDerivedStats(ctx, owner);
    }

    private static void RefreshDerivedStats(ReducerContext ctx, Identity owner)
    {
        if (ctx.Db.Player.Identity.Find(owner) is not Player player)
        {
            throw new Exception("Not in the party.");
        }

        ItemDef? weapon = player.EquippedWeaponDefId != 0 ? ctx.Db.ItemDef.Id.Find(player.EquippedWeaponDefId) : null;
        ItemDef? armor = player.EquippedArmorDefId != 0 ? ctx.Db.ItemDef.Id.Find(player.EquippedArmorDefId) : null;

        var strength = ClampStat(SaturatingAdd(player.BaseStrength, GearStat(weapon, armor, StatType.Strength)));
        var dexterity = ClampStat(SaturatingAdd(player.BaseDexterity, GearStat(weapon, armor, StatType.Dexterity)));
        var intelligence = ClampStat(SaturatingAdd(player.BaseIntelligence, GearStat(weapon, armor, StatType.Intelligence)));
        var speed = ClampStat(SaturatingAdd(player.BaseSpeed, GearStat(weapon, armor, StatType.Speed)));
        var atk = weapon?.AtkBonus ?? 0;
        var maxHealth = SaturatingAdd(ClassMaxHealth(player.Class), armor?.MaxHealthBonus ?? 0);
        var maxMana = SaturatingAdd(SaturatingAdd(ClassBaseMana(player.Class), intelligence), armor?.MaxManaBonus ?? 0);

        var currHealth = player.CurrHealth;
        if (maxHealth > player.MaxHealth)
        {
            currHealth = SaturatingAdd(currHealth, SaturatingSub(maxHealth, player.MaxHealth));
        }
        else if (currHealth > maxHealth)
        {
            currHealth = maxHealth;
        }

        var currMana = player.CurrMana;
        if (maxMana > player.MaxMana)
        {
            currMana = SaturatingAdd(currMana, SaturatingSub(maxMana, player.MaxMana));
        }
        else if (currMana > maxMana)
        {
            currMana = maxMana;
        }

        ctx.Db.Player.Identity.Update(player with
        {
            Strength = strength,
            Dexterity = dexterity,
            Intelligence = intelligence,
            Speed = speed,
            Atk = atk,
            MaxHealth = maxHealth,
            CurrHealth = currHealth,
            MaxMana = maxMana,
            CurrMana = currMana,
        });
    }

    private static uint GearStat(ItemDef? weapon, ItemDef? armor, StatType stat)
    {
        uint bonus = 0;
        if (weapon is ItemDef equippedWeapon)
        {
            bonus = SaturatingAdd(bonus, StatBonus(equippedWeapon, stat));
        }

        if (armor is ItemDef equippedArmor)
        {
            bonus = SaturatingAdd(bonus, StatBonus(equippedArmor, stat));
        }

        return bonus;
    }

    private static uint StatBonus(ItemDef item, StatType stat) => stat switch
    {
        StatType.Strength => item.StrengthBonus,
        StatType.Dexterity => item.DexterityBonus,
        StatType.Intelligence => item.IntelligenceBonus,
        StatType.Speed => item.SpeedBonus,
        _ => 0,
    };

    private static (uint Strength, uint Dexterity, uint Intelligence, uint Speed) RollStarterStats(Random rng, PlayerClass classChoice)
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

    private static void GrantClassSkills(ReducerContext ctx, Identity owner, PlayerClass classChoice, uint floor)
    {
        foreach (var skill in ctx.Db.SkillDef.Iter())
        {
            if (skill.IsEnemySkill || skill.Class != classChoice)
            {
                continue;
            }

            ctx.Db.PlayerSkill.Insert(new PlayerSkill
            {
                Id = 0,
                Owner = owner,
                SkillDefId = skill.Id,
                Unlocked = skill.UnlockFloor <= floor,
            });
        }
    }

    private static void UnlockClassSkills(ReducerContext ctx, Identity owner, PlayerClass classChoice, uint floor)
    {
        foreach (var owned in ctx.Db.PlayerSkill.Owner.Filter(owner).ToList())
        {
            if (ctx.Db.SkillDef.Id.Find(owned.SkillDefId) is not SkillDef skill)
            {
                continue;
            }

            if (!skill.IsEnemySkill && skill.Class == classChoice && skill.UnlockFloor <= floor && !owned.Unlocked)
            {
                ctx.Db.PlayerSkill.Id.Update(owned with { Unlocked = true });
            }
        }
    }

    private static bool OwnsUnlockedSkill(ReducerContext ctx, Identity owner, uint skillDefId)
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

    private static void RerollPlayer(ReducerContext ctx, Player player)
    {
        var classChoice = (PlayerClass)ctx.Rng.Next(0, 4);
        DeletePlayerOwnedRows(ctx, player.Identity);
        var rolled = RollStarterStats(ctx.Rng, classChoice);
        var maxHealth = ClassMaxHealth(classChoice);
        var maxMana = SaturatingAdd(ClassBaseMana(classChoice), rolled.Intelligence);
        ctx.Db.Player.Identity.Update(player with
        {
            Class = classChoice,
            Name = RandomPlayerName(ctx.Rng, classChoice),
            SpriteId = RollInclusive(ctx.Rng, 0, SpriteVariantCount - 1),
            Level = 1,
            Xp = 0,
            UnspentStatPoints = 0,
            MaxHealth = maxHealth,
            CurrHealth = maxHealth,
            MaxMana = maxMana,
            CurrMana = maxMana,
            Speed = rolled.Speed,
            Strength = rolled.Strength,
            Dexterity = rolled.Dexterity,
            Intelligence = rolled.Intelligence,
            BaseSpeed = rolled.Speed,
            BaseStrength = rolled.Strength,
            BaseDexterity = rolled.Dexterity,
            BaseIntelligence = rolled.Intelligence,
            Atk = 0,
            BaseDefense = 0,
            Defense = 0,
            StrengthBuff = 0,
            NextTurnStrengthBonus = 0,
            NextTurnSpeedOverride = 0,
            GoFirstNextRound = false,
            IsDefending = false,
            Alive = true,
            EquippedWeaponDefId = 0,
            EquippedArmorDefId = 0,
        });

        var weapon = RequireItemDefByName(ctx, StarterWeaponName(classChoice));
        var healthPotion = RequireItemDefByName(ctx, "Health Potion");
        GiveItem(ctx, player.Identity, weapon.Id, 1);
        GiveItem(ctx, player.Identity, healthPotion.Id, 1);
        EquipFromCatalog(ctx, player.Identity, weapon);
        GrantClassSkills(ctx, player.Identity, classChoice, 0);
    }

    private static void GiveItem(ReducerContext ctx, Identity owner, uint itemDefId, uint quantity)
    {
        ctx.Db.PlayerItem.Insert(new PlayerItem
        {
            Id = 0,
            Owner = owner,
            ItemDefId = itemDefId,
            Quantity = quantity,
        });
    }

    private static string StarterWeaponName(PlayerClass classChoice) => classChoice switch
    {
        PlayerClass.Warrior => "Starter Sword",
        PlayerClass.Archer => "Starter Bow",
        PlayerClass.Mage => "Starter Staff",
        PlayerClass.Rogue => "Starter Dagger",
        _ => "Starter Sword",
    };

    private static void DeletePlayerOwnedRows(ReducerContext ctx, Identity owner)
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

    private static void ClearEncounter(ReducerContext ctx)
    {
        foreach (var enemy in ctx.Db.Enemy.Iter().ToList())
        {
            foreach (var skill in ctx.Db.EnemySkill.EnemyId.Filter(enemy.Id).ToList())
            {
                ctx.Db.EnemySkill.Id.Delete(skill.Id);
            }

            ctx.Db.Enemy.Id.Delete(enemy.Id);
        }

        foreach (var row in ctx.Db.TurnOrder.Iter().ToList())
        {
            ctx.Db.TurnOrder.Id.Delete(row.Id);
        }

        foreach (var row in ctx.Db.CombatEvent.Iter().ToList())
        {
            ctx.Db.CombatEvent.Id.Delete(row.Id);
        }
    }

    private static void ResetRun(ReducerContext ctx, GameSession session)
    {
        ClearEncounter(ctx);
        ctx.Db.GameSession.Id.Update(session with
        {
            PlayerCount = session.PlayerCount,
            Phase = GamePhase.Waiting,
            Floor = 0,
            Biome = Biome.Forest,
            IsBossFloor = false,
            RoundNumber = 0,
            TurnNumber = 0,
            ActiveKind = CombatantKind.Player,
            ActiveCombatantId = 0,
        });
    }

    private static GameSession RequireSession(ReducerContext ctx)
    {
        if (ctx.Db.GameSession.Id.Find(SessionId) is GameSession session)
        {
            return session;
        }

        throw new Exception("Game session is missing.");
    }

    private static Player RequirePlayer(ReducerContext ctx)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is Player player)
        {
            return player;
        }

        throw new Exception("Not in the party.");
    }

    private static uint FindFreeSlot(ReducerContext ctx)
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

        throw new Exception("No free party slots.");
    }
}
