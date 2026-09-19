using System;
using System.Collections.Generic;
using SpacetimeDB;

/// Named amulet passives. Catalog rows hold the stat sticks; everything that
/// reacts to combat events is resolved here from the equipped amulet name.
public static partial class Module
{
    public static bool HasAmulet(ReducerContext ctx, Entity entity, string amuletName)
    {
        var equipped = EquippedAmuletName(ctx, entity);
        return equipped != null && equipped == amuletName;
    }

    public static string? EquippedAmuletName(ReducerContext ctx, Entity entity)
    {
        if (entity.Faction != Team.Players || !TryPlayerIdentity(ctx, entity.EntityId, out var owner))
        {
            return null;
        }

        foreach (var item in ctx.Db.PlayerItem.Owner.Filter(owner))
        {
            if (item.EquippedSlot != EquipSlot.Amulet)
            {
                continue;
            }

            if (ctx.Db.ItemDef.Id.Find(item.ItemDefId) is ItemDef def)
            {
                return def.Name;
            }
        }

        return null;
    }

    public static bool OwnsItemNamed(ReducerContext ctx, Identity owner, string name)
    {
        var defId = FindItemDefId(ctx, name);
        if (defId == 0)
        {
            return false;
        }

        foreach (var item in ctx.Db.PlayerItem.Owner.Filter(owner))
        {
            if (item.ItemDefId == defId)
            {
                return true;
            }
        }

        return false;
    }

    static uint FindItemDefId(ReducerContext ctx, string name)
    {
        foreach (var item in ctx.Db.ItemDef.Iter())
        {
            if (item.Name == name)
            {
                return item.Id;
            }
        }

        return 0;
    }

    public static int EquippedDodgeBonusPercent(ReducerContext ctx, Entity entity) =>
        HasAmulet(ctx, entity, AmuletNames.HiddenDreamcatcher) ? HiddenDreamcatcherDodgePercent : 0;

    public static bool HasEmeraldPendant(ReducerContext ctx, Entity entity) =>
        HasAmulet(ctx, entity, AmuletNames.EmeraldPendant)
        && ClassOf(ctx, entity) == PlayerClass.Mage;

    public static int BurnSpellPowerBonus(ReducerContext ctx, Entity caster) =>
        HasAmulet(ctx, caster, AmuletNames.RubyScepter) ? RubyScepterBurnPower : 0;

    public static int BurnCapOf(ReducerContext ctx, Entity caster) =>
        BurnCapFor(HasAmulet(ctx, caster, AmuletNames.DragonsFire));

    static void ApplyAmuletOnConnectedHit(
        ReducerContext ctx,
        Entity attacker,
        Entity target,
        int damage
    )
    {
        if (damage > 0 && HasAmulet(ctx, attacker, AmuletNames.CountessNecklace))
        {
            HealCombatant(ctx, attacker.EntityId, CountessNecklaceHeal, "Countess' Necklace");
        }

        if (HasAmulet(ctx, attacker, AmuletNames.RootedBlade))
        {
            if (ctx.Db.Entity.EntityId.Find(target.EntityId) is Entity living && living.Alive)
            {
                ctx.Db.Entity.EntityId.Update(
                    living with
                    {
                        NextTurnSpeedDelta = living.NextTurnSpeedDelta - RootedBladeSpeedPenalty,
                    }
                );
            }
        }
    }

    static void ApplyAmuletOnDamageTaken(
        ReducerContext ctx,
        Entity attacker,
        Entity target,
        int damage
    )
    {
        if (damage <= 0 || attacker.EntityId == target.EntityId || attacker.Faction == target.Faction)
        {
            return;
        }

        if (!HasAmulet(ctx, target, AmuletNames.RedCocoon))
        {
            return;
        }

        DealFlatReflect(ctx, target, attacker, RedCocoonReflect);
    }

