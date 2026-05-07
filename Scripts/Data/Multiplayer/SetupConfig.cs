using Godot;

[GlobalClass]
public partial class SetupConfig : Resource
{
    [Export]
    public int MaxPlayers { get; set; } = 8;

    [Export]
    public int LocalPlayerCount { get; set; } = 1;

    [Export]
    public bool OnlineEnabled { get; set; }

    [Export]
    public string ServerAddress { get; set; } = "127.0.0.1";

    [Export]
    public int ServerPort { get; set; } = 7777;

    [Export]
    public string GameModeId { get; set; } = "free_for_all";
}
