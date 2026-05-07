using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class ArenaMapData : Resource
{
    public static readonly Vector2I FloorAtlasCoords = new(0, 0);
    public static readonly Vector2I WallAtlasCoords = new(0, 1);

    private static readonly Vector2I[] NeighborOffsets =
    {
        new(-1, -1),
        new(0, -1),
        new(1, -1),
        new(-1, 0),
        new(1, 0),
        new(-1, 1),
        new(0, 1),
        new(1, 1),
    };

    private readonly HashSet<Vector2I> _floorTiles = new();
    private readonly HashSet<Vector2I> _wallTiles = new();

    [Export]
    public int SourceId { get; set; }

    public IReadOnlySet<Vector2I> FloorTiles => _floorTiles;

    public IReadOnlySet<Vector2I> WallTiles => _wallTiles;

    public void GenerateMap()
    {
    }

    public void Clear()
    {
        _floorTiles.Clear();
        _wallTiles.Clear();
    }

    public void AddFloorTile(Vector2I position)
    {
        _floorTiles.Add(position);
        _wallTiles.Remove(position);
    }

    public void AddWallTile(Vector2I position)
    {
        if (_floorTiles.Contains(position))
        {
            return;
        }

        _wallTiles.Add(position);
    }

    public void FillWallsFromFloors()
    {
        foreach (var floorTile in _floorTiles)
        {
            foreach (var offset in NeighborOffsets)
            {
                AddWallTile(floorTile + offset);
            }
        }
    }

    public Godot.Collections.Array<MapTileData> GenerateTileMapData()
    {
        var tiles = new Godot.Collections.Array<MapTileData>();

        foreach (var floorTile in _floorTiles)
        {
            tiles.Add(CreateTileData(floorTile, MapTileData.MapTileType.Floor, FloorAtlasCoords));
        }

        foreach (var wallTile in _wallTiles)
        {
            tiles.Add(CreateTileData(wallTile, MapTileData.MapTileType.Wall, WallAtlasCoords));
        }

        return tiles;
    }

    private MapTileData CreateTileData(Vector2I position, MapTileData.MapTileType tileType, Vector2I atlasCoords)
    {
        return new MapTileData
        {
            Position = position,
            TileType = tileType,
            SourceId = SourceId,
            AtlasCoords = atlasCoords,
        };
    }
}
