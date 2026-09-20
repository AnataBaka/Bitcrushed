using System;
using System.Collections.Generic;
using SpacetimeDB;

/// Player-class skill resolution. Catalog rows only store cost, targeting, and
/// unlock level; the named effects live here so each skill can keep its own rules.
public static partial class Module
{
    public static bool SkillTargetsFallenAlly(string skillName) =>
        skillName == SkillNames.Necromancy;

    public static bool SkillNeedsEnemyTarget(SkillDef skill) =>
        !skill.IsEnemySkill
        && skill.TargetCount > 0
        && !SkillTargetsFallenAlly(skill.Name);

    static void ExecutePlayerSkill(
        ReducerContext ctx,
        Entity caster,
        SkillDef skill,
        ulong targetEntityId
    )
    {
        switch (skill.Name)
        {
            case SkillNames.Bash:
                Strike(ctx, caster, targetEntityId, skill.Name, 5, isSkill: true);
                break;
            case SkillNames.Rush:
                Strike(ctx, caster, targetEntityId, skill.Name, 3, isSkill: true);
                QueueNextTurnSpeed(ctx, caster.EntityId, RushNextTurnSpeed);
                SetGoFirstNextRound(ctx, caster.EntityId, true);
                AddLog(
                    ctx,
                    $"{caster.Name} uses {skill.Name} and will act at infinite Speed next turn.",
                    LogKind.Focus,
                    caster.EntityId,
                    caster.EntityId
                );
                break;
            case SkillNames.Embolden:
                QueueStrength(ctx, caster.EntityId, 2);
                AddLog(
                    ctx,
                    $"{caster.Name} uses {skill.Name} and gains 2 Enraged next turn.",
                    LogKind.Focus,
                    caster.EntityId,
                    caster.EntityId
                );
                break;
            case SkillNames.GallantPride:
                QueueStrength(ctx, caster.EntityId, 4);
                QueueNextTurnSpeed(ctx, caster.EntityId, 1);
                AddLog(
                    ctx,
                    $"{caster.Name} uses {skill.Name}, gains 4 Enraged next turn, and will have Speed 1 next turn.",
                    LogKind.Focus,
                    caster.EntityId,
                    caster.EntityId
                );
                break;
            case SkillNames.Cleave:
                StrikeLivingEnemies(ctx, caster, skill.Name, 7, hits: 1);
                break;
            case SkillNames.Bludgeon:
                Strike(
                    ctx,
                    caster,
                    targetEntityId,
                    skill.Name,
                    30,
                    isSkill: true,
                    bludgeonFragile: true
                );
                break;
            case SkillNames.Terrify:
                foreach (var enemy in LivingMembers(ctx, Team.Enemies))
                {
                    QueueWeak(ctx, enemy.EntityId, 3);
                }

                AddLog(
                    ctx,
                    $"{caster.Name} uses {skill.Name} and inflicts 3 Weak on all enemies.",
                    LogKind.Focus,
                    caster.EntityId,
                    caster.EntityId
                );
                break;
            case SkillNames.TripleSlash:
                var enragedHits = 0;
                for (var hit = 0; hit < 3; hit++)
                {
                    if (Strike(ctx, caster, targetEntityId, skill.Name, 12, isSkill: true).Connected)
                    {
                        GainStrengthNow(ctx, caster.EntityId, 1);
                        enragedHits += 1;
                    }
                }

                if (enragedHits > 0)
                {
                    AddLog(
                        ctx,
                        $"{caster.Name} gains {enragedHits} Enraged from {skill.Name}.",
                        LogKind.Focus,
                        caster.EntityId,
                        caster.EntityId
                    );
                }

                break;
            case SkillNames.Furioso:
                var furiosoBonus = 0;
                for (var hit = 0; hit < FuriosoHitCount; hit++)
                {
                    var landed = Strike(
                        ctx,
                        caster,
                        targetEntityId,
                        skill.Name,
                        FuriosoBaseDamage + furiosoBonus,
                        isSkill: true
                    );
                    if (landed.Connected)
                    {
                        furiosoBonus += FuriosoBonusPerHit;
                    }
                }

                break;
            case SkillNames.Shoot:
                Strike(ctx, caster, targetEntityId, skill.Name, 4, isSkill: true);
                break;
            case SkillNames.Restring:
                QueueDodgeBonus(ctx, caster.EntityId, 15);
                QueueStrength(ctx, caster.EntityId, 4);
                AddLog(
                    ctx,
                    $"{caster.Name} uses {skill.Name}, gains +15% dodge and 4 Enraged next turn.",
                    LogKind.Focus,
                    caster.EntityId,
                    caster.EntityId
                );
                break;
            case SkillNames.Scheme:
                if (caster.UsedAttackLastTurn)
                {
                    RestoreMana(ctx, caster.EntityId, 20);
                    QueueStrength(ctx, caster.EntityId, 2);
                    AddLog(
                        ctx,
                        $"{caster.Name} uses {skill.Name}, recovers 20 MP, and gains 2 Enraged next turn.",
                        LogKind.Focus,
                        caster.EntityId,
                        caster.EntityId
                    );
                }
                else
                {
                    AddLog(
                        ctx,
                        $"{caster.Name} uses {skill.Name}, but no attack was used last turn.",
                        LogKind.Focus,
                        caster.EntityId,
                        caster.EntityId
                    );
                }

                break;
            case SkillNames.Evade:
                ArmEvade(ctx, caster.EntityId, EvadeDamageThreshold, fragileOnDodge: 4, strengthOnDodge: 0);
                AddLog(
                    ctx,
                    $"{caster.Name} uses {skill.Name} and will negate incoming hits below {EvadeDamageThreshold} damage.",
                    LogKind.Focus,
                    caster.EntityId,
                    caster.EntityId
                );
                break;
            case SkillNames.RainDown:
                StrikeLivingEnemies(ctx, caster, skill.Name, 4, hits: 3);
                break;
            case SkillNames.Snipe:
                if (Strike(ctx, caster, targetEntityId, skill.Name, SnipeDamage, isSkill: true).Connected)
                {
                    QueueFragile(ctx, targetEntityId, 4);
                }

                break;
            case SkillNames.CurvedShot:
                foreach (var enemy in LivingMembers(ctx, Team.Enemies))
                {
                    var connected = false;
                    for (var hit = 0; hit < 2; hit++)
                    {
                        if (Strike(ctx, caster, enemy.EntityId, skill.Name, 17, isSkill: true).Connected)
                        {
                            connected = true;
                        }
                    }

                    if (connected)
                    {
                        QueueFragile(ctx, enemy.EntityId, 4);
                    }
                }

                break;
            case SkillNames.Grandshot:
                var countedDodges = GrandshotCountedDodges(caster.DodgeCount);
                var grandshotDamage = GrandshotDamageOf(caster.DodgeCount);
                AddLog(
                    ctx,
                    $"{caster.Name} fires {skill.Name} with {countedDodges} dodge{(countedDodges == 1 ? "" : "s")} this battle (+{countedDodges * GrandshotDamagePerDodge} power).",
                    LogKind.Focus,
                    caster.EntityId,
                    caster.EntityId
                );
                Strike(ctx, caster, targetEntityId, skill.Name, grandshotDamage, isSkill: true);
                break;
            case SkillNames.MagicMissile:
                if (Strike(ctx, caster, targetEntityId, skill.Name, 10, isSkill: true).Connected)
                {
                    QueueFragile(ctx, targetEntityId, 2);
                }

                break;
            case SkillNames.Fireball:
                if (
                    Strike(
                        ctx,
                        caster,
                        targetEntityId,
                        skill.Name,
                        2 + BurnSpellPowerBonus(ctx, caster),
                        isSkill: true
                    ).Connected
                )
                {
                    ApplyBurn(ctx, targetEntityId, 5, 6, BurnCapOf(ctx, caster));
                }

                break;
            case SkillNames.Concentrate:
                RestoreMana(ctx, caster.EntityId, 20);
                QueueNextAttackBonus(ctx, caster.EntityId, 2);
                AddLog(
                    ctx,
                    $"{caster.Name} uses {skill.Name}, recovers 20 MP, and the next attack deals +2 damage.",
                    LogKind.Focus,
                    caster.EntityId,
                    caster.EntityId
                );
                break;
            case SkillNames.Pray:
                HealParty(ctx, caster, 20, 20);
                break;
            case SkillNames.MagicBullet:
                ExecuteMagicBullet(ctx, caster, targetEntityId);
                break;
            case SkillNames.GrandUndertaking:
                BeginGrandUndertaking(ctx, caster);
                break;
            case SkillNames.Necromancy:
                ReviveAlly(ctx, caster, targetEntityId);
                break;
            case SkillNames.Spear:
                Strike(ctx, caster, targetEntityId, skill.Name, 12, isSkill: true);
                DiscountNinjaSkill(ctx, caster.EntityId, SkillNames.Spear);
                break;
            case SkillNames.VerticalCut:
                Strike(ctx, caster, targetEntityId, skill.Name, 27, isSkill: true);
                DiscountNinjaSkill(ctx, caster.EntityId, SkillNames.VerticalCut);
                break;
            case SkillNames.FocusSpirit:
                ArmEvade(ctx, caster.EntityId, EvadeDamageThreshold, fragileOnDodge: 0, strengthOnDodge: 5);
                AddLog(
                    ctx,
                    $"{caster.Name} uses {skill.Name} and will negate incoming hits below {EvadeDamageThreshold} damage, gaining 5 Enraged on dodge.",
                    LogKind.Focus,
                    caster.EntityId,
                    caster.EntityId
                );
                break;
            case SkillNames.FinishTheJob:
                EnterFinishTheJob(ctx, caster);
                break;
            case SkillNames.Overthrow:
                GainStrengthNow(ctx, caster.EntityId, OverthrowEnragedStacks);
                AddLog(
                    ctx,
                    $"{caster.Name} uses {skill.Name} and applies {OverthrowEnragedStacks} Enraged to this attack.",
                    LogKind.Focus,
                    caster.EntityId,
                    caster.EntityId
                );
                foreach (var enemy in LivingMembers(ctx, Team.Enemies))
                {
                    Strike(
                        ctx,
                        caster,
                        enemy.EntityId,
                        skill.Name,
                        OverthrowDamage,
                        isSkill: true
                    );
                    QueueWeak(ctx, enemy.EntityId, OverthrowWeakStacks);
                    QueueFragile(ctx, enemy.EntityId, OverthrowFragileStacks);
                }

                AddLog(
                    ctx,
                    $"{caster.Name} inflicts {OverthrowWeakStacks} Weak and {OverthrowFragileStacks} Fragile on all enemies next turn.",
                    LogKind.Focus,
                    caster.EntityId,
                    caster.EntityId
                );
                break;
            default:
                throw new Exception($"Unhandled skill {skill.Name}.");
        }
    }

