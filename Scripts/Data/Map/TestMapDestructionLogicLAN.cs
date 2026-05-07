using Godot;

public partial class TestMapDestructionLogicLAN : Node2D
{
    public enum TestLanRole
    {
        Host,
        Client,
    }

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

    private ArenaTileLayerRenderer _tileLayerRenderer;
    private Camera2D _camera;
    private Label _statusLabel;
    private Networking _networking;

    [Export]
    public TestLanRole Role { get; set; } = TestLanRole.Host;

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

        ApplyCommandLineOverrides();

        _networking.ConnectionStateChanged += OnConnectionStateChanged;
        _networking.ArenaMapChanged += OnArenaMapChanged;

        UpdateStatusLabel();

        if (Role == TestLanRole.Host && !_networking.HasActiveNetworkPeer)
        {
            TryStartHost();
        }
        else if (Role == TestLanRole.Client && !_networking.HasActiveNetworkPeer)
        {
            TryStartClient();
        }

        if (Role == TestLanRole.Host && _networking.IsServer && _networking.HasActiveNetworkPeer)
        {
            EnsureMockArenaBuilt();
        }
        else
        {
            _tileLayerRenderer.Render(_networking.ArenaMapData);
        }

        CenterCamera();
    }

    public override void _ExitTree()
    {
        if (_networking == null)
        {
            return;
        }

        _networking.ConnectionStateChanged -= OnConnectionStateChanged;
        _networking.ArenaMapChanged -= OnArenaMapChanged;
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
        UpdateStatusLabel();
    }

    public override void _Draw()
    {
        if (!CanApplyDamageInput())
        {
            return;
        }

        var localMousePosition = GetLocalMousePosition();
        DrawArc(localMousePosition, TestExplosiveRadius, 0.0f, Mathf.Tau, 48, DebugExplosiveRadiusColor, 2.0f);
        DrawCircle(localMousePosition, 3.0f, DebugExplosiveRadiusColor);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButtonEvent || !mouseButtonEvent.Pressed)
        {
            return;
        }

        if (!CanApplyDamageInput())
        {
            return;
        }

        if (mouseButtonEvent.ButtonIndex == MouseButton.Right)
        {
            EnsureMockArenaBuilt();
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
        }
    }

    private void TryStartHost()
    {
        _networking.SetServerLocal();
        _networking.BeginHostingSession();
    }

    private void TryStartClient()
    {
        _networking.BeginDirectClientConnection(ClientAddress, ClientPort);
    }

    private void ApplyCommandLineOverrides()
    {
        var arguments = OS.GetCmdlineUserArgs();
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument == "--role" && TryGetNextArgument(arguments, ref i, out var roleValue))
            {
                ApplyRoleOverride(roleValue);
                continue;
            }

            if (argument.StartsWith("--role="))
            {
                ApplyRoleOverride(argument[7..]);
                continue;
            }

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

    private void ApplyRoleOverride(string roleValue)
    {
        if (string.Equals(roleValue, "host", System.StringComparison.OrdinalIgnoreCase))
        {
            Role = TestLanRole.Host;
            return;
        }

        if (string.Equals(roleValue, "client", System.StringComparison.OrdinalIgnoreCase))
        {
            Role = TestLanRole.Client;
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

    private void EnsureMockArenaBuilt()
    {
        if (!_networking.IsServer)
        {
            return;
        }

        _networking.ArenaMapData.Clear();
        _networking.ArenaMapData.SourceId = 0;
        _networking.ArenaMapData.WallDamageSourceId = 1;
        _networking.ArenaMapData.DefaultWallMaxDamage = 3;

        AddFloorRectangle(new Rect2I(4, 4, 10, 8));
        AddFloorRectangle(new Rect2I(14, 7, 8, 3));
        AddFloorRectangle(new Rect2I(10, 12, 6, 5));
        _networking.ArenaMapData.ResetWallTiles();
        ApplyInitialWallDamage();
        _tileLayerRenderer.Render(_networking.ArenaMapData);
    }

    private void AddFloorRectangle(Rect2I rect)
    {
        for (var x = rect.Position.X; x < rect.End.X; x++)
        {
            for (var y = rect.Position.Y; y < rect.End.Y; y++)
            {
                _networking.ArenaMapData.AddFloorTile(new Vector2I(x, y));
            }
        }
    }

    private void DamageWallUnderCursor()
    {
        if (!_networking.DamageAuthoritativeWallFromWorldPosition(GetGlobalMousePosition(), TestTileSize))
        {
            return;
        }

        _tileLayerRenderer.Render(_networking.ArenaMapData);
    }

    private void DamageWallsInExplosiveRadius()
    {
        if (!_networking.DamageAuthoritativeWallsInWorldRadius(
                GetGlobalMousePosition(),
                TestTileSize,
                TestExplosiveRadius,
                TestExplosiveDamage))
        {
            return;
        }

        _tileLayerRenderer.Render(_networking.ArenaMapData);
    }

    private void ApplyInitialWallDamage()
    {
        foreach (var (position, damageAmount) in InitialWallDamageSamples)
        {
            _networking.ArenaMapData.DamageWallTile(position, damageAmount);
        }
    }

    private bool CanApplyDamageInput()
    {
        return _networking != null
            && Role == TestLanRole.Host
            && _networking.IsServer
            && _networking.HasActiveNetworkPeer;
    }

    private void OnConnectionStateChanged()
    {
        UpdateStatusLabel();
    }

    private void OnArenaMapChanged()
    {
        _tileLayerRenderer.Render(_networking.ArenaMapData);
    }

    private void UpdateStatusLabel()
    {
        if (_networking == null)
        {
            return;
        }

        if (!_networking.HasActiveNetworkPeer)
        {
            _statusLabel.Text = $"Waiting for connection... {_networking.ConnectionStatusText}";
            return;
        }

        var roleText = Role == TestLanRole.Host ? "Host" : "Client";
        _statusLabel.Text = Role == TestLanRole.Host
            ? $"{roleText} connected. LMB: Bullet  Shift+LMB: Explosive  RMB: Reset"
            : $"{roleText} connected. Waiting for server-driven wall updates. Start both peers before host damage tests.";
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
