using System;
using System.Collections.Generic;
using Godot;

public partial class MatchLobby : Control {
    private const string MainMenuScenePath = "res://scenes/ui/menus/main_menu.tscn";
    private const string LobbyTeamContainerScenePath = "res://scenes/ui/lobby/lobby_team_container.tscn";
    private const string LobbyPlayerCardScenePath = "res://scenes/ui/lobby/lobby_player_card.tscn";
    private const string LobbyEmptyPlayerSlotScenePath = "res://scenes/ui/lobby/lobby_empty_player_slot.tscn";
    private const string ConfigSelectionOverlayScenePath = "res://scenes/ui/overlays/config_selection_overlay.tscn";
    private const string GameModePlaylistOverlayScenePath = "res://scenes/ui/overlays/game_mode_playlist_overlay.tscn";
    private const string ConfirmationOverlayScenePath = "res://scenes/ui/overlays/confirmation_overlay.tscn";
    private const string ConfigBiomeIconPath = "res://assets/ui/config_biome.svg";
    private const string ConfigStructureIconPath = "res://assets/ui/config_structure.svg";
    private const string BiomePlainsIconPath = "res://assets/ui/biome_plains.svg";
    private const string BiomeArenaIconPath = "res://assets/ui/biome_arena.svg";
    private const string StructureArenaIconPath = "res://assets/ui/structure_arena.svg";
    private static readonly int[] DefaultTeamIds = { 0, 1, 2, 3, 4 };
    private static readonly GameModeConfig.GameModeType[] AvailableGameModes = {
        GameModeConfig.GameModeType.Deathmatch,
        GameModeConfig.GameModeType.CaptureTheFlag,
    };
    private static readonly MapGenerationConfig.StructureType[] AvailableStructureTypes = {
        MapGenerationConfig.StructureType.Arena,
    };
    private static readonly BiomeConfig.BiomeType[] AvailableBiomes = {
        BiomeConfig.BiomeType.Plains,
        BiomeConfig.BiomeType.Arena,
    };
    private static readonly string[] AvailableStructureIconPaths = {
        StructureArenaIconPath,
    };
    private static readonly string[] AvailableBiomeIconPaths = {
        BiomePlainsIconPath,
        BiomeArenaIconPath,
    };

    private PackedScene _lobbyPlayerCardScene;
    private PackedScene _lobbyTeamContainerScene;
    private PackedScene _lobbyEmptyPlayerSlotScene;
    private PackedScene _configSelectionOverlayScene;
    private PackedScene _gameModePlaylistOverlayScene;
    private PackedScene _confirmationOverlayScene;
    private bool _isRefreshingConfig;
    private string _lastShownConfigApplyMessage = string.Empty;

    public override void _Ready() {
        UiInputActions.EnsureConfigured();
        _lobbyTeamContainerScene = GD.Load<PackedScene>(LobbyTeamContainerScenePath);
        _lobbyPlayerCardScene = GD.Load<PackedScene>(LobbyPlayerCardScenePath);
        _lobbyEmptyPlayerSlotScene = GD.Load<PackedScene>(LobbyEmptyPlayerSlotScenePath);
        GetNetworking().LobbyStateChanged += RefreshLobbyState;
        GetNetworking().ConnectionStateChanged += RefreshLobbyState;
        GetNetworking().ConfigApplyStateChanged += OnConfigApplyStateChanged;
        GetNode<Button>("MainLayout/Actions/StartButton").Pressed += OnStartPressed;
        GetNode<Button>("MainLayout/LobbyBody/PlayersPanel/PlayersLayout/AutofillTeamActions/Autofill2TeamsButton").Pressed += () => OnAutofillTeamsPressed(2);
        GetNode<Button>("MainLayout/LobbyBody/PlayersPanel/PlayersLayout/AutofillTeamActions/Autofill3TeamsButton").Pressed += () => OnAutofillTeamsPressed(3);
        GetNode<Button>("MainLayout/LobbyBody/PlayersPanel/PlayersLayout/AutofillTeamActions/Autofill4TeamsButton").Pressed += () => OnAutofillTeamsPressed(4);
        GetNode<Button>("MainLayout/LobbyBody/PlayersPanel/PlayersLayout/LocalTeamModeActions/FfaButton").Pressed += OnLocalFfaPressed;
        GetNode<Button>("MainLayout/LobbyBody/PlayersPanel/PlayersLayout/LocalTeamModeActions/TeamButton").Pressed += OnLocalTeamPressed;
        GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/ConfigActions/ApplyConfigButton").Pressed += OnApplyConfigPressed;
        GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/ConfigActions/RevertConfigButton").Pressed += OnRevertConfigPressed;
        GetNode<Button>("MainLayout/Actions/BackButton").Pressed += OnBackPressed;
        InitializeConfigControls();
        RefreshLobbyState();
        CallDeferred(MethodName.FocusDefaultControl);
    }

