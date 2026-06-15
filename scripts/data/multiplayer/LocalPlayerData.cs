using Godot;

[GlobalClass]
public partial class LocalPlayerData : Resource {
    public enum LocalInputType {
        None,
        KeyboardMouse,
        Gamepad,
    }

    [Export]
    public int LocalId { get; set; } = -1;

    [Export]
    public bool IsActive { get; set; }

    [Export]
    public LocalInputType InputType { get; set; } = LocalInputType.None;

    [Export]
    public int DeviceId { get; set; } = -1;

    [Export]
    public string DisplayName { get; set; } = "Player";
}
