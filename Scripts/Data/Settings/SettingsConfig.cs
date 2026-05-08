using Godot;

[GlobalClass]
public partial class SettingsConfig : Resource {
    public const string SavePath = "user://settings_config.tres";

    [Export]
    public bool ShowNetworkDebugOverlay { get; set; } = true;

    public static SettingsConfig LoadOrCreate() {
        if (!ResourceLoader.Exists(SavePath))
            return new SettingsConfig();

        var loadedSettingsConfig = ResourceLoader.Load<SettingsConfig>(SavePath);
        if (loadedSettingsConfig != null)
            return loadedSettingsConfig;

        GD.PushWarning($"Failed to load settings config from '{SavePath}'. Using defaults.");
        return new SettingsConfig();
    }

    public bool Save() {
        var error = ResourceSaver.Save(this, SavePath);
        if (error == Error.Ok)
            return true;

        GD.PushWarning($"Failed to save settings config to '{SavePath}': {error}.");
        return false;
    }
}