    public override void _UnhandledInput(InputEvent inputEvent) {
        if (!inputEvent.IsActionPressed("ui_cancel"))
            return;

        GetViewport()?.SetInputAsHandled();
        OnBackPressed();
    }

    public override void _ExitTree() {
        var networking = GetNodeOrNull<Networking>("/root/Networking");
        if (networking == null)
            return;

        networking.LobbyStateChanged -= RefreshLobbyState;
        networking.ConnectionStateChanged -= RefreshLobbyState;
        networking.ConfigApplyStateChanged -= OnConfigApplyStateChanged;
    }

    private void RefreshLobbyState() {
        var networking = GetNetworking();
        GetNode<Label>("MainLayout/TitleLabel").Text = GetTitle(networking.CurrentMode);
        RefreshConfigControls(GetEditableSetupConfig());
        RefreshPlayerSections(networking.MultiplayerData);

        var connectionSection = GetNode<Control>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/ConnectionSection");
        connectionSection.Visible = !networking.IsLocal;

        var autofillTeamActions = GetNode<Control>("MainLayout/LobbyBody/PlayersPanel/PlayersLayout/AutofillTeamActions");
        autofillTeamActions.Visible = !networking.IsLocal;
        var autofillButtonsDisabled = networking.IsLocal || !networking.IsServer || !networking.HasSelectedMode || networking.MultiplayerData.Players.Count == 0;
        SetAutofillButtonState("Autofill2TeamsButton", autofillButtonsDisabled);
        SetAutofillButtonState("Autofill3TeamsButton", autofillButtonsDisabled);
        SetAutofillButtonState("Autofill4TeamsButton", autofillButtonsDisabled);

        var localTeamModeActions = GetNode<Control>("MainLayout/LobbyBody/PlayersPanel/PlayersLayout/LocalTeamModeActions");
        localTeamModeActions.Visible = networking.IsLocal;
        var localTeamButtonsDisabled = !networking.IsLocal || networking.MultiplayerData.Players.Count == 0;
        GetNode<Button>("MainLayout/LobbyBody/PlayersPanel/PlayersLayout/LocalTeamModeActions/FfaButton").Disabled = localTeamButtonsDisabled;
        GetNode<Button>("MainLayout/LobbyBody/PlayersPanel/PlayersLayout/LocalTeamModeActions/TeamButton").Disabled = localTeamButtonsDisabled;

        var startButton = GetNode<Button>("MainLayout/Actions/StartButton");
        startButton.Visible = networking.IsServer || networking.IsLocal;
        var canStartMatch = CanStartMatchNow(networking, GetEditableSetupConfig());
        startButton.Disabled = !startButton.Visible;
        startButton.Modulate = canStartMatch ? Colors.White : new Color(0.45f, 0.45f, 0.45f);

        var applyButton = GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/ConfigActions/ApplyConfigButton");
        applyButton.Visible = networking.HasSelectedMode;
        applyButton.Disabled = !networking.HasPendingSetupConfigChanges;
        applyButton.Modulate = applyButton.Disabled ? new Color(0.45f, 0.45f, 0.45f) : Colors.White;

        var revertButton = GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/ConfigActions/RevertConfigButton");
        revertButton.Visible = networking.HasSelectedMode;
        revertButton.Disabled = !networking.HasPendingSetupConfigChanges;
        revertButton.Modulate = revertButton.Disabled ? new Color(0.45f, 0.45f, 0.45f) : Colors.White;
    }

