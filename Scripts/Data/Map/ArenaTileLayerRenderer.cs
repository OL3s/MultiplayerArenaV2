using Godot;

[GlobalClass]
public partial class ArenaTileLayerRenderer : Node2D
{
    private const string BaseAtlasPath = "res://Assets/Tiles/debug_floor_wall_atlas.svg";
    private const string WallDamageAtlasPath = "res://Assets/Tiles/debug_wall_damage_overlay.svg";

    [Export]
    public Vector2I TileSize { get; set; } = new(16, 16);

    public TileMapLayer FloorLayer => GetNode<TileMapLayer>("FloorLayer");

    public TileMapLayer WallLayer => GetNode<TileMapLayer>("WallLayer");

    public TileMapLayer WallDamageLayer => GetNode<TileMapLayer>("WallDamageLayer");

    public override void _Ready()
    {
        WallLayer.ZIndex = 1;
        WallDamageLayer.ZIndex = 2;
        WallDamageLayer.CollisionEnabled = false;
        WallDamageLayer.NavigationEnabled = false;
        WallDamageLayer.OcclusionEnabled = false;
    }

    public void Render(ArenaMapData arenaMapData)
    {
        if (arenaMapData == null)
        {
            return;
        }

        EnsureTileSets(arenaMapData);
        RenderLayer(FloorLayer, arenaMapData.GenerateLayerTileMapData(MapTileData.MapLayerType.Floor));
        RenderLayer(WallLayer, arenaMapData.GenerateLayerTileMapData(MapTileData.MapLayerType.Wall));
        RenderLayer(WallDamageLayer, arenaMapData.GenerateLayerTileMapData(MapTileData.MapLayerType.WallDamage));
    }

    public Vector2I WorldToMap(Vector2 globalPosition)
    {
        return FloorLayer.LocalToMap(FloorLayer.ToLocal(globalPosition));
    }

    private void EnsureTileSets(ArenaMapData arenaMapData)
    {
        if (FloorLayer.TileSet != null && WallLayer.TileSet != null && WallDamageLayer.TileSet != null)
        {
            return;
        }

        var tileSet = BuildTileSet(arenaMapData);
        FloorLayer.TileSet = tileSet;
        WallLayer.TileSet = tileSet;
        WallDamageLayer.TileSet = tileSet;
    }

    private TileSet BuildTileSet(ArenaMapData arenaMapData)
    {
        var tileSet = new TileSet
        {
            TileSize = TileSize,
        };

        var baseAtlasSource = new TileSetAtlasSource
        {
            Texture = GD.Load<Texture2D>(BaseAtlasPath),
            TextureRegionSize = TileSize,
        };

        baseAtlasSource.CreateTile(ArenaMapData.FloorAtlasCoords);
        baseAtlasSource.CreateTile(ArenaMapData.WallAtlasCoords);
        tileSet.AddSource(baseAtlasSource, arenaMapData.SourceId);

        var damageAtlasSource = new TileSetAtlasSource
        {
            Texture = GD.Load<Texture2D>(WallDamageAtlasPath),
            TextureRegionSize = TileSize,
        };

        damageAtlasSource.CreateTile(ArenaMapData.LightWallDamageAtlasCoords);
        damageAtlasSource.CreateTile(ArenaMapData.HeavyWallDamageAtlasCoords);
        tileSet.AddSource(damageAtlasSource, arenaMapData.WallDamageSourceId);

        return tileSet;
    }

    private void RenderLayer(TileMapLayer tileMapLayer, Godot.Collections.Array<MapTileData> tiles)
    {
        tileMapLayer.Clear();

        foreach (var tile in tiles)
        {
            tileMapLayer.SetCell(tile.Position, tile.SourceId, tile.AtlasCoords, tile.AlternativeTile);
        }

        tileMapLayer.UpdateInternals();
    }
}
