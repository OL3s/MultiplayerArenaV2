using Godot;
using System.Collections.Generic;

public partial class TestPlayerItemRoomLAN : Node2D {
    private static readonly Vector2I TestTileSize = new(16, 16);
    private const float TestPlayerMoveSpeed = 96.0f;
    private const float GamepadDeadzone = 0.25f;
    private const float InputStopThreshold = 0.18f;
    private const float InputFullEnterThreshold = 0.70f;
    private const float InputFullExitThreshold = 0.60f;
    private const float TriggerUseThreshold = 0.5f;
    private const float ActionAimDisplaySeconds = 0.5f;
    private const int ItemMenuColumns = 3;
    private const int DirectionBucketCount = 16;
    private const string DefaultItemId = "pistol_t1";
    private const string GenericBulletScenePath = "res://Scenes/Gameplay/Projectiles/GenericBullet.tscn";
    private const string GenericThrownItemScenePath = "res://Scenes/Gameplay/Projectiles/GenericThrownItem.tscn";
    private const string GenericLaunchedProjectileScenePath = "res://Scenes/Gameplay/Projectiles/GenericLaunchedProjectile.tscn";

    private static readonly string[] ModernItemIds = {
        "pistol_t1", "pistol_t2", "pistol_t3",
        "smg_t1", "smg_t2", "smg_t3",
        "ar_t1", "ar_t2", "ar_t3",
        "rifle_t1", "rifle_t2", "rifle_t3",
        "rocketlauncher", "grenadelauncher_t1", "grenadelauncher_t2",
        "nade_explosive", "nade_incendiary", "nade_smoke",
    };

    private static readonly Dictionary<string, string> ItemResourcePaths = new() {
        ["pistol_t1"] = "res://Assets/Items/Modern/Weapons/pistol_t1.tres",
        ["pistol_t2"] = "res://Assets/Items/Modern/Weapons/pistol_t2.tres",
        ["pistol_t3"] = "res://Assets/Items/Modern/Weapons/pistol_t3.tres",
        ["smg_t1"] = "res://Assets/Items/Modern/Weapons/smg_t1.tres",
        ["smg_t2"] = "res://Assets/Items/Modern/Weapons/smg_t2.tres",
        ["smg_t3"] = "res://Assets/Items/Modern/Weapons/smg_t3.tres",
        ["ar_t1"] = "res://Assets/Items/Modern/Weapons/ar_t1.tres",
        ["ar_t2"] = "res://Assets/Items/Modern/Weapons/ar_t2.tres",
        ["ar_t3"] = "res://Assets/Items/Modern/Weapons/ar_t3.tres",
        ["rifle_t1"] = "res://Assets/Items/Modern/Weapons/rifle_t1.tres",
        ["rifle_t2"] = "res://Assets/Items/Modern/Weapons/rifle_t2.tres",
        ["rifle_t3"] = "res://Assets/Items/Modern/Weapons/rifle_t3.tres",
        ["rocketlauncher"] = "res://Assets/Items/Modern/Weapons/rocketlauncher.tres",
        ["grenadelauncher_t1"] = "res://Assets/Items/Modern/Weapons/grenadelauncher_t1.tres",
        ["grenadelauncher_t2"] = "res://Assets/Items/Modern/Weapons/grenadelauncher_t2.tres",
        ["nade_explosive"] = "res://Assets/Items/Modern/Throwables/nade_explosive.tres",
        ["nade_incendiary"] = "res://Assets/Items/Modern/Throwables/nade_incendiary.tres",
        ["nade_smoke"] = "res://Assets/Items/Modern/Throwables/nade_smoke.tres",
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
    private ArenaTileLayerRenderer _tileLayerRenderer;
    private Camera2D _camera;
    private CanvasLayer _canvasLayer;
    private Label _statusLabel;
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
    private readonly Dictionary<int, PlayerEquipable> _itemsByGlobalId = new();
    private readonly Dictionary<int, PlayerItemAccuracyState> _accuracyStatesByGlobalId = new();
    private readonly Dictionary<int, float> _aimStrengthByGlobalId = new();
    private readonly Dictionary<int, PlayerItemFireMode> _selectedFireModesByGlobalId = new();
    private readonly Dictionary<int, double> _itemRecoverySecondsByGlobalId = new();
    private readonly Dictionary<int, bool> _wasUseHeldByGlobalId = new();
    private readonly Dictionary<int, bool> _suppressUseUntilReleasedByGlobalId = new();
    private readonly Dictionary<int, int> _burstUseCountsByGlobalId = new();
    private readonly Dictionary<string, PlayerEquipable> _loadedItemsById = new();
    private readonly Dictionary<string, Button> _itemMenuButtonsById = new();
    private PlayerAimIndicator _aimIndicator;
    private PackedScene _genericBulletScene;
    private PackedScene _genericThrownItemScene;
    private PackedScene _genericLaunchedProjectileScene;

    [Export]
    public string ClientAddress { get; set; } = "127.0.0.1";

    [Export]
    public int ClientPort { get; set; } = 7700;

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

        ApplyCommandLineOverrides();
        EnsureDefaultNetworkMode();
        EnsureTestLocalLobbyPlayer();

        _networking.ConnectionStateChanged += OnConnectionStateChanged;
        _networking.LobbyStateChanged += OnLobbyStateChanged;
        BuildItemMenu();
        UpdateStatusLabel();
        PrintTestNetworkLog("Scene ready.");

        if (_networking.IsServer && !_networking.HasActiveNetworkPeer)
            TryStartHost();
        else if (_networking.IsClient && !_networking.HasActiveNetworkPeer)
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

        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return;

        if (keyEvent.PhysicalKeycode == Key.F)
            CycleLocalFireMode();
    }

