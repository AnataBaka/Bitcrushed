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

    /// Centers frames on a square canvas so packs with a tighter crop (Ninja
    /// 96px) draw at the same on-screen size as Knight_1 (128px). Does not
    /// flip or scale the pixels.
    public static Sprite[] PadToSquare(Sprite[] frames, int size)
    {
        if (frames == null || frames.Length == 0 || size <= 0)
        {
            return frames;
        }

        var padded = new Sprite[frames.Length];
        for (var i = 0; i < frames.Length; i++)
        {
            padded[i] = PadToSquare(frames[i], size);
        }

        return padded;
    }

    static Sprite PadToSquare(Sprite sprite, int size)
    {
        if (sprite == null || sprite.texture == null)
        {
            return sprite;
        }

        var source = CopyPixels(sprite);
        if (source == null)
        {
            return sprite;
        }

        if (source.width == size && source.height == size)
        {
            UnityEngine.Object.Destroy(source);
            return sprite;
        }

        if (source.width > size || source.height > size)
        {
            UnityEngine.Object.Destroy(source);
            return sprite;
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = sprite.name,
            filterMode = sprite.texture.filterMode,
            wrapMode = TextureWrapMode.Clamp,
        };
        texture.SetPixels32(new Color32[size * size]);
        var x = (size - source.width) / 2;
        var y = (size - source.height) / 2;
        texture.SetPixels(x, y, source.width, source.height, source.GetPixels());
        texture.Apply(false, false);
        UnityEngine.Object.Destroy(source);

        return SpriteFromTexture(texture, texture.filterMode);
    }

    static Texture2D CopyPixels(Sprite sprite)
    {
        var texture = sprite.texture;
        var rect = sprite.textureRect;
        var x = Mathf.RoundToInt(rect.x);
        var y = Mathf.RoundToInt(rect.y);
        var width = Mathf.RoundToInt(rect.width);
        var height = Mathf.RoundToInt(rect.height);
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        try
        {
            var copy = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = texture.filterMode,
                wrapMode = TextureWrapMode.Clamp,
            };
            copy.SetPixels(texture.GetPixels(x, y, width, height));
            copy.Apply(false, false);
            return copy;
        }
        catch (UnityException)
        {
            var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;
            var full = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            full.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            full.Apply(false, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            var copy = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = texture.filterMode,
                wrapMode = TextureWrapMode.Clamp,
            };
            copy.SetPixels(full.GetPixels(x, y, width, height));
            copy.Apply(false, false);
            UnityEngine.Object.Destroy(full);
            return copy;
        }
    }
}