    static void ValidatePlayerSkill(
        ReducerContext ctx,
        Entity caster,
        SkillDef skill,
        ulong targetEntityId
    )
    {
        if (skill.Name == SkillNames.Grandshot && caster.DodgeCount < 1)
        {
            throw new Exception("Grandshot requires having dodged at least once.");
        }

        if (skill.Name == SkillNames.FinishTheJob)
        {
            if (caster.FinishTheJobUsed)
            {
                throw new Exception("Finish the Job can only be used once per battle.");
            }

            if (RequireSession(ctx).Round < FinishTheJobTurnRequirement)
            {
                throw new Exception("Finish the Job requires 4 turns to have passed.");
            }
        }

        if (skill.Name == SkillNames.Overthrow && !caster.FinishTheJobStance)
        {
            throw new Exception("Overthrow is only usable after Finish the Job.");
        }

        if (skill.Name == SkillNames.Necromancy)
        {
            if (caster.NecromancyUsed)
            {
                throw new Exception("Necromancy can only be used once.");
            }

            RequireFallenAlly(ctx, caster, targetEntityId);
            return;
        }

        if (SkillNeedsEnemyTarget(skill))
        {
            RequireEnemyOf(ctx, caster, targetEntityId);
        }
    }

    static bool IsDamagingSkill(string name) =>
        name
            is SkillNames.Bash
                or SkillNames.Rush
                or SkillNames.Cleave
                or SkillNames.Bludgeon
                or SkillNames.TripleSlash
                or SkillNames.Furioso
                or SkillNames.Shoot
                or SkillNames.RainDown
                or SkillNames.Snipe
                or SkillNames.CurvedShot
                or SkillNames.Grandshot
                or SkillNames.MagicMissile
                or SkillNames.Fireball
                or SkillNames.MagicBullet
                or SkillNames.Spear
                or SkillNames.VerticalCut
                or SkillNames.Overthrow;