    private void FocusDefaultControl() {
        UiFocusHelper.EnsureFocusWithin(
            this,
            new NodePath("MainLayout/Actions/StartButton"),
            new NodePath("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/MapSection/MapContent/MapOptions/BiomeButton"),
            new NodePath("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/GameSection/GameContent/GameModeButton"),
            new NodePath("MainLayout/Actions/BackButton"));
    }

    private static string GetTitle(Networking.NetworkMode networkMode) {
        return networkMode switch {
            Networking.NetworkMode.Local => "Local Lobby",
            Networking.NetworkMode.Lan => "LAN Lobby",
            Networking.NetworkMode.Online => "Online Lobby",
            Networking.NetworkMode.Client => "Client Lobby",
            _ => "Match Lobby",
        };
    }

    private void RefreshPlayerSections(MultiplayerData multiplayerData) {
        var teamSections = GetNode<GridContainer>("MainLayout/LobbyBody/PlayersPanel/PlayersLayout/TeamSections");
        ClearChildren(teamSections);

        var playerDataByTeam = new SortedDictionary<int, List<PlayerData>>();
        var visibleTeamIds = GetVisibleTeamIds(multiplayerData.SetupConfig);
        foreach (var teamId in visibleTeamIds)
            playerDataByTeam[teamId] = new List<PlayerData>();

        foreach (var playerData in multiplayerData.Players) {
            var teamId = multiplayerData.GetTeam(playerData);
            if (teamId == global::MultiplayerData.DefaultTeamId)
                teamId = 1;

            if (!playerDataByTeam.ContainsKey(teamId))
                playerDataByTeam[teamId] = new List<PlayerData>();

            playerDataByTeam[teamId].Add(playerData);
        }

        var teamContainers = new List<LobbyTeamContainer>();
        foreach (var teamPlayers in playerDataByTeam) {
            var teamSection = _lobbyTeamContainerScene.Instantiate<LobbyTeamContainer>();
            teamSection.Configure(
                teamPlayers.Key,
                teamPlayers.Value,
                _lobbyPlayerCardScene,
                _lobbyEmptyPlayerSlotScene,
                GetNetworking().HasSelectedMode && !GetNetworking().IsLocal);
            teamSection.TeamSelected += OnTeamHeaderPressed;
            teamSections.AddChild(teamSection);
            teamContainers.Add(teamSection);
        }

        ConfigureAssignButtonFocusGrid(teamContainers);
    }

    private void ConfigureAssignButtonFocusGrid(IReadOnlyList<LobbyTeamContainer> teamContainers) {
        if (teamContainers.Count < 4)
            return;

        LinkAssignButtonFocus(teamContainers[0].AssignButton, right: teamContainers[1].AssignButton, bottom: teamContainers[2].AssignButton);
        LinkAssignButtonFocus(teamContainers[1].AssignButton, left: teamContainers[0].AssignButton, bottom: teamContainers[3].AssignButton);
        LinkAssignButtonFocus(teamContainers[2].AssignButton, top: teamContainers[0].AssignButton, right: teamContainers[3].AssignButton);
        LinkAssignButtonFocus(teamContainers[3].AssignButton, top: teamContainers[1].AssignButton, left: teamContainers[2].AssignButton);
    }

    private static void LinkAssignButtonFocus(Button button, Button left = null, Button top = null, Button right = null, Button bottom = null) {
        if (button == null)
            return;

        if (left != null)
            button.FocusNeighborLeft = button.GetPathTo(left);
        if (top != null)
            button.FocusNeighborTop = button.GetPathTo(top);
        if (right != null)
            button.FocusNeighborRight = button.GetPathTo(right);
        if (bottom != null)
            button.FocusNeighborBottom = button.GetPathTo(bottom);
    }

    private static IEnumerable<int> GetVisibleTeamIds(SetupConfig setupConfig) {
        return DefaultTeamIds[1..];
    }

    private static string FormatTeamName(int teamId) {
        return teamId == global::MultiplayerData.DefaultTeamId ? "Auto-Assign" : $"Team {teamId}";
    }

