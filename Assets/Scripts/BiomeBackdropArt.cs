using System.Collections.Generic;
using System.IO;
using SpacetimeDB.Types;
using UnityEngine;

/// Runtime biome backdrops from StreamingAssets. Same load path as item icons:
/// File.ReadAllBytes + Texture2D.LoadImage, so a player build can find the PNGs.
public static class BiomeBackdropArt
{
    const string Folder = "Backgrounds";

    static readonly Dictionary<WorldBiome, string> FileByBiome = new Dictionary<WorldBiome, string>
    {
        { WorldBiome.Plains, "plains background.png" },
        { WorldBiome.Volcano, "volcano background.png" },
        { WorldBiome.Swamp, "swamp background.png" },
        { WorldBiome.SnowyTundra, "ice background.png" },
    };

    static readonly Dictionary<WorldBiome, Sprite> Cache = new Dictionary<WorldBiome, Sprite>();
    static readonly HashSet<WorldBiome> MissingWarned = new HashSet<WorldBiome>();
    static bool _preloaded;

    /// RGBA32 bytes of the four source textures after preload (no mipmaps).
    public static int PreloadedBytes { get; private set; }

    public static void PreloadAll()
    {
        if (_preloaded)
        {
            return;
        }

        _preloaded = true;
        PreloadedBytes = 0;
        foreach (var biome in FileByBiome.Keys)
        {
            var sprite = Load(biome);
            if (sprite != null && sprite.texture != null)
            {
                var tex = sprite.texture;
                PreloadedBytes += tex.width * tex.height * 4;
            }
        }
    }

    public static Sprite SpriteFor(WorldBiome biome)
    {
        PreloadAll();
        return Cache.TryGetValue(biome, out var sprite) ? sprite : null;
    }

    public static string FileNameFor(WorldBiome biome) =>
        FileByBiome.TryGetValue(biome, out var name) ? name : "";

    static Sprite Load(WorldBiome biome)
    {
        if (Cache.TryGetValue(biome, out var cached))
        {
            return cached;
        }

        if (!FileByBiome.TryGetValue(biome, out var fileName))
        {
            WarnOnce(biome, "no file mapping");
            Cache[biome] = null;
            return null;
        }

        var bytes = ReadPngBytes(fileName);
        if (bytes == null || bytes.Length == 0)
        {
            WarnOnce(biome, $"missing '{Folder}/{fileName}'");
            Cache[biome] = null;
            return null;
        }

        var texture = PixelStyle.Texture(2, 2);
        if (!texture.LoadImage(bytes, markNonReadable: false))
        {
            Object.Destroy(texture);
            WarnOnce(biome, $"LoadImage failed for '{fileName}'");
            Cache[biome] = null;
            return null;
        }

        // Pixel-art tiles. Point keeps the grid; bilinear would smear the dither.
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.name = fileName;

        var sprite = PixelStyle.Sprite(texture, new Vector2(0.5f, 0f));
        sprite.name = fileName;
        Cache[biome] = sprite;
        return sprite;
    }

    static void WarnOnce(WorldBiome biome, string reason)
    {
        if (!MissingWarned.Add(biome))
        {
            return;
        }

        Debug.LogWarning(
            $"Biome backdrop for {biome} failed ({reason}). Using the generated placeholder."
        );
    }

    static byte[] ReadPngBytes(string fileName)
    {
        var streaming = Path.Combine(Application.streamingAssetsPath, Folder, fileName);
        if (File.Exists(streaming))
        {
            return File.ReadAllBytes(streaming);
        }

        var project = Path.Combine(Application.dataPath, "StreamingAssets", Folder, fileName);
        if (File.Exists(project))
        {
            return File.ReadAllBytes(project);
        }

        return null;
    }
}
