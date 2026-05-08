using Godot;

[GlobalClass]
public partial class SettingsConfig : Resource
{
    [Export]
    public bool ShowNetworkDebugOverlay { get; set; } = true;
}
