using Godot;

public partial class TestMapDestructionLogicLAN : Node2D
{
    private static readonly Vector2I TestTileSize = new(16, 16);
    private const float TestExplosiveRadius = 56.0f;
    private const int TestExplosiveDamage = 2;
    private static readonly Color DebugExplosiveRadiusColor = new(1.0f, 0.55f, 0.2f, 0.9f);

    private static readonly (Vector2I Position, int DamageAmount)[] InitialWallDamageSamples =
    {
        (new Vector2I(3, 4), 1),
        (new Vector2I(14, 4), 1),
        (new Vector2I(22, 8), 3),
        (new Vector2I(9, 17), 1),
    };

    private ArenaMapData _arenaMapData;
    private ArenaTileLayerRenderer _tileLayerRenderer;
    private DebugExplosionRadiusDrawer _debugRadiusDrawer;
    private Camera2D _camera;
    private Label _statusLabel;
    private Networking _networking;

    [Export]
    public string ClientAddress { get; set; } = "127.0.0.1";

    [Export]
    public int ClientPort { get; set; } = 7700;

    public override void _Ready()
    {
        _tileLayerRenderer = GetNode<ArenaTileLayerRenderer>("ArenaTileLayerRenderer");
        _camera = GetNode<Camera2D>("Camera2D");
        _statusLabel = GetNode<Label>("CanvasLayer/StatusLabel");
        _networking = GetNode<Networking>("/root/Networking");
        _debugRadiusDrawer = CreateDebugRadiusDrawer();

        ApplyCommandLineOverrides();
        EnsureDefaultNetworkMode();

        _networking.ConnectionStateChanged += OnConnectionStateChanged;
        UpdateStatusLabel();
        PrintTestNetworkLog("Scene ready.");

        if (_networking.IsServer && !_networking.HasActiveNetworkPeer)
        {
            TryStartHost();
        }
        else if (_networking.IsClient && !_networking.HasActiveNetworkPeer)
        {
            TryStartClient();
        }

        BuildMockArena();
        CenterCamera();
    }

    public override void _ExitTree()
    {
        if (_networking == null)
        {
            return;
        }

        _networking.ConnectionStateChanged -= OnConnectionStateChanged;
    }

    public override void _Process(double delta)
    {
        _debugRadiusDrawer.QueueRedraw();
        UpdateStatusLabel();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!CanApplyHostInput() || @event is not InputEventMouseButton mouseButtonEvent || !mouseButtonEvent.Pressed)
        {
            return;
        }

        if (mouseButtonEvent.ButtonIndex == MouseButton.Right)
        {
            BuildMockArena();
            ReplicateMockArenaReset();
            return;
        }