    private static string FormatModeName(Networking.NetworkMode networkMode) {
        return networkMode switch {
            Networking.NetworkMode.Local => "Local",
            Networking.NetworkMode.Lan => "LAN",
            Networking.NetworkMode.Online => "Online",
            Networking.NetworkMode.Client => "Client",
            _ => "Not selected",
        };
    }

    private void OnTeamHeaderPressed(int teamId) {
        GetNetworking().SetLocalPeerTeam(teamId);
        RefreshLobbyState();
    }

    private void OnAutofillTeamsPressed(int teamCount) {
        GetNetworking().AutoAssignPeerTeams(teamCount);
        RefreshLobbyState();
    }

    private void SetAutofillButtonState(string buttonName, bool disabled) {
        var button = GetNode<Button>($"MainLayout/LobbyBody/PlayersPanel/PlayersLayout/AutofillTeamActions/{buttonName}");
        button.Disabled = disabled;
        button.Modulate = disabled ? new Color(0.45f, 0.45f, 0.45f) : Colors.White;
    }

    private void OnLocalFfaPressed() {
        GetNetworking().SetLocalPlayersFreeForAllTeams();
        RefreshLobbyState();
    }

    private void OnLocalTeamPressed() {
        GetNetworking().SetLocalPlayersTwoTeams();
        RefreshLobbyState();
    }

    private void OnConfigApplyStateChanged() {
        RefreshLobbyState();

        var message = GetNetworking().LastConfigApplyMessage;
        if (string.IsNullOrWhiteSpace(message) || message == _lastShownConfigApplyMessage)
            return;

        _lastShownConfigApplyMessage = message;
        ShowMessageOverlay("Config Applied", message);
    }

    private void InitializeConfigControls() {
        GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/MapSection/MapContent/MapOptions/BiomeButton").Pressed += OnBiomePressed;
        GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/MapSection/MapContent/MapOptions/StructureButton").Pressed += OnStructurePressed;
        GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/GameSection/GameContent/GameModeButton").Pressed += OnGameModePressed;
        GetNode<CheckBox>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/GameSection/GameContent/RandomOrderCheckBox").Toggled += OnRandomOrderToggled;
    }

    private void RefreshConfigControls(SetupConfig setupConfig) {
        _isRefreshingConfig = true;
        EnsureRandomSeedMode(setupConfig);
        EnsureMvpMapSelections(setupConfig);
        GetNode<Label>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/ConnectionSection/ConnectionContent/ConnectionText/ConnectionInfoLabel").Text = FormatConnectionInfo(setupConfig);
        ConfigureMapOptionButton(
            "MainLayout/LobbyBody/ConfigPanel/ConfigLayout/MapSection/MapContent/MapOptions/BiomeButton",
            "Biome",
            DescribeBiomes(setupConfig.BiomeConfig),
            GetBiomeButtonIconPath(setupConfig.BiomeConfig));
        ConfigureMapOptionButton(
            "MainLayout/LobbyBody/ConfigPanel/ConfigLayout/MapSection/MapContent/MapOptions/StructureButton",
            "Structure",
            DescribeStructures(setupConfig.MapConfig),
            GetStructureButtonIconPath(setupConfig.MapConfig));
        GetNode<Button>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/GameSection/GameContent/GameModeButton").Text = $"Mode\n{DescribeGameModes(setupConfig)}";
        GetNode<CheckBox>("MainLayout/LobbyBody/ConfigPanel/ConfigLayout/GameSection/GameContent/RandomOrderCheckBox").ButtonPressed = setupConfig.GameplayScoring.RandomizeGameModeOrder;
        _isRefreshingConfig = false;
    }

    private void ConfigureMapOptionButton(string buttonPath, string title, string value, string iconPath) {
        GetNode<Button>(buttonPath).Text = string.Empty;
        GetNode<Label>($"{buttonPath}/Content/TitleLabel").Text = title;
        GetNode<Label>($"{buttonPath}/Content/ValueLabel").Text = value;
        GetNode<TextureRect>($"{buttonPath}/Content/Icon").Texture = UiResourceLoader.LoadIconTexture(iconPath);
    }

