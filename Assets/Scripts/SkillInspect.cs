using SpacetimeDB.Types;

/// Client-side inspect copy for the skill menu. Mirrors named skill rules so
/// players can read damage, cost, and effects without opening a design doc.
public static class SkillInspect
{
    public readonly struct Info
    {
        public Info(string title, string mana, string damage, string description, string note)
        {
            Title = title;
            Mana = mana;
            Damage = damage;
            Description = description;
            Note = note;
        }

        public string Title { get; }
        public string Mana { get; }
        public string Damage { get; }
        public string Description { get; }
        public string Note { get; }
    }

    public static Info ForBasicAttack(Entity caster)
    {
        var name = caster != null && !string.IsNullOrEmpty(caster.BasicAttackName)
            ? caster.BasicAttackName
            : "Basic Attack";
        return new Info(
            name,
            "Free",
            "Weapon ATK to one enemy",
            "A free weapon swing. Adds class passives (Knight +0.5 per STR, Archer +0.2 per DEX). Mage basic attacks are not spells.",
            ""
        );
    }

    public static Info ForSkill(SkillDef skill, Entity caster, GameSession session)
    {
        if (skill == null)
        {
            return ForBasicAttack(caster);
        }

        var cost = GameManager.EffectiveManaCost(skill, caster);
        var mana = cost <= 0 ? "Free" : $"{cost} MP";
        var ready = GameManager.SkillReadyToCast(skill, caster, session);
        var note = ready ? "" : LockNote(skill, caster, session);
        Describe(skill.Name, caster, out var damage, out var description);
        return new Info(TitleOnly(skill, caster), mana, damage, description, note);
    }

    static string TitleOnly(SkillDef skill, Entity caster)
    {
        if (skill.Name == "Magic Bullet" && caster != null)
        {
            var stage = caster.MagicBulletStage < 1 ? 1 : caster.MagicBulletStage;
            if (stage > 7)
            {
                stage = 7;
            }

            return $"Magic Bullet {GameManager.ToRoman(stage)}";
        }

        return skill.Name;
    }

    static string LockNote(SkillDef skill, Entity caster, GameSession session)
    {
        if (skill.Name == "Overthrow")
        {
            return "Locked until Finish the Job is activated.";
        }

        if (skill.Name == "Grandshot")
        {
            return "Locked until you have dodged at least once this battle.";
        }

        if (skill.Name == "Finish the Job")
        {
            if (caster != null && caster.FinishTheJobUsed)
            {
                return "Already used this battle.";
            }

            var round = session == null ? 0 : session.Round;
            return $"Locked until round 8 (now round {round}).";
        }

        if (skill.Name == "Necromancy")
        {
            return "Already used this battle.";
        }

        return "Currently unusable.";
    }

