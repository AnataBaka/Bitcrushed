using System;
using System.Collections.Generic;
using System.IO;
using SpacetimeDB.Types;
using UnityEngine;

/// Loads the hand-drawn item PNGs at runtime from StreamingAssets so the
/// project does not depend on Unity sprite import settings.
public static class AmuletArt
{
    /// Final item name -> StreamingAssets relative path. One file per item.
    static readonly Dictionary<string, string> FileByName = new Dictionary<string, string>
    {
        { "Amethyst Sash", "Amulets/amethyst_sash.png" },
        { "Golden Cross", "Amulets/golden_cross.png" },
        { "Guardian's Pendant", "Amulets/guardians_pendant.png" },
        { "Countess' Necklace", "Amulets/countess_necklace.png" },
        { "Eye of the Watcher", "Amulets/eye_of_the_watcher.png" },
        { "Sigil of the Old", "Amulets/sigil_of_the_old.png" },
        { "Dragonfly Charm", "Amulets/dragonfly_charm.png" },
        { "Twin Amethyst Charm", "Amulets/twin_amethyst_charm.png" },
        { "Dragons' Fire", "Amulets/dragons_fire.png" },
        { "Emerald Pendant", "Amulets/emerald_pendant.png" },
        { "Justices' Wings", "Amulets/justices_wings.png" },
        { "Holy Grail", "Amulets/holy_grail.png" },
        { "Hidden Dreamcatcher", "Amulets/hidden_dreamcatcher.png" },
        { "Rooted Blade", "Amulets/rooted_blade.png" },
        { "Red Cocoon", "Amulets/red_cocoon.png" },
        { "Ruby Scepter", "Amulets/ruby_scepter.png" },
        { "Chipped Sword", "Weapons/chipped_sword.png" },
        { "Jagged Sword", "Weapons/jagged_sword.png" },
        { "Crimson Blade", "Weapons/crimson_blade.png" },
        { "Golden Bow", "Weapons/golden_bow.png" },
        { "Emerald Bow", "Weapons/emerald_bow.png" },
        { "Crimson Bow", "Weapons/crimson_bow.png" },
        { "Azure Cane", "Weapons/azure_cane.png" },
        { "Elegant Cane", "Weapons/elegant_cane.png" },
        { "Staff of the Queen", "Weapons/staff_of_the_queen.png" },
        { "Rusted Pommel", "Weapons/rusted_pummel.png" },
        { "Rusted Pummel", "Weapons/rusted_pummel.png" },
        { "Crimson Dagger", "Weapons/crimson_dagger.png" },
        { "Azure Dagger", "Weapons/azure_dagger.png" },
        { "Copper Sword", "Weapons/jagged_sword.png" },
        { "Wooden Bow", "Weapons/golden_bow.png" },
        { "Crooked Stick", "Weapons/azure_cane.png" },
        { "Sharpened Katana", "Weapons/crimson_dagger.png" },
        { "Health Amulet", "Amulets/golden_cross.png" },
        { "Health Potion", "Amulets/holy_grail.png" },
        { "Mana Potion", "Amulets/amethyst_sash.png" },
    };

    /// ShortName fallback so starter + unique rows that share a display name
    /// still resolve even if the name spelling drifts.
    static readonly Dictionary<string, string> FileByShortName = new Dictionary<string, string>
    {
        { "CSW", "Weapons/chipped_sword.png" },
        { "CHP", "Weapons/chipped_sword.png" },
        { "JAG", "Weapons/jagged_sword.png" },
        { "CPS", "Weapons/jagged_sword.png" },
        { "CRB", "Weapons/crimson_blade.png" },
        { "WBW", "Weapons/golden_bow.png" },
        { "GLB", "Weapons/golden_bow.png" },
        { "WDB", "Weapons/golden_bow.png" },
        { "EMB", "Weapons/emerald_bow.png" },
        { "CRW", "Weapons/crimson_bow.png" },
        { "WCN", "Weapons/azure_cane.png" },
        { "AZC", "Weapons/azure_cane.png" },
        { "CST", "Weapons/azure_cane.png" },
        { "ELC", "Weapons/elegant_cane.png" },
        { "SOQ", "Weapons/staff_of_the_queen.png" },
        { "KTN", "Weapons/rusted_pummel.png" },
        { "RPM", "Weapons/rusted_pummel.png" },
        { "SKT", "Weapons/crimson_dagger.png" },
        { "CRD", "Weapons/crimson_dagger.png" },
        { "AZD", "Weapons/azure_dagger.png" },
        { "HPA", "Amulets/golden_cross.png" },
        { "HPT", "Amulets/holy_grail.png" },
        { "MPT", "Amulets/amethyst_sash.png" },
    };