    static HitResult Strike(
        ReducerContext ctx,
        Entity caster,
        ulong targetEntityId,
        string actionName,
        int skillBaseDamage,
        bool isSkill,
        bool bludgeonFragile = false
    )
    {
        var attacker = ctx.Db.Entity.EntityId.Find(caster.EntityId) ?? caster;
        if (ctx.Db.Entity.EntityId.Find(targetEntityId) is not Entity target || !target.Alive)
        {
            return default;
        }

        return ResolveHit(
            ctx,
            attacker,
            target,
            actionName,
            skillBaseDamage,
            isSkill: isSkill,
            bludgeonFragile: bludgeonFragile
        );
    }

    static void StrikeLivingEnemies(
        ReducerContext ctx,
        Entity caster,
        string actionName,
        int skillBaseDamage,
        int hits
    )
    {
        foreach (var enemy in LivingMembers(ctx, Team.Enemies))
        {
            for (var hit = 0; hit < hits; hit++)
            {
                Strike(ctx, caster, enemy.EntityId, actionName, skillBaseDamage, isSkill: true);
            }
        }
    }

    static void ExecuteMagicBullet(ReducerContext ctx, Entity caster, ulong targetEntityId)
    {
        var stage = MagicBulletStageOf(caster);
        var def = MagicBulletStageDefOf(stage);
        if (!def.AllEnemies)
        {
            RequireEnemyOf(ctx, caster, targetEntityId);
        }

        var targets = def.AllEnemies
            ? LivingMembers(ctx, Team.Enemies)
            : new List<Entity> { RequireEnemyOf(ctx, caster, targetEntityId) };

        foreach (var target in targets)
        {
            var connected = false;
            for (var hit = 0; hit < def.Hits; hit++)
            {
                var bulletPower = def.Damage + (def.BurnStack > 0 ? BurnSpellPowerBonus(ctx, caster) : 0);
                if (Strike(ctx, caster, target.EntityId, SkillNames.MagicBullet, bulletPower, isSkill: true)
                    .Connected)
                {
                    connected = true;
                }
            }

            if (!connected)
            {
                continue;
            }

            if (def.BurnStack > 0)
            {
                ApplyBurn(ctx, target.EntityId, def.BurnStack, def.BurnCount, BurnCapOf(ctx, caster));
            }

            if (def.SpeedDelta != 0 && ctx.Db.Entity.EntityId.Find(target.EntityId) is Entity living)
            {
                ctx.Db.Entity.EntityId.Update(
                    living with { NextTurnSpeedDelta = living.NextTurnSpeedDelta + def.SpeedDelta }
                );
            }
        }

        var fresh = ctx.Db.Entity.EntityId.Find(caster.EntityId) ?? caster;
        if (def.KillsCaster)
        {
            KillEntity(ctx, fresh, $"{fresh.Name} is consumed by Magic Bullet VII!");
            return;
        }

        var next = Math.Min(stage + 1, MagicBulletStageCount);
        ctx.Db.Entity.EntityId.Update(fresh with { MagicBulletStage = next });
        AddLog(
            ctx,
            $"{fresh.Name}'s Magic Bullet advances to stage {ToRoman(next)}.",
            LogKind.Focus,
            fresh.EntityId,
            fresh.EntityId
        );
    }