    static void Describe(string name, Entity caster, out string damage, out string description)
    {
        switch (name)
        {
            case "Bash":
                damage = "5 to one enemy";
                description = "A single physical strike against one enemy.";
                return;
            case "Rush":
                damage = "3 to one enemy";
                description = "Strike one enemy, then act first next turn at infinite Speed.";
                return;
            case "Embolden":
                damage = "None";
                description = "Gain 2 Enraged next turn. Each Enraged stack adds +1 damage to your attacks that turn.";
                return;
            case "Gallant Pride":
                damage = "None";
                description = "Gain 4 Enraged next turn, but your Speed becomes 1 next turn.";
                return;
            case "Cleave":
                damage = "7 to all enemies";
                description = "Hit every living enemy once.";
                return;
            case "Bludgeon":
                damage = "30 to one enemy";
                description = "Heavy single-target hit. If the target has Fragile, this deals 1.5× damage instead of the usual Fragile bonus.";
                return;
            case "Terrify":
                damage = "None";
                description = "Inflict 3 Weak on all enemies. Weak reduces damage they deal by 10% per stack.";
                return;
            case "Triple Slash":
                damage = "3 hits of 12 to one enemy";
                description = "Three strikes on one enemy. Each hit that connects grants 1 Enraged immediately.";
                return;
            case "Furioso":
                damage = "9 hits, starting at 5";
                description = "Nine strikes on one enemy. The first hit deals 5; each connecting hit adds +9 damage to the remaining hits.";
                return;
            case "Shoot":
                damage = "4 to one enemy";
                description = "A single shot at one enemy.";
                return;
            case "Restring":
                damage = "None";
                description = "Gain +15% dodge chance next turn.";
                return;
            case "Scheme":
                damage = "None";
                description = "If you used an attack last turn, recover 20 MP and gain 2 Enraged next turn. Does nothing otherwise.";
                return;
            case "Evade":
                damage = "None";
                description = "This turn, incoming hits below 20 damage are negated. Dodging inflicts 4 Fragile on the attacker.";
                return;
            case "Rain Down":
                damage = "3 hits of 4 to all enemies";
                description = "Rain arrows on every living enemy, three hits each.";
                return;
            case "Snipe":
                damage = "30 to one enemy";
                description = "A heavy shot. If it hits, inflict 4 Fragile on the target.";
                return;
            case "Curved Shot":
                damage = "2 hits of 17 to all enemies";
                description = "Two shots against every living enemy. If any hit connects on an enemy, that enemy gains 4 Fragile.";
                return;
            case "Grandshot":
                damage = "30 + 6 per heads (9 coins)";
                description = "Flip 9 coins, then fire one shot dealing 30 plus 6 per heads (30–84). Requires having dodged at least once this battle.";
                return;
            case "Magic Missile":
                damage = "10 to one enemy";
                description = "A spell bolt. If it hits, inflict 2 Fragile. Gains Mage spell damage (+0.2 per INT).";
                return;
            case "Fireball":
                damage = "2 to one enemy";
                description = "A weak spell hit. If it connects, apply Burn 5 for 6 ticks. Gains Mage spell damage (+0.2 per INT).";
                return;
            case "Concentrate":
                damage = "None";
                description = "Recover 20 MP. Your next attack deals +2 damage.";
                return;
            case "Pray":
                damage = "None";
                description = "Heal the whole living party for 20 HP and restore 20 MP each.";
                return;
            case "Magic Bullet":
                DescribeMagicBullet(caster, out damage, out description);
                return;
            case "Grand Undertaking":
                damage = "50% enemy max HP / 10% ally max HP";
                description = "Redirect all enemy attacks to you this turn. At the start of the next round, hit all enemies for 50% of their max HP and all allies for 10% of theirs, then skip your following turn.";
                return;
            case "Necromancy":
                damage = "None";
                description = "Revive one fallen ally at 15% of their max HP. Once per battle. Target a defeated ally.";
                return;
            case "Spear":
                damage = "12 to one enemy";
                description = "A single-target skill. Each use discounts Spear's MP cost by 15 (floor 0). Ninja skills also gain +1 power per Speed above the target (max +5).";
                return;
            case "Vertical Cut":
                damage = "27 to one enemy";
                description = "A heavy single-target skill. Each use discounts Vertical Cut's MP cost by 15 (floor 0). Ninja skills also gain +1 power per Speed above the target (max +5).";
                return;
            case "Focus Spirit":
                damage = "None";
                description = "This turn, incoming hits below 20 damage are negated. Dodging grants 5 Enraged next turn.";
                return;
            case "Finish the Job":
                damage = "None";
                description = "Once per battle, after round 8. Gain 6 Enraged next turn, +6 ATK, and a battle-long stance that adds growing bonus power to your skill hits. Unlocks Overthrow.";
                return;
            case "Overthrow":
                damage = "42 to all enemies";
                description = "Hit every living enemy. Requires Finish the Job stance. Ninja skills also gain +1 power per Speed above the target (max +5).";
                return;
            default:
                damage = "See battle log";
                description = "No inspect text is recorded for this skill.";
                return;
        }
    }

    static void DescribeMagicBullet(Entity caster, out string damage, out string description)
    {
        var stage = caster == null || caster.MagicBulletStage < 1 ? 1 : caster.MagicBulletStage;
        if (stage > 7)
        {
            stage = 7;
        }

        switch (stage)
        {
            case 2:
                damage = "3 to all enemies";
                description = "Stage II. Hits every enemy. Applies Burn 1 for 3 ticks and −1 Speed next turn. Advances to III.";
                return;
            case 3:
                damage = "5 to all enemies";
                description = "Stage III. Hits every enemy. Applies Burn 3 for 2 ticks. Advances to IV.";
                return;
            case 4:
                damage = "7 to all enemies";
                description = "Stage IV. Hits every enemy. Applies Burn 3 for 2 ticks and −1 Speed next turn. Advances to V.";
                return;
            case 5:
                damage = "8 to all enemies";
                description = "Stage V. Hits every enemy. Applies Burn 4 for 2 ticks. Advances to VI.";
                return;
            case 6:
                damage = "8 hits of 5 to one enemy";
                description = "Stage VI. Eight hits on one enemy. Applies Burn 10 for 2 ticks if a hit connects. Advances to VII.";
                return;
            case 7:
                damage = "90 to all enemies";
                description = "Stage VII. Hits every enemy for 90, then the caster dies. Does not advance further.";
                return;
            default:
                damage = "5 to one enemy";
                description = "Stage I. One bolt. Applies Burn 2 for 2 ticks if it hits. Advances to II. Gains Mage spell damage (+0.2 per INT).";
                return;
        }
    }
}
