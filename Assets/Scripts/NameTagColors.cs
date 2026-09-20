using SpacetimeDB.Types;
using UnityEngine;

/// Name-plate fill per biome. Matches the session biome the backdrop paints so
/// tags and the photo cannot disagree, including rest stops and late joiners.
public static class NameTagColors
{
    public static readonly Color Default = UiFactory.TextColor;
    public static readonly Color SnowyTundra = Color.black;

    public static Color For(WorldBiome biome, bool isActive)
    {
        if (biome == WorldBiome.SnowyTundra)
        {
            return SnowyTundra;
        }

        return isActive ? UiFactory.ActiveColor : Default;
    }
}
