using System;
using System.Collections.Generic;
using Godot;

public partial class MatchLobby : Control
{
    private const string MainMenuScenePath = "res://Scenes/UI/MainMenu.tscn";
    private const string LobbyPlayerCardScenePath = "res://Scenes/UI/LobbyPlayerCard.tscn";
    private const string ConfigSelectionOverlayScenePath = "res://Scenes/UI/ConfigSelectionOverlay.tscn";
    private static readonly int[] DefaultTeamIds = { 0, 1, 2 };

    private PackedScene _lobbyPlayerCardScene;
    private PackedScene _configSelectionOverlayScene;
    private bool _isRefreshingConfig;

    public override void _Ready()
    {
        _lobbyPlayerCardScene = GD.Load<PackedScene>(LobbyPlayerCardScenePath);
        _configSelectionOverlayScene = GD.Load<PackedScene>(ConfigSelectionOverlayScenePath);
        GetNode<Button>("MainLayout/Actions/StartButton").Pressed += OnStartPressed;
        GetNode<Button>("MainLayout/Actions/BackButton").Pressed += OnBackPressed;
        InitializeConfigControls();
        RefreshLobbyState();
    }

    private void RefreshLobbyState()
    {
        var networking = GetNetworking();
        GetNode<Label>("MainLayout/TitleLabel").Text = GetTitle(networking.CurrentMode);
        GetNode<Label>("MainLayout/StatusLabel").Text = GetStatusText(networking);
        GetNode<Label>("SummaryPanel/SummaryLabel").Text = FormatSummary(networking);
        RefreshConfigControls(networking.MultiplayerData.SetupConfig);
        RefreshPlayerSections(networking.MultiplayerData);

        var startButton = GetNode<Button>("MainLayout/Actions/StartButton");
        startButton.Disabled = networking.IsClient || networking.CurrentMode == Networking.NetworkMode.NotSelected;
        startButton.Modulate = startButton.Disabled ? new Color(0.45f, 0.45f, 0.45f) : Colors.White;
    }

    private static string GetTitle(Networking.NetworkMode networkMode)
    {
        return networkMode switch
        {
            Networking.NetworkMode.LocalOnly => "Local Lobby",
            Networking.NetworkMode.ServerLocal => "LAN Host Lobby",
            Networking.NetworkMode.ServerOnline => "Online Host Lobby",
            Networking.NetworkMode.DedicatedServer => "Dedicated Server Lobby",
            Networking.NetworkMode.Client => "Client Lobby",
            _ => "Match Lobby",
        };
    }

    private static string GetStatusText(Networking networking)
    {
        if (networking.IsServer)
        {
            return "Status: Server authority. Waiting for players or ready to start.";
        }

        if (networking.IsClient)
        {
            return "Status: Client. Waiting for server lobby data.";
        }

        if (networking.IsLocalOnly)
        {
            return "Status: Local game. Ready when local players are configured.";
        }

        return "Status: No mode selected.";
    }

    private static string FormatSummary(Networking networking)
    {
        return $"Type: {FormatModeName(networking.CurrentMode)}\n"
            + $"Peers connected: {networking.MultiplayerData.Peers.Count}\n"
            + $"Players: {networking.MultiplayerData.Players.Count}";
    }

    private void RefreshPlayerSections(MultiplayerData multiplayerData)
    {
        var teamSections = GetNode<VBoxContainer>("MainLayout/LobbyBody/PlayersPanel/PlayersLayout/TeamSections");
        ClearChildren(teamSections);

        var playerDataByTeam = new SortedDictionary<int, List<PlayerData>>();
        var visibleTeamIds = GetVisibleTeamIds(multiplayerData.SetupConfig);
        foreach (var teamId in visibleTeamIds)
        {
            playerDataByTeam[teamId] = new List<PlayerData>();
        }

        foreach (var playerData in multiplayerData.Players)
        {
            var teamId = multiplayerData.GetTeam(playerData);
            if (!playerDataByTeam.ContainsKey(teamId))
            {
                playerDataByTeam[teamId] = new List<PlayerData>();
            }

            playerDataByTeam[teamId].Add(playerData);
        }

        if (multiplayerData.Players.Count == 0)
        {
            var emptyLabel = new Label { Text = "No players registered yet.", HorizontalAlignment = HorizontalAlignment.Center };
            teamSections.AddChild(emptyLabel);
            return;
        }

        foreach (var teamPlayers in playerDataByTeam)
        {
            var teamSection = CreateTeamSection(teamPlayers.Key);
            var playerList = teamSection.GetNode<VBoxContainer>("PlayerCards");
            foreach (var playerData in teamPlayers.Value)
            {
                var playerCard = _lobbyPlayerCardScene.Instantiate<LobbyPlayerCard>();
                playerCard.SetPlayer(playerData, teamPlayers.Key);
                playerList.AddChild(playerCard);
            }

            teamSections.AddChild(teamSection);
        }
    }