        if (mouseButtonEvent.ButtonIndex == MouseButton.Left)
        {
            if (mouseButtonEvent.ShiftPressed)
            {
                DamageWallsInExplosiveRadius();
            }
            else
            {
                DamageWallUnderCursor();
            }

            return;
        }
    }

    private void TryStartHost()
    {
        var started = _networking.BeginHostingSession();
        PrintTestNetworkLog($"Host start result: {started}. Port: {_networking.CurrentServerPort}.");
    }

    private void TryStartClient()
    {
        var started = _networking.BeginDirectClientConnection(ClientAddress, ClientPort);
        PrintTestNetworkLog($"Client connect start result: {started}. Target: {ClientAddress}:{ClientPort}.");
    }

    private void ApplyCommandLineOverrides()
    {
        var arguments = OS.GetCmdlineUserArgs();
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument == "--address" && TryGetNextArgument(arguments, ref i, out var addressValue))
            {
                ClientAddress = addressValue;
                continue;
            }

            if (argument.StartsWith("--address="))
            {
                ClientAddress = argument[10..];
                continue;
            }

            if (argument == "--port" && TryGetNextArgument(arguments, ref i, out var portValue))
            {
                ApplyPortOverride(portValue);
                continue;
            }

            if (argument.StartsWith("--port="))
            {
                ApplyPortOverride(argument[7..]);
            }
        }
    }

    private void EnsureDefaultNetworkMode()
    {
        if (!_networking.HasSelectedMode)
        {
            _networking.SetLan();
            PrintTestNetworkLog("No network role selected. Defaulting test scene to Lan.");
        }
    }

    private void ApplyPortOverride(string portValue)
    {
        if (int.TryParse(portValue, out var parsedPort) && parsedPort > 0 && parsedPort <= 65535)
        {
            ClientPort = parsedPort;
        }
    }

    private static bool TryGetNextArgument(string[] arguments, ref int index, out string value)
    {
        value = string.Empty;
        if (index + 1 >= arguments.Length)
        {
            return false;
        }

        index++;
        value = arguments[index];
        return true;
    }

    private void BuildMockArena()
    {
        _arenaMapData = new ArenaMapData
        {
            SourceId = 0,
            WallDamageSourceId = 1,
            DefaultWallMaxDamage = 3,
        };

        AddFloorRectangle(new Rect2I(4, 4, 10, 8));
        AddFloorRectangle(new Rect2I(14, 7, 8, 3));
        AddFloorRectangle(new Rect2I(10, 12, 6, 5));
        _arenaMapData.ResetWallTiles();
        ApplyInitialWallDamage();
        _tileLayerRenderer.Render(_arenaMapData);
    }

    private void AddFloorRectangle(Rect2I rect)
    {
        for (var x = rect.Position.X; x < rect.End.X; x++)
        {
            for (var y = rect.Position.Y; y < rect.End.Y; y++)
            {
                _arenaMapData.AddFloorTile(new Vector2I(x, y));
            }
        }
    }

    private void DamageWallUnderCursor()
    {
        var tilePosition = _arenaMapData.WorldToTile(GetGlobalMousePosition(), TestTileSize);
        if (!_arenaMapData.DamageWallTile(tilePosition))
        {
            return;
        }

        _tileLayerRenderer.Render(_arenaMapData);
        ReplicateWallDamage(tilePosition, 1);
    }

    private void DamageWallsInExplosiveRadius()
    {
        var centerTile = _arenaMapData.WorldToTile(GetGlobalMousePosition(), TestTileSize);
        var tileRadius = Mathf.CeilToInt(TestExplosiveRadius / Mathf.Max(1, TestTileSize.X));
        var changedTiles = _arenaMapData.DamageWallsInRadius(centerTile, tileRadius, TestExplosiveDamage);

        if (changedTiles.Count == 0)
        {
            return;
        }

        _tileLayerRenderer.Render(_arenaMapData);
        ReplicateRadiusDamage(centerTile, tileRadius, TestExplosiveDamage);
    }

    private void ApplyInitialWallDamage()
    {
        foreach (var (position, damageAmount) in InitialWallDamageSamples)
        {
            _arenaMapData.DamageWallTile(position, damageAmount);
        }
    }

    private DebugExplosionRadiusDrawer CreateDebugRadiusDrawer()
    {
        var debugRadiusDrawer = new DebugExplosionRadiusDrawer
        {
            Name = "DebugExplosionRadiusDrawer",
            Radius = TestExplosiveRadius,
            DrawColor = DebugExplosiveRadiusColor,
            CanDraw = CanApplyHostInput,
            ZIndex = 10,
        };

        AddChild(debugRadiusDrawer);
        return debugRadiusDrawer;
    }

    private void ReplicateMockArenaReset()
    {
        if (!CanSendHostRpc())
        {
            return;
        }

        PrintTestNetworkLog("RPC send: reset mock arena.");
        Rpc(nameof(RpcResetMockArena));
    }

    private void ReplicateWallDamage(Vector2I tilePosition, int damageAmount)
    {
        if (!CanSendHostRpc())
        {
            return;
        }

        PrintTestNetworkLog($"RPC send: wall damage at {tilePosition} amount {damageAmount}.");
        Rpc(nameof(RpcDamageWallTile), tilePosition.X, tilePosition.Y, damageAmount);
    }

    private void ReplicateRadiusDamage(Vector2I centerTile, int radius, int damageAmount)
    {
        if (!CanSendHostRpc())
        {
            return;
        }

        PrintTestNetworkLog($"RPC send: radius damage at {centerTile} radius {radius} amount {damageAmount}.");
        Rpc(nameof(RpcDamageWallsInRadius), centerTile.X, centerTile.Y, radius, damageAmount);
    }

    private bool CanSendHostRpc()
    {
        return _networking.HasActiveNetworkPeer && _networking.IsServer;
    }

    private bool CanApplyHostInput()
    {
        return _networking.IsServer || _networking.IsLocal;
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcResetMockArena()
    {
        PrintTestNetworkLog("RPC apply: reset mock arena.");
        BuildMockArena();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcDamageWallTile(int x, int y, int damageAmount)
    {
        if (_arenaMapData == null || !_arenaMapData.DamageWallTile(new Vector2I(x, y), damageAmount))
        {
            return;
        }

        PrintTestNetworkLog($"RPC apply: wall damage at ({x}, {y}) amount {damageAmount}.");
        _tileLayerRenderer.Render(_arenaMapData);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcDamageWallsInRadius(int centerX, int centerY, int radius, int damageAmount)
    {
        if (_arenaMapData == null)
        {
            return;
        }

        var changedTiles = _arenaMapData.DamageWallsInRadius(new Vector2I(centerX, centerY), radius, damageAmount);
        if (changedTiles.Count == 0)
        {
            return;
        }

        PrintTestNetworkLog($"RPC apply: radius damage at ({centerX}, {centerY}) radius {radius} amount {damageAmount}.");
        _tileLayerRenderer.Render(_arenaMapData);
    }

    private void OnConnectionStateChanged()
    {
        PrintTestNetworkLog($"Connection state changed. {GetNetworkDebugText()}");
        UpdateStatusLabel();
    }

    private int GetConnectedPeerCount()
    {
        return _networking.HasActiveNetworkPeer ? Multiplayer.GetPeers().Length : 0;
    }

    private void PrintTestNetworkLog(string message)
    {
        GD.Print($"[LANDestructionTest][Mode={_networking.CurrentMode}] {message}");
    }

    private void UpdateStatusLabel()
    {
        if (_networking == null)
        {
            return;
        }

        _statusLabel.Text = CanApplyHostInput()
            ? $"Peers connected: {GetConnectedPeerCount()}"
            : string.Empty;
    }

    private string GetNetworkDebugText()
    {
        return $"Network: {_networking.CurrentMode}. Client: {GetClientConnectionText()}. Peers connected: {GetConnectedPeerCount()}.";
    }

    private string GetClientConnectionText()
    {
        if (!_networking.IsClient)
        {
            return "Not client";
        }

        return IsClientConnected()
            ? $"Connected to {ClientAddress}:{ClientPort}"
            : $"Not connected to {ClientAddress}:{ClientPort}";
    }

    private bool IsClientConnected()
    {
        return _networking.HasActiveNetworkPeer
            && Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;
    }

    private void CenterCamera()
    {
        var usedRect = _tileLayerRenderer.FloorLayer.GetUsedRect();
        if (usedRect.Size == Vector2I.Zero)
        {
            return;
        }

        var centerCell = usedRect.Position + (usedRect.Size / 2);
        _camera.Position = _tileLayerRenderer.FloorLayer.MapToLocal(centerCell);
    }
}