    static void ApplyAmuletOnAllyDefeat(ReducerContext ctx, Entity fallen)
    {
        if (fallen.Faction != Team.Players)
        {
            return;
        }

        foreach (var ally in ctx.Db.Entity.Iter().ToList())
        {
            if (
                ally.Faction != Team.Players
                || !ally.Alive
                || ally.EntityId == fallen.EntityId
                || !HasAmulet(ctx, ally, AmuletNames.DragonflyCharm)
            )
            {
                continue;
            }

            ctx.Db.Entity.EntityId.Update(ally with { NextTurnDoubleStrength = true });
            AddLog(
                ctx,
                $"{ally.Name}'s Dragonfly Charm will double Strength next turn.",
                LogKind.Focus,
                ally.EntityId,
                ally.EntityId
            );
        }
    }

    static void ApplyAmuletOnKill(ReducerContext ctx, Entity attacker)
    {
        if (!HasAmulet(ctx, attacker, AmuletNames.HolyGrail))
        {
            return;
        }

        HealCombatant(ctx, attacker.EntityId, HolyGrailHeal, "Holy Grail");
    }

    static void HealCombatant(ReducerContext ctx, ulong entityId, int amount, string source)
    {
        if (amount <= 0 || ctx.Db.Entity.EntityId.Find(entityId) is not Entity entity || !entity.Alive)
        {
            return;
        }

        var hp = Math.Min(entity.MaxHp, entity.Hp + amount);
        var healed = hp - entity.Hp;
        if (healed <= 0)
        {
            return;
        }

        ctx.Db.Entity.EntityId.Update(entity with { Hp = hp });
        AddLog(
            ctx,
            $"{entity.Name} recovers {healed} HP from {source}.",
            LogKind.Heal,
            entity.EntityId,
            entity.EntityId,
            healing: healed
        );
    }

    static void DealFlatReflect(ReducerContext ctx, Entity source, Entity victim, int damage)
    {
        victim = ctx.Db.Entity.EntityId.Find(victim.EntityId) ?? victim;
        if (!victim.Alive || damage <= 0)
        {
            return;
        }

        var hp = Math.Max(0, victim.Hp - damage);
        var alive = hp > 0;
        var wasAlive = victim.Alive;
        ctx.Db.Entity.EntityId.Update(victim with { Hp = hp, Alive = alive });
        AddLog(
            ctx,
            $"{source.Name}'s Red Cocoon deals {damage} damage to {victim.Name}.",
            LogKind.Attack,
            source.EntityId,
            victim.EntityId,
            damage
        );

        if (wasAlive && !alive)
        {
            HandleDefeat(ctx, source, victim);
        }
    }

    public static void GiveRandomStartingAmulet(ReducerContext ctx, Identity owner)
    {
        var names = new List<string>(AllAmuletNames);
        if (names.Count == 0)
        {
            return;
        }

        var pick = names[ctx.Rng.Next(0, names.Count)];
        EquipFresh(ctx, owner, RequireItem(ctx, pick));
    }

    public static void GrantRandomUnownedAmulet(ReducerContext ctx, Identity owner, string ownerName)
    {
        var missing = new List<string>();
        foreach (var name in AllAmuletNames)
        {
            if (!OwnsItemNamed(ctx, owner, name))
            {
                missing.Add(name);
            }
        }

        if (missing.Count == 0 || BagCount(ctx, owner) >= BagCapacity)
        {
            return;
        }

        var pick = missing[ctx.Rng.Next(0, missing.Count)];
        GiveToBag(ctx, owner, RequireItem(ctx, pick).Id, 1);
        AddLog(ctx, $"{ownerName} found {pick}.");
    }

    public static void GrantMissingAmulets(ReducerContext ctx, Identity owner)
    {
        foreach (var name in AllAmuletNames)
        {
            if (OwnsItemNamed(ctx, owner, name))
            {
                continue;
            }

            if (BagCount(ctx, owner) >= BagCapacity)
            {
                return;
            }

            GiveToBag(ctx, owner, RequireItem(ctx, name).Id, 1);
        }
    }
}
