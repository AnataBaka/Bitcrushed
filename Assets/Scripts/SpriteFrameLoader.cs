using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// Loads a folder of PNG frames as sprites. Prefers the Unity-imported assets
/// in the editor; falls back to reading the PNG files from disk so Play Mode
/// still works without a Resources copy.
public static class SpriteFrameLoader
{
    public static Sprite[] LoadFolder(string assetsRelativeDir, FilterMode filter)
    {
        if (string.IsNullOrEmpty(assetsRelativeDir))
        {
            return Array.Empty<Sprite>();
        }

#if UNITY_EDITOR
        var fromEditor = LoadFromAssetDatabase(assetsRelativeDir);
        if (fromEditor.Length > 0)
        {
            return fromEditor;
        }
#endif

        var fromResources = LoadFromResources(assetsRelativeDir, filter);
        if (fromResources.Length > 0)
        {
            return fromResources;
        }

        return LoadFromDisk(assetsRelativeDir, filter);
    }

#if UNITY_EDITOR
    static Sprite[] LoadFromAssetDatabase(string assetsRelativeDir)
    {
        var folder = ("Assets/" + assetsRelativeDir).Replace('\\', '/');
        if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
        {
            return Array.Empty<Sprite>();
        }

        var guids = UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        var paths = new List<string>(guids.Length);
        foreach (var guid in guids)
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(path);
            }
        }

        paths.Sort(StringComparer.OrdinalIgnoreCase);
        var frames = new List<Sprite>(paths.Count);
        foreach (var path in paths)
        {
            var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                frames.Add(sprite);
            }
        }

        return frames.ToArray();
    }
#endif

    static Sprite[] LoadFromResources(string assetsRelativeDir, FilterMode filter)
    {
        var textures = Resources.LoadAll<Texture2D>(assetsRelativeDir.Replace('\\', '/'));
        if (textures == null || textures.Length == 0)
        {
            return Array.Empty<Sprite>();
        }

        Array.Sort(textures, (a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        var frames = new Sprite[textures.Length];
        for (var i = 0; i < textures.Length; i++)
        {
            frames[i] = SpriteFromTexture(textures[i], filter);
        }

        return frames;
    }

    static Sprite[] LoadFromDisk(string assetsRelativeDir, FilterMode filter)
    {
        var dir = Path.Combine(Application.dataPath, assetsRelativeDir.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(dir))
        {
            Debug.LogError($"Sprite folder missing: {dir}");
            return Array.Empty<Sprite>();
        }

        var files = Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        if (files.Length == 0)
        {
            Debug.LogError($"No PNG frames in {dir}");
            return Array.Empty<Sprite>();
        }

        var frames = new List<Sprite>(files.Length);
        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(file);
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

            texture.filterMode = filter;
            texture.wrapMode = TextureWrapMode.Clamp;
            frames.Add(SpriteFromTexture(texture, filter));
        }

        return frames.ToArray();
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
