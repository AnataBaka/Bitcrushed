using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// Loads the hand-drawn item PNGs at runtime from StreamingAssets so the
/// project does not depend on Unity sprite import settings.
public static class AmuletArt
{
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
        { "Rusted Pummel", "Weapons/rusted_pummel.png" },
        { "Crimson Dagger", "Weapons/crimson_dagger.png" },
        { "Azure Dagger", "Weapons/azure_dagger.png" },
    };

    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    public static bool HasIcon(string itemName) =>
        !string.IsNullOrEmpty(itemName) && FileByName.ContainsKey(itemName);

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

        var bytes = ReadPngBytes(fileName);
        if (bytes == null || bytes.Length == 0)
        {
            return null;
        }

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes))
        {
            Object.Destroy(texture);
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
        sprite.name = itemName;
        Cache[itemName] = sprite;
        return sprite;
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

        var bytes = ReadPngBytes(relativePath);
        if (bytes == null || bytes.Length == 0)
        {
            var fromSprites = Path.Combine(Application.dataPath, "Sprites", Path.GetFileName(relativePath));
            if (File.Exists(fromSprites))
            {
                bytes = File.ReadAllBytes(fromSprites);
            }
        }

        if (bytes == null || bytes.Length == 0)
        {
            return null;
        }

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes))
        {
            Object.Destroy(texture);
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
        sprite.name = relativePath;
        Cache[relativePath] = sprite;
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