    private bool IsItemMenuToggleEvent(InputEvent @event) {
        if (@event is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.B })
            return true;

        return @event is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.B };
    }

    private void TryStartHost() {
        var started = _networking.BeginHostingSession();
        PrintTestNetworkLog($"Host start result: {started}. Port: {_networking.CurrentServerPort}.");
    }

    private void TryStartClient() {
        var started = _networking.BeginDirectClientConnection(ClientAddress, ClientPort);
        PrintTestNetworkLog($"Client connect start result: {started}. Target: {ClientAddress}:{ClientPort}.");
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
    }

    private void AddItemMenuButton(string itemId) {
        var item = LoadItem(itemId);
        var button = new Button {
            Name = $"ItemButton_{itemId}",
            Text = item?.DisplayName ?? itemId,
            CustomMinimumSize = new Vector2(150.0f, 44.0f),
            FocusMode = Control.FocusModeEnum.All,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        button.Pressed += () => SelectItemFromMenu(itemId);
        _itemMenuButtonsById[itemId] = button;
        _itemMenuGrid.AddChild(button);
    }

    private void SelectItemFromMenu(string itemId) {
        ApplyLocalItemSelection(itemId);
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
            PrintTestNetworkLog("No network role selected. Defaulting test scene to Lan.");
        }
    }

    private void EnsureTestLocalLobbyPlayer() {
        _networking.LocalLobbyData.LocalPlayers.Clear();
        var inputType = _networking.IsClient
            ? LocalPlayerData.LocalInputType.Gamepad
            : LocalPlayerData.LocalInputType.KeyboardMouse;
        var deviceId = _networking.IsClient ? 0 : -1;

        _networking.LocalLobbyData.LocalPlayers.Add(new LocalPlayerData {
            LocalId = 0,
            IsActive = true,
            InputType = inputType,
            DeviceId = deviceId,
            DisplayName = _networking.IsClient ? "Client Player" : "Host Player",
        });
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

        AddFloorRectangle(new Rect2I(5, 5, 18, 10));
        _arenaMapData.ResetWallTiles();
        RenderArenaWithCollision();
        BuildCenterProp();
        RebuildPlayersFromNetworkData();
    }

    private void AddFloorRectangle(Rect2I rect) {
        for (var x = rect.Position.X; x < rect.End.X; x++) {
            for (var y = rect.Position.Y; y < rect.End.Y; y++)
                _arenaMapData.AddFloorTile(new Vector2I(x, y));
        }
    }

    private void BuildCenterProp() {
        if (_centerProp != null && IsInstanceValid(_centerProp))
            _centerProp.QueueFree();

        var propData = new LevelPropData();
        propData.Configure(LevelPropType.Barrel);
        _centerProp = new LevelProp { Name = "CenterBarrel" };
        AddChild(_centerProp);
        _centerProp.Initialize(propData, TileToWorldCenter(new Vector2I(14, 10)));
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
            if (_playersByGlobalId.TryGetValue(playerData.GlobalId, out var existingPlayer) && IsInstanceValid(existingPlayer))
                continue;

            AddPlayer(playerData.GlobalId, GetTestPlayerTilePosition(playerData.GlobalId));
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
            _movementStatesByGlobalId.Remove(removedGlobalId);
            _aimStatesByGlobalId.Remove(removedGlobalId);
            _lastLocalMovementStatesByGlobalId.Remove(removedGlobalId);
            _lastLocalAimStatesByGlobalId.Remove(removedGlobalId);
            _isAimingByGlobalId.Remove(removedGlobalId);
            _lastLocalIsAimingByGlobalId.Remove(removedGlobalId);
            _itemsByGlobalId.Remove(removedGlobalId);
            _accuracyStatesByGlobalId.Remove(removedGlobalId);
            _aimStrengthByGlobalId.Remove(removedGlobalId);
            _selectedFireModesByGlobalId.Remove(removedGlobalId);
            _itemRecoverySecondsByGlobalId.Remove(removedGlobalId);
            _wasUseHeldByGlobalId.Remove(removedGlobalId);
            _suppressUseUntilReleasedByGlobalId.Remove(removedGlobalId);
            _burstUseCountsByGlobalId.Remove(removedGlobalId);
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
        _accuracyStatesByGlobalId.Clear();
        _aimStrengthByGlobalId.Clear();
        _selectedFireModesByGlobalId.Clear();
        _itemRecoverySecondsByGlobalId.Clear();
        _wasUseHeldByGlobalId.Clear();
        _suppressUseUntilReleasedByGlobalId.Clear();
        _burstUseCountsByGlobalId.Clear();
    }

    private void AddPlayer(int globalId, Vector2I tilePosition) {
        var player = new DamageTestPlayer { Name = $"ItemTestPlayer{globalId}" };
        AddChild(player);
        player.Initialize(globalId, TileToWorldCenter(tilePosition));
        _playersByGlobalId[globalId] = player;
        _movementStatesByGlobalId[globalId] = GetNoInputState();
        _aimStatesByGlobalId[globalId] = new QuantizedInputState(0, InputStrength.Full);
        _isAimingByGlobalId[globalId] = false;
        _aimStrengthByGlobalId[globalId] = 1.0f;
        _itemRecoverySecondsByGlobalId[globalId] = 0.0;
        _wasUseHeldByGlobalId[globalId] = false;
        _suppressUseUntilReleasedByGlobalId[globalId] = false;
        _burstUseCountsByGlobalId[globalId] = 0;
        _accuracyStatesByGlobalId[globalId] = new PlayerItemAccuracyState();
        SetPlayerItem(globalId, DefaultItemId);
        player.SetEstimatedAimDirection(DirectionIndexToVector(0), true);
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

        if (_networking.IsClient && _networking.HasActiveNetworkPeer)
            RpcId(1, nameof(RpcRequestSetPlayerMovementVector), globalId, movementVector.X, movementVector.Y);
        else if (CanSendHostRpc()) {
            SetPlayerMovementState(globalId, movementState, false);
            SyncPlayerMovementState(globalId, movementState, true);
        }
        else {
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

        if (_networking.IsClient && _networking.HasActiveNetworkPeer)
            RpcId(1, nameof(RpcRequestSetPlayerAimState), globalId, aimState.DirectionIndex, (int)aimState.Strength, isAiming);
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
        return _itemsByGlobalId.TryGetValue(globalId, out var item) && item != null
            ? item.AimMoveSpeedMultiplier
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

        Rpc(nameof(RpcSyncPlayerAimState), globalId, aimState.DirectionIndex, (int)aimState.Strength, isAiming);
    }

    private void ApplyLocalItemSelection(string itemId) {
        foreach (var playerEntry in _playersByGlobalId) {
            var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(playerEntry.Key);
            if (playerData == null || !playerData.IsLocalPlayer)
                continue;

            if (_networking.IsClient && _networking.HasActiveNetworkPeer)
                RpcId(1, nameof(RpcRequestSetPlayerItem), playerEntry.Key, itemId);
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

    private void SetPlayerItem(int globalId, string itemId) {
        var item = LoadItem(itemId) ?? LoadItem(DefaultItemId);
        if (item == null)
            return;

        _itemsByGlobalId[globalId] = item;
        if (!_accuracyStatesByGlobalId.TryGetValue(globalId, out var accuracyState)) {
            accuracyState = new PlayerItemAccuracyState();
            _accuracyStatesByGlobalId[globalId] = accuracyState;
        }

        accuracyState.SetItem(item);
        EnsureSelectedFireMode(globalId, item);
        _itemRecoverySecondsByGlobalId[globalId] = 0.0;
        _burstUseCountsByGlobalId[globalId] = 0;
        _wasUseHeldByGlobalId[globalId] = false;
        if (_playersByGlobalId.TryGetValue(globalId, out var player) && IsInstanceValid(player))
            player.SetHeldTexture(item.HeldTexture);

        PrintTestNetworkLog($"P{globalId} item: {item.DisplayName}.");
        UpdateStatusLabel();
    }

    private void SyncPlayerItem(int globalId, string itemId) {
        if (CanSendHostRpc())
            Rpc(nameof(RpcSyncPlayerItem), globalId, itemId);
    }

    private PlayerEquipable LoadItem(string itemId) {
        if (_loadedItemsById.TryGetValue(itemId, out var loadedItem))
            return loadedItem;

        if (!ItemResourcePaths.TryGetValue(itemId, out var itemPath))
            return null;

        var item = GD.Load<PlayerEquipable>(itemPath);
        if (item != null)
            _loadedItemsById[itemId] = item;

        return item;
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

    private void ProcessLocalPlayerItemUse(int globalId) {
        if (!_itemsByGlobalId.TryGetValue(globalId, out var item) || item == null)
            return;

        var isUseHeld = IsLocalItemUseHeld(globalId);
        var wasUseHeld = _wasUseHeldByGlobalId.TryGetValue(globalId, out var previousUseHeld) && previousUseHeld;
        _wasUseHeldByGlobalId[globalId] = isUseHeld;

        if (!isUseHeld) {
            _burstUseCountsByGlobalId[globalId] = 0;
            return;
        }

        if (_itemRecoverySecondsByGlobalId[globalId] > 0.0)
            return;

        var selectedFireMode = GetSelectedFireMode(globalId, item);
        if (selectedFireMode == PlayerItemFireMode.Solo && wasUseHeld)
            return;

        if (selectedFireMode == PlayerItemFireMode.Burst) {
            var burstUseCount = _burstUseCountsByGlobalId.TryGetValue(globalId, out var currentBurstUseCount) ? currentBurstUseCount : 0;
            if (burstUseCount >= Mathf.Max(item.BurstMaxUseCount, 1))
                return;

            _burstUseCountsByGlobalId[globalId] = burstUseCount + 1;
        }

        RequestLocalItemUse(globalId, item);
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

    private void RequestLocalItemUse(int globalId, PlayerEquipable item) {
        if (!_playersByGlobalId.TryGetValue(globalId, out var player) || !IsInstanceValid(player) || player.IsDead())
            return;

        var aimDirection = player.DisplayAimDirection;
        if (aimDirection.LengthSquared() <= 0.0001f)
            aimDirection = GetAimDirection(globalId);
        if (aimDirection.LengthSquared() <= 0.0001f)
            aimDirection = Vector2.Right;

        player.ShowActionAimDirection(aimDirection, ActionAimDisplaySeconds);
        var aimStrength = _aimStrengthByGlobalId.TryGetValue(globalId, out var strength) ? strength : 1.0f;
        if (_networking.IsClient && _networking.HasActiveNetworkPeer) {
            ApplyItemUsePushbackAndRecovery(globalId, item);
            RpcId(1, nameof(RpcRequestUsePlayerItem), globalId, aimDirection.X, aimDirection.Y, aimStrength);
        }
        else {
            ExecuteValidatedItemUse(globalId, aimDirection, aimStrength, true);
        }
    }

    private void ApplyItemUsePushbackAndRecovery(int globalId, PlayerEquipable item) {
        if (_accuracyStatesByGlobalId.TryGetValue(globalId, out var accuracyState))
            accuracyState.ApplyUsePushback();

        _itemRecoverySecondsByGlobalId[globalId] = Mathf.Max(item.RecoverySeconds, 0.0f);
    }

    private void ExecuteValidatedItemUse(int globalId, Vector2 aimDirection, float aimStrength, bool syncToPeers) {
        if (!_itemsByGlobalId.TryGetValue(globalId, out var item) || item == null)
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
        ApplyItemUsePushbackAndRecovery(globalId, item);

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
        if (syncToPeers)
            SyncItemUse(globalId, item.ItemId, startPosition, direction, startPosition.DistanceTo(targetPosition), targetPosition);
    }

    private Vector2 GetThrowableTargetPosition(PlayerEquipable item, Vector2 startPosition, Vector2 direction, float aimStrength) {
        var distance = item.Range;
        if (item is PlayerItemThrowable throwable) {
            var throwStrength = throwable.ThrowStrengthAffectsRange ? Mathf.Clamp(aimStrength, 0.0f, 1.0f) : 1.0f;
            distance = Mathf.Lerp(throwable.MinThrowRange, throwable.Range, throwStrength);
        }

        return startPosition + (direction * distance);
    }

    private PlayerProjectileData EnsureProjectileData(PlayerProjectileData projectileData, PlayerEquipable item, bool bullet) {
        projectileData ??= new PlayerProjectileData();
        projectileData.ProjectileScene ??= bullet ? _genericBulletScene : _genericLaunchedProjectileScene;
        if (projectileData.Range <= 0.0f)
            projectileData.Range = item.Range;
        if (projectileData.Damage == null || projectileData.Damage.DamageValues.Count == 0)
            projectileData.Damage = CreateDefaultDamageResource(item, bullet);
        return projectileData;
    }

    private DamageResource CreateDefaultDamageResource(PlayerEquipable item, bool bullet) {
        var damage = new DamageResource();
        var value = bullet ? GetDefaultBulletDamage(item) : 90.0f;
        damage.AddDamageValue(bullet ? DamageType.Crush : DamageType.Explosive, value);
        return damage;
    }

    private PlayerItemObjective GetObjectiveForItem(PlayerEquipable item, PlayerItemObjective fallbackObjective) {
        if (item.UseObjective != null)
            return item.UseObjective;

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

    private static float GetDefaultBulletDamage(PlayerEquipable item) {
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

    private void CycleLocalFireMode() {
        foreach (var playerEntry in _playersByGlobalId) {
            var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(playerEntry.Key);
            if (playerData == null || !playerData.IsLocalPlayer)
                continue;

            if (!_itemsByGlobalId.TryGetValue(playerEntry.Key, out var item) || item == null)
                return;

            var selectedFireMode = GetSelectedFireMode(playerEntry.Key, item);
            var currentIndex = item.AvailableFireModes.IndexOf(selectedFireMode);
            if (currentIndex < 0)
                currentIndex = 0;

            var nextIndex = Mathf.PosMod(currentIndex + 1, item.AvailableFireModes.Count);
            _selectedFireModesByGlobalId[playerEntry.Key] = item.AvailableFireModes[nextIndex];
            _burstUseCountsByGlobalId[playerEntry.Key] = 0;
            PrintTestNetworkLog($"P{playerEntry.Key} fire mode: {_selectedFireModesByGlobalId[playerEntry.Key]}.");
            UpdateStatusLabel();

            return;
        }
    }

    private PlayerItemFireMode GetSelectedFireMode(int globalId, PlayerEquipable item) {
        EnsureSelectedFireMode(globalId, item);
        return _selectedFireModesByGlobalId[globalId];
    }

    private void EnsureSelectedFireMode(int globalId, PlayerEquipable item) {
        if (item.AvailableFireModes.Count == 0) {
            _selectedFireModesByGlobalId[globalId] = PlayerItemFireMode.Solo;
            return;
        }

        if (_selectedFireModesByGlobalId.TryGetValue(globalId, out var selectedFireMode) && item.SupportsFireMode(selectedFireMode))
            return;

        _selectedFireModesByGlobalId[globalId] = item.AvailableFireModes[0];
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
            var spreadAccuracy = _accuracyStatesByGlobalId.TryGetValue(playerEntry.Key, out var accuracyState) ? accuracyState.CurrentSpreadAccuracy : item.DefaultAccuracy;
            var spreadRadius = PlayerItemAccuracyState.GetSpreadRadiusAtDistance(spreadAccuracy, aimDistance);
            _aimIndicator.SetAim(player.GlobalPosition, aimEnd, spreadRadius, true);
            return;
        }

        _aimIndicator.SetAim(Vector2.Zero, Vector2.Zero, 0.0f, false);
    }

    private float GetAimProjectionDistance(int globalId, PlayerEquipable item, Vector2 start, Vector2 direction, float aimStrength) {
        if (item is PlayerItemThrowable throwable)
            return GetThrowableProjectionDistance(globalId, throwable, start, direction, aimStrength);

        return GetCollisionAwareProjectionDistance(globalId, start, direction, Mathf.Max(item.GetAimDisplayDistance(), 0.0f));
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
        if (!_networking.HasActiveNetworkPeer || !_networking.IsServer)
            return false;

        var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(globalId);
        return playerData != null && playerData.PeerId == Multiplayer.GetRemoteSenderId();
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
        if (!IsPlayerStateRequestAllowed(globalId))
            return;

        _movementStatesByGlobalId.TryGetValue(globalId, out var previousMovementState);
        var movementState = QuantizeInput(new Vector2(movementX, movementY), previousMovementState);
        SetPlayerMovementState(globalId, movementState, false);
        SyncPlayerMovementState(globalId, movementState, true);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRequestSetPlayerAimState(int globalId, int directionIndex, int strengthValue, bool isAiming) {
        if (!IsPlayerStateRequestAllowed(globalId))
            return;

        var aimState = new QuantizedInputState(directionIndex, ToInputStrength(strengthValue));
        SetPlayerAimState(globalId, aimState, isAiming, true);
        SyncPlayerAimState(globalId, aimState, isAiming);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRequestSetPlayerItem(int globalId, string itemId) {
        if (!IsPlayerStateRequestAllowed(globalId))
            return;

        SetPlayerItem(globalId, itemId);
        SyncPlayerItem(globalId, itemId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRequestUsePlayerItem(int globalId, float aimX, float aimY, float aimStrength) {
        if (!IsPlayerStateRequestAllowed(globalId))
            return;

        if (_itemRecoverySecondsByGlobalId.TryGetValue(globalId, out var recoverySeconds) && recoverySeconds > 0.0)
            return;

        ExecuteValidatedItemUse(globalId, new Vector2(aimX, aimY), Mathf.Clamp(aimStrength, 0.0f, 1.0f), true);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcSyncPlayerMovementState(int globalId, int directionIndex, int strengthValue, float worldX, float worldY, bool includePosition) {
        SetPlayerMovementState(globalId, new QuantizedInputState(directionIndex, ToInputStrength(strengthValue)), includePosition, worldX, worldY);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    public void RpcSyncPlayerPosition(int globalId, float worldX, float worldY) {
        if (_playersByGlobalId.TryGetValue(globalId, out var player) && IsInstanceValid(player))
            player.SetSyncedPosition(new Vector2(worldX, worldY));
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcSyncPlayerAimState(int globalId, int directionIndex, int strengthValue, bool isAiming) {
        SetPlayerAimState(globalId, new QuantizedInputState(directionIndex, ToInputStrength(strengthValue)), isAiming, true);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcSyncPlayerItem(int globalId, string itemId) {
        SetPlayerItem(globalId, itemId);
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
        var item = LoadItem(itemId);
        if (item == null)
            return;

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

    private Vector2I GetTestPlayerTilePosition(int globalId) {
        return globalId switch {
            0 => new Vector2I(7, 10),
            1 => new Vector2I(21, 10),
            _ => new Vector2I(7 + (globalId % 14), 10),
        };
    }

    private void OnConnectionStateChanged() {
        PrintTestNetworkLog($"Connection state changed. {GetNetworkDebugText()}");
        UpdateStatusLabel();
    }

    private void OnLobbyStateChanged() {
        SyncPlayersWithNetworkData();
        UpdateStatusLabel();
    }

    private int GetConnectedPeerCount() {
        return _networking.HasActiveNetworkPeer ? Multiplayer.GetPeers().Length : 0;
    }

    private void PrintTestNetworkLog(string message) {
        GD.Print($"[PlayerItemRoomTest][Mode={_networking.CurrentMode}] {message}");
    }

    private void UpdateStatusLabel() {
        if (_networking == null)
            return;

        _statusLabel.Text = $"Player Item Test Room\nPeers connected: {GetConnectedPeerCount()}\nControls: B item menu | arrows/d-pad + Enter/A select | F fire mode | left mouse / Xbox RT use\nPlayers: {GetPlayerText()}";
    }

    private string GetPlayerText() {
        var playerTexts = new List<string>();
        foreach (var playerEntry in _playersByGlobalId) {
            var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(playerEntry.Key);
            var ownerText = playerData == null ? "peer ?" : $"peer {playerData.PeerId}:local {playerData.LocalId}";
            var inputText = GetPlayerInputText(playerEntry.Key);
            var itemText = _itemsByGlobalId.TryGetValue(playerEntry.Key, out var item) ? item.DisplayName : "none";
            var fireModeText = item != null ? $" {GetSelectedFireMode(playerEntry.Key, item)}" : string.Empty;
            var accuracyText = _accuracyStatesByGlobalId.TryGetValue(playerEntry.Key, out var accuracyState) ? $" acc {accuracyState.CurrentAccuracy:0.000}" : string.Empty;
            playerTexts.Add($"P{playerEntry.Key} {ownerText} {inputText} item {itemText}{fireModeText}{accuracyText}");
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
