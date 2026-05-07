using Godot;

[GlobalClass]
public partial class MapTileData : Resource
{
    public enum MapLayerType
    {
        Floor,
        Wall,
        WallDamage,
    }

    public enum MapTileType
    {
        Floor,
        Wall,
        WallDamageOverlay,
    }

    [Export]
    public Vector2I Position { get; set; }

    [Export]
    public MapTileType TileType { get; set; }

    [Export]
    public MapLayerType LayerType { get; set; }

    [Export]
    public int SourceId { get; set; }

    [Export]
    public Vector2I AtlasCoords { get; set; }

    [Export]
    public int AlternativeTile { get; set; }
}
