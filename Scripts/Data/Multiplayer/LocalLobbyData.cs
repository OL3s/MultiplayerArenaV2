using Godot;

[GlobalClass]
public partial class LocalLobbyData : Resource {
    [Export]
    public Godot.Collections.Array<LocalPlayerData> LocalPlayers { get; set; } = new();
}