    static void BeginGrandUndertaking(ReducerContext ctx, Entity caster)
    {
        var session = RequireSession(ctx);
        ctx.Db.GameSession.Id.Update(session with { AttackRedirectEntityId = caster.EntityId });
        if (ctx.Db.Entity.EntityId.Find(caster.EntityId) is Entity fresh)
        {
            ctx.Db.Entity.EntityId.Update(fresh with { GrandUndertakingPending = true });
        }

        AddLog(
            ctx,
            $"{caster.Name} begins a Grand Undertaking. All enemy attacks this turn are redirected!",
            LogKind.Focus,
            caster.EntityId,
            caster.EntityId
        );
    }

    static void ResolveGrandUndertaking(ReducerContext ctx, Entity mage)
    {
        ctx.Db.Entity.EntityId.Update(
            mage with { GrandUndertakingPending = false, SkipNextTurn = true }
        );

        AddLog(
            ctx,
            $"{mage.Name}'s Grand Undertaking erupts!",
            LogKind.Attack,
            mage.EntityId,
            mage.EntityId
        );

        foreach (var enemy in LivingMembers(ctx, Team.Enemies))
        {
            ApplyPercentMaxHpDamage(ctx, mage, enemy, GrandUndertakingEnemyHpBps, "Grand Undertaking");
        }

        foreach (var ally in LivingMembers(ctx, Team.Players))
        {
            ApplyPercentMaxHpDamage(ctx, mage, ally, GrandUndertakingAllyHpBps, "Grand Undertaking");
        }

        var session = RequireSession(ctx);
        if (session.AttackRedirectEntityId == mage.EntityId)
        {
            ctx.Db.GameSession.Id.Update(session with { AttackRedirectEntityId = 0 });
        }
    }