    private static IEnumerable<int> GetVisibleTeamIds(SetupConfig setupConfig)
    {
        if (setupConfig.ForceFreeForAllTeams)
        {
            return new[] { MultiplayerData.FreeForAllTeamId };
        }

        return DefaultTeamIds;
    }

    private static string FormatTeamName(int teamId)
    {
        return teamId == MultiplayerData.FreeForAllTeamId ? "FFA" : $"Team {teamId}";
    }

    private static string FormatModeName(Networking.NetworkMode networkMode)
    {
        return networkMode switch
        {
            Networking.NetworkMode.LocalOnly => "Local only",
            Networking.NetworkMode.ServerLocal => "LAN host",
            Networking.NetworkMode.ServerOnline => "Online host",
            Networking.NetworkMode.DedicatedServer => "Dedicated server",
            Networking.NetworkMode.Client => "Client",
            _ => "Not selected",
        };
    }

    private VBoxContainer CreateTeamSection(int teamId)
    {
        var teamSection = new VBoxContainer();
        teamSection.AddThemeConstantOverride("separation", 8);

        var teamButton = new Button
        {
            Text = $"[{FormatTeamName(teamId)}]",
            CustomMinimumSize = new Vector2(0, 34),
        };
        teamButton.AddThemeFontSizeOverride("font_size", 18);
        teamButton.Pressed += () => OnTeamHeaderPressed(teamId);

        var playerCards = new VBoxContainer { Name = "PlayerCards" };
        playerCards.AddThemeConstantOverride("separation", 8);

        teamSection.AddChild(teamButton);
        teamSection.AddChild(playerCards);
        return teamSection;
    }

    private void OnTeamHeaderPressed(int teamId)
    {
        GetNetworking().SetLocalPeerTeam(teamId);
        RefreshLobbyState();
    }

    private void InitializeConfigControls()
    {
        GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/MapSection/BiomeRow/BiomeButton").Pressed += OnBiomePressed;
        GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/MapSection/MapTypeRow/MapTypeButton").Pressed += OnMapTypePressed;
        GetNode<SpinBox>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/MapSection/SeedRow/SeedSpinBox").ValueChanged += OnSeedChanged;
        GetNode<CheckBox>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/MapSection/SeedRow/RandomSeedCheckBox").Toggled += OnRandomSeedToggled;
        GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/GameSection/GameModeButton").Pressed += OnGameModePressed;
    }

    private void RefreshConfigControls(SetupConfig setupConfig)
    {
        EnsureDefaultGameMode(setupConfig);

        _isRefreshingConfig = true;
        GetNode<Label>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/InternetSection/ConnectionInfoLabel").Text = FormatConnectionInfo(setupConfig);
        GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/MapSection/MapTypeRow/MapTypeButton").Text = DescribeMapTypes(setupConfig.MapConfig);
        GetNode<SpinBox>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/MapSection/SeedRow/SeedSpinBox").Value = setupConfig.MapConfig.FixedSeed;
        GetNode<CheckBox>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/MapSection/SeedRow/RandomSeedCheckBox").ButtonPressed = setupConfig.MapConfig.SelectedSeedMode == MapGenerationConfig.SeedMode.AlwaysRandom;
        GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/MapSection/BiomeRow/BiomeButton").Text = DescribeBiomes(setupConfig.BiomeConfig);
        GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/GameSection/GameModeButton").Text = DescribeGameModes(setupConfig);
        _isRefreshingConfig = false;
    }

    private void OnMapTypePressed()
    {
        ShowSelectionOverlay(
            "Map Types",
            Enum.GetNames(typeof(MapGenerationConfig.MapType)),
            index => GetSetupConfig().MapConfig.HasMapType((MapGenerationConfig.MapType)index),
            (index, enabled) =>
            {
                var mapType = (MapGenerationConfig.MapType)index;
                if (enabled)
                {
                    GetSetupConfig().MapConfig.AddMapType(mapType);
                }
                else
                {
                    GetSetupConfig().MapConfig.RemoveMapType(mapType);
                }

                RefreshLobbyState();
            });
    }

    private void OnSeedChanged(double seed)
    {
        if (_isRefreshingConfig) return;
        GetSetupConfig().MapConfig.FixedSeed = (int)seed;
    }

    private void OnRandomSeedToggled(bool enabled)
    {
        if (_isRefreshingConfig) return;
        GetSetupConfig().MapConfig.SelectedSeedMode = enabled ? MapGenerationConfig.SeedMode.AlwaysRandom : MapGenerationConfig.SeedMode.FixedSeed;
    }

