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

    [Export]
    public int Score { get; set; }

    [Export]
    public int Kills { get; set; }

    [Export]
    public int Deaths { get; set; }

    [Export]
    public int Assists { get; set; }

    public bool IsLocalPlayer { get; set; }
}
