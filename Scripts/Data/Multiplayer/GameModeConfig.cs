using Godot;

[GlobalClass]
public partial class GameModeConfig : Resource
{
    public enum GameModeType
    {
        Deathmatch,
        CaptureTheFlag,
    }

    [Export]
    public GameModeType ModeType { get; set; } = GameModeType.Deathmatch;

    [Export]
    public string DisplayName { get; set; } = "Deathmatch";

    [Export]
    public bool IsEnabled { get; set; } = true;

    public GameModeConfig Clone()
    {
        return new GameModeConfig
        {
            ModeType = ModeType,
            DisplayName = DisplayName,
            IsEnabled = IsEnabled,
        };
    }
}
