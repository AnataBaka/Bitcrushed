using System.Collections.Generic;
using SpacetimeDB.Types;

/// Builds inspect text from ItemDef table fields. Effect copy lives on the server.
public static class ItemInspect
{
    public readonly struct Info
    {
        public Info(string title, string slot, string stats, string effect)
        {
            Title = title;
            Slot = slot;
            Stats = stats;
            Effect = effect;
        }

        public string Title { get; }
        public string Slot { get; }
        public string Stats { get; }
        public string Effect { get; }
    }

    public static Info For(ItemDef def)
    {
        if (def == null)
        {
            return new Info("Empty", "", "", "No item in this slot.");
        }

        return new Info(def.Name, KindLabel(def), StatsOf(def), EffectOf(def));
    }

    static string KindLabel(ItemDef def) =>
        def.Kind switch
        {
            ItemKind.Weapon => $"Weapon - {WeaponTypeName(def.WeaponType)}",
            ItemKind.Amulet => "Amulet",
            ItemKind.Consumable => "Item",
            _ => def.Kind.ToString(),
        };

    static string WeaponTypeName(WeaponType type) =>
        type switch
        {
            WeaponType.Sword => "Sword",
            WeaponType.Staff => "Staff",
            WeaponType.Katana => "Katana",
            WeaponType.Bow => "Bow",
            _ => type.ToString(),
        };

    static string StatsOf(ItemDef def)
    {
        var parts = new List<string>();
        AddBonus(parts, def.AtkBonus, "ATK");
        AddBonus(parts, def.DefenseBonus, "Defense");
        AddBonus(parts, def.StrengthBonus, "Strength");
        AddBonus(parts, def.DexterityBonus, "Dexterity");
        AddBonus(parts, def.IntelligenceBonus, "Intelligence");
        AddBonus(parts, def.SpeedBonus, "Speed");
        AddBonus(parts, def.MaxHpBonus, "Max HP");
        AddBonus(parts, def.MaxManaBonus, "Max MP");
        if (def.HealAmount > 0)
        {
            parts.Add($"Restores {def.HealAmount} HP");
        }

        if (def.ManaRestoreAmount > 0)
        {
            parts.Add($"Restores {def.ManaRestoreAmount} MP");
        }

        return string.Join("\n", parts);
    }

    static string EffectOf(ItemDef def)
    {
        if (!string.IsNullOrEmpty(def.Description))
        {
            return def.Description;
        }

        return StatsOf(def).Length == 0 ? "No special effect." : "";
    }

    static void AddBonus(List<string> parts, int value, string label)
    {
        if (value != 0)
        {
            parts.Add($"{Signed(value)} {label}");
        }
    }

    static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString();
}