    static void ReviveAlly(ReducerContext ctx, Entity caster, ulong targetEntityId)
    {
        var target = RequireFallenAlly(ctx, caster, targetEntityId);
        var hp = Math.Max(1, ScaleByBps(target.MaxHp, NecromancyReviveHpBps));
        var revived = target with
        {
            Hp = Math.Min(target.MaxHp, hp),
            Alive = true,
            MagicBulletStage = 1,
        };
        ctx.Db.Entity.EntityId.Update(revived);

        if (ctx.Db.Entity.EntityId.Find(caster.EntityId) is Entity freshCaster)
        {
            ctx.Db.Entity.EntityId.Update(freshCaster with { NecromancyUsed = true });
        }

        AddLog(
            ctx,
            $"{caster.Name} uses Necromancy and revives {target.Name} with {revived.Hp} HP.",
            LogKind.Heal,
            caster.EntityId,
            target.EntityId,
            healing: revived.Hp
        );
    }

    static void EnterFinishTheJob(ReducerContext ctx, Entity caster)
    {
        if (ctx.Db.Entity.EntityId.Find(caster.EntityId) is not Entity fresh)
        {
            return;
        }

        ctx.Db.Entity.EntityId.Update(
            fresh with
            {
                FinishTheJobUsed = true,
                FinishTheJobStance = true,
                FinishTheJobPower = 0,
                NextTurnStrengthBonus = fresh.NextTurnStrengthBonus + 6,
            }
        );

        if (TryPlayerIdentity(ctx, caster.EntityId, out var owner))
        {
            RecomputeStats(ctx, owner);
        }
        else if (ctx.Db.Entity.EntityId.Find(caster.EntityId) is Entity stance)
        {
            ctx.Db.Entity.EntityId.Update(stance with { Atk = stance.Atk + 6 });
        }

        AddLog(
            ctx,
            $"{caster.Name} finishes the job, gaining 6 Enraged next turn and entering a battle-long stance.",
            LogKind.Focus,
            caster.EntityId,
            caster.EntityId
        );
    }

