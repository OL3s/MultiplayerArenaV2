using Godot;

[GlobalClass]
public partial class MapGeneratorController : Resource {
    public int LastResolvedSeed { get; private set; }

    public StructureGenerationData GenerateStructure(MapGenerationConfig mapConfig, MapGenerationConfig.StructureType fallbackStructureType = MapGenerationConfig.StructureType.Arena) {
        var structureType = GetStructureType(mapConfig, fallbackStructureType);
        LastResolvedSeed = ResolveSeed(mapConfig);
        var structureGenerationData = new StructureGenerationData();
        structureGenerationData.Generate(structureType, LastResolvedSeed);
        return structureGenerationData;
    }

    private static MapGenerationConfig.StructureType GetStructureType(MapGenerationConfig mapConfig, MapGenerationConfig.StructureType fallbackStructureType) {
        if (mapConfig == null || mapConfig.EnabledStructureTypes.Count == 0)
            return fallbackStructureType;

        return mapConfig.EnabledStructureTypes[0];
    }

    private static int ResolveSeed(MapGenerationConfig mapConfig) {
        if (mapConfig == null)
            return 0;

        if (mapConfig.SelectedSeedMode == MapGenerationConfig.SeedMode.FixedSeed)
            return mapConfig.FixedSeed;

        if (mapConfig.SelectedSeedMode == MapGenerationConfig.SeedMode.SeedPool && mapConfig.SeedPool.Count > 0)
            return mapConfig.SeedPool[0];

        return mapConfig.FixedSeed;
    }
}
