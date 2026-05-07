using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class ArenaMapData : Resource
{
    public static readonly Vector2I FloorAtlasCoords = new(0, 0);
    public static readonly Vector2I WallAtlasCoords = new(0, 1);
    public static readonly Vector2I LightWallDamageAtlasCoords = new(0, 0);
    public static readonly Vector2I HeavyWallDamageAtlasCoords = new(0, 1);

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
    private readonly Dictionary<Vector2I, WallDamageData> _hitWallTiles = new();

    [Export]
    public int SourceId { get; set; }

    [Export]
    public int WallDamageSourceId { get; set; }

    [Export]
    public int DefaultWallMaxDamage { get; set; } = 3;

    public IReadOnlySet<Vector2I> FloorTiles => _floorTiles;

    public IReadOnlySet<Vector2I> WallTiles => _wallTiles;

    public IReadOnlyDictionary<Vector2I, WallDamageData> HitWallTiles => _hitWallTiles;

    public void GenerateMap()
    {
    }

    public void Clear()
    {
        _floorTiles.Clear();
        _wallTiles.Clear();
        _hitWallTiles.Clear();
    }

    public void AddFloorTile(Vector2I position)
    {
        _floorTiles.Add(position);
        _wallTiles.Remove(position);
        _hitWallTiles.Remove(position);
    }

    public void AddWallTile(Vector2I position)
    {
        if (_floorTiles.Contains(position))
        {
            return;
        }

        _wallTiles.Add(position);
    }

    public bool IsFloorTile(Vector2I position)
    {
        return _floorTiles.Contains(position);
    }

    public bool IsWallTile(Vector2I position)
    {
        return _wallTiles.Contains(position);
    }

    public WallDamageData GetWallDamageData(Vector2I position)
    {
        _hitWallTiles.TryGetValue(position, out var wallDamageData);
        return wallDamageData;
    }

    public void ResetWallTiles()
    {
        _wallTiles.Clear();

        foreach (var floorTile in _floorTiles)
        {
            foreach (var offset in NeighborOffsets)
            {
                AddWallTile(floorTile + offset);
            }
        }

        PruneInvalidWallDamage();
    }

    public void FillWallsFromFloors()
    {
        ResetWallTiles();
    }

    public Godot.Collections.Array<MapTileData> GenerateTileMapData()
    {
        var tiles = new Godot.Collections.Array<MapTileData>();

        tiles.AddRange(GenerateLayerTileMapData(MapTileData.MapLayerType.Floor));
        tiles.AddRange(GenerateLayerTileMapData(MapTileData.MapLayerType.Wall));
        tiles.AddRange(GenerateLayerTileMapData(MapTileData.MapLayerType.WallDamage));

        return tiles;
    }

    public Godot.Collections.Array<MapTileData> GenerateLayerTileMapData(MapTileData.MapLayerType layerType)
    {
        var tiles = new Godot.Collections.Array<MapTileData>();

        switch (layerType)
        {
            case MapTileData.MapLayerType.Floor:
                foreach (var floorTile in _floorTiles)
                {
                    tiles.Add(CreateTileData(
                        floorTile,
                        MapTileData.MapTileType.Floor,
                        MapTileData.MapLayerType.Floor,
                        SourceId,
                        FloorAtlasCoords));
                }

                break;

            case MapTileData.MapLayerType.Wall:
                foreach (var wallTile in _wallTiles)
                {
                    tiles.Add(CreateTileData(
                        wallTile,
                        MapTileData.MapTileType.Wall,
                        MapTileData.MapLayerType.Wall,
                        SourceId,
                        WallAtlasCoords));
                }

                break;

            case MapTileData.MapLayerType.WallDamage:
                foreach (var hitWallTile in _hitWallTiles)
                {
                    if (hitWallTile.Value.DamageStage <= 0 || !_wallTiles.Contains(hitWallTile.Key))
                    {
                        continue;
                    }

                    tiles.Add(CreateTileData(
                        hitWallTile.Key,
                        MapTileData.MapTileType.WallDamageOverlay,
                        MapTileData.MapLayerType.WallDamage,
                        WallDamageSourceId,
                        GetWallDamageAtlasCoords(hitWallTile.Value.DamageStage)));
                }

                break;
        }

        return tiles;
    }

    public bool DamageWallTile(Vector2I position, int damageAmount = 1)
    {
        if (!_wallTiles.Contains(position) || damageAmount <= 0)
        {
            return false;
        }

        if (!_hitWallTiles.TryGetValue(position, out var wallDamageData))
        {
            wallDamageData = new WallDamageData { MaxDamage = DefaultWallMaxDamage };
            _hitWallTiles[position] = wallDamageData;
        }

        wallDamageData.Damage += damageAmount;
        wallDamageData.DamageStage = CalculateWallDamageStage(wallDamageData.Damage, wallDamageData.MaxDamage);

        if (wallDamageData.Damage >= wallDamageData.MaxDamage)
        {
            DestroyWallTile(position);
        }

        return true;
    }

    public bool DestroyWallTile(Vector2I position)
    {
        if (!_wallTiles.Contains(position))
        {
            return false;
        }

        AddFloorTile(position);
        ResetWallTiles();
        return true;
    }

    private int CalculateWallDamageStage(int damage, int maxDamage)
    {
        if (damage <= 0 || maxDamage <= 1)
        {
            return 0;
        }

        return Mathf.Clamp(damage, 0, maxDamage - 1);
    }

    private Vector2I GetWallDamageAtlasCoords(int damageStage)
    {
        return damageStage >= 2 ? HeavyWallDamageAtlasCoords : LightWallDamageAtlasCoords;
    }

    private void PruneInvalidWallDamage()
    {
        var invalidPositions = new List<Vector2I>();

        foreach (var hitWallTile in _hitWallTiles)
        {
            if (_wallTiles.Contains(hitWallTile.Key))
            {
                continue;
            }

            invalidPositions.Add(hitWallTile.Key);
        }

        foreach (var invalidPosition in invalidPositions)
        {
            _hitWallTiles.Remove(invalidPosition);
        }
    }

    private MapTileData CreateTileData(
        Vector2I position,
        MapTileData.MapTileType tileType,
        MapTileData.MapLayerType layerType,
        int sourceId,
        Vector2I atlasCoords)
    {
        return new MapTileData
        {
            Position = position,
            TileType = tileType,
            LayerType = layerType,
            SourceId = sourceId,
            AtlasCoords = atlasCoords,
        };
    }
}
