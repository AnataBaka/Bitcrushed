using UnityEngine;

/// Battle standing positions. Slots are normalized inside one shared land
/// rectangle (image UV, origin bottom-left); Stage maps them through the fitted
/// biome photo so feet stay on solid ground at every aspect ratio.
public static class StageLayout
{
    /// Uniform sprite scale. 1 keeps the existing card sizes; tighten spacing first.
    public const float EntityScale = 1f;

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

    public static Vector2 PlayerSize => new Vector2(250f, 190f) * EntityScale;
    public static Vector2 EnemySize => new Vector2(220f, 155f) * EntityScale;
    public static Vector2 BossSize => new Vector2(320f, 250f) * EntityScale;

    public static Vector2[] Players(BiomeBackdropView backdrop, RectTransform field)
    {
        var slots = new Vector2[PlayerLand.Length];
        for (var i = 0; i < PlayerLand.Length; i++)
        {
            slots[i] = ToField(backdrop, field, PlayerLand[i]);
        }

        return slots;
    }

    public static Vector2 Boss(BiomeBackdropView backdrop, RectTransform field) =>
        ToField(backdrop, field, BossLand);

    public static Vector2[] Enemies(BiomeBackdropView backdrop, RectTransform field, int pack)
    {
        pack = Mathf.Clamp(pack, 1, EnemyLand.Length - 1);
        var land = EnemyLand[pack];
        var slots = new Vector2[land.Length];
        for (var i = 0; i < land.Length; i++)
        {
            slots[i] = ToField(backdrop, field, land[i]);
        }

        return slots;
    }

    public static Vector2 ToField(BiomeBackdropView backdrop, RectTransform field, Vector2 landUv)
    {
        var imageUv = new Vector2(
            Land.xMin + landUv.x * Land.width,
            Land.yMin + landUv.y * Land.height
        );
        if (backdrop != null && field != null && backdrop.TryMapImageUv(imageUv, field, out var anchored))
        {
            return anchored;
        }

        if (field == null)
        {
            return Vector2.zero;
        }

        return new Vector2(
            (0.18f + landUv.x * 0.46f) * field.rect.width,
            (0.10f + landUv.y * 0.28f) * field.rect.height
        );
    }
}
