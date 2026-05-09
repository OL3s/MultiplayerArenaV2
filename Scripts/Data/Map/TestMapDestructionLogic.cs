using Godot;

public partial class TestMapDestructionLogic : Node2D {
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

    public override void _Ready() {
        _tileLayerRenderer = GetNode<ArenaTileLayerRenderer>("ArenaTileLayerRenderer");
        _camera = GetNode<Camera2D>("Camera2D");
        _debugRadiusDrawer = CreateDebugRadiusDrawer();

        BuildMockArena();
        CenterCamera();
    }

    public override void _Process(double delta) {
        _debugRadiusDrawer.QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event) {
        if (@event is not InputEventMouseButton mouseButtonEvent || !mouseButtonEvent.Pressed)
            return;

        if (mouseButtonEvent.ButtonIndex == MouseButton.Right) {
            BuildMockArena();
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

    private void BuildMockArena() {
        _arenaMapData = new ArenaMapData {
            SourceId = 0,
            WallDamageSourceId = 1,
            DefaultWallMaxDamage = WallDamageData.DefaultWallHealth,
        };

        AddFloorRectangle(new Rect2I(4, 4, 10, 8));
        AddFloorRectangle(new Rect2I(14, 7, 8, 3));
        AddFloorRectangle(new Rect2I(10, 12, 6, 5));
        _arenaMapData.ResetWallTiles();
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
        if (!_arenaMapData.DamageWallFromWorldPosition(GetGlobalMousePosition(), TestTileSize, DamageType.Crush, TestBulletDamage))
            return;

        _tileLayerRenderer.Render(_arenaMapData);
    }

    private void DamageWallsInExplosiveRadius() {
        var changedTiles = _arenaMapData.DamageWallsInWorldRadius(
            GetGlobalMousePosition(),
            TestTileSize,
            TestExplosiveRadius,
            DamageType.Explosive,
            TestExplosiveDamage);

        if (changedTiles.Count == 0)
            return;

        _tileLayerRenderer.Render(_arenaMapData);
    }

    private void ApplyInitialWallDamage() {
        foreach (var (position, damageAmount) in InitialWallDamageSamples)
            _arenaMapData.DamageWallTile(position, DamageType.Crush, damageAmount);
    }

    private DebugExplosionRadiusDrawer CreateDebugRadiusDrawer() {
        var debugRadiusDrawer = new DebugExplosionRadiusDrawer {
            Name = "DebugExplosionRadiusDrawer",
            Radius = TestExplosiveRadius,
            DrawColor = DebugExplosiveRadiusColor,
            ZIndex = 10,
        };

        AddChild(debugRadiusDrawer);
        return debugRadiusDrawer;
    }

    private void CenterCamera() {
        var usedRect = _tileLayerRenderer.FloorLayer.GetUsedRect();
        if (usedRect.Size == Vector2I.Zero)
            return;

        var centerCell = usedRect.Position + (usedRect.Size / 2);
        _camera.Position = _tileLayerRenderer.FloorLayer.MapToLocal(centerCell);
    }
}
