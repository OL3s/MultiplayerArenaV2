using Godot;
using System.Collections.Generic;

public partial class TestPlayerItemRoomLAN : Node2D {
    private static readonly Vector2I TestTileSize = new(16, 16);
    private const float TestPlayerMoveSpeed = 96.0f;
    private const float GamepadDeadzone = 0.25f;
    private const float InputStopThreshold = 0.18f;
    private const float InputFullEnterThreshold = 0.70f;
    private const float InputFullExitThreshold = 0.60f;
    private const int DirectionBucketCount = 16;

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

        public LocalInputVectors(Vector2 movement, Vector2 aim, bool aimFallsBackToMovement) {
            Movement = movement;
            Aim = aim;
            AimFallsBackToMovement = aimFallsBackToMovement;
        }
    }

    private ArenaMapData _arenaMapData;
    private ArenaTileLayerRenderer _tileLayerRenderer;
    private Camera2D _camera;
    private Label _statusLabel;
    private Networking _networking;
    private LevelProp _centerProp;
    private readonly Dictionary<int, DamageTestPlayer> _playersByGlobalId = new();
    private readonly Dictionary<int, QuantizedInputState> _movementStatesByGlobalId = new();
    private readonly Dictionary<int, QuantizedInputState> _aimStatesByGlobalId = new();
    private readonly Dictionary<int, QuantizedInputState> _lastLocalMovementStatesByGlobalId = new();
    private readonly Dictionary<int, QuantizedInputState> _lastLocalAimStatesByGlobalId = new();
    private readonly Dictionary<int, string> _itemOverrideByGlobalId = new();

    [Export]
    public string ClientAddress { get; set; } = "127.0.0.1";

    [Export]
    public int ClientPort { get; set; } = 7700;

    public override void _Ready() {
        _tileLayerRenderer = GetNode<ArenaTileLayerRenderer>("ArenaTileLayerRenderer");
        _camera = GetNode<Camera2D>("Camera2D");
        _statusLabel = GetNode<Label>("CanvasLayer/StatusLabel");
        _networking = GetNode<Networking>("/root/Networking");

        ApplyCommandLineOverrides();
        EnsureDefaultNetworkMode();
        EnsureTestLocalLobbyPlayer();

        _networking.ConnectionStateChanged += OnConnectionStateChanged;
        _networking.LobbyStateChanged += OnLobbyStateChanged;
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
        UpdateStatusLabel();
    }

    public override void _PhysicsProcess(double delta) {
        ProcessLocalPlayerInputStates();
        if (_networking.IsServer || _networking.IsLocal)
            SimulatePlayerMovement(delta);

        SyncMovingPlayerPositions();
    }

    public override void _UnhandledInput(InputEvent @event) {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return;

        var itemOverride = keyEvent.PhysicalKeycode switch {
            Key.Key1 => "Pistol-T1",
            Key.Key2 => "Smg-T1",
            Key.Key3 => "AR-T1",
            Key.Key4 => "Rifle-T1",
            Key.Key5 => "NadeExplosive",
            _ => string.Empty,
        };

        if (!string.IsNullOrEmpty(itemOverride))
            ApplyLocalItemOverride(itemOverride);
    }

    private void TryStartHost() {
        var started = _networking.BeginHostingSession();
        PrintTestNetworkLog($"Host start result: {started}. Port: {_networking.CurrentServerPort}.");
    }

    private void TryStartClient() {
        var started = _networking.BeginDirectClientConnection(ClientAddress, ClientPort);
        PrintTestNetworkLog($"Client connect start result: {started}. Target: {ClientAddress}:{ClientPort}.");
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
            _itemOverrideByGlobalId.Remove(removedGlobalId);
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
        _itemOverrideByGlobalId.Clear();
    }

    private void AddPlayer(int globalId, Vector2I tilePosition) {
        var player = new DamageTestPlayer { Name = $"ItemTestPlayer{globalId}" };
        AddChild(player);
        player.Initialize(globalId, TileToWorldCenter(tilePosition));
        _playersByGlobalId[globalId] = player;
        _movementStatesByGlobalId[globalId] = GetNoInputState();
        _aimStatesByGlobalId[globalId] = new QuantizedInputState(0, InputStrength.Full);
        _itemOverrideByGlobalId[globalId] = "Pistol-T1";
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

            _lastLocalMovementStatesByGlobalId.TryGetValue(playerEntry.Key, out var previousMovementState);
            _lastLocalAimStatesByGlobalId.TryGetValue(playerEntry.Key, out var previousAimState);

            var inputVectors = GetLocalInputVectors(localPlayerData, player);
            var movementState = QuantizeInput(inputVectors.Movement, previousMovementState);
            var aimState = GetAimState(inputVectors, movementState, previousAimState);
            ApplyLocalAimDisplay(player, inputVectors, aimState);
            ApplyLocalMovementStateChange(playerEntry.Key, inputVectors.Movement, movementState);
            ApplyLocalAimStateChange(playerEntry.Key, aimState);
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

    private void ApplyLocalAimStateChange(int globalId, QuantizedInputState aimState) {
        if (_lastLocalAimStatesByGlobalId.TryGetValue(globalId, out var lastState) && lastState.Equals(aimState))
            return;

        _lastLocalAimStatesByGlobalId[globalId] = aimState;
        SetPlayerAimState(globalId, aimState, false);

        if (_networking.IsClient && _networking.HasActiveNetworkPeer)
            RpcId(1, nameof(RpcRequestSetPlayerAimState), globalId, aimState.DirectionIndex, (int)aimState.Strength);
        else if (CanSendHostRpc())
            SyncPlayerAimState(globalId, aimState);
    }

    private void SimulatePlayerMovement(double delta) {
        foreach (var playerEntry in _playersByGlobalId) {
            var player = playerEntry.Value;
            if (!IsInstanceValid(player) || player.IsDead())
                continue;

            if (!_movementStatesByGlobalId.TryGetValue(playerEntry.Key, out var movementState) || !movementState.HasInput)
                continue;

            var speedMultiplier = movementState.Strength == InputStrength.Full ? 1.0f : 0.5f;
            player.MoveWithVelocity(DirectionIndexToVector(movementState.DirectionIndex) * TestPlayerMoveSpeed * speedMultiplier);
        }
    }

    private void SetPlayerMovementState(int globalId, QuantizedInputState movementState, bool forcePositionSync, float worldX = 0.0f, float worldY = 0.0f) {
        _movementStatesByGlobalId[globalId] = movementState;

        if (forcePositionSync && _playersByGlobalId.TryGetValue(globalId, out var player) && IsInstanceValid(player))
            player.SetSyncedPosition(new Vector2(worldX, worldY));
    }

    private void SetPlayerAimState(int globalId, QuantizedInputState aimState, bool syncToPlayer) {
        _aimStatesByGlobalId[globalId] = aimState;

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

    private void SyncPlayerAimState(int globalId, QuantizedInputState aimState) {
        if (!CanSendHostRpc())
            return;

        Rpc(nameof(RpcSyncPlayerAimState), globalId, aimState.DirectionIndex, (int)aimState.Strength);
    }

    private void ApplyLocalItemOverride(string itemOverride) {
        foreach (var playerEntry in _playersByGlobalId) {
            var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(playerEntry.Key);
            if (playerData == null || !playerData.IsLocalPlayer)
                continue;

            if (_networking.IsClient && _networking.HasActiveNetworkPeer)
                RpcId(1, nameof(RpcRequestSetPlayerItemOverride), playerEntry.Key, itemOverride);
            else if (CanSendHostRpc()) {
                SetPlayerItemOverride(playerEntry.Key, itemOverride);
                SyncPlayerItemOverride(playerEntry.Key, itemOverride);
            }
            else {
                SetPlayerItemOverride(playerEntry.Key, itemOverride);
            }

            return;
        }
    }

    private void SetPlayerItemOverride(int globalId, string itemOverride) {
        _itemOverrideByGlobalId[globalId] = itemOverride;
        PrintTestNetworkLog($"P{globalId} item override: {itemOverride}.");
        UpdateStatusLabel();
    }

    private void SyncPlayerItemOverride(int globalId, string itemOverride) {
        if (CanSendHostRpc())
            Rpc(nameof(RpcSyncPlayerItemOverride), globalId, itemOverride);
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
                false),
            LocalPlayerData.LocalInputType.Gamepad => new LocalInputVectors(
                GetGamepadMovementInput(localPlayerData.DeviceId),
                GetGamepadAimInput(localPlayerData.DeviceId),
                true),
            _ => new LocalInputVectors(Vector2.Zero, Vector2.Zero, false),
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
    public void RpcRequestSetPlayerAimState(int globalId, int directionIndex, int strengthValue) {
        if (!IsPlayerStateRequestAllowed(globalId))
            return;

        var aimState = new QuantizedInputState(directionIndex, ToInputStrength(strengthValue));
        SetPlayerAimState(globalId, aimState, true);
        SyncPlayerAimState(globalId, aimState);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRequestSetPlayerItemOverride(int globalId, string itemOverride) {
        if (!IsPlayerStateRequestAllowed(globalId))
            return;

        SetPlayerItemOverride(globalId, itemOverride);
        SyncPlayerItemOverride(globalId, itemOverride);
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
    public void RpcSyncPlayerAimState(int globalId, int directionIndex, int strengthValue) {
        SetPlayerAimState(globalId, new QuantizedInputState(directionIndex, ToInputStrength(strengthValue)), true);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcSyncPlayerItemOverride(int globalId, string itemOverride) {
        SetPlayerItemOverride(globalId, itemOverride);
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

        _statusLabel.Text = $"Player Item Test Room\nPeers connected: {GetConnectedPeerCount()}\nControls: 1 Pistol-T1 | 2 Smg-T1 | 3 AR-T1 | 4 Rifle-T1 | 5 NadeExplosive\nPlayers: {GetPlayerText()}";
    }

    private string GetPlayerText() {
        var playerTexts = new List<string>();
        foreach (var playerEntry in _playersByGlobalId) {
            var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(playerEntry.Key);
            var ownerText = playerData == null ? "peer ?" : $"peer {playerData.PeerId}:local {playerData.LocalId}";
            var inputText = GetPlayerInputText(playerEntry.Key);
            var itemText = _itemOverrideByGlobalId.TryGetValue(playerEntry.Key, out var itemOverride) ? itemOverride : "none";
            playerTexts.Add($"P{playerEntry.Key} {ownerText} {inputText} item {itemText}");
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
