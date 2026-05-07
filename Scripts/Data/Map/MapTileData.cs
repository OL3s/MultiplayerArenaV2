using Godot;

[GlobalClass]
public partial class MapTileData : Resource
{
    public enum MapTileType
    {
        Floor,
        Wall,
    }

    [Export]
    public Vector2I Position { get; set; }

    [Export]
    public MapTileType TileType { get; set; }

    [Export]
    public int SourceId { get; set; }

    [Export]
    public Vector2I AtlasCoords { get; set; }

    [Export]
    public int AlternativeTile { get; set; }
}