    private static string GetBiomeButtonIconPath(BiomeConfig biomeConfig) {
        if (biomeConfig?.EnabledBiomes.Count != 1)
            return ConfigBiomeIconPath;

        return GetBiomeIconPath(biomeConfig.EnabledBiomes[0]);
    }

    private static string GetStructureButtonIconPath(MapGenerationConfig mapConfig) {
        if (mapConfig?.EnabledStructureTypes.Count != 1)
            return ConfigStructureIconPath;

        return GetStructureIconPath(mapConfig.EnabledStructureTypes[0]);
    }

    private static string GetBiomeIconPath(BiomeConfig.BiomeType biomeType) {
        return biomeType switch {
            BiomeConfig.BiomeType.Plains => BiomePlainsIconPath,
            BiomeConfig.BiomeType.Arena => BiomeArenaIconPath,
            _ => ConfigBiomeIconPath,
        };
    }

    private static string GetStructureIconPath(MapGenerationConfig.StructureType structureType) {
        return structureType switch {
            MapGenerationConfig.StructureType.Arena => StructureArenaIconPath,
            _ => ConfigStructureIconPath,
        };
    }

    private static void EnsureRandomSeedMode(SetupConfig setupConfig) {
        if (setupConfig?.MapConfig == null)
            return;

        setupConfig.MapConfig.SelectedSeedMode = MapGenerationConfig.SeedMode.AlwaysRandom;
    }

    private static void EnsureMvpMapSelections(SetupConfig setupConfig) {
        if (setupConfig?.MapConfig == null || setupConfig.BiomeConfig == null)
            return;

        for (var i = setupConfig.MapConfig.EnabledStructureTypes.Count - 1; i >= 0; i--) {
            if (!IsAvailableStructure(setupConfig.MapConfig.EnabledStructureTypes[i]))
                setupConfig.MapConfig.EnabledStructureTypes.RemoveAt(i);
        }

        if (setupConfig.MapConfig.EnabledStructureTypes.Count == 0)
            setupConfig.MapConfig.AddStructureType(MapGenerationConfig.StructureType.Arena);

        for (var i = setupConfig.BiomeConfig.EnabledBiomes.Count - 1; i >= 0; i--) {
            if (!IsAvailableBiome(setupConfig.BiomeConfig.EnabledBiomes[i]))
                setupConfig.BiomeConfig.EnabledBiomes.RemoveAt(i);
        }

        if (setupConfig.BiomeConfig.EnabledBiomes.Count == 0) {
            setupConfig.BiomeConfig.AddBiome(BiomeConfig.BiomeType.Plains);
            setupConfig.BiomeConfig.AddBiome(BiomeConfig.BiomeType.Arena);
        }
    }

    private static bool IsAvailableStructure(MapGenerationConfig.StructureType structureType) {
        foreach (var availableStructureType in AvailableStructureTypes) {
            if (availableStructureType == structureType)
                return true;
        }

        return false;
    }

    private static bool IsAvailableBiome(BiomeConfig.BiomeType biomeType) {
        foreach (var availableBiome in AvailableBiomes) {
            if (availableBiome == biomeType)
                return true;
        }

        return false;
    }

    private void OnStructurePressed() {
        ShowSelectionOverlay(
            "Structures",
            GetStructureDisplayNames(),
            AvailableStructureIconPaths,
            index => GetEditableSetupConfig().MapConfig.HasStructureType(AvailableStructureTypes[index]),
            (index, enabled) => {
                var structureType = AvailableStructureTypes[index];
                if (enabled)
                    GetEditableSetupConfig().MapConfig.AddStructureType(structureType);
                else {
                    GetEditableSetupConfig().MapConfig.RemoveStructureType(structureType);
                }

                RefreshLobbyState();
            });
    }

    private void OnBiomePressed() {
        ShowSelectionOverlay(
            "Biomes",
            GetBiomeDisplayNames(),
            AvailableBiomeIconPaths,
            index => GetEditableSetupConfig().BiomeConfig.HasBiome(AvailableBiomes[index]),
            (index, enabled) => {
                var biomeType = AvailableBiomes[index];
                if (enabled)
                    GetEditableSetupConfig().BiomeConfig.AddBiome(biomeType);
                else {
                    GetEditableSetupConfig().BiomeConfig.RemoveBiome(biomeType);
                }

                RefreshLobbyState();
            });
    }

