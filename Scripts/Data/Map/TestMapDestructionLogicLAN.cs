using Godot;
using System.Collections.Generic;

public partial class TestMapDestructionLogicLAN : Node2D {
    private static readonly Vector2I TestTileSize = new(16, 16);
    private const float TestExplosiveRadius = 56.0f;
    private const float TestBulletDamage = 125.0f;
    private const float TestExplosiveDamage = 250.0f;
    private static readonly Color DebugExplosiveRadiusColor = new(1.0f, 0.55f, 0.2f, 0.9f);

    private static readonly (Vector2I Position, float DamageAmount)[] InitialWallDamageSamples = {
        (new Vector2I(3, 4), 125.0f),
        (new Vector2I(14, 4), 250.0f),
        (new Vector2I(22, 8), 375.0f),
        (new Vector2I(9, 17), 125.0f),
    };

    private ArenaMapData _arenaMapData;
    private ArenaTileLayerRenderer _tileLayerRenderer;
    private DebugExplosionRadiusDrawer _debugRadiusDrawer;
    private Camera2D _camera;
    private Label _statusLabel;
    private Networking _networking;
    private DamageType _selectedDamageType = DamageType.Crush;
    private readonly List<LevelProp> _props = new();
    private readonly Dictionary<int, DamageTestPlayer> _playersByGlobalId = new();

    [Export]
    public string ClientAddress { get; set; } = "127.0.0.1";

    [Export]
    public int ClientPort { get; set; } = 7700;

    [Export]
    public BiomeConfig.BiomeType TestWallBiome { get; set; } = BiomeConfig.BiomeType.Arena;

    public override void _Ready() {
        _tileLayerRenderer = GetNode<ArenaTileLayerRenderer>("ArenaTileLayerRenderer");
        _camera = GetNode<Camera2D>("Camera2D");
        _statusLabel = GetNode<Label>("CanvasLayer/StatusLabel");
        _networking = GetNode<Networking>("/root/Networking");
        _debugRadiusDrawer = CreateDebugRadiusDrawer();

        ApplyCommandLineOverrides();
        EnsureDefaultNetworkMode();
        EnsureTestLocalLobbyPlayer();

        _networking.ConnectionStateChanged += OnConnectionStateChanged;
        _networking.LobbyStateChanged += OnLobbyStateChanged;
        UpdateStatusLabel();
        PrintTestNetworkLog("Scene ready.");

        if (_networking.IsServer && !_networking.HasActiveNetworkPeer)
            TryStartHost();
        else if (_networking.IsClient && !_networking.HasActiveNetworkPeer) {
            TryStartClient();
        }

        BuildMockArena();
        CenterCamera();
    }

    public override void _ExitTree() {
        if (_networking == null)
            return;

        _networking.ConnectionStateChanged -= OnConnectionStateChanged;
        _networking.LobbyStateChanged -= OnLobbyStateChanged;
    }

    public override void _Process(double delta) {
        _debugRadiusDrawer.QueueRedraw();
        UpdateStatusLabel();
    }

    public override void _UnhandledInput(InputEvent @event) {
        if (!CanApplyHostInput())
            return;

        if (HandleDamageTypeInput(@event))
            return;

        if (@event is not InputEventMouseButton mouseButtonEvent || !mouseButtonEvent.Pressed)
            return;

        if (mouseButtonEvent.ButtonIndex == MouseButton.Right) {
            BuildMockArena();
            ReplicateMockArenaReset();
            return;
        }

        if (mouseButtonEvent.ButtonIndex == MouseButton.Left) {
            if (mouseButtonEvent.ShiftPressed)
                DamageWallsInExplosiveRadius();
            else {
                DamageWallUnderCursor();
            }

            return;
        }
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
        var targetLocalPlayerCount = _networking.IsClient ? 2 : 1;
        _networking.LocalLobbyData.LocalPlayers.Clear();

        for (var localId = 0; localId < targetLocalPlayerCount; localId++) {
            _networking.LocalLobbyData.LocalPlayers.Add(new LocalPlayerData {
                LocalId = localId,
                IsActive = true,
                InputType = localId == 0 ? LocalPlayerData.LocalInputType.KeyboardMouse : LocalPlayerData.LocalInputType.Gamepad,
                DeviceId = localId == 0 ? 0 : localId - 1,
                DisplayName = GetDefaultTestPlayerName(localId),
            });
        }
    }

