using Godot;

[GlobalClass]
public partial class SetupConfig : Resource
{
    [Export]
    public int MaxPlayers { get; set; } = 16;

    [Export]
    public int LocalPlayerCount { get; set; } = 1;

    [Export]
    public bool OnlineEnabled { get; set; }

    [Export]
    public bool ForceFreeForAllTeams { get; set; }

    [Export]
    public string ServerAddress { get; set; } = "127.0.0.1";

    [Export]
    public int ServerPort { get; set; } = 7777;

    [Export]
    public string GameModeId { get; set; } = "free_for_all";

    [Export]
    public Godot.Collections.Array<GameModeConfig> GameModes { get; set; } = new();

    [Export]
    public MapGenerationConfig MapConfig { get; set; } = new();

    [Export]
    public BiomeConfig BiomeConfig { get; set; } = new();

    public void AddGameMode(GameModeConfig gameModeConfig)
    {
        if (gameModeConfig == null || HasGameMode(gameModeConfig.ModeType))
        {
            return;
        }

        GameModes.Add(gameModeConfig);
    }

    public void RemoveGameMode(GameModeConfig.GameModeType modeType)
    {
        for (var i = GameModes.Count - 1; i >= 0; i--)
        {
            if (GameModes[i].ModeType == modeType)
            {
                GameModes.RemoveAt(i);
            }
        }
    }

    public bool HasGameMode(GameModeConfig.GameModeType modeType)
    {
        foreach (var gameModeConfig in GameModes)
        {
            if (gameModeConfig.ModeType == modeType)
            {
                return true;
            }
        }

        return false;
    }
}
