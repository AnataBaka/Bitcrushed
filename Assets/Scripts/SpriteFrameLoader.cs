using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// Loads a folder of PNG frames. Resources first (Play Mode and player builds),
/// then raw PNG files under Assets, then the Editor asset database.
public static class SpriteFrameLoader
{
    public static Sprite[] LoadFolder(string assetsRelativeDir, FilterMode filter)
    {
        if (string.IsNullOrEmpty(assetsRelativeDir))
        {
            return Array.Empty<Sprite>();
        }

        var fromResources = LoadFromResources(assetsRelativeDir, filter);
        if (fromResources.Length > 0)
        {
            return fromResources;
        }

        var fromDisk = LoadFromDisk(assetsRelativeDir, filter);
        if (fromDisk.Length > 0)
        {
            return fromDisk;
        }

#if UNITY_EDITOR
        var fromEditor = LoadFromAssetDatabase(assetsRelativeDir);
        if (fromEditor.Length > 0)
        {
            return fromEditor;
        }
#endif

        Debug.LogError($"Failed to load sprite frames at '{assetsRelativeDir}'.");
        return Array.Empty<Sprite>();
    }

#if UNITY_EDITOR
    static Sprite[] LoadFromAssetDatabase(string assetsRelativeDir)
    {
        try
        {
            var folder = ("Assets/" + assetsRelativeDir).Replace('\\', '/');
            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
            {
                folder = ("Assets/Resources/" + assetsRelativeDir).Replace('\\', '/');
            }

            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
            {
                return Array.Empty<Sprite>();
            }

            var guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            var frames = new List<Sprite>(guids.Length);
            var paths = new List<string>(guids.Length);
            foreach (var guid in guids)
            {
                paths.Add(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            }

            paths.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths)
            {
                foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Sprite sprite)
                    {
                        frames.Add(sprite);
                    }
                }
            }

            return frames.ToArray();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Editor sprite load failed for {assetsRelativeDir}: {ex.Message}");
            return Array.Empty<Sprite>();
        }
    }
#endif

    static Sprite[] LoadFromResources(string assetsRelativeDir, FilterMode filter)
    {
        var resourcePath = assetsRelativeDir.Replace('\\', '/').Trim('/');
        var sprites = Resources.LoadAll<Sprite>(resourcePath);
        if (sprites != null && sprites.Length > 0)
        {
            Array.Sort(sprites, (a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return sprites;
        }

        var textures = Resources.LoadAll<Texture2D>(resourcePath);
        if (textures == null || textures.Length == 0)
        {
            return Array.Empty<Sprite>();
        }

        Array.Sort(textures, (a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        var frames = new List<Sprite>(textures.Length);
        for (var i = 0; i < textures.Length; i++)
        {
            frames.Add(SpriteFromTexture(textures[i], filter));
        }

        return frames.ToArray();
    }

    static Sprite[] LoadFromDisk(string assetsRelativeDir, FilterMode filter)
    {
        foreach (var dir in DiskCandidates(assetsRelativeDir))
        {
            if (!Directory.Exists(dir))
            {
                continue;
            }

            var files = Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            if (files.Length == 0)
            {
                continue;
            }

            var frames = new List<Sprite>(files.Length);
            foreach (var file in files)
            {
                byte[] bytes;
                try
                {
                    bytes = File.ReadAllBytes(file);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Could not read {file}: {ex.Message}");
                    continue;
                }

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = Path.GetFileNameWithoutExtension(file),
                    filterMode = filter,
                    wrapMode = TextureWrapMode.Clamp,
                };
                if (!texture.LoadImage(bytes))
                {
                    UnityEngine.Object.Destroy(texture);
                    continue;
                }

                frames.Add(SpriteFromTexture(texture, filter));
            }

            if (frames.Count > 0)
            {
                return frames.ToArray();
            }
        }

        return Array.Empty<Sprite>();
    }

    static IEnumerable<string> DiskCandidates(string assetsRelativeDir)
    {
        var rel = assetsRelativeDir.Replace('/', Path.DirectorySeparatorChar);
        if (!string.IsNullOrEmpty(Application.dataPath))
        {
            yield return Path.Combine(Application.dataPath, rel);
            yield return Path.Combine(Application.dataPath, "Resources", rel);
        }

        if (!string.IsNullOrEmpty(Application.streamingAssetsPath))
        {
            yield return Path.Combine(Application.streamingAssetsPath, rel);
        }
    }

    static Sprite SpriteFromTexture(Texture2D texture, FilterMode filter)
    {
        texture.filterMode = filter;
        texture.wrapMode = TextureWrapMode.Clamp;
        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect
        );
        sprite.name = texture.name;
        return sprite;
    }
}
