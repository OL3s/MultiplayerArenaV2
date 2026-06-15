using Godot;
using System;
using System.Collections.Generic;

public partial class ArenaMatch : Node2D {
    private static readonly Vector2I TestTileSize = new(16, 16);
    private const float TestPlayerMoveSpeed = 96.0f;
    private const float GamepadDeadzone = 0.25f;
    private const float InputStopThreshold = 0.18f;
    private const float InputFullEnterThreshold = 0.70f;
    private const float InputFullExitThreshold = 0.60f;
    private const float TriggerUseThreshold = 0.5f;
    private const float ActionAimDisplaySeconds = 0.5f;
    private const float NeutralObjectiveRadius = 36.0f;
    private const float TeamSpawnRadius = 32.0f;
    private const float TeamObjectiveRadius = 12.0f;
    private const float RespawnDelaySeconds = 1.0f;
    private const float SpawnImmobilizeSeconds = 1.0f;
    private const int DefaultTestMapSeed = 12000;
    private const int ItemMenuColumns = 3;
    private const int DirectionBucketCount = 16;
    private const string DefaultItemId = "pistol_t1";
    private const string GenericBulletScenePath = "res://scenes/gameplay/projectiles/generic_bullet.tscn";
    private const string GenericThrownItemScenePath = "res://scenes/gameplay/projectiles/generic_thrown_item.tscn";
    private const string GenericLaunchedProjectileScenePath = "res://scenes/gameplay/projectiles/generic_launched_projectile.tscn";
    private const string NeutralObjectiveScenePath = "res://scenes/gameplay/objectives/neutral_objective.tscn";
    private const string TeamSpawnBaseMarkerScenePath = "res://scenes/gameplay/objectives/team_spawn_base_marker.tscn";
    private const string LocalPlayersHudScenePath = "res://scenes/ui/hud/local_players_hud.tscn";

    private static readonly string[] ModernItemIds = {
        "pistol_t1", "pistol_t2", "pistol_t3",
        "smg_t1", "smg_t2", "smg_t3",
        "ar_t1", "ar_t2", "ar_t3",
        "rifle_t1", "rifle_t2", "rifle_t3",
        "rocketlauncher", "grenadelauncher_t1", "grenadelauncher_t2",
        "nade_explosive", "nade_incendiary", "nade_smoke",
    };

    private static readonly string[] ModernArmorIds = {
        "light_armor", "heavy_armor",
    };

    private static readonly Dictionary<string, string> ItemResourcePaths = new() {
        ["pistol_t1"] = "res://assets/items/modern/weapons/pistol_t1.tres",
        ["pistol_t2"] = "res://assets/items/modern/weapons/pistol_t2.tres",
        ["pistol_t3"] = "res://assets/items/modern/weapons/pistol_t3.tres",
        ["smg_t1"] = "res://assets/items/modern/weapons/smg_t1.tres",
        ["smg_t2"] = "res://assets/items/modern/weapons/smg_t2.tres",
        ["smg_t3"] = "res://assets/items/modern/weapons/smg_t3.tres",
        ["ar_t1"] = "res://assets/items/modern/weapons/ar_t1.tres",
        ["ar_t2"] = "res://assets/items/modern/weapons/ar_t2.tres",
        ["ar_t3"] = "res://assets/items/modern/weapons/ar_t3.tres",
        ["rifle_t1"] = "res://assets/items/modern/weapons/rifle_t1.tres",
        ["rifle_t2"] = "res://assets/items/modern/weapons/rifle_t2.tres",
        ["rifle_t3"] = "res://assets/items/modern/weapons/rifle_t3.tres",
        ["rocketlauncher"] = "res://assets/items/modern/weapons/rocketlauncher.tres",
        ["grenadelauncher_t1"] = "res://assets/items/modern/weapons/grenadelauncher_t1.tres",
        ["grenadelauncher_t2"] = "res://assets/items/modern/weapons/grenadelauncher_t2.tres",
        ["nade_explosive"] = "res://assets/items/modern/throwables/nade_explosive.tres",
        ["nade_incendiary"] = "res://assets/items/modern/throwables/nade_incendiary.tres",
        ["nade_smoke"] = "res://assets/items/modern/throwables/nade_smoke.tres",
    };

    private static readonly Dictionary<string, string> ArmorResourcePaths = new() {
        ["light_armor"] = "res://assets/items/modern/armor/light_armor.tres",
        ["heavy_armor"] = "res://assets/items/modern/armor/heavy_armor.tres",
    };

    private enum InputStrength {
        None,
        Some,
        Full,
    }

    private readonly struct QuantizedInputState {
        public readonly int DirectionIndex;
        public readonly InputStrength Strength;

        public QuantizedInputState(int directionIndex, InputStrength strength) {
            DirectionIndex = directionIndex;
            Strength = strength;
        }

        public bool HasInput => Strength != InputStrength.None && DirectionIndex >= 0;

        public bool Equals(QuantizedInputState other) {
            return DirectionIndex == other.DirectionIndex && Strength == other.Strength;
        }
    }

    private readonly struct LocalInputVectors {
        public readonly Vector2 Movement;
        public readonly Vector2 Aim;
        public readonly bool AimFallsBackToMovement;
        public readonly bool IsAiming;

        public LocalInputVectors(Vector2 movement, Vector2 aim, bool aimFallsBackToMovement, bool isAiming) {
            Movement = movement;
            Aim = aim;
            AimFallsBackToMovement = aimFallsBackToMovement;
            IsAiming = isAiming;
        }
    }

    private ArenaMapData _arenaMapData;
    private StructureGenerationData _structureGenerationData;
    private MapGeneratorController _mapGeneratorController = new();
    private ArenaTileLayerRenderer _tileLayerRenderer;
    private Camera2D _camera;
    private CanvasLayer _canvasLayer;
    private Label _statusLabel;
    private LocalPlayersHud _localPlayersHud;
    private PanelContainer _itemMenuPanel;
    private GridContainer _itemMenuGrid;
    private Networking _networking;
    private LevelProp _centerProp;
    private readonly Dictionary<int, DamageTestPlayer> _playersByGlobalId = new();
    private readonly Dictionary<int, QuantizedInputState> _movementStatesByGlobalId = new();
    private readonly Dictionary<int, QuantizedInputState> _aimStatesByGlobalId = new();
    private readonly Dictionary<int, QuantizedInputState> _lastLocalMovementStatesByGlobalId = new();
    private readonly Dictionary<int, QuantizedInputState> _lastLocalAimStatesByGlobalId = new();
    private readonly Dictionary<int, bool> _isAimingByGlobalId = new();
    private readonly Dictionary<int, bool> _lastLocalIsAimingByGlobalId = new();
    private readonly Dictionary<int, PlayerItem> _itemsByGlobalId = new();
    private readonly Dictionary<int, PlayerItemAccuracyState> _accuracyStatesByGlobalId = new();
    private readonly Dictionary<int, PlayerLoadoutState> _loadoutsByGlobalId = new();
    private readonly Dictionary<int, float> _aimStrengthByGlobalId = new();
    private readonly Dictionary<int, double> _itemRecoverySecondsByGlobalId = new();
    private readonly Dictionary<int, bool> _wasUseHeldByGlobalId = new();
    private readonly Dictionary<int, bool> _suppressUseUntilReleasedByGlobalId = new();
    private readonly Dictionary<int, float> _respawnSecondsByGlobalId = new();
    private readonly Dictionary<int, float> _spawnSecondsByGlobalId = new();
    private readonly Dictionary<int, int> _playerTeamIdsByGlobalId = new();
    private readonly HashSet<int> _wipedTeamIds = new();
    private readonly GameplaySpawnManager _spawnManager = new();
    private readonly List<TeamSpawnBaseMarker> _teamSpawnBaseMarkers = new();
    private readonly List<GameplaySpawnMarker> _itemSpawnMarkers = new();
    private readonly Dictionary<string, PlayerItem> _loadedItemsById = new();
    private readonly Dictionary<string, PlayerArmor> _loadedArmorById = new();
    private readonly Dictionary<string, Button> _itemMenuButtonsById = new();
    private NeutralObjective _neutralObjective;
    private int _objectiveControllingTeamId = -1;
    private bool _objectiveIsContested;
    private int _lastAutoAssignedPlayerCount = -1;
    private PlayerAimIndicator _aimIndicator;
    private PackedScene _genericBulletScene;
    private PackedScene _genericThrownItemScene;
    private PackedScene _genericLaunchedProjectileScene;
    private PackedScene _neutralObjectiveScene;
    private PackedScene _teamSpawnBaseMarkerScene;
    private PackedScene _localPlayersHudScene;

    [Export]
    public string ClientAddress { get; set; } = "127.0.0.1";

    [Export]
    public int ClientPort { get; set; } = 12000;

    [Export]
    public GameModeConfig.GameModeType GameModeOverride { get; set; } = GameModeConfig.GameModeType.Deathmatch;

    [Export]
    public int MapSeedOverride { get; set; } = DefaultTestMapSeed;

    [Export]
    public bool UseLanTestBootstrap { get; set; }

    [Export]
    public bool ForceTestSetupOverrides { get; set; }

    public event Action<int> TeamWiped;

    public override void _Ready() {
        UiInputActions.EnsureConfigured();
        _tileLayerRenderer = GetNode<ArenaTileLayerRenderer>("ArenaTileLayerRenderer");
        _camera = GetNode<Camera2D>("Camera2D");
        _canvasLayer = GetNode<CanvasLayer>("CanvasLayer");
        _statusLabel = _canvasLayer.GetNode<Label>("StatusLabel");
        _networking = GetNode<Networking>("/root/Networking");
        _aimIndicator = new PlayerAimIndicator { Name = "AimIndicator", ZIndex = 20 };
        AddChild(_aimIndicator);
        _genericBulletScene = GD.Load<PackedScene>(GenericBulletScenePath);
        _genericThrownItemScene = GD.Load<PackedScene>(GenericThrownItemScenePath);
        _genericLaunchedProjectileScene = GD.Load<PackedScene>(GenericLaunchedProjectileScenePath);
        _neutralObjectiveScene = GD.Load<PackedScene>(NeutralObjectiveScenePath);
        _teamSpawnBaseMarkerScene = GD.Load<PackedScene>(TeamSpawnBaseMarkerScenePath);
        _localPlayersHudScene = GD.Load<PackedScene>(LocalPlayersHudScenePath);

        ApplyCommandLineOverrides();
        if (UseLanTestBootstrap) {
            EnsureDefaultNetworkMode();
            EnsureTestLocalLobbyPlayer();
        }

        if (ForceTestSetupOverrides)
            ApplyTestSetupOverrides();

        _networking.ConnectionStateChanged += OnConnectionStateChanged;
        _networking.LobbyStateChanged += OnLobbyStateChanged;
        BuildLocalPlayersHud();
        BuildItemMenu();
        UpdateStatusLabel();
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.Lifecycle, "SceneReady", $"clientTarget={ClientAddress}:{ClientPort}");

        if (UseLanTestBootstrap && _networking.IsServer && !_networking.HasActiveNetworkPeer)
            TryStartHost();
        else if (UseLanTestBootstrap && _networking.IsClient && !_networking.HasActiveNetworkPeer)
            TryStartClient();

