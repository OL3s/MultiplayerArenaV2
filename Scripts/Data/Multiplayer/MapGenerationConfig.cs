using Godot;

[GlobalClass]
public partial class MapGenerationConfig : Resource
{
    public enum MapType
    {
        Random,
        Arena,
        Rooms,
        Caves,
        Islands,
    }

    public enum SeedMode
    {
        AlwaysRandom,
        FixedSeed,
        SeedPool,
    }

    [Export]
    public SeedMode SelectedSeedMode { get; set; } = SeedMode.AlwaysRandom;

    [Export]
    public int FixedSeed { get; set; }

    [Export]
    public Godot.Collections.Array<int> SeedPool { get; set; } = new();

    [Export]
    public Godot.Collections.Array<MapType> EnabledMapTypes { get; set; } = new();

    public void AddMapType(MapType mapType)
    {
        if (HasMapType(mapType))
        {
            return;
        }

        EnabledMapTypes.Add(mapType);
    }

    public void RemoveMapType(MapType mapType)
    {
        for (var i = EnabledMapTypes.Count - 1; i >= 0; i--)
        {
            if (EnabledMapTypes[i] == mapType)
            {
                EnabledMapTypes.RemoveAt(i);
            }
        }
    }

    public bool HasMapType(MapType mapType)
    {
        foreach (var enabledMapType in EnabledMapTypes)
        {
            if (enabledMapType == mapType)
            {
                return true;
            }
        }

        return false;
    }

    public MapGenerationConfig Clone()
    {
        var clone = new MapGenerationConfig
        {
            SelectedSeedMode = SelectedSeedMode,
            FixedSeed = FixedSeed,
        };

        foreach (var seed in SeedPool)
        {
            clone.SeedPool.Add(seed);
        }

        foreach (var mapType in EnabledMapTypes)
        {
            clone.EnabledMapTypes.Add(mapType);
        }

        return clone;
    }
}
