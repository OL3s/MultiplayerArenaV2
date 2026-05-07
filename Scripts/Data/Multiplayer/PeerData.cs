using Godot;

[GlobalClass]
public partial class PeerData : Resource
{
    [Export]
    public int PeerId { get; set; } = -1;

    [Export]
    public bool IsHost { get; set; }

    [Export]
    public int TeamId { get; set; } = MultiplayerData.FreeForAllTeamId;

    [Export]
    public int RequestedLocalPlayerCount { get; set; }

    [Export]
    public int MaxLocalPlayers { get; set; } = 4;
}
