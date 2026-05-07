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
    private string _lastShownConfigApplyMessage = string.Empty;

    public override void _Ready()
    {
        _lobbyPlayerCardScene = GD.Load<PackedScene>(LobbyPlayerCardScenePath);
        _configSelectionOverlayScene = GD.Load<PackedScene>(ConfigSelectionOverlayScenePath);
        GetNetworking().LobbyStateChanged += RefreshLobbyState;
        GetNetworking().ConnectionStateChanged += RefreshLobbyState;
        GetNetworking().ConfigApplyStateChanged += OnConfigApplyStateChanged;
        GetNode<Button>("MainLayout/Actions/StartButton").Pressed += OnStartPressed;
        GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/ConfigActions/ApplyConfigButton").Pressed += OnApplyConfigPressed;
        GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/ConfigActions/RevertConfigButton").Pressed += OnRevertConfigPressed;
        GetNode<Button>("MainLayout/Actions/BackButton").Pressed += OnBackPressed;
        InitializeConfigControls();
        RefreshLobbyState();
    }

    public override void _ExitTree()
    {
        var networking = GetNodeOrNull<Networking>("/root/Networking");
        if (networking == null)
        {
            return;
        }

        networking.LobbyStateChanged -= RefreshLobbyState;
        networking.ConnectionStateChanged -= RefreshLobbyState;
        networking.ConfigApplyStateChanged -= OnConfigApplyStateChanged;
    }

    private void RefreshLobbyState()
    {
        var networking = GetNetworking();
        GetNode<Label>("MainLayout/TitleLabel").Text = GetTitle(networking.CurrentMode);
        GetNode<Label>("MainLayout/StatusLabel").Text = GetStatusText(networking);
        GetNode<Label>("SummaryPanel/SummaryLabel").Text = FormatSummary(networking);
        RefreshConfigControls(GetEditableSetupConfig());
        RefreshPlayerSections(networking.MultiplayerData);

        var startButton = GetNode<Button>("MainLayout/Actions/StartButton");
        startButton.Visible = networking.IsServer || networking.IsLocalOnly;
        startButton.Disabled = !startButton.Visible || networking.HasPendingSetupConfigChanges || networking.CurrentMode == Networking.NetworkMode.NotSelected;
        startButton.Modulate = startButton.Disabled ? new Color(0.45f, 0.45f, 0.45f) : Colors.White;

        var applyButton = GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/ConfigActions/ApplyConfigButton");
        applyButton.Visible = networking.HasSelectedMode;
        applyButton.Disabled = !networking.HasPendingSetupConfigChanges;
        applyButton.Modulate = applyButton.Disabled ? new Color(0.45f, 0.45f, 0.45f) : Colors.White;

        var revertButton = GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/ConfigActions/RevertConfigButton");
        revertButton.Visible = networking.HasSelectedMode;
        revertButton.Disabled = !networking.HasPendingSetupConfigChanges;
        revertButton.Modulate = revertButton.Disabled ? new Color(0.45f, 0.45f, 0.45f) : Colors.White;
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
            return networking.ConnectionStatusText;
        }

        if (networking.IsClient)
        {
            return networking.ConnectionStatusText;
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

    private void OnConfigApplyStateChanged()
    {
        RefreshLobbyState();

        var message = GetNetworking().LastConfigApplyMessage;
        if (string.IsNullOrWhiteSpace(message) || message == _lastShownConfigApplyMessage)
        {
            return;
        }

        _lastShownConfigApplyMessage = message;
        ShowMessageOverlay("Config Applied", message);
    }

    private void InitializeConfigControls()
    {
        GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/MapSection/BiomeRow/BiomeButton").Pressed += OnBiomePressed;
        GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/MapSection/MapTypeRow/MapTypeButton").Pressed += OnStructurePressed;
        GetNode<SpinBox>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/MapSection/SeedRow/SeedSpinBox").ValueChanged += OnSeedChanged;
        GetNode<CheckBox>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/MapSection/SeedRow/RandomSeedCheckBox").Toggled += OnRandomSeedToggled;
        GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/GameSection/GameModeButton").Pressed += OnGameModePressed;
    }

    private void RefreshConfigControls(SetupConfig setupConfig)
    {
        _isRefreshingConfig = true;
        GetNode<Label>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/InternetSection/ConnectionInfoLabel").Text = FormatConnectionInfo(setupConfig);
        GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/MapSection/MapTypeRow/MapTypeButton").Text = DescribeStructures(setupConfig.MapConfig);
        GetNode<SpinBox>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/MapSection/SeedRow/SeedSpinBox").Value = setupConfig.MapConfig.FixedSeed;
        GetNode<CheckBox>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/MapSection/SeedRow/RandomSeedCheckBox").ButtonPressed = setupConfig.MapConfig.SelectedSeedMode == MapGenerationConfig.SeedMode.AlwaysRandom;
        GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/MapSection/BiomeRow/BiomeButton").Text = DescribeBiomes(setupConfig.BiomeConfig);
        GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/GameSection/GameModeButton").Text = DescribeGameModes(setupConfig);
        _isRefreshingConfig = false;
    }

    private void OnStructurePressed()
    {
        ShowSelectionOverlay(
            "Structures",
            GetStructureDisplayNames(),
            index => GetEditableSetupConfig().MapConfig.HasStructureType((MapGenerationConfig.StructureType)index),
            (index, enabled) =>
            {
                var structureType = (MapGenerationConfig.StructureType)index;
                if (enabled)
                {
                    GetEditableSetupConfig().MapConfig.AddStructureType(structureType);
                }
                else
                {
                    GetEditableSetupConfig().MapConfig.RemoveStructureType(structureType);
                }

                RefreshLobbyState();
            });
    }

    private void OnSeedChanged(double seed)
    {
        if (_isRefreshingConfig) return;
        GetEditableSetupConfig().MapConfig.FixedSeed = (int)seed;
        RefreshLobbyState();
    }

    private void OnRandomSeedToggled(bool enabled)
    {
        if (_isRefreshingConfig) return;
        GetEditableSetupConfig().MapConfig.SelectedSeedMode = enabled ? MapGenerationConfig.SeedMode.AlwaysRandom : MapGenerationConfig.SeedMode.FixedSeed;
        RefreshLobbyState();
    }

    private void OnBiomePressed()
    {
        ShowSelectionOverlay(
            "Biomes",
            Enum.GetNames(typeof(BiomeConfig.BiomeType)),
            index => GetEditableSetupConfig().BiomeConfig.HasBiome((BiomeConfig.BiomeType)index),
            (index, enabled) =>
            {
                var biomeType = (BiomeConfig.BiomeType)index;
                if (enabled)
                {
                    GetEditableSetupConfig().BiomeConfig.AddBiome(biomeType);
                }
                else
                {
                    GetEditableSetupConfig().BiomeConfig.RemoveBiome(biomeType);
                }

                RefreshLobbyState();
            });
    }

    private void OnGameModePressed()
    {
        ShowSelectionOverlay(
            "Game Modes",
            new[] { "Free For All", "Team Deathmatch", "Objective" },
            index => GetEditableSetupConfig().HasGameMode((GameModeConfig.GameModeType)index),
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

        var setupConfig = GetEditableSetupConfig();
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

    private void ShowSelectionOverlay(string title, string[] options, Func<int, bool> isSelected, Action<int, bool> onToggled)
    {
        var overlay = SceneOverlay.GetOrCreate(this);
        var selectionOverlay = _configSelectionOverlayScene.Instantiate<ConfigSelectionOverlay>();
        selectionOverlay.Configure(title, options, isSelected, onToggled);
        overlay?.AddOverlay(selectionOverlay, true);
    }

    private void ShowMessageOverlay(string title, string message)
    {
        var overlay = SceneOverlay.GetOrCreate(this);
        if (overlay == null)
        {
            return;
        }

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(420, 200),
        };
        panel.SetAnchorsPreset(LayoutPreset.Center);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 12);

        var titleLabel = new Label
        {
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 24);

        var messageLabel = new Label
        {
            Text = message,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };

        var closeButton = new Button
        {
            Text = "Close",
            CustomMinimumSize = new Vector2(140, 42),
        };
        closeButton.Pressed += panel.QueueFree;

        content.AddChild(titleLabel);
        content.AddChild(messageLabel);
        content.AddChild(closeButton);
        margin.AddChild(content);
        panel.AddChild(margin);
        overlay.AddOverlay(panel, true);
    }

    private static string DescribeBiomes(BiomeConfig biomeConfig)
    {
        return DescribeSelection(biomeConfig.EnabledBiomes.Count, Enum.GetValues(typeof(BiomeConfig.BiomeType)).Length, biomeConfig.EnabledBiomes.Count == 1 ? biomeConfig.EnabledBiomes[0].ToString() : "Biome");
    }

    private static string DescribeStructures(MapGenerationConfig mapConfig)
    {
        return DescribeSelection(
            mapConfig.EnabledStructureTypes.Count,
            Enum.GetValues(typeof(MapGenerationConfig.StructureType)).Length,
            mapConfig.EnabledStructureTypes.Count == 1 ? FormatStructureName(mapConfig.EnabledStructureTypes[0]) : "Structure");
    }

    private static string[] GetStructureDisplayNames()
    {
        var structureTypes = (MapGenerationConfig.StructureType[])Enum.GetValues(typeof(MapGenerationConfig.StructureType));
        var displayNames = new string[structureTypes.Length];
        for (var i = 0; i < structureTypes.Length; i++)
        {
            displayNames[i] = FormatStructureName(structureTypes[i]);
        }

        return displayNames;
    }

    private static string FormatStructureName(MapGenerationConfig.StructureType structureType)
    {
        return structureType switch
        {
            MapGenerationConfig.StructureType.Narrow => "Narrow",
            _ => structureType.ToString(),
        };
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

    private void OnApplyConfigPressed()
    {
        if (!GetNetworking().ApplyCachedSetupConfigChanges())
        {
            return;
        }

        RefreshLobbyState();
    }

    private void OnRevertConfigPressed()
    {
        if (!GetNetworking().RevertCachedSetupConfigChanges())
        {
            return;
        }

        RefreshLobbyState();
    }

    private void OnBackPressed()
    {
        GetNetworking().ResetSessionState();
        GetTree().ChangeSceneToFile(MainMenuScenePath);
    }

    private Networking GetNetworking()
    {
        return GetNode<Networking>("/root/Networking");
    }

    private SetupConfig GetEditableSetupConfig()
    {
        return GetNetworking().GetEditableSetupConfig();
    }
}
