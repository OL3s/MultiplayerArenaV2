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
    public string ServerAddress { get; set; } = "127.0.0.1";

    [Export]
    public int ServerPort { get; set; } = 7777;

    [Export]
    public string GameModeId { get; set; } = "deathmatch";

    [Export]
    public GameplayScoring GameplayScoring { get; set; } = new();

    [Export]
    public Godot.Collections.Array<GameModeConfig> GameModes { get; set; } = new();

    [Export]
    public MapGenerationConfig MapConfig { get; set; } = new();

    [Export]
    public BiomeConfig BiomeConfig { get; set; } = new();

    public void AddGameMode(GameModeConfig gameModeConfig)
    {
        if (gameModeConfig == null)
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

    public SetupConfig Clone()
    {
        var clone = new SetupConfig
        {
            MaxPlayers = MaxPlayers,
            LocalPlayerCount = LocalPlayerCount,
            OnlineEnabled = OnlineEnabled,
            ServerAddress = ServerAddress,
            ServerPort = ServerPort,
            GameModeId = GameModeId,
            GameplayScoring = GameplayScoring?.Clone() ?? new GameplayScoring(),
            MapConfig = MapConfig?.Clone() ?? new MapGenerationConfig(),
            BiomeConfig = BiomeConfig?.Clone() ?? new BiomeConfig(),
        };

        foreach (var gameModeConfig in GameModes)
        {
            if (gameModeConfig != null)
            {
                clone.GameModes.Add(gameModeConfig.Clone());
            }
        }

        return clone;
    }

    public void CopyFrom(SetupConfig source)
    {
        if (source == null)
        {
            return;
        }

        MaxPlayers = source.MaxPlayers;
        LocalPlayerCount = source.LocalPlayerCount;
        OnlineEnabled = source.OnlineEnabled;
        ServerAddress = source.ServerAddress;
        ServerPort = source.ServerPort;
        GameModeId = source.GameModeId;
        GameplayScoring = source.GameplayScoring?.Clone() ?? new GameplayScoring();
        MapConfig = source.MapConfig?.Clone() ?? new MapGenerationConfig();
        BiomeConfig = source.BiomeConfig?.Clone() ?? new BiomeConfig();
        GameModes.Clear();

        foreach (var gameModeConfig in source.GameModes)
        {
            if (gameModeConfig != null)
            {
                GameModes.Add(gameModeConfig.Clone());
            }
        }
    }
}
