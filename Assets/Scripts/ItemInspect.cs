using SpacetimeDB.Types;

/// Client-side inspect copy for worn gear, especially amulet passives.
public static class ItemInspect
{
    public readonly struct Info
    {
        public Info(string title, string slot, string description)
        {
            Title = title;
            Slot = slot;
            Description = description;
        }

        public string Title { get; }
        public string Slot { get; }
        public string Description { get; }
    }

    public static Info For(ItemDef def)
    {
        if (def == null)
        {
            return new Info("Empty", "", "No item in this slot.");
        }

        return new Info(def.Name, SlotLabel(def), DescriptionOf(def));
    }

    static string SlotLabel(ItemDef def) =>
        def.Kind switch
        {
            ItemKind.Weapon => "Weapon",
            ItemKind.Amulet => "Amulet",
            ItemKind.Consumable => "Consumable",
            _ => def.Kind.ToString(),
        };

    static string DescriptionOf(ItemDef def)
    {
        switch (def.Name)
        {
            case "Amethyst Sash":
                return "Focus heals 50 MP.";
            case "Golden Cross":
                return "Grants +8 max health.";
            case "Guardian's Pendant":
                return "Reduces damage received by 7%.";
            case "Countess' Necklace":
                return "Every time you inflict damage with an attack, heal 2 health. Multi-hits heal multiple times.";
            case "Eye of the Watcher":
                return "Grants 4 Intelligence.";
            case "Sigil of the Old":
                return "Grants -2 Dexterity and +5 Strength.";
            case "Dragonfly Charm":
                return "Strength is doubled for one turn after an ally dies.";
            case "Twin Amethyst Charm":
                return "Grants 5 Intelligence.";
            case "Dragons' Fire":
                return "Burn cap is increased to 30.";
            case "Emerald Pendant":
                return "Spells require 5 less MP.";
            case "Justices' Wings":
                return "Speed is increased by 3.";
            case "Holy Grail":
                return "Heal 5 HP per kill.";
            case "Hidden Dreamcatcher":
                return "Dodge chance is increased by 5%. Can exceed the Archer dodge cap of 50%.";
            case "Rooted Blade":
                return "Every time you hit an enemy, reduce their Speed by 1 next turn (lasts one turn).";
            case "Red Cocoon":
                return "Every time you take damage, deal 1 damage to the enemy who hurt you.";
            case "Ruby Scepter":
                return "If a spell applies Burn, it gains +3 base power.";
            default:
                return StatSummary(def);
        }
    }

    static string StatSummary(ItemDef def)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (def.AtkBonus != 0)
        {
            parts.Add($"ATK {Signed(def.AtkBonus)}");
        }

        if (def.DefenseBonus != 0)
        {
            parts.Add($"DEF {Signed(def.DefenseBonus)}");
        }

        if (def.StrengthBonus != 0)
        {
            parts.Add($"STR {Signed(def.StrengthBonus)}");
        }

        if (def.DexterityBonus != 0)
        {
            parts.Add($"DEX {Signed(def.DexterityBonus)}");
        }

        if (def.IntelligenceBonus != 0)
        {
            parts.Add($"INT {Signed(def.IntelligenceBonus)}");
        }

        if (def.SpeedBonus != 0)
        {
            parts.Add($"SPD {Signed(def.SpeedBonus)}");
        }

        if (def.MaxHpBonus != 0)
        {
            parts.Add($"Max HP {Signed(def.MaxHpBonus)}");
        }

        if (def.MaxManaBonus != 0)
        {
            parts.Add($"Max MP {Signed(def.MaxManaBonus)}");
        }

        if (def.HealAmount != 0)
        {
            parts.Add($"Restores {def.HealAmount} HP");
        }

        if (def.ManaRestoreAmount != 0)
        {
            parts.Add($"Restores {def.ManaRestoreAmount} MP");
        }

        return parts.Count == 0 ? "No special effect." : string.Join(", ", parts) + ".";
    }

    static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString();
}
