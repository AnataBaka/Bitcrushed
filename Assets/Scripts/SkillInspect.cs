using System;
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
            ScaledHit(0, caster, "to one enemy"),
            "A free weapon swing. Adds weapon ATK plus your class stat (Knight +1 per STR, Archer +1 per DEX and +2% dodge per DEX capped at 50%, Mage +1 per INT, Ninja +1 per base Speed). Enraged multiplies the hit by +10% per stack.",
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
        description += ScalingNote(skill.Name, caster);
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
            return $"Locked until round {GameManager.FinishTheJobTurnRequirement} (now round {round}).";
        }

        return "Currently unusable.";
    }

    static void Describe(string name, Entity caster, out string damage, out string description)
    {
        switch (name)
        {
            case "Bash":
                damage = ScaledHit(5, caster, "to one enemy");
                description = "A single physical strike against one enemy.";
                return;
            case "Rush":
                damage = ScaledHit(3, caster, "to one enemy");
                description = "Strike one enemy, then act first next turn at infinite Speed.";
                return;
            case "Embolden":
                damage = "None";
                description = "Gain 2 Enraged next turn. Each Enraged stack increases damage you deal by 10% that turn.";
                return;
            case "Gallant Pride":
                damage = "None";
                description = "Gain 4 Enraged next turn (+40% damage), but your Speed becomes 1 next turn.";
                return;
            case "Cleave":
                damage = ScaledHit(7, caster, "to all enemies");
                description = "Hit every living enemy once.";
                return;
            case "Bludgeon":
                damage = ScaledHit(30, caster, "to one enemy");
                description = "Heavy single-target hit. If the target has Fragile, this deals 1.5× damage instead of the usual Fragile bonus.";
                return;
            case "Terrify":
                damage = "None";
                description = "Inflict 3 Weak on all enemies. Weak reduces damage they deal by 10% per stack.";
                return;
            case "Triple Slash":
                damage = ScaledHit(12, caster, "×3 to one enemy");
                description = "Three strikes on one enemy. Each hit that connects grants 1 Enraged immediately (+10% damage per stack).";
                return;
            case "Furioso":
                damage = ScaledHit(5, caster, "×9, starting at 5");
                description = "Nine strikes on one enemy. The first hit deals 5; each connecting hit adds +3 base power to the remaining hits of this skill only (153 base if every hit lands). Unlocked at level 10.";
                return;
            case "Shoot":
                damage = ScaledHit(4, caster, "to one enemy");
                description = "A single shot at one enemy.";
                return;
            case "Restring":
                damage = "None";
                description = "Gain +15% dodge chance and 4 Enraged next turn (+40% damage).";
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
                damage = ScaledHit(4, caster, "×3 to all enemies");
                description = "Rain arrows on every living enemy, three hits each.";
                return;
            case "Snipe":
                damage = ScaledHit(40, caster, "to one enemy");
                description = "A single shot. If it hits, inflict 4 Fragile on the target. 50 MP. Unlocked at level 1.";
                return;
            case "Curved Shot":
                damage = ScaledHit(17, caster, "×2 to all enemies");
                description = "Two shots against every living enemy. If any hit connects on an enemy, that enemy gains 4 Fragile.";
                return;
            case "Grandshot":
                var dodges = caster == null ? 0 : Math.Clamp(caster.DodgeCount, 0, 4);
                var grandshot = 50 + (dodges * 50);
                damage = ScaledHit(grandshot, caster, "(50 + 50 per dodge, cap 4)");
                description = "Can only be used after you have dodged once this battle. Base 50 power, plus 50 for each dodge this battle (max 4 dodges, 250 power). Unlocked at level 10.";
                return;
            case "Magic Missile":
                damage = ScaledHit(10, caster, "to one enemy");
                description = "A spell bolt. If it hits, inflict 2 Fragile.";
                return;
            case "Fireball":
                damage = ScaledHit(2, caster, "to one enemy");
                description = "A weak spell hit. If it connects, apply Burn 5 for 6 ticks. Burn stack is capped at 25 (30 with Dragons' Fire); extra applications still add duration. Ruby Scepter adds +3 base power.";
                return;
            case "Concentrate":
                damage = "None";
                description = "Recover 20 MP. Your next attack deals +2 damage.";
                return;
            case "Pray":
                var prayHeal = 20 + (caster == null ? 0 : Math.Max(0, caster.Intelligence));
                damage = "None";
                description = $"Heal the whole living party for {prayHeal} HP (20 + INT) and restore 20 MP each.";
                return;
            case "Magic Bullet":
                DescribeMagicBullet(caster, out damage, out description);
                return;
            case "Grand Undertaking":
                damage = "50% enemy max HP / 10% ally max HP";
                description = "Redirect all enemy attacks to you this turn. At the start of the next round, hit all enemies for 50% of their max HP and all allies for 10% of theirs, then skip your following turn. Unlocked at level 10.";
                return;
            case "Spear":
                damage = ScaledHit(12, caster, "to one enemy");
                description = "A single-target skill. Each use discounts Spear's MP cost by 15 (floor 0). Skills also gain +1 power per Speed above the target (max +5).";
                return;
            case "Vertical Cut":
                damage = ScaledHit(27, caster, "to one enemy");
                description = "A heavy single-target skill. Each use discounts Vertical Cut's MP cost by 15 (floor 0). Skills also gain +1 power per Speed above the target (max +5).";
                return;
            case "Focus Spirit":
                damage = "None";
                description = "Gain +50 HP until your next turn. This turn, incoming hits below 20 damage are negated. Dodging grants 5 Enraged next turn.";
                return;
            case "Finish the Job":
                damage = "None";
                description = $"Once per battle, after {GameManager.FinishTheJobTurnRequirement} turns have passed. Gain 6 Enraged next turn (+60% damage), +6 ATK, and a battle-long stance that adds +2 skill power plus +8 more at the start of every turn (caps at +40). Unlocks Overthrow. Unlocked at level 8.";
                return;
            case "Overthrow":
                damage = ScaledHit(42, caster, "to all enemies, × Enraged");
                description = "Gain 12 Enraged on this Overthrow (applied now, +120% damage, not next turn), deal 42 to all enemies, and inflict 3 Weak and 4 Fragile on all enemies next turn. 40 MP. Requires Finish the Job stance. Unlocked at level 10.";
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
                description = "Stage I. One bolt. Applies Burn 2 for 2 ticks if it hits. Advances to II. Burn stack is capped at 25 (30 with Dragons' Fire). Ruby Scepter adds +3 base power to burning stages.";
                return;
        }
    }

    static string ScalingNote(string skillName, Entity caster)
    {
        if (!DamagingOrHealingSkill(skillName))
        {
            return "";
        }

        var stat = MainStatLabel(caster);
        var enraged = caster != null && caster.StrengthBuff > 0
            ? $" Enraged currently adds +{caster.StrengthBuff * 10}%."
            : " Enraged adds +10% damage per stack.";
        return $" Adds {stat} and weapon ATK.{enraged}";
    }

    static bool DamagingOrHealingSkill(string name) =>
        name == "Bash"
        || name == "Rush"
        || name == "Cleave"
        || name == "Bludgeon"
        || name == "Triple Slash"
        || name == "Furioso"
        || name == "Shoot"
        || name == "Rain Down"
        || name == "Snipe"
        || name == "Curved Shot"
        || name == "Grandshot"
        || name == "Magic Missile"
        || name == "Fireball"
        || name == "Magic Bullet"
        || name == "Spear"
        || name == "Vertical Cut"
        || name == "Overthrow";

    static string MainStatLabel(Entity caster)
    {
        if (caster == null || string.IsNullOrEmpty(caster.ClassName))
        {
            return "your class stat";
        }

        return caster.ClassName switch
        {
            "Knight" => "STR",
            "Archer" => "DEX",
            "Mage" => "INT",
            "Ninja" => "base Speed",
            _ => "your class stat",
        };
    }

    static int MainStatValue(Entity caster)
    {
        if (caster == null)
        {
            return 0;
        }

        return caster.ClassName switch
        {
            "Knight" => Math.Max(0, caster.Strength),
            "Archer" => Math.Max(0, caster.Dexterity),
            "Mage" => Math.Max(0, caster.Intelligence),
            "Ninja" => caster.BaseSpeed <= 0 || caster.BaseSpeed >= 999999999
                ? 0
                : Math.Max(0, caster.BaseSpeed),
            _ => 0,
        };
    }

    static string ScaledHit(int skillBase, Entity caster, string suffix)
    {
        var stat = MainStatLabel(caster);
        var atk = caster == null ? 0 : Math.Max(0, caster.Atk);
        var bonus = MainStatValue(caster);
        var total = skillBase + bonus + atk;
        if (caster != null && caster.StrengthBuff > 0)
        {
            total = Math.Max(0, (int)((long)total * (10000 + (1000 * caster.StrengthBuff)) / 10000));
        }

        if (skillBase <= 0)
        {
            return $"{total} (ATK + {stat}) {suffix}";
        }

        return $"{total} ({skillBase} + {stat} + ATK) {suffix}";
    }
}
