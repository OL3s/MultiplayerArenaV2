using Godot;

[GlobalClass]
public partial class PlayerData : Resource {
    [Export]
    public int GlobalId { get; set; } = -1;

    [Export]
    public int LocalId { get; set; } = -1;

    [Export]
    public int PeerId { get; set; } = -1;

    [Export]
    public int TeamId { get; set; } = MultiplayerData.DefaultTeamId;

    [Export]
    public string DisplayName { get; set; } = "Player";

    public bool IsLocalPlayer { get; set; }
}