    private string GetDefaultTestPlayerName(int localId) {
        return _networking.IsClient ? $"Client LocalId {localId}" : $"Host LocalId {localId}";
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

    private void BuildMockArena() {
        _arenaMapData = new ArenaMapData {
            SourceId = 0,
            WallDamageSourceId = 1,
            DefaultWallMaxDamage = WallDamageData.DefaultWallHealth,
            DefaultWallBiome = TestWallBiome,
        };

        AddFloorRectangle(new Rect2I(4, 4, 10, 8));
        AddFloorRectangle(new Rect2I(14, 7, 8, 3));
        AddFloorRectangle(new Rect2I(10, 12, 6, 5));
        _arenaMapData.ResetWallTiles();
        RebuildDamageTestPlayersFromNetworkData();
        BuildMockProps();
        ApplyInitialWallDamage();
        _tileLayerRenderer.Render(_arenaMapData);
    }

    private void AddFloorRectangle(Rect2I rect) {
        for (var x = rect.Position.X; x < rect.End.X; x++) {
            for (var y = rect.Position.Y; y < rect.End.Y; y++)
                _arenaMapData.AddFloorTile(new Vector2I(x, y));
        }
    }

    private void DamageWallUnderCursor() {
        var mousePosition = GetGlobalMousePosition();
        var playerGlobalId = GetPlayerGlobalIdAtWorldPosition(mousePosition);
        if (playerGlobalId >= 0) {
            if (!DamagePlayer(playerGlobalId, _selectedDamageType, TestBulletDamage))
                return;

            ReplicatePlayerDamage(playerGlobalId, _selectedDamageType, TestBulletDamage);
            return;
        }

        var propIndex = GetPropIndexAtWorldPosition(mousePosition);
        if (propIndex >= 0) {
            if (!DamageProp(propIndex, _selectedDamageType, TestBulletDamage))
                return;

            ReplicatePropDamage(propIndex, _selectedDamageType, TestBulletDamage);
            return;
        }

        var tilePosition = _arenaMapData.WorldToTile(GetGlobalMousePosition(), TestTileSize);
        if (!_arenaMapData.DamageWallTile(tilePosition, _selectedDamageType, TestBulletDamage))
            return;

        _tileLayerRenderer.Render(_arenaMapData);
        ReplicateWallDamage(tilePosition, _selectedDamageType, TestBulletDamage);
    }

    private void DamageWallsInExplosiveRadius() {
        var worldCenter = GetGlobalMousePosition();
        var changedPlayers = DamagePlayersInWorldRadius(worldCenter, TestExplosiveRadius, _selectedDamageType, TestExplosiveDamage);
        var changedProps = DamagePropsInWorldRadius(worldCenter, TestExplosiveRadius, _selectedDamageType, TestExplosiveDamage);
        var centerTile = _arenaMapData.WorldToTile(worldCenter, TestTileSize);
        var tileRadius = Mathf.CeilToInt(TestExplosiveRadius / Mathf.Max(1, TestTileSize.X));
        var changedTiles = _arenaMapData.DamageWallsInRadius(centerTile, tileRadius, _selectedDamageType, TestExplosiveDamage);

        if (changedTiles.Count == 0 && !changedProps && !changedPlayers)
            return;

        _tileLayerRenderer.Render(_arenaMapData);
        ReplicateRadiusDamage(worldCenter, centerTile, tileRadius, _selectedDamageType, TestExplosiveDamage);
    }

    private void ApplyInitialWallDamage() {
        foreach (var (position, damageAmount) in InitialWallDamageSamples)
            _arenaMapData.DamageWallTile(position, DamageType.Crush, damageAmount);
    }

    private void BuildMockProps() {
        ClearMockProps();
        AddMockProp(LevelPropType.Barrel, new Vector2I(7, 7));
        AddMockProp(LevelPropType.Rock, new Vector2I(17, 8));
        AddMockProp(LevelPropType.Tree, new Vector2I(12, 14));
    }

    private void AddMockProp(LevelPropType propType, Vector2I tilePosition) {
        var propData = new LevelPropData();
        propData.Configure(propType);

        var prop = new LevelProp { Name = propData.DisplayName };
        AddChild(prop);
        prop.Initialize(propData, TileToWorldCenter(tilePosition));
        _props.Add(prop);
    }

    private void ClearMockProps() {
        foreach (var prop in _props) {
            if (IsInstanceValid(prop))
                prop.QueueFree();
        }

        _props.Clear();
    }

    private void SyncDamageTestPlayersWithNetworkData() {
        var activeGlobalIds = new HashSet<int>();
        foreach (var playerData in _networking.MultiplayerData.Players) {
            if (playerData.GlobalId < 0)
                continue;

            activeGlobalIds.Add(playerData.GlobalId);
            if (_playersByGlobalId.TryGetValue(playerData.GlobalId, out var existingPlayer) && IsInstanceValid(existingPlayer))
                continue;

            AddDamageTestPlayer(playerData.GlobalId, GetTestPlayerTilePosition(playerData.GlobalId));
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
        }
    }

    private void RebuildDamageTestPlayersFromNetworkData() {
        ClearDamageTestPlayers();
        SyncDamageTestPlayersWithNetworkData();
    }

    private void ClearDamageTestPlayers() {
        foreach (var player in _playersByGlobalId.Values) {
            if (IsInstanceValid(player))
                player.QueueFree();
        }

        _playersByGlobalId.Clear();
    }

    private void AddDamageTestPlayer(int globalId, Vector2I tilePosition) {
        var player = new DamageTestPlayer { Name = $"DamageTestPlayer{globalId}" };
        AddChild(player);
        player.Initialize(globalId, TileToWorldCenter(tilePosition));
        _playersByGlobalId[globalId] = player;
    }

    private void RespawnDamageTestPlayers() {
        foreach (var playerEntry in _playersByGlobalId)
            playerEntry.Value.Respawn(TileToWorldCenter(GetTestPlayerTilePosition(playerEntry.Key)));
    }

    private DebugExplosionRadiusDrawer CreateDebugRadiusDrawer() {
        var debugRadiusDrawer = new DebugExplosionRadiusDrawer {
            Name = "DebugExplosionRadiusDrawer",
            Radius = TestExplosiveRadius,
            DrawColor = DebugExplosiveRadiusColor,
            CanDraw = CanApplyHostInput,
            ZIndex = 10,
        };

        AddChild(debugRadiusDrawer);
        return debugRadiusDrawer;
    }

    private void ReplicateMockArenaReset() {
        if (!CanSendHostRpc())
            return;

        PrintTestNetworkLog("RPC send: reset mock arena.");
        Rpc(nameof(RpcResetMockArena));
    }

    private void ReplicateWallDamage(Vector2I tilePosition, DamageType damageType, float damageAmount) {
        if (!CanSendHostRpc())
            return;

        PrintTestNetworkLog($"RPC send: {damageType} wall damage at {tilePosition} amount {damageAmount}.");
        Rpc(nameof(RpcDamageWallTile), tilePosition.X, tilePosition.Y, (int)damageType, damageAmount);
    }

    private void ReplicatePropDamage(int propIndex, DamageType damageType, float damageAmount) {
        if (!CanSendHostRpc())
            return;

        PrintTestNetworkLog($"RPC send: {damageType} prop damage at index {propIndex} amount {damageAmount}.");
        Rpc(nameof(RpcDamageProp), propIndex, (int)damageType, damageAmount);
    }

    private void ReplicatePlayerDamage(int globalId, DamageType damageType, float damageAmount) {
        if (!CanSendHostRpc())
            return;

        PrintTestNetworkLog($"RPC send: {damageType} player damage for global id {globalId} amount {damageAmount}.");
        Rpc(nameof(RpcDamagePlayer), globalId, (int)damageType, damageAmount);
    }

    private void ReplicateRadiusDamage(Vector2 worldCenter, Vector2I centerTile, int radius, DamageType damageType, float damageAmount) {
        if (!CanSendHostRpc())
            return;

        PrintTestNetworkLog($"RPC send: {damageType} radius damage at {centerTile} radius {radius} amount {damageAmount}.");
        Rpc(nameof(RpcDamageInRadius), worldCenter.X, worldCenter.Y, centerTile.X, centerTile.Y, radius, (int)damageType, damageAmount);
    }

    private bool HandleDamageTypeInput(InputEvent @event) {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return false;

        var damageType = keyEvent.PhysicalKeycode switch {
            Key.Key1 => DamageType.Crush,
            Key.Key2 => DamageType.Slash,
            Key.Key3 => DamageType.Heat,
            Key.Key4 => DamageType.Explosive,
            Key.Kp1 => DamageType.Crush,
            Key.Kp2 => DamageType.Slash,
            Key.Kp3 => DamageType.Heat,
            Key.Kp4 => DamageType.Explosive,
            _ => _selectedDamageType,
        };

        if (damageType == _selectedDamageType)
            return false;

        _selectedDamageType = damageType;
        PrintTestNetworkLog($"Selected damage type: {_selectedDamageType}.");
        UpdateStatusLabel();
        return true;
    }

    private bool CanSendHostRpc() {
        return _networking.HasActiveNetworkPeer && _networking.IsServer;
    }

    private bool CanApplyHostInput() {
        return _networking.IsServer || _networking.IsLocal;
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcResetMockArena() {
        PrintTestNetworkLog("RPC apply: reset mock arena.");
        BuildMockArena();
        RespawnDamageTestPlayers();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcDamageWallTile(int x, int y, int damageTypeValue, float damageAmount) {
        var damageType = ToDamageType(damageTypeValue);
        if (_arenaMapData == null || !_arenaMapData.DamageWallTile(new Vector2I(x, y), damageType, damageAmount))
            return;

        PrintTestNetworkLog($"RPC apply: {damageType} wall damage at ({x}, {y}) amount {damageAmount}.");
        _tileLayerRenderer.Render(_arenaMapData);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcDamageProp(int propIndex, int damageTypeValue, float damageAmount) {
        var damageType = ToDamageType(damageTypeValue);
        if (!DamageProp(propIndex, damageType, damageAmount))
            return;

        PrintTestNetworkLog($"RPC apply: {damageType} prop damage at index {propIndex} amount {damageAmount}.");
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcDamagePlayer(int globalId, int damageTypeValue, float damageAmount) {
        var damageType = ToDamageType(damageTypeValue);
        if (!DamagePlayer(globalId, damageType, damageAmount))
            return;

        PrintTestNetworkLog($"RPC apply: {damageType} player damage for global id {globalId} amount {damageAmount}.");
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcDamageWallsInRadius(int centerX, int centerY, int radius, int damageTypeValue, float damageAmount) {
        ApplyRadiusDamage(Vector2.Zero, new Vector2I(centerX, centerY), radius, ToDamageType(damageTypeValue), damageAmount, false);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcDamageInRadius(float worldCenterX, float worldCenterY, int centerX, int centerY, int radius, int damageTypeValue, float damageAmount) {
        ApplyRadiusDamage(new Vector2(worldCenterX, worldCenterY), new Vector2I(centerX, centerY), radius, ToDamageType(damageTypeValue), damageAmount, true);
    }

    private void ApplyRadiusDamage(Vector2 worldCenter, Vector2I centerTile, int radius, DamageType damageType, float damageAmount, bool damageProps) {
        if (_arenaMapData == null)
            return;

        var changedPlayers = damageProps && DamagePlayersInWorldRadius(worldCenter, TestExplosiveRadius, damageType, damageAmount);
        var changedProps = damageProps && DamagePropsInWorldRadius(worldCenter, TestExplosiveRadius, damageType, damageAmount);
        var changedTiles = _arenaMapData.DamageWallsInRadius(centerTile, radius, damageType, damageAmount);
        if (changedTiles.Count == 0 && !changedProps && !changedPlayers)
            return;

        PrintTestNetworkLog($"RPC apply: {damageType} radius damage at {centerTile} radius {radius} amount {damageAmount}.");
        _tileLayerRenderer.Render(_arenaMapData);
    }

    private int GetPropIndexAtWorldPosition(Vector2 worldPosition) {
        for (var i = 0; i < _props.Count; i++) {
            var prop = _props[i];
            if (IsInstanceValid(prop) && prop.ContainsWorldPosition(worldPosition))
                return i;
        }

        return -1;
    }

    private int GetPlayerGlobalIdAtWorldPosition(Vector2 worldPosition) {
        foreach (var playerEntry in _playersByGlobalId) {
            var player = playerEntry.Value;
            if (IsInstanceValid(player) && player.ContainsWorldPosition(worldPosition))
                return playerEntry.Key;
        }

        return -1;
    }

    private bool DamagePlayer(int globalId, DamageType damageType, float damageAmount) {
        if (!_playersByGlobalId.TryGetValue(globalId, out var player) || !IsInstanceValid(player))
            return false;

        return player.ApplyDamage(DamageContainer.FromDamage(damageType, damageAmount));
    }

    private bool DamagePlayersInWorldRadius(Vector2 worldCenter, float radius, DamageType damageType, float damageAmount) {
        var changed = false;
        foreach (var player in _playersByGlobalId.Values) {
            if (!IsInstanceValid(player) || !player.IsInsideWorldRadius(worldCenter, radius))
                continue;

            var multiplier = player.GetRadiusDamageMultiplier(worldCenter, radius);
            if (multiplier <= 0.0f)
                continue;

            player.ApplyDamage(DamageContainer.FromDamage(damageType, damageAmount * multiplier));
            changed = true;
        }

        return changed;
    }

    private bool DamageProp(int propIndex, DamageType damageType, float damageAmount) {
        if (propIndex < 0 || propIndex >= _props.Count)
            return false;

        var prop = _props[propIndex];
        if (!IsInstanceValid(prop))
            return false;

        return prop.ApplyDamage(DamageContainer.FromDamage(damageType, damageAmount));
    }

    private bool DamagePropsInWorldRadius(Vector2 worldCenter, float radius, DamageType damageType, float damageAmount) {
        var changed = false;
        foreach (var prop in _props) {
            if (!IsInstanceValid(prop) || !prop.IsInsideWorldRadius(worldCenter, radius))
                continue;

            var multiplier = prop.GetRadiusDamageMultiplier(worldCenter, radius);
            if (multiplier <= 0.0f)
                continue;

            prop.ApplyDamage(DamageContainer.FromDamage(damageType, damageAmount * multiplier));
            changed = true;
        }

        return changed;
    }

    private Vector2 TileToWorldCenter(Vector2I tilePosition) {
        return new Vector2(
            (tilePosition.X * TestTileSize.X) + (TestTileSize.X * 0.5f),
            (tilePosition.Y * TestTileSize.Y) + (TestTileSize.Y * 0.5f));
    }

    private Vector2I GetTestPlayerTilePosition(int globalId) {
        return globalId switch {
            0 => new Vector2I(8, 8),
            1 => new Vector2I(18, 9),
            2 => new Vector2I(12, 15),
            3 => new Vector2I(20, 8),
            _ => new Vector2I(8 + (globalId % 10), 8 + (globalId / 10)),
        };
    }

    private static DamageType ToDamageType(int damageTypeValue) {
        return System.Enum.IsDefined(typeof(DamageType), damageTypeValue)
            ? (DamageType)damageTypeValue
            : DamageType.Crush;
    }

    private void OnConnectionStateChanged() {
        PrintTestNetworkLog($"Connection state changed. {GetNetworkDebugText()}");
        UpdateStatusLabel();
    }

    private void OnLobbyStateChanged() {
        SyncDamageTestPlayersWithNetworkData();
        UpdateStatusLabel();
    }

    private int GetConnectedPeerCount() {
        return _networking.HasActiveNetworkPeer ? Multiplayer.GetPeers().Length : 0;
    }

    private void PrintTestNetworkLog(string message) {
        GD.Print($"[LANDestructionTest][Mode={_networking.CurrentMode}] {message}");
    }

    private void UpdateStatusLabel() {
        if (_networking == null)
            return;

        _statusLabel.Text = CanApplyHostInput()
            ? $"Peers connected: {GetConnectedPeerCount()}\nBiome: {GetCurrentWallBiome()}\nDamage type: {GetDamageTypeSelectionText()}\nPlayers: {GetPlayerHealthText()}"
            : string.Empty;
    }

    private string GetPlayerHealthText() {
        var playerTexts = new List<string>();
        foreach (var playerEntry in _playersByGlobalId) {
            var player = playerEntry.Value;
            if (!IsInstanceValid(player) || player.Health == null)
                continue;

            var playerData = _networking.MultiplayerData.GetPlayerByGlobalId(playerEntry.Key);
            var ownerText = playerData == null ? "peer ?" : $"peer {playerData.PeerId}:local {playerData.LocalId}";
            playerTexts.Add($"P{playerEntry.Key} {ownerText} {player.Health.CurrentHealth}/{player.Health.MaxHealth}");
        }

        return playerTexts.Count == 0 ? "none" : string.Join(", ", playerTexts);
    }

    private BiomeConfig.BiomeType GetCurrentWallBiome() {
        return _arenaMapData?.DefaultWallBiome ?? TestWallBiome;
    }

    private string GetDamageTypeSelectionText() {
        return $"1 {FormatDamageTypeOption(DamageType.Crush)} | 2 {FormatDamageTypeOption(DamageType.Slash)} | 3 {FormatDamageTypeOption(DamageType.Heat)} | 4 {FormatDamageTypeOption(DamageType.Explosive)}";
    }

    private string FormatDamageTypeOption(DamageType damageType) {
        return damageType == _selectedDamageType ? $"[{damageType}]" : damageType.ToString();
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