    static void HealParty(ReducerContext ctx, Entity caster, int hpAmount, int manaAmount)
    {
        AddLog(
            ctx,
            $"{caster.Name} uses Pray.",
            LogKind.Heal,
            caster.EntityId,
            caster.EntityId
        );

        foreach (var ally in LivingMembers(ctx, Team.Players))
        {
            var hp = Math.Min(ally.MaxHp, ally.Hp + hpAmount);
            var mana = Math.Min(ally.MaxMana, ally.Mana + manaAmount);
            var healed = hp - ally.Hp;
            var restored = mana - ally.Mana;
            ctx.Db.Entity.EntityId.Update(ally with { Hp = hp, Mana = mana });
            AddLog(
                ctx,
                $"{ally.Name} recovers {healed} HP and {restored} MP.",
                LogKind.Heal,
                caster.EntityId,
                ally.EntityId,
                healing: Math.Max(healed, restored)
            );
        }
    }

    static void DiscountNinjaSkill(ReducerContext ctx, ulong entityId, string skillName)
    {
        if (ctx.Db.Entity.EntityId.Find(entityId) is not Entity entity)
        {
            return;
        }

        if (skillName == SkillNames.Spear)
        {
            var next = entity.SpearDiscount + SkillManaDiscountPerUse;
            ctx.Db.Entity.EntityId.Update(entity with { SpearDiscount = next });
            AddLog(
                ctx,
                $"{entity.Name}'s Spear now costs {Math.Max(0, SpearBaseManaCost - next)} MP.",
                LogKind.Focus,
                entity.EntityId,
                entity.EntityId
            );
            return;
        }

        var vertical = entity.VerticalCutDiscount + SkillManaDiscountPerUse;
        ctx.Db.Entity.EntityId.Update(entity with { VerticalCutDiscount = vertical });
        AddLog(
            ctx,
            $"{entity.Name}'s Vertical Cut now costs {Math.Max(0, VerticalCutBaseManaCost - vertical)} MP.",
            LogKind.Focus,
            entity.EntityId,
            entity.EntityId
        );
    }

    static void QueueStrength(ReducerContext ctx, ulong entityId, int stacks)
    {
        if (ctx.Db.Entity.EntityId.Find(entityId) is Entity entity)
        {
            ctx.Db.Entity.EntityId.Update(
                entity with { NextTurnStrengthBonus = entity.NextTurnStrengthBonus + stacks }
            );
        }
    }

    static void GainStrengthNow(ReducerContext ctx, ulong entityId, int stacks)
    {
        if (ctx.Db.Entity.EntityId.Find(entityId) is Entity entity)
        {
            ctx.Db.Entity.EntityId.Update(
                entity with { StrengthBuff = entity.StrengthBuff + stacks }
            );
        }
    }

    static void QueueWeak(ReducerContext ctx, ulong entityId, int stacks)
    {
        if (ctx.Db.Entity.EntityId.Find(entityId) is Entity entity)
        {
            ctx.Db.Entity.EntityId.Update(entity with { NextTurnWeak = entity.NextTurnWeak + stacks });
        }
    }

    static void QueueFragile(ReducerContext ctx, ulong entityId, int stacks)
    {
        if (ctx.Db.Entity.EntityId.Find(entityId) is Entity entity)
        {
            ctx.Db.Entity.EntityId.Update(
                entity with { NextTurnFragile = entity.NextTurnFragile + stacks }
            );
        }
    }

    static void QueueDodgeBonus(ReducerContext ctx, ulong entityId, int percent)
    {
        if (ctx.Db.Entity.EntityId.Find(entityId) is Entity entity)
        {
            ctx.Db.Entity.EntityId.Update(
                entity with { NextTurnDodgeBonus = entity.NextTurnDodgeBonus + percent }
            );
        }
    }