    private void OnGameModePressed() {
        var overlay = SceneOverlay.GetOrCreate(this);
        _gameModePlaylistOverlayScene ??= LoadPackedScene(GameModePlaylistOverlayScenePath);
        if (overlay == null || _gameModePlaylistOverlayScene == null)
            return;

        var playlistOverlay = _gameModePlaylistOverlayScene.Instantiate<GameModePlaylistOverlay>();
        playlistOverlay.Configure(
            AddGameModeEntry,
            MoveGameModeEntryUp,
            MoveGameModeEntryDown,
            RemoveGameModeEntry,
            ClearGameModeEntries);
        playlistOverlay.RefreshList(GetEditableSetupConfig());
        overlay.AddOverlay(playlistOverlay, true);
    }

    private void OnRandomOrderToggled(bool enabled) {
        if (_isRefreshingConfig)
            return;

        GetEditableSetupConfig().GameplayScoring.RandomizeGameModeOrder = enabled;
        RefreshLobbyState();
    }

    private void ShowSelectionOverlay(string title, string[] options, string[] optionIconPaths, Func<int, bool> isSelected, Action<int, bool> onToggled) {
        var overlay = SceneOverlay.GetOrCreate(this);
        _configSelectionOverlayScene ??= LoadPackedScene(ConfigSelectionOverlayScenePath);
        if (overlay == null || _configSelectionOverlayScene == null)
            return;

        var selectionOverlay = _configSelectionOverlayScene.Instantiate<ConfigSelectionOverlay>();
        selectionOverlay.Configure(title, options, optionIconPaths, isSelected, onToggled);
        overlay.AddOverlay(selectionOverlay, true);
    }

    private void ShowMessageOverlay(string title, string message) {
        var overlay = SceneOverlay.GetOrCreate(this);
        if (overlay == null)
            return;

        var overlayRoot = new Control {
            MouseFilter = MouseFilterEnum.Stop,
        };
        overlayRoot.SetAnchorsPreset(LayoutPreset.FullRect);

        var centerContainer = new CenterContainer();
        centerContainer.SetAnchorsPreset(LayoutPreset.FullRect);
        overlayRoot.AddChild(centerContainer);

        var panel = new PanelContainer {
            CustomMinimumSize = new Vector2(420, 200),
        };

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 12);

        var titleLabel = new Label {
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 24);

        var messageLabel = new Label {
            Text = message,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };

        var closeButton = new Button {
            Text = "Close",
            CustomMinimumSize = new Vector2(140, 42),
        };
        closeButton.Pressed += overlayRoot.QueueFree;

