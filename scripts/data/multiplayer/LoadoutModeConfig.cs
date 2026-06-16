using Godot;

[GlobalClass]
public partial class LoadoutModeConfig : Resource {
    public enum LoadoutModeType {
        BuyOnSpawn,
        PersistentBudget,
        RandomOnRespawn,
        MirrorLoadout,
        MapPickups,
    }

    [Export]
    public Godot.Collections.Array<LoadoutModeType> EnabledLoadoutModes { get; set; } = new();

    [Export]
    public LoadoutModeType SelectedLoadoutMode { get; set; } = LoadoutModeType.BuyOnSpawn;

    [Export]
    public int StartingCredits { get; set; } = 1000;

    [Export]
    public int CreditsPerKill { get; set; } = 100;

    [Export]
    public int CreditsPerSpawn { get; set; } = 50;

    public void AddLoadoutMode(LoadoutModeType loadoutModeType) {
        if (HasLoadoutMode(loadoutModeType))
            return;

        EnabledLoadoutModes.Add(loadoutModeType);
    }

    public void RemoveLoadoutMode(LoadoutModeType loadoutModeType) {
        for (var i = EnabledLoadoutModes.Count - 1; i >= 0; i--) {
            if (EnabledLoadoutModes[i] == loadoutModeType)
                EnabledLoadoutModes.RemoveAt(i);
        }
    }

    public bool HasLoadoutMode(LoadoutModeType loadoutModeType) {
        foreach (var enabledLoadoutMode in EnabledLoadoutModes) {
            if (enabledLoadoutMode == loadoutModeType)
                return true;
        }

        return false;
    }

    public LoadoutModeConfig Clone() {
        var clone = new LoadoutModeConfig {
            SelectedLoadoutMode = SelectedLoadoutMode,
            StartingCredits = StartingCredits,
            CreditsPerKill = CreditsPerKill,
            CreditsPerSpawn = CreditsPerSpawn,
        };

        foreach (var loadoutModeType in EnabledLoadoutModes)
            clone.EnabledLoadoutModes.Add(loadoutModeType);

        return clone;
    }
}
