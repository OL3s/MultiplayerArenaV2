using Godot;

[GlobalClass]
public partial class MapGenerationConfig : Resource {
    public enum StructureType {
        Arena,
        Rooms,
        Narrow,
        Islands,
        Plain,
    }

    public enum SeedMode {
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
    public Godot.Collections.Array<StructureType> EnabledStructureTypes { get; set; } = new();

    public void AddStructureType(StructureType structureType) {
        if (HasStructureType(structureType))
            return;

        EnabledStructureTypes.Add(structureType);
    }

    public void RemoveStructureType(StructureType structureType) {
        for (var i = EnabledStructureTypes.Count - 1; i >= 0; i--) {
            if (EnabledStructureTypes[i] == structureType)
                EnabledStructureTypes.RemoveAt(i);
        }
    }

    public bool HasStructureType(StructureType structureType) {
        foreach (var enabledStructureType in EnabledStructureTypes) {
            if (enabledStructureType == structureType)
                return true;
        }

        return false;
    }

    public MapGenerationConfig Clone() {
        var clone = new MapGenerationConfig {
            SelectedSeedMode = SelectedSeedMode,
            FixedSeed = FixedSeed,
        };

        foreach (var seed in SeedPool)
            clone.SeedPool.Add(seed);

        foreach (var structureType in EnabledStructureTypes)
            clone.EnabledStructureTypes.Add(structureType);

        return clone;
    }
}
