using Godot;

public partial class TestMapDestructionLogic : Node2D
{
    private static readonly (Vector2I Position, int DamageAmount)[] InitialWallDamageSamples =
    {
        (new Vector2I(3, 4), 1),
        (new Vector2I(14, 4), 1),
        (new Vector2I(22, 8), 2),
        (new Vector2I(9, 17), 1),
    };

    private ArenaMapData _arenaMapData;
    private ArenaTileLayerRenderer _tileLayerRenderer;
    private Camera2D _camera;

    public override void _Ready()
    {
        _tileLayerRenderer = GetNode<ArenaTileLayerRenderer>("ArenaTileLayerRenderer");
        _camera = GetNode<Camera2D>("Camera2D");

        BuildMockArena();
        CenterCamera();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButtonEvent || !mouseButtonEvent.Pressed)
        {
            return;
        }

        if (mouseButtonEvent.ButtonIndex == MouseButton.Left)
        {
            DamageWallUnderCursor();
            return;
        }

        if (mouseButtonEvent.ButtonIndex == MouseButton.Right)
        {
            BuildMockArena();
        }
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
        var mapPosition = _tileLayerRenderer.WorldToMap(GetGlobalMousePosition());
        if (!_arenaMapData.DamageWallTile(mapPosition))
        {
            return;
        }

        _tileLayerRenderer.Render(_arenaMapData);
    }

    private void ApplyInitialWallDamage()
    {
        foreach (var (position, damageAmount) in InitialWallDamageSamples)
        {
            _arenaMapData.DamageWallTile(position, damageAmount);
        }
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
