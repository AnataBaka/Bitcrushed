using SpacetimeDB.Types;
using UnityEngine;

/// Battle standing positions. Slots are normalized inside one shared land
/// rectangle (image UV, origin bottom-left); Stage maps them through the fitted
/// biome photo so feet stay on solid ground at every aspect ratio.
/// Swamp enemies use a separate image-UV table on the right-hand bank; other
/// biomes keep the shared Land slots unchanged.
public static class StageLayout
{
    /// Uniform sprite scale. 1.25 is the largest step in 1.2–1.4 that keeps
    /// 3+4 and 3+boss on land after snapping sizes to whole pixels.
    public const float EntityScale = 1.25f;

    public static int ScaledPx(float value) => Mathf.Max(1, Mathf.RoundToInt(value * EntityScale));

    public static int ScaledFont(int pts) => GameFont.Snap(Mathf.RoundToInt(pts * EntityScale));

    /// Shared solid-land region in normalized image coordinates (bottom-left origin).
    /// Intersection of plains grass, tundra snow, volcano rock, and swamp bank.
    public static readonly Rect Land = new Rect(0.16f, 0.12f, 0.68f, 0.22f);

    static readonly Vector2[] PlayerLand =
    {
        new Vector2(0.10f, 0.16f),
        new Vector2(0.30f, 0.50f),
        new Vector2(0.48f, 0.84f),
    };

    static readonly Vector2 BossLand = new Vector2(0.80f, 0.40f);

    static readonly Vector2[][] EnemyLand =
    {
        System.Array.Empty<Vector2>(),
        new[] { new Vector2(0.82f, 0.52f) },
        new[] { new Vector2(0.84f, 0.22f), new Vector2(0.80f, 0.78f) },
        new[]
        {
            new Vector2(0.90f, 0.14f),
            new Vector2(0.70f, 0.50f),
            new Vector2(0.88f, 0.86f),
        },
        new[]
        {
            new Vector2(0.92f, 0.10f),
            new Vector2(0.72f, 0.36f),
            new Vector2(0.90f, 0.64f),
            new Vector2(0.70f, 0.90f),
        },
    };

    /// Swamp-only standing spots in image UV (bottom-left). The shared Land
    /// strip sits on water here; these sit on the dark right-hand bank, shallow
    /// staggered so pack-4 footprints stay on land without a flat row.
    static readonly Vector2 SwampBossImage = new Vector2(0.74f, 0.12f);

    static readonly Vector2[][] SwampEnemyImage =
    {
        System.Array.Empty<Vector2>(),
        new[] { new Vector2(0.72f, 0.13f) },
        new[] { new Vector2(0.64f, 0.11f), new Vector2(0.80f, 0.15f) },
        new[]
        {
            new Vector2(0.58f, 0.10f),
            new Vector2(0.70f, 0.16f),
            new Vector2(0.82f, 0.12f),
        },
        new[]
        {
            new Vector2(0.54f, 0.09f),
            new Vector2(0.65f, 0.17f),
            new Vector2(0.76f, 0.10f),
            new Vector2(0.86f, 0.16f),
        },
    };

    public static Vector2 PlayerSize =>
        new Vector2(ScaledPx(250f), ScaledPx(190f));
    public static Vector2 EnemySize =>
        new Vector2(ScaledPx(220f), ScaledPx(155f));
    public static Vector2 BossSize =>
        new Vector2(ScaledPx(320f), ScaledPx(250f));

    public static Vector2[] Players(BiomeBackdropView backdrop, RectTransform field)
    {
        var slots = new Vector2[PlayerLand.Length];
        for (var i = 0; i < PlayerLand.Length; i++)
        {
            slots[i] = ToField(backdrop, field, PlayerLand[i]);
        }

        return slots;
    }

    public static Vector2 Boss(BiomeBackdropView backdrop, RectTransform field)
    {
        if (IsSwamp(backdrop))
        {
            return ToFieldImage(backdrop, field, SwampBossImage);
        }

        return ToField(backdrop, field, BossLand);
    }

    public static Vector2[] Enemies(BiomeBackdropView backdrop, RectTransform field, int pack)
    {
        pack = Mathf.Clamp(pack, 1, EnemyLand.Length - 1);
        var swamp = IsSwamp(backdrop);
        var land = swamp ? SwampEnemyImage[pack] : EnemyLand[pack];
        var slots = new Vector2[land.Length];
        for (var i = 0; i < land.Length; i++)
        {
            slots[i] = swamp
                ? ToFieldImage(backdrop, field, land[i])
                : ToField(backdrop, field, land[i]);
        }

        return slots;
    }

    static bool IsSwamp(BiomeBackdropView backdrop) =>
        backdrop != null && backdrop.Biome == WorldBiome.Swamp;

    public static Vector2 ToField(BiomeBackdropView backdrop, RectTransform field, Vector2 landUv)
    {
        var imageUv = new Vector2(
            Land.xMin + landUv.x * Land.width,
            Land.yMin + landUv.y * Land.height
        );
        return ToFieldImage(backdrop, field, imageUv, landUv);
    }

    static Vector2 ToFieldImage(BiomeBackdropView backdrop, RectTransform field, Vector2 imageUv) =>
        ToFieldImage(backdrop, field, imageUv, imageUv);

    static Vector2 ToFieldImage(
        BiomeBackdropView backdrop,
        RectTransform field,
        Vector2 imageUv,
        Vector2 fallbackUv
    )
    {
        if (backdrop != null && field != null && backdrop.TryMapImageUv(imageUv, field, out var anchored))
        {
            return anchored;
        }

        if (field == null)
        {
            return Vector2.zero;
        }

        return new Vector2(
            (0.18f + fallbackUv.x * 0.46f) * field.rect.width,
            (0.10f + fallbackUv.y * 0.28f) * field.rect.height
        );
    }
}