    private void OnBiomePressed()
    {
        ShowSelectionOverlay(
            "Biomes",
            Enum.GetNames(typeof(BiomeConfig.BiomeType)),
            index => GetSetupConfig().BiomeConfig.HasBiome((BiomeConfig.BiomeType)index),
            (index, enabled) =>
            {
                var biomeType = (BiomeConfig.BiomeType)index;
                if (enabled)
                {
                    GetSetupConfig().BiomeConfig.AddBiome(biomeType);
                }
                else
                {
                    GetSetupConfig().BiomeConfig.RemoveBiome(biomeType);
                }

                RefreshLobbyState();
            });
    }

    private void OnGameModePressed()
    {
        ShowSelectionOverlay(
            "Game Modes",
            new[] { "Free For All", "Team Deathmatch", "Objective" },
            index => GetSetupConfig().HasGameMode((GameModeConfig.GameModeType)index),
            (index, enabled) =>
            {
                var modeType = (GameModeConfig.GameModeType)index;
                if (enabled)
                {
                    OnGameModeToggled(modeType, GetGameModeDisplayName(modeType), true);
                }
                else
                {
                    OnGameModeToggled(modeType, GetGameModeDisplayName(modeType), false);
                }

                RefreshLobbyState();
            });
    }

    private void OnGameModeToggled(GameModeConfig.GameModeType modeType, string displayName, bool enabled)
    {
        if (_isRefreshingConfig) return;

        var setupConfig = GetSetupConfig();
        if (enabled)
        {
            setupConfig.AddGameMode(new GameModeConfig
            {
                ModeType = modeType,
                DisplayName = displayName,
                IsEnabled = true,
            });
            return;
        }

        setupConfig.RemoveGameMode(modeType);
    }

    private static void EnsureDefaultGameMode(SetupConfig setupConfig)
    {
        if (setupConfig.GameModes.Count > 0)
        {
            return;
        }

        setupConfig.AddGameMode(new GameModeConfig
        {
            ModeType = GameModeConfig.GameModeType.FreeForAll,
            DisplayName = "Free For All",
            IsEnabled = true,
        });
    }

    private void ShowSelectionOverlay(string title, string[] options, Func<int, bool> isSelected, Action<int, bool> onToggled)
    {
        var overlay = SceneOverlay.GetOrCreate(this);
        var selectionOverlay = _configSelectionOverlayScene.Instantiate<ConfigSelectionOverlay>();
        selectionOverlay.Configure(title, options, isSelected, onToggled);
        overlay?.AddOverlay(selectionOverlay, true);
    }

    private static string DescribeBiomes(BiomeConfig biomeConfig)
    {
        return DescribeSelection(biomeConfig.EnabledBiomes.Count, Enum.GetValues(typeof(BiomeConfig.BiomeType)).Length, biomeConfig.EnabledBiomes.Count == 1 ? biomeConfig.EnabledBiomes[0].ToString() : "Biome");
    }

    private static string DescribeMapTypes(MapGenerationConfig mapConfig)
    {
        return DescribeSelection(mapConfig.EnabledMapTypes.Count, Enum.GetValues(typeof(MapGenerationConfig.MapType)).Length, mapConfig.EnabledMapTypes.Count == 1 ? mapConfig.EnabledMapTypes[0].ToString() : "Type");
    }

    private static string DescribeGameModes(SetupConfig setupConfig)
    {
        return DescribeSelection(setupConfig.GameModes.Count, Enum.GetValues(typeof(GameModeConfig.GameModeType)).Length, setupConfig.GameModes.Count == 1 ? setupConfig.GameModes[0].DisplayName : "Game Mode");
    }

    private static string DescribeSelection(int selectedCount, int totalCount, string singleValue)
    {
        if (selectedCount <= 0)
        {
            return "None";
        }

        if (selectedCount == 1)
        {
            return singleValue;
        }

        if (selectedCount == totalCount)
        {
            return "All";
        }

        return "Custom";
    }

    private static string GetGameModeDisplayName(GameModeConfig.GameModeType modeType)
    {
        return modeType switch
        {
            GameModeConfig.GameModeType.FreeForAll => "Free For All",
            GameModeConfig.GameModeType.TeamDeathmatch => "Team Deathmatch",
            GameModeConfig.GameModeType.Objective => "Objective",
            _ => modeType.ToString(),
        };
    }

    private static string FormatConnectionInfo(SetupConfig setupConfig)
    {
        return $"Online: {FormatBool(setupConfig.OnlineEnabled)}\n"
            + $"Address: {setupConfig.ServerAddress}\n"
            + $"Port: {setupConfig.ServerPort}";
    }

    private static string FormatBool(bool value)
    {
        return value ? "yes" : "no";
    }

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void OnStartPressed()
    {
    }

    private void OnBackPressed()
    {
        GetNetworking().ClearMode();
        GetTree().ChangeSceneToFile(MainMenuScenePath);
    }

    private Networking GetNetworking()
    {
        return GetNode<Networking>("/root/Networking");
    }

    private SetupConfig GetSetupConfig()
    {
        return GetNetworking().MultiplayerData.SetupConfig;
    }
}
