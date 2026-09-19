using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// Loads the hand-drawn amulet PNGs at runtime from StreamingAssets so the
/// project does not depend on Unity sprite import settings.
public static class AmuletArt
{
    static readonly Dictionary<string, string> FileByName = new Dictionary<string, string>
    {
        { "Amethyst Sash", "amethyst_sash.png" },
        { "Golden Cross", "golden_cross.png" },
        { "Guardian's Pendant", "guardians_pendant.png" },
        { "Countess' Necklace", "countess_necklace.png" },
        { "Eye of the Watcher", "eye_of_the_watcher.png" },
        { "Sigil of the Old", "sigil_of_the_old.png" },
        { "Dragonfly Charm", "dragonfly_charm.png" },
        { "Twin Amethyst Charm", "twin_amethyst_charm.png" },
        { "Dragons' Fire", "dragons_fire.png" },
        { "Emerald Pendant", "emerald_pendant.png" },
        { "Justices' Wings", "justices_wings.png" },
        { "Holy Grail", "holy_grail.png" },
        { "Hidden Dreamcatcher", "hidden_dreamcatcher.png" },
        { "Rooted Blade", "rooted_blade.png" },
        { "Red Cocoon", "red_cocoon.png" },
        { "Ruby Scepter", "ruby_scepter.png" },
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

    static byte[] ReadPngBytes(string fileName)
    {
        var streaming = Path.Combine(Application.streamingAssetsPath, "Amulets", fileName);
        if (File.Exists(streaming))
        {
            return File.ReadAllBytes(streaming);
        }

        var project = Path.Combine(Application.dataPath, "StreamingAssets", "Amulets", fileName);
        if (File.Exists(project))
        {
            return File.ReadAllBytes(project);
        }

        return null;
    }
}
