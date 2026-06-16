using System;
using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class SetupConfig : Resource {
    [Export]
    public int MaxPlayers { get; set; } = 16;

    [Export]
    public int LocalPlayerCount { get; set; } = 1;

    [Export]
    public bool OnlineEnabled { get; set; }

    [Export]
    public string ServerAddress { get; set; } = "127.0.0.1";

    [Export]
    public int ServerPort { get; set; } = 12000;

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

    [Export]
    public ItemThemeConfig ItemThemeConfig { get; set; } = new();

    [Export]
    public LoadoutModeConfig LoadoutModeConfig { get; set; } = new();

    public void AddGameMode(GameModeConfig gameModeConfig) {
        if (gameModeConfig == null)
            return;

        GameModes.Add(gameModeConfig);
    }

    public void RemoveGameMode(GameModeConfig.GameModeType modeType) {
        for (var i = GameModes.Count - 1; i >= 0; i--) {
            if (GameModes[i].ModeType == modeType)
                GameModes.RemoveAt(i);
        }
    }

    public bool HasGameMode(GameModeConfig.GameModeType modeType) {
        foreach (var gameModeConfig in GameModes) {
            if (gameModeConfig.ModeType == modeType)
                return true;
        }

        return false;
    }

    public SetupConfig Clone() {
        var clone = new SetupConfig {
            MaxPlayers = MaxPlayers,
            LocalPlayerCount = LocalPlayerCount,
            OnlineEnabled = OnlineEnabled,
            ServerAddress = ServerAddress,
            ServerPort = ServerPort,
            GameModeId = GameModeId,
            GameplayScoring = GameplayScoring?.Clone() ?? new GameplayScoring(),
            MapConfig = MapConfig?.Clone() ?? new MapGenerationConfig(),
            BiomeConfig = BiomeConfig?.Clone() ?? new BiomeConfig(),
            ItemThemeConfig = ItemThemeConfig?.Clone() ?? new ItemThemeConfig(),
            LoadoutModeConfig = LoadoutModeConfig?.Clone() ?? new LoadoutModeConfig(),
        };

        foreach (var gameModeConfig in GameModes) {
            if (gameModeConfig != null)
                clone.GameModes.Add(gameModeConfig.Clone());
        }

        return clone;
    }

    public void CopyFrom(SetupConfig source) {
        if (source == null)
            return;

        MaxPlayers = source.MaxPlayers;
        LocalPlayerCount = source.LocalPlayerCount;
        OnlineEnabled = source.OnlineEnabled;
        ServerAddress = source.ServerAddress;
        ServerPort = source.ServerPort;
        GameModeId = source.GameModeId;
        GameplayScoring = source.GameplayScoring?.Clone() ?? new GameplayScoring();
        MapConfig = source.MapConfig?.Clone() ?? new MapGenerationConfig();
        BiomeConfig = source.BiomeConfig?.Clone() ?? new BiomeConfig();
        ItemThemeConfig = source.ItemThemeConfig?.Clone() ?? new ItemThemeConfig();
        LoadoutModeConfig = source.LoadoutModeConfig?.Clone() ?? new LoadoutModeConfig();
        GameModes.Clear();

        foreach (var gameModeConfig in source.GameModes) {
            if (gameModeConfig != null)
                GameModes.Add(gameModeConfig.Clone());
        }
    }

    public bool IsEquivalentTo(SetupConfig other) {
        if (other == null)
            return false;

        return SerializeForNetwork() == other.SerializeForNetwork();
    }

    public string SerializeForNetwork() {
        var mapTypes = new List<string>();
        foreach (var structureType in MapConfig.EnabledStructureTypes)
            mapTypes.Add(((int)structureType).ToString());

        var biomes = new List<string>();
        foreach (var biome in BiomeConfig.EnabledBiomes)
            biomes.Add(((int)biome).ToString());

        var gameModes = new List<string>();
        foreach (var gameMode in GameModes) {
            if (gameMode == null)
                continue;

            gameModes.Add($"{(int)gameMode.ModeType},{EscapeNetworkValue(gameMode.DisplayName)},{(gameMode.IsEnabled ? 1 : 0)}");
        }

        var itemThemes = new List<string>();
        foreach (var themePath in ItemThemeConfig.EnabledThemeDefinitionPaths)
            itemThemes.Add(EscapeNetworkValue(themePath));

        var loadoutModes = new List<string>();
        foreach (var loadoutMode in LoadoutModeConfig.EnabledLoadoutModes)
            loadoutModes.Add(((int)loadoutMode).ToString());

        return string.Join(
            "|",
            MaxPlayers,
            LocalPlayerCount,
            OnlineEnabled ? 1 : 0,
            EscapeNetworkValue(ServerAddress),
            ServerPort,
            EscapeNetworkValue(GameModeId),
            (int)MapConfig.SelectedSeedMode,
            MapConfig.FixedSeed,
            GameplayScoring.BestOfRoundsPerGameMode,
            GameplayScoring.BestOfGameModes,
            GameplayScoring.RandomizeGameModeOrder ? 1 : 0,
            string.Join(",", mapTypes),
            string.Join(",", biomes),
            string.Join(";", gameModes),
            string.Join(",", itemThemes),
            string.Join(",", loadoutModes),
            LoadoutModeConfig.StartingBudget);
    }

    public static bool TryDeserializeForNetwork(string serializedSetupConfig, out SetupConfig setupConfig) {
        setupConfig = null;

        if (string.IsNullOrWhiteSpace(serializedSetupConfig))
            return false;

        var parts = serializedSetupConfig.Split('|');
        if (parts.Length < 14)
            return false;

        if (!int.TryParse(parts[0], out var maxPlayers)
            || !int.TryParse(parts[1], out var localPlayerCount)
            || !int.TryParse(parts[2], out var onlineEnabled)
            || !int.TryParse(parts[4], out var serverPort)
            || !int.TryParse(parts[6], out var seedMode)
            || !int.TryParse(parts[7], out var fixedSeed)) {
            return false;
        }

        if (!int.TryParse(parts[8], out var bestOfRoundsPerGameMode)
            || !int.TryParse(parts[9], out var bestOfGameModes)
            || !int.TryParse(parts[10], out var randomizeGameModeOrder)) {
            return false;
        }

        setupConfig = new SetupConfig {
            MaxPlayers = maxPlayers,
            LocalPlayerCount = localPlayerCount,
            OnlineEnabled = onlineEnabled == 1,
            ServerAddress = UnescapeNetworkValue(parts[3]),
            ServerPort = serverPort,
            GameModeId = UnescapeNetworkValue(parts[5]),
            MapConfig = new MapGenerationConfig {
                SelectedSeedMode = (MapGenerationConfig.SeedMode)seedMode,
                FixedSeed = fixedSeed,
            },
            BiomeConfig = new BiomeConfig(),
            ItemThemeConfig = new ItemThemeConfig(),
            LoadoutModeConfig = new LoadoutModeConfig(),
            GameplayScoring = new GameplayScoring {
                BestOfRoundsPerGameMode = bestOfRoundsPerGameMode,
                BestOfGameModes = bestOfGameModes,
                RandomizeGameModeOrder = randomizeGameModeOrder == 1,
            },
        };

        if (!string.IsNullOrWhiteSpace(parts[11])) {
            foreach (var mapType in parts[11].Split(',', StringSplitOptions.RemoveEmptyEntries)) {
                if (int.TryParse(mapType, out var mapTypeValue))
                    setupConfig.MapConfig.EnabledStructureTypes.Add((MapGenerationConfig.StructureType)mapTypeValue);
            }
        }

        if (!string.IsNullOrWhiteSpace(parts[12])) {
            foreach (var biome in parts[12].Split(',', StringSplitOptions.RemoveEmptyEntries)) {
                if (int.TryParse(biome, out var biomeValue))
                    setupConfig.BiomeConfig.EnabledBiomes.Add((BiomeConfig.BiomeType)biomeValue);
            }
        }

        if (!string.IsNullOrWhiteSpace(parts[13])) {
            foreach (var gameModeEntry in parts[13].Split(';', StringSplitOptions.RemoveEmptyEntries)) {
                var gameModeParts = gameModeEntry.Split(',');
                if (gameModeParts.Length != 3
                    || !int.TryParse(gameModeParts[0], out var modeType)
                    || !int.TryParse(gameModeParts[2], out var isEnabled)) {
                    continue;
                }

                setupConfig.GameModes.Add(new GameModeConfig {
                    ModeType = (GameModeConfig.GameModeType)modeType,
                    DisplayName = UnescapeNetworkValue(gameModeParts[1]),
                    IsEnabled = isEnabled == 1,
                });
            }
        }

        if (parts.Length > 14 && !string.IsNullOrWhiteSpace(parts[14])) {
            foreach (var themePath in parts[14].Split(',', StringSplitOptions.RemoveEmptyEntries))
                setupConfig.ItemThemeConfig.EnabledThemeDefinitionPaths.Add(UnescapeNetworkValue(themePath));
        }

        if (parts.Length > 15 && !string.IsNullOrWhiteSpace(parts[15])) {
            foreach (var loadoutMode in parts[15].Split(',', StringSplitOptions.RemoveEmptyEntries)) {
                if (int.TryParse(loadoutMode, out var loadoutModeValue))
                    setupConfig.LoadoutModeConfig.EnabledLoadoutModes.Add((LoadoutModeConfig.LoadoutModeType)loadoutModeValue);
            }
        }

        if (parts.Length > 16 && int.TryParse(parts[16], out var startingBudget))
            setupConfig.LoadoutModeConfig.StartingBudget = startingBudget;

        setupConfig.EnsureDefaultSelections();
        return true;
    }

    public void EnsureDefaultSelections() {
        if (GameModes.Count == 0) {
            var defaultGameModes = new[] {
                GameModeConfig.GameModeType.Deathmatch,
                GameModeConfig.GameModeType.CaptureTheFlag,
                GameModeConfig.GameModeType.KingOfTheHill,
                GameModeConfig.GameModeType.Headquarters,
            };

            foreach (var modeType in defaultGameModes) {
                AddGameMode(new GameModeConfig {
                    ModeType = modeType,
                    DisplayName = GetGameModeDisplayName(modeType),
                    IsEnabled = true,
                });
            }
        }

        if (MapConfig.EnabledStructureTypes.Count == 0) {
            MapConfig.EnabledStructureTypes.Add(MapGenerationConfig.StructureType.Arena);
        }

        if (BiomeConfig.EnabledBiomes.Count == 0) {
            BiomeConfig.EnabledBiomes.Add(BiomeConfig.BiomeType.Woods);
            BiomeConfig.EnabledBiomes.Add(BiomeConfig.BiomeType.Arena);
            BiomeConfig.EnabledBiomes.Add(BiomeConfig.BiomeType.Medieval);
        }

        if (ItemThemeConfig.EnabledThemeDefinitionPaths.Count == 0) {
            foreach (var themePath in ItemThemeRegistry.GetDefaultEnabledThemePaths())
                ItemThemeConfig.EnabledThemeDefinitionPaths.Add(themePath);
        }

        if (LoadoutModeConfig.EnabledLoadoutModes.Count == 0) {
            LoadoutModeConfig.EnabledLoadoutModes.Add(LoadoutModeConfig.LoadoutModeType.BuyOnSpawn);
            LoadoutModeConfig.EnabledLoadoutModes.Add(LoadoutModeConfig.LoadoutModeType.PersistentBudget);
            LoadoutModeConfig.EnabledLoadoutModes.Add(LoadoutModeConfig.LoadoutModeType.RandomOnRespawn);
            LoadoutModeConfig.EnabledLoadoutModes.Add(LoadoutModeConfig.LoadoutModeType.MirrorLoadout);
        }
    }

    private static string GetGameModeDisplayName(GameModeConfig.GameModeType modeType) {
        return modeType switch {
            GameModeConfig.GameModeType.Deathmatch => "Deathmatch",
            GameModeConfig.GameModeType.CaptureTheFlag => "Capture the Flag",
            GameModeConfig.GameModeType.KingOfTheHill => "King of the Hill",
            GameModeConfig.GameModeType.Headquarters => "Headquarters",
            _ => modeType.ToString(),
        };
    }

    private static string EscapeNetworkValue(string value) {
        return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("%", "%25").Replace("|", "%7C").Replace(",", "%2C").Replace(";", "%3B");
    }

    private static string UnescapeNetworkValue(string value) {
        return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("%3B", ";").Replace("%2C", ",").Replace("%7C", "|").Replace("%25", "%");
    }
}
