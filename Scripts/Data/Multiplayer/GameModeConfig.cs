using Godot;

[GlobalClass]
public partial class GameModeConfig : Resource
{
    public enum GameModeType
    {
        FreeForAll,
        TeamDeathmatch,
        Objective,
    }

    [Export]
    public GameModeType ModeType { get; set; } = GameModeType.FreeForAll;

    [Export]
    public string DisplayName { get; set; } = "Free For All";

    [Export]
    public bool IsEnabled { get; set; } = true;
}