        content.AddChild(titleLabel);
        content.AddChild(messageLabel);
        content.AddChild(closeButton);
        margin.AddChild(content);
        panel.AddChild(margin);
        centerContainer.AddChild(panel);
        overlay.AddOverlay(overlayRoot, true);
    }

    private static string DescribeBiomes(BiomeConfig biomeConfig) {
        return DescribeSelection(biomeConfig.EnabledBiomes.Count, AvailableBiomes.Length, biomeConfig.EnabledBiomes.Count == 1 ? biomeConfig.EnabledBiomes[0].ToString() : "Biome");
    }

    private static string DescribeStructures(MapGenerationConfig mapConfig) {
        return DescribeSelection(
            mapConfig.EnabledStructureTypes.Count,
            AvailableStructureTypes.Length,
            mapConfig.EnabledStructureTypes.Count == 1 ? FormatStructureName(mapConfig.EnabledStructureTypes[0]) : "Structure");
    }

    private static string[] GetStructureDisplayNames() {
        var displayNames = new string[AvailableStructureTypes.Length];
        for (var i = 0; i < AvailableStructureTypes.Length; i++)
            displayNames[i] = FormatStructureName(AvailableStructureTypes[i]);

        return displayNames;
    }

    private static string[] GetBiomeDisplayNames() {
        var displayNames = new string[AvailableBiomes.Length];
        for (var i = 0; i < AvailableBiomes.Length; i++)
            displayNames[i] = AvailableBiomes[i].ToString();

        return displayNames;
    }

    private static string FormatStructureName(MapGenerationConfig.StructureType structureType) {
        return structureType.ToString();
    }

    private static string DescribeGameModes(SetupConfig setupConfig) {
        var totalConfiguredModes = GetConfiguredGameModeCount(setupConfig);
        if (totalConfiguredModes <= 0)
            return "Custom";

        if (totalConfiguredModes == 1)
            return "One";

        return HasDefaultGameModeList(setupConfig) ? "All" : "Custom";
    }

    private static int GetConfiguredGameModeCount(SetupConfig setupConfig) {
        var count = 0;
        foreach (var gameMode in setupConfig.GameModes) {
            if (gameMode != null)
                count++;
        }

        return count;
    }

    private static bool HasDefaultGameModeList(SetupConfig setupConfig) {
        if (setupConfig == null || setupConfig.GameModes.Count != AvailableGameModes.Length)
            return false;

        for (var i = 0; i < AvailableGameModes.Length; i++) {
            if (setupConfig.GameModes[i] == null || setupConfig.GameModes[i].ModeType != AvailableGameModes[i])
                return false;
        }

        return true;
    }

    private void AddGameModeEntry(GameModeConfig.GameModeType modeType) {
        GetEditableSetupConfig().GameModes.Add(new GameModeConfig {
            ModeType = modeType,
            DisplayName = GetGameModeDisplayName(modeType),
            IsEnabled = true,
        });
        RefreshLobbyState();
        RefreshTopPlaylistOverlay();
    }

    private void MoveGameModeEntryUp(int index) {
        var gameModes = GetEditableSetupConfig().GameModes;
        if (index <= 0 || index >= gameModes.Count)
            return;

        var gameMode = gameModes[index];
        gameModes.RemoveAt(index);
        gameModes.Insert(index - 1, gameMode);
        RefreshLobbyState();
        RefreshTopPlaylistOverlay();
    }

    private void MoveGameModeEntryDown(int index) {
        var gameModes = GetEditableSetupConfig().GameModes;
        if (index < 0 || index >= gameModes.Count - 1)
            return;

        var gameMode = gameModes[index];
        gameModes.RemoveAt(index);
        gameModes.Insert(index + 1, gameMode);
        RefreshLobbyState();
        RefreshTopPlaylistOverlay();
    }

    private void RemoveGameModeEntry(int index) {
        var gameModes = GetEditableSetupConfig().GameModes;
        if (index < 0 || index >= gameModes.Count)
            return;

        gameModes.RemoveAt(index);
        RefreshLobbyState();
        RefreshTopPlaylistOverlay();
    }

    private void ClearGameModeEntries() {
        GetEditableSetupConfig().GameModes.Clear();
        RefreshLobbyState();
        RefreshTopPlaylistOverlay();
    }

    private void RefreshTopPlaylistOverlay() {
        var overlay = SceneOverlay.Get(this);
        if (overlay == null)
            return;

        for (var i = overlay.GetChildCount() - 1; i >= 0; i--) {
            if (overlay.GetChild(i) is GameModePlaylistOverlay playlistOverlay) {
                playlistOverlay.RefreshList(GetEditableSetupConfig());
                return;
            }
        }
    }

    private static bool CanStartMatch(SetupConfig setupConfig) {
        return setupConfig != null
            && setupConfig.BiomeConfig.EnabledBiomes.Count > 0
            && setupConfig.MapConfig.EnabledStructureTypes.Count > 0
            && GetConfiguredGameModeCount(setupConfig) > 0;
    }

    private static string DescribeSelection(int selectedCount, int totalCount, string singleValue) {
        if (selectedCount <= 0)
            return "None";

        if (selectedCount == 1)
            return singleValue;

        if (selectedCount == totalCount)
            return "All";

        return "Custom";
    }

    private static string GetGameModeDisplayName(GameModeConfig.GameModeType modeType) {
        return modeType switch {
            GameModeConfig.GameModeType.Deathmatch => "Deathmatch",
            GameModeConfig.GameModeType.CaptureTheFlag => "Capture the Flag",
            _ => modeType.ToString(),
        };
    }

    private static string FormatConnectionInfo(SetupConfig setupConfig) {
        return $"Online: {FormatBool(setupConfig.OnlineEnabled)}\n"
            + $"Address: {setupConfig.ServerAddress}\n"
            + $"Port: {setupConfig.ServerPort}";
    }

    private static string FormatBool(bool value) {
        return value ? "yes" : "no";
    }

    private static void ClearChildren(Node node) {
        foreach (var child in node.GetChildren()) {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void OnStartPressed() {
        var networking = GetNetworking();
        var startBlockReason = GetStartBlockReason(networking, GetEditableSetupConfig());
        if (!string.IsNullOrWhiteSpace(startBlockReason)) {
            ShowMessageOverlay("Cannot Start Match", startBlockReason);
            return;
        }

        GameLog.Warn(GameLogScope.MatchSetup, "StartMatchNotImplemented", "reason=matchSceneFlowMissing");
        ShowMessageOverlay("Start Match", "Starting the actual match scene is not implemented yet.");
    }

    private static bool CanStartMatchNow(Networking networking, SetupConfig setupConfig) {
        return string.IsNullOrWhiteSpace(GetStartBlockReason(networking, setupConfig));
    }

    private static string GetStartBlockReason(Networking networking, SetupConfig setupConfig) {
        if (networking == null)
            return "Networking is not available yet.";

        if (networking.CurrentMode == Networking.NetworkMode.NotSelected)
            return "Choose Local, LAN, or Online hosting before starting.";

        if (networking.IsClient)
            return "Only the host can start the match.";

        if (networking.HasPendingSetupConfigChanges)
            return "Apply or revert the pending Match Config changes before starting.";

        if (setupConfig == null)
            return "Match Config is not available yet.";

        if (setupConfig.BiomeConfig.EnabledBiomes.Count == 0)
            return "Select at least one biome before starting.";

        if (setupConfig.MapConfig.EnabledStructureTypes.Count == 0)
            return "Select at least one structure before starting.";

        if (GetConfiguredGameModeCount(setupConfig) == 0)
            return "Add at least one game mode before starting.";

        return string.Empty;
    }

    private void OnApplyConfigPressed() {
        if (!GetNetworking().ApplyCachedSetupConfigChanges())
            return;

        RefreshLobbyState();
    }

    private void OnRevertConfigPressed() {
        if (!GetNetworking().RevertCachedSetupConfigChanges())
            return;

        RefreshLobbyState();
    }

    private void OnBackPressed() {
        ShowConfirmationOverlay(
            "Leave Lobby?",
            "Are you sure you want to leave the lobby and return to the main menu?",
            "Leave",
            "Stay",
            () => {
                GetNetworking().ResetSessionState();
                GetTree().ChangeSceneToFile(MainMenuScenePath);
            });
    }

    private Networking GetNetworking() {
        return GetNode<Networking>("/root/Networking");
    }

    private SetupConfig GetEditableSetupConfig() {
        return GetNetworking().GetEditableSetupConfig();
    }

    private void ShowConfirmationOverlay(string title, string message, string confirmText, string cancelText, Action onConfirmed) {
        _confirmationOverlayScene ??= LoadPackedScene(ConfirmationOverlayScenePath);
        if (_confirmationOverlayScene == null) {
            GD.PushError($"Failed to load confirmation overlay scene at '{ConfirmationOverlayScenePath}'.");
            return;
        }

        var overlay = SceneOverlay.GetOrCreate(this);
        if (overlay == null)
            return;

        var confirmationOverlay = _confirmationOverlayScene.Instantiate<ConfirmationOverlay>();
        confirmationOverlay.Configure(title, message, confirmText, cancelText, onConfirmed);
        overlay.AddOverlay(confirmationOverlay, true);
    }

    private static PackedScene LoadPackedScene(string scenePath) {
        var scene = ResourceLoader.Load<PackedScene>(scenePath);
        if (scene != null)
            return scene;

        GameLog.Error(GameLogScope.UI, "PackedSceneLoadFailed", $"path={scenePath}");
        return null;
    }
}
