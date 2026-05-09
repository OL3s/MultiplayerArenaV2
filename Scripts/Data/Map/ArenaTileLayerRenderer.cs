using Godot;

[GlobalClass]
public partial class ArenaTileLayerRenderer : Node2D {
    private const string FloorTileSetPath = "res://Assets/Tiles/TileSets/FloorTileset.tres";
    private const string WallTileSetPath = "res://Assets/Tiles/TileSets/WallTileset.tres";
    private const string WallDamageTileSetPath = "res://Assets/Tiles/TileSets/WallDamagedTileset.tres";

    [Export]
    public Vector2I TileSize { get; set; } = new(16, 16);

    public TileMapLayer FloorLayer => GetNode<TileMapLayer>("FloorLayer");

    public TileMapLayer WallLayer => GetNode<TileMapLayer>("WallLayer");

    public TileMapLayer WallDamageLayer => GetNode<TileMapLayer>("WallDamageLayer");

    public override void _Ready() {
        WallLayer.ZIndex = 1;
        WallDamageLayer.ZIndex = 2;
        WallDamageLayer.CollisionEnabled = false;
        WallDamageLayer.NavigationEnabled = false;
        WallDamageLayer.OcclusionEnabled = false;
    }

    public void Render(ArenaMapData arenaMapData) {
        if (arenaMapData == null)
            return;

        EnsureTileSets(arenaMapData);
        RenderLayer(FloorLayer, arenaMapData.GenerateLayerTileMapData(MapTileData.MapLayerType.Floor), 0);
        RenderLayer(WallLayer, arenaMapData.GenerateLayerTileMapData(MapTileData.MapLayerType.Wall), 0);
        RenderLayer(WallDamageLayer, arenaMapData.GenerateLayerTileMapData(MapTileData.MapLayerType.WallDamage), 0);
    }

    public Vector2I WorldToMap(Vector2 globalPosition) {
        return FloorLayer.LocalToMap(FloorLayer.ToLocal(globalPosition));
    }

    private void EnsureTileSets(ArenaMapData arenaMapData) {
        if (FloorLayer.TileSet != null && WallLayer.TileSet != null && WallDamageLayer.TileSet != null)
            return;

        FloorLayer.TileSet = GD.Load<TileSet>(FloorTileSetPath);
        WallLayer.TileSet = GD.Load<TileSet>(WallTileSetPath);
        WallDamageLayer.TileSet = GD.Load<TileSet>(WallDamageTileSetPath);
        EnsureWallTileCollision();
    }

    private void EnsureWallTileCollision() {
        if (WallLayer.TileSet == null)
            return;

        if (WallLayer.TileSet.GetPhysicsLayersCount() == 0) {
            WallLayer.TileSet.AddPhysicsLayer();
            WallLayer.TileSet.SetPhysicsLayerCollisionLayer(0, 1);
            WallLayer.TileSet.SetPhysicsLayerCollisionMask(0, 1);
        }

        if (WallLayer.TileSet.GetSource(0) is not TileSetAtlasSource atlasSource)
            return;

        var tileData = atlasSource.GetTileData(ArenaMapData.WallAtlasCoords, 0);
        if (tileData == null)
            return;

        tileData.SetCollisionPolygonsCount(0, 1);
        tileData.SetCollisionPolygonPoints(
            0,
            0,
            new[] {
                new Vector2(-TileSize.X * 0.5f, -TileSize.Y * 0.5f),
                new Vector2(TileSize.X * 0.5f, -TileSize.Y * 0.5f),
                new Vector2(TileSize.X * 0.5f, TileSize.Y * 0.5f),
                new Vector2(-TileSize.X * 0.5f, TileSize.Y * 0.5f),
            });
    }

    private void RenderLayer(TileMapLayer tileMapLayer, Godot.Collections.Array<MapTileData> tiles, int sourceId) {
        tileMapLayer.Clear();

        foreach (var tile in tiles)
            tileMapLayer.SetCell(tile.Position, sourceId, tile.AtlasCoords, tile.AlternativeTile);

        tileMapLayer.UpdateInternals();
    }
}
