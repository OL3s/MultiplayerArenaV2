using Godot;

[GlobalClass]
public partial class BiomeConfig : Resource {
    public enum BiomeType {
        Plains,
        Arena,
    }

    [Export]
    public Godot.Collections.Array<BiomeType> EnabledBiomes { get; set; } = new();

    public void AddBiome(BiomeType biomeType) {
        if (HasBiome(biomeType))
            return;

        EnabledBiomes.Add(biomeType);
    }

    public void RemoveBiome(BiomeType biomeType) {
        for (var i = EnabledBiomes.Count - 1; i >= 0; i--) {
            if (EnabledBiomes[i] == biomeType)
                EnabledBiomes.RemoveAt(i);
        }
    }

    public bool HasBiome(BiomeType biomeType) {
        foreach (var enabledBiome in EnabledBiomes) {
            if (enabledBiome == biomeType)
                return true;
        }

        return false;
    }

    public BiomeConfig Clone() {
        var clone = new BiomeConfig();
        foreach (var biome in EnabledBiomes)
            clone.EnabledBiomes.Add(biome);

        return clone;
    }
}
