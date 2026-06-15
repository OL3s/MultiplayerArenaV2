using Godot;
using System.Collections.Generic;

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
    private readonly List<LevelProp> _props = new();

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
        if (DamagePropAtWorldPosition(mousePosition, DamageType.Crush, TestBulletDamage))
            return;

        if (!_arenaMapData.DamageWallFromWorldPosition(mousePosition, TestTileSize, DamageType.Crush, TestBulletDamage))
            return;

        _tileLayerRenderer.Render(_arenaMapData);
    }

    private void DamageWallsInExplosiveRadius() {
        var worldCenter = GetGlobalMousePosition();
        var changedProps = DamagePropsInWorldRadius(worldCenter, TestExplosiveRadius, DamageType.Explosive, TestExplosiveDamage);
        var changedTiles = _arenaMapData.DamageWallsInWorldRadius(
            worldCenter,
            TestTileSize,
            TestExplosiveRadius,
            DamageType.Explosive,
            TestExplosiveDamage);

        if (changedTiles.Count == 0 && !changedProps)
            return;

        _tileLayerRenderer.Render(_arenaMapData);
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

    private bool DamagePropAtWorldPosition(Vector2 worldPosition, DamageType damageType, float damageAmount) {
        foreach (var prop in _props) {
            if (!IsInstanceValid(prop) || !prop.ContainsWorldPosition(worldPosition))
                continue;

            prop.ApplyDamage(DamageContainer.FromDamage(damageType, damageAmount));
            return true;
        }

        return false;
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