    static readonly HashSet<string> StarterShortNames = new HashSet<string>
    {
        "CSW",
        "WBW",
        "WCN",
        "KTN",
    };

    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
    static readonly Dictionary<uint, Sprite> CacheById = new Dictionary<uint, Sprite>();
    static readonly Dictionary<string, uint> FileOwner = new Dictionary<string, uint>(
        StringComparer.OrdinalIgnoreCase
    );
    static readonly HashSet<uint> MissingWarned = new HashSet<uint>();
    static bool _claimed;

    public static bool HasIcon(string itemName) =>
        !string.IsNullOrEmpty(itemName) && FileByName.ContainsKey(itemName);

    static bool TryFileFor(ItemDef def, out string fileName)
    {
        fileName = null;
        if (def == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(def.Name) && FileByName.TryGetValue(def.Name, out fileName))
        {
            return true;
        }

        return !string.IsNullOrEmpty(def.ShortName)
            && FileByShortName.TryGetValue(def.ShortName, out fileName);
    }

    /// Shared loader keyed by item definition id. Duplicate files stay placeholders.
    public static Sprite ForDef(ItemDef def)
    {
        if (def == null)
        {
            return null;
        }

        EnsureClaims();
        if (CacheById.TryGetValue(def.Id, out var cached))
        {
            return cached;
        }

        return null;
    }

    public static Sprite Icon(string itemName)
    {
        if (string.IsNullOrEmpty(itemName))
        {
            return null;
        }

        if (Cache.TryGetValue(itemName, out var cached) && cached != null)
        {
            return cached;
        }

        if (!FileByName.TryGetValue(itemName, out var fileName))
        {
            return null;
        }

        return LoadFile(itemName, fileName);
    }

    /// Runtime PNG under StreamingAssets, same path rules as item icons.
    public static Sprite Png(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return null;
        }

        if (Cache.TryGetValue(relativePath, out var cached) && cached != null)
        {
            return cached;
        }

        return LoadFile(relativePath, relativePath);
    }

    public static void ClaimCatalog()
    {
        _claimed = false;
        CacheById.Clear();
        FileOwner.Clear();
        MissingWarned.Clear();
        EnsureClaims();
    }

    static void EnsureClaims()
    {
        if (_claimed || GameManager.Conn == null)
        {
            return;
        }

        _claimed = true;
        var defs = new List<ItemDef>();
        foreach (var def in GameManager.Conn.Db.ItemDef.Iter())
        {
            defs.Add(def);
        }

        defs.Sort(CompareClaimOrder);
        foreach (var def in defs)
        {
            Claim(def);
        }
    }

    static int CompareClaimOrder(ItemDef a, ItemDef b)
    {
        var aStarter = StarterShortNames.Contains(a.ShortName) ? 0 : 1;
        var bStarter = StarterShortNames.Contains(b.ShortName) ? 0 : 1;
        var starter = aStarter.CompareTo(bStarter);
        return starter != 0 ? starter : a.Id.CompareTo(b.Id);
    }

    static void Claim(ItemDef def)
    {
        if (!TryFileFor(def, out var fileName))
        {
            WarnMissing(def, "no matching sprite file");
            CacheById[def.Id] = null;
            return;
        }

        if (FileOwner.TryGetValue(fileName, out var owner) && owner != def.Id)
        {
            if (CacheById.TryGetValue(owner, out var shared) && shared != null)
            {
                CacheById[def.Id] = shared;
                return;
            }
        }

        var sprite = LoadFile(fileName, fileName);
        if (sprite == null)
        {
            WarnMissing(def, $"file '{fileName}' failed to load");
            CacheById[def.Id] = null;
            return;
        }

        FileOwner[fileName] = def.Id;
        CacheById[def.Id] = sprite;
    }

    static void WarnMissing(ItemDef def, string reason)
    {
        if (!MissingWarned.Add(def.Id))
        {
            return;
        }

        Debug.LogWarning($"No sprite for item '{def.Name}' (id {def.Id}, {reason}). Using placeholder.");
    }

    static Sprite LoadFile(string cacheKey, string fileName)
    {
        if (Cache.TryGetValue(cacheKey, out var cached) && cached != null)
        {
            return cached;
        }

        var bytes = ReadPngBytes(fileName);
        if (bytes == null || bytes.Length == 0)
        {
            return null;
        }

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes))
        {
            UnityEngine.Object.Destroy(texture);
            return null;
        }

        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );
        sprite.name = cacheKey;
        Cache[cacheKey] = sprite;
        return sprite;
    }

    static byte[] ReadPngBytes(string fileName)
    {
        var streaming = Path.Combine(Application.streamingAssetsPath, fileName);
        if (File.Exists(streaming))
        {
            return File.ReadAllBytes(streaming);
        }

        var project = Path.Combine(Application.dataPath, "StreamingAssets", fileName);
        if (File.Exists(project))
        {
            return File.ReadAllBytes(project);
        }

        return null;
    }
}