    static void QueueNextTurnSpeed(ReducerContext ctx, ulong entityId, int speed)
    {
        if (ctx.Db.Entity.EntityId.Find(entityId) is Entity entity)
        {
            ctx.Db.Entity.EntityId.Update(entity with { NextTurnSpeedSet = speed });
        }
    }

    static void SetGoFirstNextRound(ReducerContext ctx, ulong entityId, bool value)
    {
        if (ctx.Db.Entity.EntityId.Find(entityId) is Entity entity)
        {
            ctx.Db.Entity.EntityId.Update(entity with { GoFirstNextRound = value });
        }
    }

    static void QueueNextAttackBonus(ReducerContext ctx, ulong entityId, int bonus)
    {
        if (ctx.Db.Entity.EntityId.Find(entityId) is Entity entity)
        {
            ctx.Db.Entity.EntityId.Update(
                entity with { NextAttackBonus = entity.NextAttackBonus + bonus }
            );
        }
    }

    static void ArmEvade(
        ReducerContext ctx,
        ulong entityId,
        int threshold,
        int fragileOnDodge,
        int strengthOnDodge
    )
    {
        if (ctx.Db.Entity.EntityId.Find(entityId) is Entity entity)
        {
            ctx.Db.Entity.EntityId.Update(
                entity with
                {
                    EvadeThreshold = threshold,
                    EvadeFragileOnDodge = fragileOnDodge,
                    EvadeStrengthOnDodge = strengthOnDodge,
                }
            );
        }
    }

    static void ApplyBurn(ReducerContext ctx, ulong entityId, int stack, int count, int cap = BurnStackCap)
    {
        if (ctx.Db.Entity.EntityId.Find(entityId) is not Entity entity || !entity.Alive)
        {
            return;
        }

        var burnCap = cap < BurnStackCap ? BurnStackCap : cap;
        var nextStack = Math.Min(burnCap, entity.BurnStack + Math.Max(0, stack));
        var nextCount = entity.BurnCount + Math.Max(0, count);
        ctx.Db.Entity.EntityId.Update(entity with { BurnStack = nextStack, BurnCount = nextCount });
        var updated = ctx.Db.Entity.EntityId.Find(entityId) ?? entity;
        var capped = entity.BurnStack + stack > burnCap;
        AddLog(
            ctx,
            capped
                ? $"{updated.Name} is burned (stack {updated.BurnStack} capped, {updated.BurnCount} turns)."
                : $"{updated.Name} is burned (stack {updated.BurnStack}, {updated.BurnCount} turns).",
            LogKind.Attack,
            0,
            updated.EntityId
        );
    }

    static void RestoreMana(ReducerContext ctx, ulong entityId, int amount)
    {
        if (ctx.Db.Entity.EntityId.Find(entityId) is not Entity entity)
        {
            return;
        }

        ctx.Db.Entity.EntityId.Update(
            entity with { Mana = Math.Min(entity.MaxMana, entity.Mana + amount) }
        );
    }

    static Entity RequireFallenAlly(ReducerContext ctx, Entity caster, ulong targetEntityId)
    {
        if (ctx.Db.Entity.EntityId.Find(targetEntityId) is not Entity target)
        {
            throw new Exception("That target does not exist.");
        }

        if (target.Faction != caster.Faction)
        {
            throw new Exception("Necromancy can only revive an ally.");
        }

        if (target.Alive)
        {
            throw new Exception("That ally is still standing.");
        }

        if (target.EntityId == caster.EntityId)
        {
            throw new Exception("Necromancy cannot revive its caster.");
        }

        return target;
    }

    static bool TryPlayerIdentity(ReducerContext ctx, ulong entityId, out Identity identity)
    {
        foreach (var player in ctx.Db.Player.Iter())
        {
            if (player.EntityId == entityId)
            {
                identity = player.Identity;
                return true;
            }
        }

        identity = default;
        return false;
    }

    static string ToRoman(int value) =>
        value switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            5 => "V",
            6 => "VI",
            7 => "VII",
            _ => value.ToString(),
        };
}