        BuildTestRoom();
        CenterCamera();
    }

    public override void _ExitTree() {
        if (_networking == null)
            return;

        _networking.ConnectionStateChanged -= OnConnectionStateChanged;
        _networking.LobbyStateChanged -= OnLobbyStateChanged;
    }

    public override void _Process(double delta) {
        UpdateItemRecoveries(delta);
        UpdateRespawns(delta);
        UpdateObjective(delta);
        ProcessLocalItemUse(delta);
        UpdateStatusLabel();
        UpdateLocalAimIndicator();
    }

    public override void _PhysicsProcess(double delta) {
        ProcessLocalPlayerInputStates();
        UpdateAccuracyStates(delta);
        if (_networking.IsServer || _networking.IsLocal)
            SimulatePlayerMovement(delta);

        SyncMovingPlayerPositions();
    }

    public override void _UnhandledInput(InputEvent @event) {
        if (IsItemMenuToggleEvent(@event)) {
            ToggleItemMenu();
            GetViewport().SetInputAsHandled();
            return;
        }

    }

    private bool IsItemMenuToggleEvent(InputEvent @event) {
        if (@event is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.B })
            return true;

        return @event is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.B };
    }

    private void TryStartHost() {
        var started = _networking.BeginHostingSession();
        if (started)
            ApplyTestSetupOverrides();

        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.Lifecycle, "HostStartResult", $"started={started} port={_networking.CurrentServerPort}");
    }

    private void ApplyTestSetupOverrides() {
        if (_networking?.MultiplayerData?.SetupConfig == null)
            return;

        var setupConfig = _networking.MultiplayerData.SetupConfig;
        setupConfig.GameModes.Clear();
        setupConfig.GameModes.Add(new GameModeConfig {
            ModeType = GameModeOverride,
            DisplayName = GetGameModeDisplayName(GameModeOverride),
            IsEnabled = true,
        });
        setupConfig.GameModeId = GetGameModeId(GameModeOverride);
        setupConfig.MapConfig.SelectedSeedMode = MapGenerationConfig.SeedMode.FixedSeed;
        setupConfig.MapConfig.FixedSeed = MapSeedOverride;
        setupConfig.MapConfig.EnabledStructureTypes.Clear();
        setupConfig.MapConfig.EnabledStructureTypes.Add(MapGenerationConfig.StructureType.Square);
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.StateChange, "TestSetupOverridesApplied", $"mode={GameModeOverride} id={setupConfig.GameModeId} structure=Square seed={MapSeedOverride}");
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

    private static string GetGameModeId(GameModeConfig.GameModeType modeType) {
        return modeType switch {
            GameModeConfig.GameModeType.Deathmatch => "deathmatch",
            GameModeConfig.GameModeType.CaptureTheFlag => "capture_the_flag",
            GameModeConfig.GameModeType.KingOfTheHill => "king_of_the_hill",
            GameModeConfig.GameModeType.Headquarters => "headquarters",
            _ => modeType.ToString().ToLowerInvariant(),
        };
    }

    private void TryStartClient() {
        var started = _networking.BeginDirectClientConnection(ClientAddress, ClientPort);
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.Lifecycle, "ClientConnectStartResult", $"started={started} target={ClientAddress}:{ClientPort}");
    }

    private void BuildItemMenu() {
        _itemMenuPanel = new PanelContainer {
            Name = "ItemMenuPanel",
            Visible = false,
            OffsetLeft = 220.0f,
            OffsetTop = 80.0f,
            OffsetRight = 720.0f,
            OffsetBottom = 500.0f,
        };
        _canvasLayer.AddChild(_itemMenuPanel);

        var margin = new MarginContainer { Name = "ItemMenuMargin" };
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        _itemMenuPanel.AddChild(margin);

        var layout = new VBoxContainer { Name = "ItemMenuLayout" };
        layout.AddThemeConstantOverride("separation", 10);
        margin.AddChild(layout);

        var title = new Label {
            Name = "ItemMenuTitle",
            Text = "Select Test Item",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 20);
        layout.AddChild(title);

        _itemMenuGrid = new GridContainer {
            Name = "ItemMenuGrid",
            Columns = ItemMenuColumns,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _itemMenuGrid.AddThemeConstantOverride("h_separation", 8);
        _itemMenuGrid.AddThemeConstantOverride("v_separation", 8);
        layout.AddChild(_itemMenuGrid);

        foreach (var itemId in ModernItemIds)
            AddItemMenuButton(itemId);

        foreach (var armorId in ModernArmorIds)
            AddArmorMenuButton(armorId);
    }

    private void BuildLocalPlayersHud() {
        if (_localPlayersHud != null && IsInstanceValid(_localPlayersHud))
            return;

        _localPlayersHud = _localPlayersHudScene?.Instantiate<LocalPlayersHud>() ?? new LocalPlayersHud();
        _localPlayersHud.Name = "LocalPlayersHud";
        _canvasLayer.AddChild(_localPlayersHud);
    }

    private void AddItemMenuButton(string itemId) {
        var item = LoadItem(itemId);
        var button = new Button {
            Name = $"ItemButton_{itemId}",
            Text = item?.DisplayName ?? itemId,
            Icon = item?.GetShowcaseTexture(),
            ExpandIcon = true,
            CustomMinimumSize = new Vector2(150.0f, 70.0f),
            FocusMode = Control.FocusModeEnum.All,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        button.AddThemeConstantOverride("icon_max_width", 34);
        button.Pressed += () => SelectItemFromMenu(itemId);
        _itemMenuButtonsById[itemId] = button;
        _itemMenuGrid.AddChild(button);
    }

    private void AddArmorMenuButton(string armorId) {
        var armor = LoadArmor(armorId);
        var button = new Button {
            Name = $"ArmorButton_{armorId}",
            Text = armor?.DisplayName ?? armorId,
            Icon = armor?.GetShowcaseTexture(),
            ExpandIcon = true,
            CustomMinimumSize = new Vector2(150.0f, 70.0f),
            FocusMode = Control.FocusModeEnum.All,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        button.AddThemeConstantOverride("icon_max_width", 34);
        button.Pressed += () => SelectArmorFromMenu(armorId);
        _itemMenuGrid.AddChild(button);
    }

    private void SelectItemFromMenu(string itemId) {
        ApplyLocalItemSelection(itemId);
        SuppressLocalItemUseUntilReleased();
        CloseItemMenu();
    }

    private void SelectArmorFromMenu(string armorId) {
        ApplyLocalArmorSelection(armorId);
        SuppressLocalItemUseUntilReleased();
        CloseItemMenu();
    }

    private void SuppressLocalItemUseUntilReleased() {
        foreach (var playerEntry in _playersByGlobalId) {
            var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(playerEntry.Key);
            if (playerData != null && playerData.IsLocalPlayer)
                _suppressUseUntilReleasedByGlobalId[playerEntry.Key] = true;
        }
    }

    private void ToggleItemMenu() {
        if (_itemMenuPanel != null && _itemMenuPanel.Visible)
            CloseItemMenu();
        else
            OpenItemMenu();
    }

    private void OpenItemMenu() {
        if (_itemMenuPanel == null)
            return;

        _itemMenuPanel.Visible = true;
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.UI, "ItemMenuOpened");
        SetLocalPlayersControlState(PlayerControlState.Menu);
        var focusItemId = GetFirstLocalPlayerItemId();
        if (focusItemId != string.Empty && _itemMenuButtonsById.TryGetValue(focusItemId, out var selectedButton))
            selectedButton.GrabFocus();
        else if (_itemMenuGrid.GetChildCount() > 0 && _itemMenuGrid.GetChild(0) is Button firstButton)
            firstButton.GrabFocus();
    }

    private void CloseItemMenu() {
        if (_itemMenuPanel == null)
            return;

        _itemMenuPanel.Visible = false;
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.UI, "ItemMenuClosed");
        SetLocalPlayersControlState(PlayerControlState.Gameplay);
    }

    private void SetLocalPlayersControlState(PlayerControlState controlState) {
        foreach (var playerEntry in _playersByGlobalId) {
            var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(playerEntry.Key);
            if (playerData == null || !playerData.IsLocalPlayer)
                continue;

            var player = playerEntry.Value;
            if (!IsInstanceValid(player))
                continue;

            player.SetControlState(controlState);
            if (controlState == PlayerControlState.Menu)
                StopLocalPlayerGameplayInput(playerEntry.Key);
        }
    }

    private void StopLocalPlayerGameplayInput(int globalId) {
        var noInputState = GetNoInputState();
        _aimStrengthByGlobalId[globalId] = 1.0f;
        ApplyLocalMovementStateChange(globalId, Vector2.Zero, noInputState);
        ApplyLocalAimStateChange(globalId, noInputState, false);
    }

    private string GetFirstLocalPlayerItemId() {
        foreach (var playerEntry in _playersByGlobalId) {
            var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(playerEntry.Key);
            if (playerData == null || !playerData.IsLocalPlayer)
                continue;

            return _itemsByGlobalId.TryGetValue(playerEntry.Key, out var item) && item != null
                ? item.ItemId
                : DefaultItemId;
        }

        return string.Empty;
    }

    private void ApplyCommandLineOverrides() {
        var arguments = OS.GetCmdlineUserArgs();
        for (var i = 0; i < arguments.Length; i++) {
            var argument = arguments[i];
            if (argument == "--address" && TryGetNextArgument(arguments, ref i, out var addressValue)) {
                ClientAddress = addressValue;
                continue;
            }

            if (argument.StartsWith("--address=")) {
                ClientAddress = argument[10..];
                continue;
            }

            if (argument == "--port" && TryGetNextArgument(arguments, ref i, out var portValue)) {
                ApplyPortOverride(portValue);
                continue;
            }

            if (argument.StartsWith("--port="))
                ApplyPortOverride(argument[7..]);
        }
    }

    private void EnsureDefaultNetworkMode() {
        if (!_networking.HasSelectedMode) {
            _networking.SetLan();
            GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.StateChange, "DefaultNetworkModeSelected", "mode=Lan");
        }
    }

    private void EnsureTestLocalLobbyPlayer() {
        _networking.LocalLobbyData.LocalPlayers.Clear();
        var inputType = LocalPlayerData.LocalInputType.KeyboardMouse;
        var deviceId = -1;

        _networking.LocalLobbyData.LocalPlayers.Add(new LocalPlayerData {
            LocalId = 0,
            IsActive = true,
            InputType = inputType,
            DeviceId = deviceId,
            DisplayName = _networking.IsClient ? "Client Player" : "Host Player",
        });
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.StateChange, "TestLocalLobbyPlayerReady", $"input={inputType} device={deviceId}");
    }

    private void ApplyPortOverride(string portValue) {
        if (int.TryParse(portValue, out var parsedPort) && parsedPort > 0 && parsedPort <= 65535)
            ClientPort = parsedPort;
    }

    private static bool TryGetNextArgument(string[] arguments, ref int index, out string value) {
        value = string.Empty;
        if (index + 1 >= arguments.Length)
            return false;

        index++;
        value = arguments[index];
        return true;
    }

    private void BuildTestRoom() {
        _arenaMapData = new ArenaMapData {
            SourceId = 0,
            WallDamageSourceId = 1,
            DefaultWallMaxDamage = WallDamageData.DefaultWallHealth,
            DefaultWallBiome = BiomeConfig.BiomeType.Arena,
        };

        GenerateStructureLayout();
        RenderArenaWithCollision();
        BuildTeamSpawnBaseMarkers();
        BuildNeutralObjective();
        BuildItemSpawnMarkers();
        BuildCenterProp();
        RebuildPlayersFromNetworkData();
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.Lifecycle, "ArenaMatchBuilt", $"floorTiles={_arenaMapData.FloorTiles.Count} wallTiles={_arenaMapData.WallTiles.Count}");
    }

    private void GenerateStructureLayout() {
        _spawnManager.Clear();
        _mapGeneratorController ??= new MapGeneratorController();
        var mapConfig = GetMatchMapGenerationConfig();
        var structureType = GetSelectedStructureType(mapConfig);
        _structureGenerationData = _mapGeneratorController.GenerateStructure(mapConfig, structureType);
        _structureGenerationData.ApplyToArenaMap(_arenaMapData);

        ApplyGeneratedTeamSpawns();
        _spawnManager.SetItemSpawnTiles(ToArray(_structureGenerationData.GetSpawnTiles(StructureGenerationData.SpawnPointType.ItemSpawn)));
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.StateChange, "StructureGenerated", $"structure={structureType} seed={_structureGenerationData.Seed}");
    }

    private MapGenerationConfig GetMatchMapGenerationConfig() {
        var mapConfig = _networking?.MultiplayerData?.SetupConfig?.MapConfig?.Clone() ?? new MapGenerationConfig();
        if (ForceTestSetupOverrides) {
            mapConfig.EnabledStructureTypes.Clear();
            mapConfig.EnabledStructureTypes.Add(MapGenerationConfig.StructureType.Square);
            mapConfig.SelectedSeedMode = MapGenerationConfig.SeedMode.FixedSeed;
            mapConfig.FixedSeed = MapSeedOverride;
        }

        return mapConfig;
    }

    private static MapGenerationConfig.StructureType GetSelectedStructureType(MapGenerationConfig mapConfig) {
        if (mapConfig?.EnabledStructureTypes.Count > 0)
            return mapConfig.EnabledStructureTypes[0];

        return MapGenerationConfig.StructureType.Arena;
    }

    private void ApplyGeneratedTeamSpawns() {
        foreach (var teamId in _structureGenerationData.GetTeamIds())
            _spawnManager.SetSpawnTiles(teamId, ToArray(_structureGenerationData.GetTeamSpawnTiles(teamId)));
    }

    private void BuildCenterProp() {
        if (_centerProp != null && IsInstanceValid(_centerProp))
            _centerProp.QueueFree();

        var propData = new LevelPropData();
        propData.Configure(LevelPropType.Barrel);
        _centerProp = new LevelProp { Name = "CenterBarrel" };
        AddChild(_centerProp);
        _centerProp.Initialize(propData, TileToWorldCenter(GetObjectiveTilePosition()));
    }

    private void BuildTeamSpawnBaseMarkers() {
        foreach (var marker in _teamSpawnBaseMarkers) {
            if (IsInstanceValid(marker))
                marker.QueueFree();
        }

        _teamSpawnBaseMarkers.Clear();
        foreach (var teamId in _structureGenerationData.GetTeamIds()) {
            var teamPlayerCount = GetTeamPlayerCount(teamId);
            if (teamPlayerCount <= 0)
                continue;

            var marker = _teamSpawnBaseMarkerScene?.Instantiate<TeamSpawnBaseMarker>() ?? new TeamSpawnBaseMarker();
            marker.Name = $"Team{teamId}SpawnBaseMarker";
            marker.ZIndex = 7;
            marker.Configure(teamId, teamPlayerCount, TeamSpawnRadius, TeamObjectiveRadius);
            AddChild(marker);
            marker.GlobalPosition = TileToWorldCenter(_structureGenerationData.GetTeamObjectiveTile(teamId));
            _teamSpawnBaseMarkers.Add(marker);
        }
    }

    private int GetTeamPlayerCount(int teamId) {
        var playerCount = 0;
        foreach (var playerData in _networking.MultiplayerData.Players) {
            if (playerData.GlobalId >= 0 && TryGetBackendTeamId(playerData, out var playerTeamId, "team-player-count") && playerTeamId == teamId)
                playerCount++;
        }

        return Mathf.Clamp(playerCount, 0, 4);
    }

    private void BuildNeutralObjective() {
        if (_neutralObjective != null && IsInstanceValid(_neutralObjective))
            _neutralObjective.QueueFree();

        _neutralObjective = _neutralObjectiveScene?.Instantiate<NeutralObjective>() ?? new NeutralObjective();
        _neutralObjective.Name = "NeutralCenterObjective";
        _neutralObjective.ZIndex = 5;
        _neutralObjective.Configure(NeutralObjectiveRadius, TeamObjectiveRadius);
        AddChild(_neutralObjective);
        _neutralObjective.GlobalPosition = TileToWorldCenter(GetObjectiveTilePosition());
        _neutralObjective.SetState(-1, false);
    }

    private static Vector2I[] ToArray(IReadOnlyList<Vector2I> tiles) {
        if (tiles == null || tiles.Count == 0)
            return Array.Empty<Vector2I>();

        var result = new Vector2I[tiles.Count];
        for (var i = 0; i < tiles.Count; i++)
            result[i] = tiles[i];

        return result;
    }

    private void BuildItemSpawnMarkers() {
        foreach (var marker in _itemSpawnMarkers) {
            if (IsInstanceValid(marker))
                marker.QueueFree();
        }

        _itemSpawnMarkers.Clear();
        foreach (var itemSpawnTile in _spawnManager.GetItemSpawnTiles()) {
            var marker = new GameplaySpawnMarker { Name = "ItemSpawnMarker", ZIndex = 6 };
            AddChild(marker);
            marker.GlobalPosition = TileToWorldCenter(itemSpawnTile);
            _itemSpawnMarkers.Add(marker);
        }
    }

    private void RenderArenaWithCollision() {
        _tileLayerRenderer.Render(_arenaMapData);
    }

    private void SyncPlayersWithNetworkData() {
        var activeGlobalIds = new HashSet<int>();
        foreach (var playerData in _networking.MultiplayerData.Players) {
            if (playerData.GlobalId < 0)
                continue;

            activeGlobalIds.Add(playerData.GlobalId);
            if (!TryGetBackendTeamId(playerData, out var teamId, "sync-players"))
                continue;

            if (_playersByGlobalId.TryGetValue(playerData.GlobalId, out var existingPlayer) && IsInstanceValid(existingPlayer)) {
                UpdatePlayerTeamSpawn(playerData.GlobalId, teamId, existingPlayer);
                continue;
            }

            AddPlayer(playerData.GlobalId, teamId, GetTestPlayerTilePosition(playerData.GlobalId, teamId));
        }

        var removedGlobalIds = new List<int>();
        foreach (var playerEntry in _playersByGlobalId) {
            if (!activeGlobalIds.Contains(playerEntry.Key))
                removedGlobalIds.Add(playerEntry.Key);
        }

        foreach (var removedGlobalId in removedGlobalIds) {
            if (_playersByGlobalId.TryGetValue(removedGlobalId, out var player) && IsInstanceValid(player))
                player.QueueFree();

            _playersByGlobalId.Remove(removedGlobalId);
            GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.Despawn, "PlayerRemoved", $"global={removedGlobalId}");
            _movementStatesByGlobalId.Remove(removedGlobalId);
            _aimStatesByGlobalId.Remove(removedGlobalId);
            _lastLocalMovementStatesByGlobalId.Remove(removedGlobalId);
            _lastLocalAimStatesByGlobalId.Remove(removedGlobalId);
            _isAimingByGlobalId.Remove(removedGlobalId);
            _lastLocalIsAimingByGlobalId.Remove(removedGlobalId);
            _itemsByGlobalId.Remove(removedGlobalId);
            _loadoutsByGlobalId.Remove(removedGlobalId);
            _accuracyStatesByGlobalId.Remove(removedGlobalId);
            _aimStrengthByGlobalId.Remove(removedGlobalId);
            _itemRecoverySecondsByGlobalId.Remove(removedGlobalId);
            _wasUseHeldByGlobalId.Remove(removedGlobalId);
            _suppressUseUntilReleasedByGlobalId.Remove(removedGlobalId);
            _respawnSecondsByGlobalId.Remove(removedGlobalId);
            _spawnSecondsByGlobalId.Remove(removedGlobalId);
            _playerTeamIdsByGlobalId.Remove(removedGlobalId);
        }
    }

    private void RebuildPlayersFromNetworkData() {
        ClearPlayers();
        SyncPlayersWithNetworkData();
    }

    private void ClearPlayers() {
        foreach (var player in _playersByGlobalId.Values) {
            if (IsInstanceValid(player))
                player.QueueFree();
        }

        _playersByGlobalId.Clear();
        _movementStatesByGlobalId.Clear();
        _aimStatesByGlobalId.Clear();
        _lastLocalMovementStatesByGlobalId.Clear();
        _lastLocalAimStatesByGlobalId.Clear();
        _isAimingByGlobalId.Clear();
        _lastLocalIsAimingByGlobalId.Clear();
        _itemsByGlobalId.Clear();
        _loadoutsByGlobalId.Clear();
        _accuracyStatesByGlobalId.Clear();
        _aimStrengthByGlobalId.Clear();
        _itemRecoverySecondsByGlobalId.Clear();
        _wasUseHeldByGlobalId.Clear();
        _suppressUseUntilReleasedByGlobalId.Clear();
        _respawnSecondsByGlobalId.Clear();
        _spawnSecondsByGlobalId.Clear();
        _playerTeamIdsByGlobalId.Clear();
    }

    private void AddPlayer(int globalId, int teamId, Vector2I tilePosition) {
        var player = new DamageTestPlayer { Name = $"ItemTestPlayer{globalId}" };
        AddChild(player);
        player.Initialize(globalId, TileToWorldCenter(tilePosition));
        _playersByGlobalId[globalId] = player;
        _playerTeamIdsByGlobalId[globalId] = teamId;
        _movementStatesByGlobalId[globalId] = GetNoInputState();
        _aimStatesByGlobalId[globalId] = new QuantizedInputState(0, InputStrength.Full);
        _isAimingByGlobalId[globalId] = false;
        _aimStrengthByGlobalId[globalId] = 1.0f;
        _itemRecoverySecondsByGlobalId[globalId] = 0.0;
        _wasUseHeldByGlobalId[globalId] = false;
        _suppressUseUntilReleasedByGlobalId[globalId] = false;
        _respawnSecondsByGlobalId[globalId] = -1.0f;
        _spawnSecondsByGlobalId[globalId] = 0.0f;
        _accuracyStatesByGlobalId[globalId] = new PlayerItemAccuracyState();
        _loadoutsByGlobalId[globalId] = new PlayerLoadoutState();
        SetPlayerItem(globalId, DefaultItemId);
        SetPlayerTeamColor(globalId);
        SetPlayerLocalMarker(globalId);
        player.SetEstimatedAimDirection(DirectionIndexToVector(0), true);
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.Spawn, "PlayerAdded", $"global={globalId} team={teamId} spawnIndex={GetTeamPlayerIndex(globalId, teamId)} tile={tilePosition} world={player.GlobalPosition}");
    }

    private void UpdatePlayerTeamSpawn(int globalId, int teamId, DamageTestPlayer player) {
        if (_playerTeamIdsByGlobalId.TryGetValue(globalId, out var previousTeamId) && previousTeamId == teamId)
            return;

        var spawnTile = GetTestPlayerTilePosition(globalId, teamId);
        var spawnPosition = TileToWorldCenter(spawnTile);
        player.SetSyncedPosition(spawnPosition);
        _playerTeamIdsByGlobalId[globalId] = teamId;
        SetPlayerTeamColor(globalId);
        SetPlayerLocalMarker(globalId);
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.Spawn, "PlayerTeamSpawnUpdated", $"global={globalId} team={teamId} spawnIndex={GetTeamPlayerIndex(globalId, teamId)} tile={spawnTile} world={spawnPosition}");
    }

    private void ProcessLocalPlayerInputStates() {
        foreach (var playerEntry in _playersByGlobalId) {
            var player = playerEntry.Value;
            if (!IsInstanceValid(player) || player.IsDead())
                continue;

            var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(playerEntry.Key);
            if (playerData == null || !playerData.IsLocalPlayer)
                continue;

            var localPlayerData = GetLocalPlayerData(playerData.LocalId);
            if (localPlayerData == null)
                continue;

            if (!player.CanProcessMovementInput && !player.CanProcessAimInput) {
                StopLocalPlayerGameplayInput(playerEntry.Key);
                continue;
            }

            _lastLocalMovementStatesByGlobalId.TryGetValue(playerEntry.Key, out var previousMovementState);
            _lastLocalAimStatesByGlobalId.TryGetValue(playerEntry.Key, out var previousAimState);

            var inputVectors = GetLocalInputVectors(localPlayerData, player);
            var movementVector = player.CanProcessMovementInput ? inputVectors.Movement : Vector2.Zero;
            var aimVector = player.CanProcessAimInput ? inputVectors.Aim : Vector2.Zero;
            var lockedInputVectors = new LocalInputVectors(
                movementVector,
                aimVector,
                player.CanProcessAimInput && inputVectors.AimFallsBackToMovement,
                player.CanProcessAimInput && inputVectors.IsAiming);
            var movementState = QuantizeInput(lockedInputVectors.Movement, previousMovementState);
            var aimState = GetAimState(lockedInputVectors, movementState, previousAimState);
            _aimStrengthByGlobalId[playerEntry.Key] = GetAimStrength(lockedInputVectors, movementState, localPlayerData);
            ApplyLocalAimDisplay(player, lockedInputVectors, aimState);
            ApplyLocalMovementStateChange(playerEntry.Key, lockedInputVectors.Movement, movementState);
            ApplyLocalAimStateChange(playerEntry.Key, aimState, lockedInputVectors.IsAiming);
        }
    }

    private void UpdateAccuracyStates(double delta) {
        foreach (var accuracyStateEntry in _accuracyStatesByGlobalId) {
            var movementStrength = GetMovementStrength(accuracyStateEntry.Key);
            accuracyStateEntry.Value.Update(movementStrength, delta);
        }
    }

    private void ApplyLocalAimDisplay(DamageTestPlayer player, LocalInputVectors inputVectors, QuantizedInputState aimState) {
        if (inputVectors.Aim.Length() >= GamepadDeadzone) {
            player.SetLocalAimDirection(inputVectors.Aim, true);
            return;
        }

        if (inputVectors.AimFallsBackToMovement && aimState.HasInput) {
            player.SetLocalAimDirection(DirectionIndexToVector(aimState.DirectionIndex), true);
            return;
        }

        player.SetLocalAimDirection(Vector2.Zero, false);
    }

    private void ApplyLocalMovementStateChange(int globalId, Vector2 movementVector, QuantizedInputState movementState) {
        if (_lastLocalMovementStatesByGlobalId.TryGetValue(globalId, out var lastState) && lastState.Equals(movementState))
            return;

        _lastLocalMovementStatesByGlobalId[globalId] = movementState;

        if (_networking.IsClient && _networking.HasActiveNetworkPeer) {
            GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcSend, "RpcRequestSetPlayerMovementVector", $"global={globalId} vector=({movementVector.X:0.000},{movementVector.Y:0.000}) state={FormatInputState(movementState)}");
            RpcId(1, nameof(RpcRequestSetPlayerMovementVector), globalId, movementVector.X, movementVector.Y);
        }
        else if (CanSendHostRpc()) {
            GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.Movement, "ApplyHostMovementState", $"global={globalId} state={FormatInputState(movementState)}");
            SetPlayerMovementState(globalId, movementState, false);
            SyncPlayerMovementState(globalId, movementState, true);
        }
        else {
            GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.Movement, "ApplyLocalMovementState", $"global={globalId} state={FormatInputState(movementState)}");
            SetPlayerMovementState(globalId, movementState, false);
        }
    }

    private void ApplyLocalAimStateChange(int globalId, QuantizedInputState aimState, bool isAiming) {
        if (_lastLocalAimStatesByGlobalId.TryGetValue(globalId, out var lastState)
            && lastState.Equals(aimState)
            && _lastLocalIsAimingByGlobalId.TryGetValue(globalId, out var lastIsAiming)
            && lastIsAiming == isAiming)
            return;

        _lastLocalAimStatesByGlobalId[globalId] = aimState;
        _lastLocalIsAimingByGlobalId[globalId] = isAiming;
        SetPlayerAimState(globalId, aimState, isAiming, false);

        if (_networking.IsClient && _networking.HasActiveNetworkPeer) {
            GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcSend, "RpcRequestSetPlayerAimState", $"global={globalId} state={FormatInputState(aimState)} aiming={isAiming}");
            RpcId(1, nameof(RpcRequestSetPlayerAimState), globalId, aimState.DirectionIndex, (int)aimState.Strength, isAiming);
        }
        else if (CanSendHostRpc())
            SyncPlayerAimState(globalId, aimState, isAiming);
    }

    private void SimulatePlayerMovement(double delta) {
        foreach (var playerEntry in _playersByGlobalId) {
            var player = playerEntry.Value;
            if (!IsInstanceValid(player) || player.IsDead())
                continue;

            if (!_movementStatesByGlobalId.TryGetValue(playerEntry.Key, out var movementState) || !movementState.HasInput)
                continue;

            var speedMultiplier = movementState.Strength == InputStrength.Full ? 1.0f : 0.5f;
            if (_isAimingByGlobalId.TryGetValue(playerEntry.Key, out var isAiming) && isAiming)
                speedMultiplier *= GetAimMoveSpeedMultiplier(playerEntry.Key);

            player.MoveWithVelocity(DirectionIndexToVector(movementState.DirectionIndex) * TestPlayerMoveSpeed * speedMultiplier);
        }
    }

    private float GetAimMoveSpeedMultiplier(int globalId) {
        return _itemsByGlobalId.TryGetValue(globalId, out var item) && item is IPlayerUsable usable
            ? usable.AimMoveSpeedMultiplier
            : 0.9f;
    }

    private void SetPlayerMovementState(int globalId, QuantizedInputState movementState, bool forcePositionSync, float worldX = 0.0f, float worldY = 0.0f) {
        _movementStatesByGlobalId[globalId] = movementState;

        if (forcePositionSync && _playersByGlobalId.TryGetValue(globalId, out var player) && IsInstanceValid(player))
            player.SetSyncedPosition(new Vector2(worldX, worldY));
    }

    private void SetPlayerAimState(int globalId, QuantizedInputState aimState, bool isAiming, bool syncToPlayer) {
        _aimStatesByGlobalId[globalId] = aimState;
        _isAimingByGlobalId[globalId] = isAiming;

        if (!_playersByGlobalId.TryGetValue(globalId, out var player) || !IsInstanceValid(player))
            return;

        if (!aimState.HasInput) {
            player.SetEstimatedAimDirection(GetAimDirection(globalId), false);
            return;
        }

        player.SetEstimatedAimDirection(DirectionIndexToVector(aimState.DirectionIndex), true);
    }

    private void SyncPlayerMovementState(int globalId, QuantizedInputState movementState, bool includePosition) {
        if (!CanSendHostRpc())
            return;

        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcSend, "RpcSyncPlayerMovementState", $"global={globalId} state={FormatInputState(movementState)} includePosition={includePosition}");
        Rpc(
            nameof(RpcSyncPlayerMovementState),
            globalId,
            movementState.DirectionIndex,
            (int)movementState.Strength,
            GetPlayerPositionX(globalId),
            GetPlayerPositionY(globalId),
            includePosition);
    }

    private void SyncMovingPlayerPositions() {
        if (!CanSendHostRpc())
            return;

        foreach (var playerEntry in _playersByGlobalId) {
            if (!_movementStatesByGlobalId.TryGetValue(playerEntry.Key, out var movementState) || !movementState.HasInput)
                continue;

            var player = playerEntry.Value;
            if (IsInstanceValid(player))
                Rpc(nameof(RpcSyncPlayerPosition), playerEntry.Key, player.GlobalPosition.X, player.GlobalPosition.Y);
        }
    }

    private void SyncPlayerAimState(int globalId, QuantizedInputState aimState, bool isAiming) {
        if (!CanSendHostRpc())
            return;

        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcSend, "RpcSyncPlayerAimState", $"global={globalId} state={FormatInputState(aimState)} aiming={isAiming}");
        Rpc(nameof(RpcSyncPlayerAimState), globalId, aimState.DirectionIndex, (int)aimState.Strength, isAiming);
    }

    private void ApplyLocalItemSelection(string itemId) {
        foreach (var playerEntry in _playersByGlobalId) {
            var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(playerEntry.Key);
            if (playerData == null || !playerData.IsLocalPlayer)
                continue;

            if (_networking.IsClient && _networking.HasActiveNetworkPeer) {
                GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcSend, "RpcRequestSetPlayerItem", $"global={playerEntry.Key} item={itemId}");
                RpcId(1, nameof(RpcRequestSetPlayerItem), playerEntry.Key, itemId);
            }
            else if (CanSendHostRpc()) {
                SetPlayerItem(playerEntry.Key, itemId);
                SyncPlayerItem(playerEntry.Key, itemId);
            }
            else {
                SetPlayerItem(playerEntry.Key, itemId);
            }

            return;
        }
    }

    private void ApplyLocalArmorSelection(string armorId) {
        foreach (var playerEntry in _playersByGlobalId) {
            var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(playerEntry.Key);
            if (playerData == null || !playerData.IsLocalPlayer)
                continue;

            if (_networking.IsClient && _networking.HasActiveNetworkPeer) {
                GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcSend, "RpcRequestSetPlayerArmor", $"global={playerEntry.Key} armor={armorId}");
                RpcId(1, nameof(RpcRequestSetPlayerArmor), playerEntry.Key, armorId);
            }
            else if (CanSendHostRpc()) {
                SetPlayerArmor(playerEntry.Key, armorId);
                SyncPlayerArmor(playerEntry.Key, armorId);
            }
            else {
                SetPlayerArmor(playerEntry.Key, armorId);
            }

            return;
        }
    }

    private void SetPlayerItem(int globalId, string itemId) {
        var item = LoadItem(itemId) ?? LoadItem(DefaultItemId);
        if (item == null)
            return;

        var loadout = GetOrCreateLoadout(globalId);
        if (!loadout.EquipItem(item)) {
            GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.Validation, "EquipItemRejected", $"global={globalId} item={item.ItemId} reason=noArmorCapacity");
            return;
        }

        _itemsByGlobalId[globalId] = item;
        if (!_accuracyStatesByGlobalId.TryGetValue(globalId, out var accuracyState)) {
            accuracyState = new PlayerItemAccuracyState();
            _accuracyStatesByGlobalId[globalId] = accuracyState;
        }

        if (item is IPlayerUsable usable)
            accuracyState.SetItem(usable);
        _itemRecoverySecondsByGlobalId[globalId] = 0.0;
        _wasUseHeldByGlobalId[globalId] = false;
        if (_playersByGlobalId.TryGetValue(globalId, out var player) && IsInstanceValid(player))
            player.SetHeldTexture(item.HeldTexture);

        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.ItemEquip, "PlayerItemEquipped", $"global={globalId} item={item.ItemId} name={item.DisplayName} {loadout.GetLoadoutText()}");
        UpdateStatusLabel();
    }

    private void SyncPlayerItem(int globalId, string itemId) {
        if (CanSendHostRpc()) {
            GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcSend, "RpcSyncPlayerItem", $"global={globalId} item={itemId}");
            Rpc(nameof(RpcSyncPlayerItem), globalId, itemId);
        }
    }

    private void SetPlayerArmor(int globalId, string armorId) {
        var armor = LoadArmor(armorId);
        if (armor == null)
            return;

        var loadout = GetOrCreateLoadout(globalId);
        loadout.EquipArmor(armor);
        var selectedItem = loadout.SelectedItem ?? LoadItem(DefaultItemId);
        if (selectedItem != null) {
            _itemsByGlobalId[globalId] = selectedItem;
            if (_accuracyStatesByGlobalId.TryGetValue(globalId, out var accuracyState) && selectedItem is IPlayerUsable selectedUsable)
                accuracyState.SetItem(selectedUsable);
            if (_playersByGlobalId.TryGetValue(globalId, out var existingPlayer) && IsInstanceValid(existingPlayer))
                existingPlayer.SetHeldTexture(selectedItem.HeldTexture);
        }

        if (_playersByGlobalId.TryGetValue(globalId, out var player) && IsInstanceValid(player))
            player.SetArmorTexture(armor.HeldTexture);

        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.ItemEquip, "PlayerArmorEquipped", $"global={globalId} armor={armor.ItemId} name={armor.DisplayName} {loadout.GetLoadoutText()}");
        UpdateStatusLabel();
    }

    private void SetPlayerTeamColor(int globalId) {
        if (!_playersByGlobalId.TryGetValue(globalId, out var player) || !IsInstanceValid(player))
            return;

        var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(globalId);
        if (!TryGetBackendTeamId(playerData, out var backendTeamId, "set-player-team-color"))
            return;

        player.SetTeamColor(TeamVisuals.GetTeamColor(GetPaletteTeamId(backendTeamId)));
    }

    private void SetPlayerLocalMarker(int globalId) {
        if (!_playersByGlobalId.TryGetValue(globalId, out var player) || !IsInstanceValid(player))
            return;

        var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(globalId);
        player.SetLocalPlayerMarker(playerData?.IsLocalPlayer == true, playerData?.LocalId ?? -1);
    }

    private void ResetPlayerUsesToMax(int globalId) {
        GetOrCreateLoadout(globalId).ResetUsesToMax();
        UpdateStatusLabel();
    }

    private PlayerLoadoutState GetOrCreateLoadout(int globalId) {
        if (!_loadoutsByGlobalId.TryGetValue(globalId, out var loadout)) {
            loadout = new PlayerLoadoutState();
            _loadoutsByGlobalId[globalId] = loadout;
        }

        return loadout;
    }

    private void SyncPlayerArmor(int globalId, string armorId) {
        if (CanSendHostRpc()) {
            GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcSend, "RpcSyncPlayerArmor", $"global={globalId} armor={armorId}");
            Rpc(nameof(RpcSyncPlayerArmor), globalId, armorId);
        }
    }

    private PlayerItem LoadItem(string itemId) {
        if (_loadedItemsById.TryGetValue(itemId, out var loadedItem))
            return loadedItem;

        if (!ItemResourcePaths.TryGetValue(itemId, out var itemPath))
            return null;

        var item = GD.Load<PlayerItem>(itemPath);
        if (item != null)
            _loadedItemsById[itemId] = item;

        return item;
    }

    private PlayerArmor LoadArmor(string armorId) {
        if (_loadedArmorById.TryGetValue(armorId, out var loadedArmor))
            return loadedArmor;

        if (!ArmorResourcePaths.TryGetValue(armorId, out var armorPath))
            return null;

        var armor = GD.Load<PlayerArmor>(armorPath);
        if (armor != null)
            _loadedArmorById[armorId] = armor;

        return armor;
    }

    private void ProcessLocalItemUse(double delta) {
        foreach (var playerEntry in _playersByGlobalId) {
            var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(playerEntry.Key);
            if (playerData == null || !playerData.IsLocalPlayer)
                continue;

            ProcessLocalPlayerItemUse(playerEntry.Key);
        }
    }

    private void UpdateItemRecoveries(double delta) {
        foreach (var globalId in _playersByGlobalId.Keys) {
            _itemRecoverySecondsByGlobalId[globalId] = Mathf.Max(
                _itemRecoverySecondsByGlobalId.TryGetValue(globalId, out var recoverySeconds) ? recoverySeconds - delta : 0.0,
                0.0);
        }
    }

    private void UpdateRespawns(double delta) {
        if (_networking == null || (!_networking.IsServer && !_networking.IsLocal))
            return;

        UpdateTeamWipeState();

        foreach (var playerEntry in _playersByGlobalId) {
            var globalId = playerEntry.Key;
            var player = playerEntry.Value;
            if (!IsInstanceValid(player))
                continue;

            if (player.IsDead()) {
                var respawnSeconds = _respawnSecondsByGlobalId.TryGetValue(globalId, out var currentRespawnSeconds) && currentRespawnSeconds >= 0.0f
                    ? currentRespawnSeconds
                    : RespawnDelaySeconds;
                respawnSeconds -= (float)delta;
                _respawnSecondsByGlobalId[globalId] = respawnSeconds;
                if (respawnSeconds <= 0.0f)
                    ResetPlayerForRespawn(globalId, true);
                continue;
            }

            _respawnSecondsByGlobalId[globalId] = -1.0f;
            if (!_spawnSecondsByGlobalId.TryGetValue(globalId, out var spawnSeconds) || spawnSeconds <= 0.0f)
                continue;

            spawnSeconds -= (float)delta;
            _spawnSecondsByGlobalId[globalId] = spawnSeconds;
            if (spawnSeconds <= 0.0f)
                FinishPlayerSpawn(globalId, true);
        }
    }

    private void ResetPlayerForRespawn(int globalId, bool syncToPeers) {
        if (!_playersByGlobalId.TryGetValue(globalId, out var player) || !IsInstanceValid(player))
            return;

        var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(globalId);
        if (!TryGetBackendTeamId(playerData, out var teamId, "respawn"))
            return;

        var spawnTile = GetTestPlayerTilePosition(globalId, teamId);
        var spawnPosition = TileToWorldCenter(spawnTile);
        player.BeginSpawn(spawnPosition);
        player.SetEstimatedAimDirection(DirectionIndexToVector(0), true);
        _movementStatesByGlobalId[globalId] = GetNoInputState();
        _aimStatesByGlobalId[globalId] = new QuantizedInputState(0, InputStrength.Full);
        _isAimingByGlobalId[globalId] = false;
        _itemRecoverySecondsByGlobalId[globalId] = 0.0;
        _wasUseHeldByGlobalId[globalId] = false;
        _suppressUseUntilReleasedByGlobalId[globalId] = true;
        _respawnSecondsByGlobalId[globalId] = -1.0f;
        _spawnSecondsByGlobalId[globalId] = SpawnImmobilizeSeconds;
        if (_loadoutsByGlobalId.TryGetValue(globalId, out var loadout))
            loadout.ResetUsesToMax();

        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.Spawn, "PlayerRespawnStarted", $"global={globalId} tile={spawnTile} world={spawnPosition}");
        if (syncToPeers)
            SyncPlayerRespawn(globalId, spawnPosition, SpawnImmobilizeSeconds);
    }

    private void FinishPlayerSpawn(int globalId, bool syncToPeers) {
        if (!_playersByGlobalId.TryGetValue(globalId, out var player) || !IsInstanceValid(player))
            return;

        player.FinishSpawn();
        _spawnSecondsByGlobalId[globalId] = 0.0f;
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.Spawn, "PlayerSpawnFinished", $"global={globalId}");
        if (syncToPeers)
            SyncPlayerSpawnFinished(globalId);
    }

    private void SyncPlayerRespawn(int globalId, Vector2 spawnPosition, float spawnSeconds) {
        if (CanSendHostRpc())
            Rpc(nameof(RpcSyncPlayerRespawn), globalId, spawnPosition.X, spawnPosition.Y, spawnSeconds);
    }

    private void SyncPlayerSpawnFinished(int globalId) {
        if (CanSendHostRpc())
            Rpc(nameof(RpcSyncPlayerSpawnFinished), globalId);
    }

    private void UpdateTeamWipeState() {
        var teamAliveState = GetTeamAliveState();
        foreach (var teamEntry in teamAliveState) {
            if (teamEntry.Value) {
                _wipedTeamIds.Remove(teamEntry.Key);
                continue;
            }

            if (_wipedTeamIds.Contains(teamEntry.Key))
                continue;

            _wipedTeamIds.Add(teamEntry.Key);
            OnTeamWiped(teamEntry.Key);
        }
    }

    private Dictionary<int, bool> GetTeamAliveState() {
        var teamAliveState = new Dictionary<int, bool>();
        foreach (var playerEntry in _playersByGlobalId) {
            var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(playerEntry.Key);
            if (!TryGetBackendTeamId(playerData, out var teamId, "team-alive-state"))
                continue;

            if (!teamAliveState.ContainsKey(teamId))
                teamAliveState[teamId] = false;

            if (IsInstanceValid(playerEntry.Value) && !playerEntry.Value.IsDead())
                teamAliveState[teamId] = true;
        }

        return teamAliveState;
    }

    private void OnTeamWiped(int teamId) {
        TeamWiped?.Invoke(teamId);
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.StateChange, "TeamWiped", $"team={teamId}");
    }

    private void ProcessLocalPlayerItemUse(int globalId) {
        if (!_itemsByGlobalId.TryGetValue(globalId, out var item) || item is not IPlayerUsable usable)
            return;

        var isUseHeld = IsLocalItemUseHeld(globalId);
        var wasUseHeld = _wasUseHeldByGlobalId.TryGetValue(globalId, out var previousUseHeld) && previousUseHeld;
        _wasUseHeldByGlobalId[globalId] = isUseHeld;

        if (!isUseHeld) {
            return;
        }

        if (_itemRecoverySecondsByGlobalId[globalId] > 0.0)
            return;

        if (wasUseHeld && !IsHeldUseAllowed(item))
            return;

        RequestLocalItemUse(globalId, item);
    }

    private static bool IsHeldUseAllowed(PlayerItem item) {
        return item is PlayerWeapon { IsFullAuto: true };
    }

    private bool IsLocalItemUseHeld(int globalId) {
        if (_itemMenuPanel != null && _itemMenuPanel.Visible)
            return false;

        if (!_playersByGlobalId.TryGetValue(globalId, out var player) || !IsInstanceValid(player) || !player.CanUseItems)
            return false;

        var localPlayerData = GetLocalPlayerDataForGlobalId(globalId);
        if (localPlayerData == null)
            return false;

        var isUseHeld = localPlayerData.InputType switch {
            LocalPlayerData.LocalInputType.KeyboardMouse => Input.IsMouseButtonPressed(MouseButton.Left),
            LocalPlayerData.LocalInputType.Gamepad => Input.GetJoyAxis(localPlayerData.DeviceId, JoyAxis.TriggerRight) >= TriggerUseThreshold,
            _ => false,
        };

        if (!_suppressUseUntilReleasedByGlobalId.TryGetValue(globalId, out var suppressUse) || !suppressUse)
            return isUseHeld;

        if (isUseHeld)
            return false;

        _suppressUseUntilReleasedByGlobalId[globalId] = false;
        return false;
    }

    private void RequestLocalItemUse(int globalId, PlayerItem item) {
        if (!_playersByGlobalId.TryGetValue(globalId, out var player) || !IsInstanceValid(player) || player.IsDead())
            return;

        var loadout = GetOrCreateLoadout(globalId);
        if (loadout.GetMaxUses(item) > 0 && loadout.GetCurrentUses(item) <= 0) {
            GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.Validation, "LocalItemUseRejected", $"global={globalId} item={item.ItemId} reason=empty");
            UpdateStatusLabel();
            return;
        }

        var aimDirection = player.DisplayAimDirection;
        if (aimDirection.LengthSquared() <= 0.0001f)
            aimDirection = GetAimDirection(globalId);
        if (aimDirection.LengthSquared() <= 0.0001f)
            aimDirection = Vector2.Right;

        player.ShowActionAimDirection(aimDirection, ActionAimDisplaySeconds);
        var aimStrength = _aimStrengthByGlobalId.TryGetValue(globalId, out var strength) ? strength : 1.0f;
        if (_networking.IsClient && _networking.HasActiveNetworkPeer) {
            ApplyItemUsePushbackAndRecovery(globalId, item);
            GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcSend, "RpcRequestUsePlayerItem", $"global={globalId} item={item.ItemId} aim=({aimDirection.X:0.000},{aimDirection.Y:0.000}) strength={aimStrength:0.000}");
            RpcId(1, nameof(RpcRequestUsePlayerItem), globalId, aimDirection.X, aimDirection.Y, aimStrength);
        }
        else {
            GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.ItemUse, "ExecuteLocalItemUse", $"global={globalId} item={item.ItemId} aim=({aimDirection.X:0.000},{aimDirection.Y:0.000}) strength={aimStrength:0.000}");
            ExecuteValidatedItemUse(globalId, aimDirection, aimStrength, true);
        }
    }

    private void ApplyItemUsePushbackAndRecovery(int globalId, PlayerItem item) {
        if (item is not IPlayerUsable usable)
            return;

        if (_accuracyStatesByGlobalId.TryGetValue(globalId, out var accuracyState))
            accuracyState.ApplyUsePushback();

        _itemRecoverySecondsByGlobalId[globalId] = Mathf.Max(usable.RecoverySeconds, 0.0f);
    }

    private void ExecuteValidatedItemUse(int globalId, Vector2 aimDirection, float aimStrength, bool syncToPeers) {
        if (!_itemsByGlobalId.TryGetValue(globalId, out var item) || item is not IPlayerUsable usable)
            return;

        if (!_playersByGlobalId.TryGetValue(globalId, out var player) || !IsInstanceValid(player) || player.IsDead())
            return;

        if (aimDirection.LengthSquared() <= 0.0001f)
            aimDirection = GetAimDirection(globalId);
        if (aimDirection.LengthSquared() <= 0.0001f)
            aimDirection = Vector2.Right;

        var normalizedAim = aimDirection.Normalized();
        var startPosition = player.GlobalPosition + (normalizedAim * 10.0f);
        player.ShowActionAimDirection(normalizedAim, ActionAimDisplaySeconds);
        var loadout = GetOrCreateLoadout(globalId);
        if (!loadout.TryConsumeUse(item)) {
            GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.Validation, "ItemUseRejected", $"global={globalId} item={item.ItemId} reason=empty");
            UpdateStatusLabel();
            return;
        }

        ApplyItemUsePushbackAndRecovery(globalId, item);
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.ItemUse, "ItemUseAccepted", $"global={globalId} item={item.ItemId} usesLeft={loadout.GetCurrentUses(item)} aim=({normalizedAim.X:0.000},{normalizedAim.Y:0.000}) strength={aimStrength:0.000}");

        if (item is PlayerItemThrowable throwable)
            SpawnThrownItem(globalId, throwable, startPosition, normalizedAim, aimStrength, syncToPeers);
        else if (item is PlayerItemShootable shootable)
            SpawnBullet(globalId, shootable, startPosition, ApplyAccuracySpread(globalId, normalizedAim), syncToPeers);
        else if (item is PlayerItemProjectile projectile)
            SpawnLaunchedProjectile(globalId, projectile, startPosition, ApplyAccuracySpread(globalId, normalizedAim), syncToPeers);
    }

    private Vector2 ApplyAccuracySpread(int globalId, Vector2 direction) {
        var accuracy = _accuracyStatesByGlobalId.TryGetValue(globalId, out var accuracyState)
            ? accuracyState.CurrentAccuracy
            : 0.0f;
        if (accuracy <= 0.0f)
            return direction;

        var spreadAngle = (float)GD.RandRange(-accuracy, accuracy);
        return direction.Rotated(spreadAngle).Normalized();
    }

    private void SpawnBullet(int globalId, PlayerItemShootable item, Vector2 startPosition, Vector2 direction, bool syncToPeers) {
        var projectileData = EnsureProjectileData(item.Projectile, item, true);
        var scene = projectileData.ProjectileScene ?? _genericBulletScene;
        var bullet = GenericBullet.Create(
            scene,
            CreateRuntimeContext(globalId),
            projectileData,
            GetObjectiveForItem(item, projectileData.CollisionObjective),
            startPosition,
            direction,
            item.Range);
        if (bullet == null)
            return;

        AddChild(bullet);
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.Projectile, "BulletSpawned", $"global={globalId} item={item.ItemId} start={FormatVector(startPosition)} dir={FormatVector(direction)} range={item.Range:0.0}");
        if (syncToPeers)
            SyncItemUse(globalId, item.ItemId, startPosition, direction, item.Range, GetThrowableTargetPosition(item, startPosition, direction, 1.0f));
    }

    private void SpawnLaunchedProjectile(int globalId, PlayerItemProjectile item, Vector2 startPosition, Vector2 direction, bool syncToPeers) {
        var projectileData = EnsureProjectileData(item.Projectile, item, false);
        var scene = projectileData.ProjectileScene ?? _genericLaunchedProjectileScene;
        var projectile = GenericLaunchedProjectile.Create(
            scene,
            CreateRuntimeContext(globalId),
            projectileData,
            GetObjectiveForItem(item, projectileData.CollisionObjective),
            startPosition,
            direction,
            item.Range);
        if (projectile == null)
            return;

        AddChild(projectile);
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.Projectile, "LaunchedProjectileSpawned", $"global={globalId} item={item.ItemId} start={FormatVector(startPosition)} dir={FormatVector(direction)} range={item.Range:0.0}");
        if (syncToPeers)
            SyncItemUse(globalId, item.ItemId, startPosition, direction, item.Range, GetThrowableTargetPosition(item, startPosition, direction, 1.0f));
    }

    private void SpawnThrownItem(int globalId, PlayerItemThrowable item, Vector2 startPosition, Vector2 direction, float aimStrength, bool syncToPeers) {
        var scene = item.ThrowableScene ?? _genericThrownItemScene;
        var targetPosition = GetThrowableTargetPosition(item, startPosition, direction, aimStrength);
        var thrownItem = GenericThrownItem.Create(
            scene,
            CreateRuntimeContext(globalId),
            item,
            GetObjectiveForItem(item, null),
            startPosition,
            targetPosition);
        if (thrownItem == null)
            return;

        AddChild(thrownItem);
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.Projectile, "ThrownItemSpawned", $"global={globalId} item={item.ItemId} start={FormatVector(startPosition)} target={FormatVector(targetPosition)} strength={aimStrength:0.000}");
        if (syncToPeers)
            SyncItemUse(globalId, item.ItemId, startPosition, direction, startPosition.DistanceTo(targetPosition), targetPosition);
    }

    private Vector2 GetThrowableTargetPosition(PlayerItem item, Vector2 startPosition, Vector2 direction, float aimStrength) {
        var distance = item is IPlayerUsable usable ? usable.Range : 0.0f;
        if (item is PlayerItemThrowable throwable) {
            var throwStrength = throwable.ThrowStrengthAffectsRange ? Mathf.Clamp(aimStrength, 0.0f, 1.0f) : 1.0f;
            distance = Mathf.Lerp(throwable.MinThrowRange, throwable.Range, throwStrength);
        }

        return startPosition + (direction * distance);
    }

    private PlayerProjectileData EnsureProjectileData(PlayerProjectileData projectileData, PlayerItem item, bool bullet) {
        projectileData ??= new PlayerProjectileData();
        projectileData.ProjectileScene ??= bullet ? _genericBulletScene : _genericLaunchedProjectileScene;
        if (projectileData.Range <= 0.0f && item is IPlayerUsable usable)
            projectileData.Range = usable.Range;
        if (projectileData.Damage == null || projectileData.Damage.DamageValues.Count == 0)
            projectileData.Damage = CreateDefaultDamageResource(item, bullet);
        return projectileData;
    }

    private DamageResource CreateDefaultDamageResource(PlayerItem item, bool bullet) {
        var damage = new DamageResource();
        var value = bullet ? GetDefaultBulletDamage(item) : 90.0f;
        damage.AddDamageValue(bullet ? DamageType.Crush : DamageType.Explosive, value);
        return damage;
    }

    private PlayerItemObjective GetObjectiveForItem(PlayerItem item, PlayerItemObjective fallbackObjective) {
        if (item is IPlayerUsable usable && usable.UseObjective != null)
            return usable.UseObjective;

        if (fallbackObjective != null)
            return fallbackObjective;

        if (item is PlayerItemProjectile)
            return CreateExplosionObjective(70.0f, 46.0f);

        if (item is PlayerItemThrowable throwable) {
            if (throwable.ItemId.Contains("smoke"))
                return new PlayerItemObjective { Type = PlayerItemObjective.ObjectiveType.None, DurationSeconds = 5.0f };
            if (throwable.ItemId.Contains("incendiary"))
                return CreateExplosionObjective(45.0f, 44.0f, DamageType.Heat);
            return CreateExplosionObjective(80.0f, 48.0f);
        }

        return null;
    }

    private static PlayerItemObjective CreateExplosionObjective(float damageValue, float radius, DamageType damageType = DamageType.Explosive) {
        var damage = new DamageResource();
        damage.AddDamageValue(damageType, damageValue);
        return new PlayerItemObjective {
            Type = PlayerItemObjective.ObjectiveType.Explosion,
            Radius = radius,
            DamageResource = damage,
        };
    }

    private static float GetDefaultBulletDamage(PlayerItem item) {
        if (item.ItemId.StartsWith("rifle"))
            return 42.0f;
        if (item.ItemId.StartsWith("ar"))
            return 28.0f;
        if (item.ItemId.StartsWith("smg"))
            return 18.0f;
        return 24.0f;
    }

    private PlayerItemRuntimeContext CreateRuntimeContext(int ownerGlobalId) {
        var props = new List<LevelProp>();
        if (_centerProp != null && IsInstanceValid(_centerProp))
            props.Add(_centerProp);

        return new PlayerItemRuntimeContext {
            OwnerGlobalId = ownerGlobalId,
            World = this,
            ArenaMapData = _arenaMapData,
            TileSize = TestTileSize,
            PlayersByGlobalId = _playersByGlobalId,
            Props = props,
            ArenaChanged = RenderArenaWithCollision,
        };
    }

    private void SyncItemUse(int globalId, string itemId, Vector2 startPosition, Vector2 direction, float range, Vector2 targetPosition) {
        if (!CanSendHostRpc())
            return;

        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcSend, "RpcSyncUsePlayerItem", $"global={globalId} item={itemId} start={FormatVector(startPosition)} dir={FormatVector(direction)} range={range:0.0} target={FormatVector(targetPosition)}");
        Rpc(
            nameof(RpcSyncUsePlayerItem),
            globalId,
            itemId,
            startPosition.X,
            startPosition.Y,
            direction.X,
            direction.Y,
            range,
            targetPosition.X,
            targetPosition.Y);
    }

    private LocalPlayerData GetLocalPlayerData(int localId) {
        foreach (var localPlayerData in _networking.LocalLobbyData.LocalPlayers) {
            if (localPlayerData.IsActive && localPlayerData.LocalId == localId)
                return localPlayerData;
        }

        return null;
    }

    private LocalInputVectors GetLocalInputVectors(LocalPlayerData localPlayerData, DamageTestPlayer player) {
        return localPlayerData.InputType switch {
            LocalPlayerData.LocalInputType.KeyboardMouse => new LocalInputVectors(
                GetKeyboardMovementInput(),
                GetGlobalMousePosition() - player.GlobalPosition,
                false,
                IsKeyboardMouseAiming()),
            LocalPlayerData.LocalInputType.Gamepad => new LocalInputVectors(
                GetGamepadMovementInput(localPlayerData.DeviceId),
                GetGamepadAimInput(localPlayerData.DeviceId),
                true,
                GetGamepadAimInput(localPlayerData.DeviceId).Length() >= GamepadDeadzone),
            _ => new LocalInputVectors(Vector2.Zero, Vector2.Zero, false, false),
        };
    }

    private QuantizedInputState GetAimState(LocalInputVectors inputVectors, QuantizedInputState movementState, QuantizedInputState previousAimState) {
        var aimState = QuantizeInput(inputVectors.Aim, previousAimState);
        if (aimState.HasInput || !inputVectors.AimFallsBackToMovement)
            return aimState;

        if (movementState.HasInput)
            return movementState;

        return previousAimState.HasInput ? previousAimState : GetNoInputState();
    }

    private static float GetAimStrength(LocalInputVectors inputVectors, QuantizedInputState movementState, LocalPlayerData localPlayerData) {
        if (localPlayerData.InputType == LocalPlayerData.LocalInputType.KeyboardMouse)
            return 1.0f;

        var aimLength = Mathf.Clamp(inputVectors.Aim.Length(), 0.0f, 1.0f);
        if (aimLength >= GamepadDeadzone)
            return aimLength;

        if (inputVectors.AimFallsBackToMovement && movementState.HasInput)
            return movementState.Strength == InputStrength.Full ? 1.0f : 0.5f;

        return 1.0f;
    }

    private static Vector2 GetKeyboardMovementInput() {
        var direction = Vector2.Zero;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
            direction.X -= 1.0f;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
            direction.X += 1.0f;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
            direction.Y -= 1.0f;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
            direction.Y += 1.0f;

        if (direction.LengthSquared() > 1.0f)
            direction = direction.Normalized();

        return IsKeyboardWalkHeld() ? direction * 0.5f : direction;
    }

    private static Vector2 GetGamepadMovementInput(int deviceId) {
        return ClampInputVector(new Vector2(
            Input.GetJoyAxis(deviceId, JoyAxis.LeftX),
            Input.GetJoyAxis(deviceId, JoyAxis.LeftY)));
    }

    private static Vector2 GetGamepadAimInput(int deviceId) {
        return ClampInputVector(new Vector2(
            Input.GetJoyAxis(deviceId, JoyAxis.RightX),
            Input.GetJoyAxis(deviceId, JoyAxis.RightY)));
    }

    private static Vector2 ClampInputVector(Vector2 input) {
        return input.LengthSquared() > 1.0f ? input.Normalized() : input;
    }

    private static bool IsKeyboardWalkHeld() {
        return Input.IsKeyPressed(Key.Shift);
    }

    private static bool IsKeyboardMouseAiming() {
        return Input.IsKeyPressed(Key.Ctrl) || Input.IsMouseButtonPressed(MouseButton.Right);
    }

    private static QuantizedInputState QuantizeInput(Vector2 input, QuantizedInputState previousState) {
        var length = input.Length();
        var inputThreshold = previousState.HasInput ? InputStopThreshold : GamepadDeadzone;
        if (length < inputThreshold)
            return GetNoInputState();

        var angle = Mathf.PosMod(input.Angle(), Mathf.Tau);
        var directionIndex = Mathf.PosMod(Mathf.RoundToInt(angle / Mathf.Tau * DirectionBucketCount), DirectionBucketCount);
        if (previousState.HasInput && IsAngleInsideDirectionBucket(angle, previousState.DirectionIndex, 0.25f))
            directionIndex = previousState.DirectionIndex;

        var strength = previousState.Strength == InputStrength.Full
            ? length >= InputFullExitThreshold ? InputStrength.Full : InputStrength.Some
            : length >= InputFullEnterThreshold ? InputStrength.Full : InputStrength.Some;
        return new QuantizedInputState(directionIndex, strength);
    }

    private static bool IsAngleInsideDirectionBucket(float angle, int directionIndex, float extraBucketFraction) {
        if (directionIndex < 0)
            return false;

        var bucketAngle = Mathf.Tau * directionIndex / DirectionBucketCount;
        var angleDelta = Mathf.Abs(Mathf.AngleDifference(angle, bucketAngle));
        var bucketWidth = Mathf.Tau / DirectionBucketCount;
        return angleDelta <= (bucketWidth * (0.5f + extraBucketFraction));
    }

    private static QuantizedInputState GetNoInputState() {
        return new QuantizedInputState(-1, InputStrength.None);
    }

    private static Vector2 DirectionIndexToVector(int directionIndex) {
        if (directionIndex < 0)
            return Vector2.Zero;

        var angle = Mathf.Tau * directionIndex / DirectionBucketCount;
        return Vector2.FromAngle(angle);
    }

    private Vector2 GetAimDirection(int globalId) {
        return _aimStatesByGlobalId.TryGetValue(globalId, out var aimState) && aimState.HasInput
            ? DirectionIndexToVector(aimState.DirectionIndex)
            : Vector2.Right;
    }

    private float GetMovementStrength(int globalId) {
        if (!_movementStatesByGlobalId.TryGetValue(globalId, out var movementState) || !movementState.HasInput)
            return 0.0f;

        return movementState.Strength == InputStrength.Full ? 1.0f : 0.5f;
    }

    private void UpdateLocalAimIndicator() {
        if (_aimIndicator == null)
            return;

        foreach (var playerEntry in _playersByGlobalId) {
            var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(playerEntry.Key);
            if (playerData == null || !playerData.IsLocalPlayer)
                continue;

            var player = playerEntry.Value;
            if (!IsInstanceValid(player) || player.IsDead()) {
                _aimIndicator.SetAim(Vector2.Zero, Vector2.Zero, 0.0f, false);
                return;
            }

            if (!_itemsByGlobalId.TryGetValue(playerEntry.Key, out var item) || item == null) {
                _aimIndicator.SetAim(Vector2.Zero, Vector2.Zero, 0.0f, false);
                return;
            }

            if (!_isAimingByGlobalId.TryGetValue(playerEntry.Key, out var isAiming) || !isAiming) {
                _aimIndicator.SetAim(Vector2.Zero, Vector2.Zero, 0.0f, false);
                return;
            }

            var aimDirection = player.DisplayAimDirection;
            if (aimDirection.LengthSquared() <= 0.0001f) {
                _aimIndicator.SetAim(Vector2.Zero, Vector2.Zero, 0.0f, false);
                return;
            }

            var aimStrength = _aimStrengthByGlobalId.TryGetValue(playerEntry.Key, out var strength) ? strength : 1.0f;
            var aimDirectionNormalized = aimDirection.Normalized();
            var aimDistance = GetAimProjectionDistance(playerEntry.Key, item, player.GlobalPosition, aimDirectionNormalized, aimStrength);
            var aimEnd = player.GlobalPosition + (aimDirectionNormalized * aimDistance);
            var spreadAccuracy = _accuracyStatesByGlobalId.TryGetValue(playerEntry.Key, out var accuracyState)
                ? accuracyState.CurrentSpreadAccuracy
                : item is IPlayerUsable usable ? usable.DefaultAccuracy : 0.0f;
            var spreadRadius = PlayerItemAccuracyState.GetSpreadRadiusAtDistance(spreadAccuracy, aimDistance);
            _aimIndicator.SetAim(player.GlobalPosition, aimEnd, spreadRadius, true);
            return;
        }

        _aimIndicator.SetAim(Vector2.Zero, Vector2.Zero, 0.0f, false);
    }

    private float GetAimProjectionDistance(int globalId, PlayerItem item, Vector2 start, Vector2 direction, float aimStrength) {
        if (item is PlayerItemThrowable throwable)
            return GetThrowableProjectionDistance(globalId, throwable, start, direction, aimStrength);

        return item is IPlayerUsable usable
            ? GetCollisionAwareProjectionDistance(globalId, start, direction, Mathf.Max(usable.GetAimDisplayDistance(), 0.0f))
            : 0.0f;
    }

    private float GetThrowableProjectionDistance(int globalId, PlayerItemThrowable throwable, Vector2 start, Vector2 direction, float aimStrength) {
        var throwStrength = throwable.ThrowStrengthAffectsRange ? Mathf.Clamp(aimStrength, 0.0f, 1.0f) : 1.0f;
        var maxDistance = Mathf.Lerp(throwable.MinThrowRange, throwable.Range, throwStrength);
        return GetCollisionAwareProjectionDistance(globalId, start, direction, maxDistance);
    }

    private float GetCollisionAwareProjectionDistance(int globalId, Vector2 start, Vector2 direction, float maxDistance) {
        const float sampleStep = 4.0f;
        for (var distance = sampleStep; distance <= maxDistance; distance += sampleStep) {
            var samplePosition = start + (direction * distance);
            if (IsAimProjectionBlocked(globalId, samplePosition))
                return distance;
        }

        return maxDistance;
    }

    private bool IsAimProjectionBlocked(int ownerGlobalId, Vector2 worldPosition) {
        if (_arenaMapData != null && _arenaMapData.IsWallTile(_arenaMapData.WorldToTile(worldPosition, TestTileSize)))
            return true;

        if (_centerProp != null && IsInstanceValid(_centerProp) && _centerProp.ContainsWorldPosition(worldPosition))
            return true;

        foreach (var playerEntry in _playersByGlobalId) {
            if (playerEntry.Key == ownerGlobalId)
                continue;

            if (IsInstanceValid(playerEntry.Value) && playerEntry.Value.ContainsWorldPosition(worldPosition))
                return true;
        }

        return false;
    }

    private float GetPlayerPositionX(int globalId) {
        return _playersByGlobalId.TryGetValue(globalId, out var player) && IsInstanceValid(player) ? player.GlobalPosition.X : 0.0f;
    }

    private float GetPlayerPositionY(int globalId) {
        return _playersByGlobalId.TryGetValue(globalId, out var player) && IsInstanceValid(player) ? player.GlobalPosition.Y : 0.0f;
    }

    private bool IsPlayerStateRequestAllowed(int globalId) {
        if (!_networking.HasActiveNetworkPeer || !_networking.IsServer) {
            GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.Authority, "PlayerStateRequestRejected", $"global={globalId} reason=notServerOrNoPeer");
            return false;
        }

        var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(globalId);
        var remotePeerId = Multiplayer.GetRemoteSenderId();
        var allowed = playerData != null && playerData.PeerId == remotePeerId;
        if (!allowed)
            GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.Authority, "PlayerStateRequestRejected", $"global={globalId} remotePeer={remotePeerId} ownerPeer={playerData?.PeerId.ToString() ?? "none"}");

        return allowed;
    }

    private static InputStrength ToInputStrength(int strengthValue) {
        return System.Enum.IsDefined(typeof(InputStrength), strengthValue)
            ? (InputStrength)strengthValue
            : InputStrength.None;
    }

    private bool CanSendHostRpc() {
        return _networking.HasActiveNetworkPeer && _networking.IsServer;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRequestSetPlayerMovementVector(int globalId, float movementX, float movementY) {
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcReceive, "RpcRequestSetPlayerMovementVector", $"from={Multiplayer.GetRemoteSenderId()} global={globalId} vector=({movementX:0.000},{movementY:0.000})");
        if (!IsPlayerStateRequestAllowed(globalId))
            return;

        _movementStatesByGlobalId.TryGetValue(globalId, out var previousMovementState);
        var movementState = QuantizeInput(new Vector2(movementX, movementY), previousMovementState);
        SetPlayerMovementState(globalId, movementState, false);
        SyncPlayerMovementState(globalId, movementState, true);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRequestSetPlayerAimState(int globalId, int directionIndex, int strengthValue, bool isAiming) {
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcReceive, "RpcRequestSetPlayerAimState", $"from={Multiplayer.GetRemoteSenderId()} global={globalId} dir={directionIndex} strength={ToInputStrength(strengthValue)} aiming={isAiming}");
        if (!IsPlayerStateRequestAllowed(globalId))
            return;

        var aimState = new QuantizedInputState(directionIndex, ToInputStrength(strengthValue));
        SetPlayerAimState(globalId, aimState, isAiming, true);
        SyncPlayerAimState(globalId, aimState, isAiming);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRequestSetPlayerItem(int globalId, string itemId) {
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcReceive, "RpcRequestSetPlayerItem", $"from={Multiplayer.GetRemoteSenderId()} global={globalId} item={itemId}");
        if (!IsPlayerStateRequestAllowed(globalId))
            return;

        SetPlayerItem(globalId, itemId);
        SyncPlayerItem(globalId, itemId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRequestSetPlayerArmor(int globalId, string armorId) {
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcReceive, "RpcRequestSetPlayerArmor", $"from={Multiplayer.GetRemoteSenderId()} global={globalId} armor={armorId}");
        if (!IsPlayerStateRequestAllowed(globalId))
            return;

        SetPlayerArmor(globalId, armorId);
        SyncPlayerArmor(globalId, armorId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRequestUsePlayerItem(int globalId, float aimX, float aimY, float aimStrength) {
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcReceive, "RpcRequestUsePlayerItem", $"from={Multiplayer.GetRemoteSenderId()} global={globalId} aim=({aimX:0.000},{aimY:0.000}) strength={aimStrength:0.000}");
        if (!IsPlayerStateRequestAllowed(globalId))
            return;

        if (_itemRecoverySecondsByGlobalId.TryGetValue(globalId, out var recoverySeconds) && recoverySeconds > 0.0) {
            GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.Validation, "RpcRequestUsePlayerItemRejected", $"global={globalId} reason=recovery recovery={recoverySeconds:0.000}");
            return;
        }

        ExecuteValidatedItemUse(globalId, new Vector2(aimX, aimY), Mathf.Clamp(aimStrength, 0.0f, 1.0f), true);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcSyncPlayerMovementState(int globalId, int directionIndex, int strengthValue, float worldX, float worldY, bool includePosition) {
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcReceive, "RpcSyncPlayerMovementState", $"global={globalId} dir={directionIndex} strength={ToInputStrength(strengthValue)} includePosition={includePosition} world=({worldX:0.0},{worldY:0.0})");
        SetPlayerMovementState(globalId, new QuantizedInputState(directionIndex, ToInputStrength(strengthValue)), includePosition, worldX, worldY);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    public void RpcSyncPlayerPosition(int globalId, float worldX, float worldY) {
        if (_playersByGlobalId.TryGetValue(globalId, out var player) && IsInstanceValid(player))
            player.SetSyncedPosition(new Vector2(worldX, worldY));
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcSyncPlayerAimState(int globalId, int directionIndex, int strengthValue, bool isAiming) {
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcReceive, "RpcSyncPlayerAimState", $"global={globalId} dir={directionIndex} strength={ToInputStrength(strengthValue)} aiming={isAiming}");
        SetPlayerAimState(globalId, new QuantizedInputState(directionIndex, ToInputStrength(strengthValue)), isAiming, true);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcSyncPlayerItem(int globalId, string itemId) {
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcReceive, "RpcSyncPlayerItem", $"global={globalId} item={itemId}");
        SetPlayerItem(globalId, itemId);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcSyncPlayerArmor(int globalId, string armorId) {
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcReceive, "RpcSyncPlayerArmor", $"global={globalId} armor={armorId}");
        SetPlayerArmor(globalId, armorId);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcSyncPlayerRespawn(int globalId, float worldX, float worldY, float spawnSeconds) {
        if (!_playersByGlobalId.TryGetValue(globalId, out var player) || !IsInstanceValid(player))
            return;

        player.BeginSpawn(new Vector2(worldX, worldY));
        _movementStatesByGlobalId[globalId] = GetNoInputState();
        _itemRecoverySecondsByGlobalId[globalId] = 0.0;
        _respawnSecondsByGlobalId[globalId] = -1.0f;
        _spawnSecondsByGlobalId[globalId] = spawnSeconds;
        if (_loadoutsByGlobalId.TryGetValue(globalId, out var loadout))
            loadout.ResetUsesToMax();

        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcReceive, "RpcSyncPlayerRespawn", $"global={globalId} world=({worldX:0.0},{worldY:0.0}) spawn={spawnSeconds:0.0}");
        UpdateStatusLabel();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcSyncPlayerSpawnFinished(int globalId) {
        FinishPlayerSpawn(globalId, false);
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcReceive, "RpcSyncPlayerSpawnFinished", $"global={globalId}");
        UpdateStatusLabel();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcSyncObjectiveState(int controllingTeamId, bool isContested) {
        _objectiveControllingTeamId = controllingTeamId;
        _objectiveIsContested = isContested;
        if (_neutralObjective != null && IsInstanceValid(_neutralObjective))
            _neutralObjective.SetState(controllingTeamId, isContested);

        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcReceive, "RpcSyncObjectiveState", GetObjectiveText());
        UpdateStatusLabel();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcSyncUsePlayerItem(
        int globalId,
        string itemId,
        float startX,
        float startY,
        float directionX,
        float directionY,
        float range,
        float targetX,
        float targetY) {
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.RpcReceive, "RpcSyncUsePlayerItem", $"global={globalId} item={itemId} start=({startX:0.0},{startY:0.0}) dir=({directionX:0.000},{directionY:0.000}) range={range:0.0} target=({targetX:0.0},{targetY:0.0})");
        var item = LoadItem(itemId);
        if (item == null)
            return;

        GetOrCreateLoadout(globalId).TryConsumeUse(item);

        var startPosition = new Vector2(startX, startY);
        var direction = new Vector2(directionX, directionY);
        if (direction.LengthSquared() <= 0.0001f)
            direction = Vector2.Right;

        if (item is PlayerItemThrowable throwable) {
            ShowSyncedActionAim(globalId, direction);
            var scene = throwable.ThrowableScene ?? _genericThrownItemScene;
            var thrownItem = GenericThrownItem.Create(
                scene,
                CreateRuntimeContext(globalId),
                throwable,
                GetObjectiveForItem(throwable, null),
                startPosition,
                new Vector2(targetX, targetY));
            if (thrownItem == null)
                return;

            AddChild(thrownItem);
            return;
        }

        if (item is PlayerItemShootable shootable) {
            ShowSyncedActionAim(globalId, direction);
            var projectileData = EnsureProjectileData(shootable.Projectile, shootable, true);
            var scene = projectileData.ProjectileScene ?? _genericBulletScene;
            var bullet = GenericBullet.Create(
                scene,
                CreateRuntimeContext(globalId),
                projectileData,
                GetObjectiveForItem(shootable, projectileData.CollisionObjective),
                startPosition,
                direction,
                range);
            if (bullet == null)
                return;

            AddChild(bullet);
            return;
        }

        if (item is PlayerItemProjectile projectileItem) {
            ShowSyncedActionAim(globalId, direction);
            var projectileData = EnsureProjectileData(projectileItem.Projectile, projectileItem, false);
            var scene = projectileData.ProjectileScene ?? _genericLaunchedProjectileScene;
            var projectile = GenericLaunchedProjectile.Create(
                scene,
                CreateRuntimeContext(globalId),
                projectileData,
                GetObjectiveForItem(projectileItem, projectileData.CollisionObjective),
                startPosition,
                direction,
                range);
            if (projectile == null)
                return;

            AddChild(projectile);
        }
    }

    private void ShowSyncedActionAim(int globalId, Vector2 direction) {
        if (_playersByGlobalId.TryGetValue(globalId, out var player) && IsInstanceValid(player))
            player.ShowActionAimDirection(direction, ActionAimDisplaySeconds);
    }

    private LocalPlayerData GetLocalPlayerDataForGlobalId(int globalId) {
        var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(globalId);
        return playerData == null ? null : GetLocalPlayerData(playerData.LocalId);
    }

    private Vector2 TileToWorldCenter(Vector2I tilePosition) {
        return new Vector2(
            (tilePosition.X * TestTileSize.X) + (TestTileSize.X * 0.5f),
            (tilePosition.Y * TestTileSize.Y) + (TestTileSize.Y * 0.5f));
    }

    private Vector2I GetTestPlayerTilePosition(int globalId, int teamId) {
        return _spawnManager.GetSpawnTile(teamId, GetTeamPlayerIndex(globalId, teamId), _arenaMapData);
    }

    private int GetTeamPlayerIndex(int globalId, int teamId) {
        var teamPlayerIndex = 0;
        foreach (var playerData in _networking.MultiplayerData.Players) {
            if (playerData.GlobalId < 0)
                continue;

            if (!TryGetBackendTeamId(playerData, out var playerTeamId, "team-player-index") || playerTeamId != teamId)
                continue;

            if (playerData.GlobalId == globalId)
                return teamPlayerIndex;

            teamPlayerIndex++;
        }

        return 0;
    }

    private Vector2I GetObjectiveTilePosition() {
        var neutralObjectiveTiles = _structureGenerationData?.GetSpawnTiles(StructureGenerationData.SpawnPointType.NeutralObjective);
        return neutralObjectiveTiles != null && neutralObjectiveTiles.Count > 0
            ? neutralObjectiveTiles[0]
            : Vector2I.Zero;
    }

    private void UpdateObjective(double delta) {
        if (_neutralObjective == null || !IsInstanceValid(_neutralObjective))
            return;

        if (_networking == null || (!_networking.IsServer && !_networking.IsLocal))
            return;

        EvaluateObjectiveOccupancy(out var controllingTeamId, out var isContested);
        if (controllingTeamId != _objectiveControllingTeamId || isContested != _objectiveIsContested) {
            _objectiveControllingTeamId = controllingTeamId;
            _objectiveIsContested = isContested;
            _neutralObjective.SetState(_objectiveControllingTeamId, _objectiveIsContested);
            SyncObjectiveState();
            GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.StateChange, "ObjectiveStateChanged", GetObjectiveText());
        }
    }

    private void EvaluateObjectiveOccupancy(out int controllingTeamId, out bool isContested) {
        controllingTeamId = -1;
        isContested = false;

        foreach (var playerEntry in _playersByGlobalId) {
            var player = playerEntry.Value;
            if (!IsInstanceValid(player) || player.IsDead() || !_neutralObjective.ContainsInnerPosition(player.GlobalPosition))
                continue;

            var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(playerEntry.Key);
            if (!TryGetBackendTeamId(playerData, out var teamId, "objective-occupancy"))
                continue;

            if (controllingTeamId < 0) {
                controllingTeamId = teamId;
                continue;
            }

            if (controllingTeamId != teamId) {
                controllingTeamId = -1;
                isContested = true;
                return;
            }
        }
    }

    private void SyncObjectiveState() {
        if (!CanSendHostRpc())
            return;

        Rpc(nameof(RpcSyncObjectiveState), _objectiveControllingTeamId, _objectiveIsContested);
    }

    private void OnConnectionStateChanged() {
        GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.StateChange, "ConnectionStateChanged", GetNetworkDebugText());
        UpdateStatusLabel();
    }

    private void OnLobbyStateChanged() {
        AutoAssignLanTestTeams();
        SyncPlayersWithNetworkData();
        foreach (var globalId in _playersByGlobalId.Keys) {
            SetPlayerTeamColor(globalId);
            SetPlayerLocalMarker(globalId);
        }
        if (_structureGenerationData != null)
            BuildTeamSpawnBaseMarkers();
        UpdateStatusLabel();
    }

    private void AutoAssignLanTestTeams() {
        if (_networking == null || !_networking.IsHostAuthority || _networking.MultiplayerData.Players.Count <= 0)
            return;

        var playerCount = _networking.MultiplayerData.Players.Count;
        if (_lastAutoAssignedPlayerCount == playerCount)
            return;

        _lastAutoAssignedPlayerCount = playerCount;
        _networking.AutoAssignPeerTeams(2);
    }

    private bool TryGetBackendTeamId(PlayerData playerData, out int teamId, string context) {
        teamId = MultiplayerData.DefaultTeamId;
        var networkTeamId = _networking?.MultiplayerData?.GetTeam(playerData) ?? MultiplayerData.DefaultTeamId;
        if (networkTeamId == MultiplayerData.DefaultTeamId) {
            GameLog.Print(GameLogScope.PlayerItemRoom, GameLogType.Warning, "UnassignedTeamUsed", $"context={context} global={playerData?.GlobalId ?? -1} peer={playerData?.PeerId ?? -1} team={networkTeamId}");
            return false;
        }

        teamId = MultiplayerData.NormalizeTeamId(networkTeamId);
        return teamId != MultiplayerData.DefaultTeamId;
    }

    private static int GetPaletteTeamId(int backendTeamId) {
        return Mathf.Clamp(backendTeamId + 1, 1, 4);
    }

    private int GetConnectedPeerCount() {
        return _networking.HasActiveNetworkPeer ? Multiplayer.GetPeers().Length : 0;
    }

    private static string FormatInputState(QuantizedInputState state) {
        return $"dir={state.DirectionIndex} strength={state.Strength}";
    }

    private static string FormatVector(Vector2 vector) {
        return $"({vector.X:0.0},{vector.Y:0.0})";
    }

    private void UpdateStatusLabel() {
        if (_networking == null)
            return;

        _statusLabel.Text = $"Arena Match\nPeers connected: {GetConnectedPeerCount()}\nControls: B item menu | arrows/d-pad + Enter/A select | left mouse / Xbox RT use\nObjective: {GetObjectiveText()}\nPlayers: {GetPlayerText()}";
        UpdateLocalPlayersHud();
    }

    private void UpdateLocalPlayersHud() {
        if (_localPlayersHud == null || !IsInstanceValid(_localPlayersHud) || _networking?.MultiplayerData == null)
            return;

        _localPlayersHud.BeginRefresh(_networking.CurrentMode != Networking.NetworkMode.Local);
        foreach (var playerEntry in _playersByGlobalId) {
            var globalId = playerEntry.Key;
            var player = playerEntry.Value;
            var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(globalId);
            if (playerData == null || !playerData.IsLocalPlayer || !IsInstanceValid(player))
                continue;

            var backendTeamId = _networking.MultiplayerData.GetTeam(playerData);
            var teamColor = backendTeamId == MultiplayerData.DefaultTeamId
                ? new Color(0.42f, 0.46f, 0.52f)
                : TeamVisuals.GetTeamColor(GetPaletteTeamId(Mathf.Clamp(backendTeamId, MultiplayerData.MinTeamId, MultiplayerData.MaxTeamId)));
            var loadout = GetOrCreateLoadout(globalId);
            var selectedItem = loadout.SelectedItem;
            var maxUses = loadout.GetMaxUses(selectedItem);
            var currentUses = maxUses > 0 ? loadout.GetCurrentUses(selectedItem) : 0;

            _localPlayersHud.SetPlayerState(
                globalId,
                playerData.LocalId,
                backendTeamId,
                GetPaletteTeamId(Mathf.Clamp(backendTeamId, MultiplayerData.MinTeamId, MultiplayerData.MaxTeamId)),
                playerData.DisplayName,
                GetPlayerHudStatus(player),
                player.Health?.CurrentHealth ?? 0,
                player.Health?.MaxHealth ?? 0,
                selectedItem?.GetShowcaseTexture(),
                currentUses,
                maxUses,
                selectedItem?.AmmoCaliber ?? PlayerItem.AmmoCaliberType.Standard,
                GetGadgetHudText(loadout),
                teamColor);
        }

        _localPlayersHud.EndRefresh();
    }

    private static string GetPlayerHudStatus(DamageTestPlayer player) {
        if (player == null || player.IsDead())
            return "DEAD";

        return player.ControlState == PlayerControlState.Spawning ? "SPAWN" : "ALIVE";
    }

    private string GetGadgetHudText(PlayerLoadoutState loadout) {
        if (loadout == null)
            return "G --";

        foreach (var gadget in loadout.Gadgets) {
            if (gadget == null)
                continue;

            var maxUses = loadout.GetMaxUses(gadget);
            return maxUses > 0
                ? $"G {loadout.GetCurrentUses(gadget)}/{maxUses}"
                : $"G {gadget.DisplayName}";
        }

        return "G empty";
    }

    private string GetObjectiveText() {
        if (_objectiveIsContested)
            return "contested";

        return _objectiveControllingTeamId >= 0
            ? $"occupied by team {GetPaletteTeamId(_objectiveControllingTeamId)}"
            : "neutral";
    }

    private string GetPlayerText() {
        var playerTexts = new List<string>();
        foreach (var playerEntry in _playersByGlobalId) {
            var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(playerEntry.Key);
            var ownerText = playerData == null ? "peer ?" : $"peer {playerData.PeerId}:local {playerData.LocalId}";
            var inputText = GetPlayerInputText(playerEntry.Key);
            var itemText = _itemsByGlobalId.TryGetValue(playerEntry.Key, out var item) ? item.DisplayName : "none";
            var accuracyText = _accuracyStatesByGlobalId.TryGetValue(playerEntry.Key, out var accuracyState) ? $" acc {accuracyState.CurrentAccuracy:0.000}" : string.Empty;
            var loadoutText = _loadoutsByGlobalId.TryGetValue(playerEntry.Key, out var loadout) ? $" {loadout.GetLoadoutText()}" : string.Empty;
            playerTexts.Add($"P{playerEntry.Key} {ownerText} {inputText} item {itemText}{accuracyText}{loadoutText}");
        }

        return playerTexts.Count == 0 ? "waiting" : string.Join(", ", playerTexts);
    }

    private string GetPlayerInputText(int globalId) {
        var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(globalId);
        if (playerData == null || !playerData.IsLocalPlayer)
            return "remote";

        var localPlayerData = GetLocalPlayerDataForGlobalId(globalId);
        if (localPlayerData == null)
            return "input ?";

        return localPlayerData.InputType switch {
            LocalPlayerData.LocalInputType.KeyboardMouse => "keyboard+mouse",
            LocalPlayerData.LocalInputType.Gamepad => $"gamepad {localPlayerData.DeviceId}",
            _ => "input none",
        };
    }

    private string GetNetworkDebugText() {
        return $"Network: {_networking.CurrentMode}. Client: {GetClientConnectionText()}. Peers connected: {GetConnectedPeerCount()}.";
    }

    private string GetClientConnectionText() {
        if (!_networking.IsClient)
            return "Not client";

        return IsClientConnected()
            ? $"Connected to {ClientAddress}:{ClientPort}"
            : $"Not connected to {ClientAddress}:{ClientPort}";
    }

    private bool IsClientConnected() {
        return _networking.HasActiveNetworkPeer
            && Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;
    }

    private void CenterCamera() {
        var usedRect = _tileLayerRenderer.FloorLayer.GetUsedRect();
        if (usedRect.Size == Vector2I.Zero)
            return;

        var centerCell = usedRect.Position + (usedRect.Size / 2);
        _camera.Position = _tileLayerRenderer.FloorLayer.MapToLocal(centerCell);
    }
}
