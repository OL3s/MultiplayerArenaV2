using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class ArenaMapData : Resource {
    public static readonly Vector2I FloorAtlasCoords = new(0, 0);
    public static readonly Vector2I WallAtlasCoords = new(0, 1);
    public static readonly Vector2I LightWallDamageAtlasCoords = new(0, 0);
    public static readonly Vector2I HeavyWallDamageAtlasCoords = new(0, 1);

    private static readonly Vector2I[] NeighborOffsets = {
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
    public int DefaultWallMaxDamage { get; set; } = WallDamageData.DefaultWallHealth;

    [Export]
    public BiomeConfig.BiomeType DefaultWallBiome { get; set; } = BiomeConfig.BiomeType.Arena;

    public IReadOnlySet<Vector2I> FloorTiles => _floorTiles;

    public IReadOnlySet<Vector2I> WallTiles => _wallTiles;

    public IReadOnlyDictionary<Vector2I, WallDamageData> HitWallTiles => _hitWallTiles;

    public void GenerateMap() {
    }

    public void Clear() {
        _floorTiles.Clear();
        _wallTiles.Clear();
        _hitWallTiles.Clear();
    }

    public void AddFloorTile(Vector2I position) {
        _floorTiles.Add(position);
        _wallTiles.Remove(position);
        _hitWallTiles.Remove(position);
    }

    public void AddWallTile(Vector2I position) {
        if (_floorTiles.Contains(position))
            return;

        _wallTiles.Add(position);
    }

    public bool IsFloorTile(Vector2I position) {
        return _floorTiles.Contains(position);
    }

    public bool IsWallTile(Vector2I position) {
        return _wallTiles.Contains(position);
    }

    public WallDamageData GetWallDamageData(Vector2I position) {
        _hitWallTiles.TryGetValue(position, out var wallDamageData);
        return wallDamageData;
    }

    public void ResetWallTiles() {
        _wallTiles.Clear();

        foreach (var floorTile in _floorTiles) {
            foreach (var offset in NeighborOffsets)
                AddWallTile(floorTile + offset);
        }

        PruneInvalidWallDamage();
    }

    public void FillWallsFromFloors() {
        ResetWallTiles();
    }

    public Godot.Collections.Array<MapTileData> GenerateTileMapData() {
        var tiles = new Godot.Collections.Array<MapTileData>();

        tiles.AddRange(GenerateLayerTileMapData(MapTileData.MapLayerType.Floor));
        tiles.AddRange(GenerateLayerTileMapData(MapTileData.MapLayerType.Wall));
        tiles.AddRange(GenerateLayerTileMapData(MapTileData.MapLayerType.WallDamage));

        return tiles;
    }

    public Godot.Collections.Array<MapTileData> GenerateLayerTileMapData(MapTileData.MapLayerType layerType) {
        var tiles = new Godot.Collections.Array<MapTileData>();

        switch (layerType) {
            case MapTileData.MapLayerType.Floor:
                foreach (var floorTile in _floorTiles) {
                    tiles.Add(CreateTileData(
                        floorTile,
                        MapTileData.MapTileType.Floor,
                        MapTileData.MapLayerType.Floor,
                        SourceId,
                        FloorAtlasCoords));
                }

                break;

            case MapTileData.MapLayerType.Wall:
                foreach (var wallTile in _wallTiles) {
                    tiles.Add(CreateTileData(
                        wallTile,
                        MapTileData.MapTileType.Wall,
                        MapTileData.MapLayerType.Wall,
                        SourceId,
                        WallAtlasCoords));
                }

                break;

            case MapTileData.MapLayerType.WallDamage:
                foreach (var hitWallTile in _hitWallTiles) {
                    if (hitWallTile.Value.DamageStage <= 0 || !_wallTiles.Contains(hitWallTile.Key))
                        continue;

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

    public bool DamageWallTile(Vector2I position, int damageAmount = 1) {
        if (damageAmount <= 0)
            return false;

        return DamageWallTile(position, DamageType.Crush, damageAmount);
    }

    public bool DamageWallTile(Vector2I position, DamageType damageType, float damageAmount) {
        if (damageAmount <= 0.0f)
            return false;

        return DamageWallTile(position, DamageContainer.FromDamage(damageType, damageAmount));
    }

    public bool DamageWallTile(Vector2I position, DamageContainer damageContainer) {
        if (!_wallTiles.Contains(position) || damageContainer?.Damage == null)
            return false;

        if (!_hitWallTiles.TryGetValue(position, out var wallDamageData)) {
            wallDamageData = new WallDamageData();
            wallDamageData.ConfigureForBiome(DefaultWallBiome, DefaultWallMaxDamage);
            _hitWallTiles[position] = wallDamageData;
        }

        wallDamageData.ApplyDamage(damageContainer);
        wallDamageData.DamageStage = CalculateWallDamageStage(wallDamageData.Damage, wallDamageData.MaxDamage);

        if (wallDamageData.IsDestroyed())
            DestroyWallTile(position);

        return true;
    }

    public bool DamageWallFromWorldPosition(Vector2 worldPosition, Vector2I tileSize, int damageAmount = 1) {
        return DamageWallTile(WorldToTile(worldPosition, tileSize), damageAmount);
    }

    public bool DamageWallFromWorldPosition(Vector2 worldPosition, Vector2I tileSize, DamageType damageType, float damageAmount) {
        return DamageWallTile(WorldToTile(worldPosition, tileSize), damageType, damageAmount);
    }

    public Godot.Collections.Array<Vector2I> DamageWallsInRadius(Vector2I centerTile, int radius, int damageAmount = 1) {
        if (damageAmount <= 0)
            return new Godot.Collections.Array<Vector2I>();

        return DamageWallsInRadius(centerTile, radius, DamageType.Explosive, damageAmount);
    }

    public Godot.Collections.Array<Vector2I> DamageWallsInRadius(Vector2I centerTile, int radius, DamageType damageType, float damageAmount) {
        if (damageAmount <= 0.0f)
            return new Godot.Collections.Array<Vector2I>();

        return DamageWallsInRadius(centerTile, radius, DamageContainer.FromDamage(damageType, damageAmount));
    }

    public Godot.Collections.Array<Vector2I> DamageWallsInRadius(Vector2I centerTile, int radius, DamageContainer damageContainer) {
        var changedTiles = new Godot.Collections.Array<Vector2I>();
        if (radius < 0 || damageContainer?.Damage == null)
            return changedTiles;

        var affectedTiles = GetTilesInRadius(centerTile, radius);
        foreach (var tilePosition in affectedTiles) {
            var damageMultiplier = GetRadiusDamageMultiplier(centerTile, tilePosition, radius);
            if (damageMultiplier <= 0.0f)
                continue;

            var scaledDamageContainer = new DamageContainer {
                Damage = damageContainer.Damage.Scaled(damageMultiplier),
            };
            if (!DamageWallTile(tilePosition, scaledDamageContainer))
                continue;

            changedTiles.Add(tilePosition);
        }

        return changedTiles;
    }

    public Godot.Collections.Array<Vector2I> DamageWallsInWorldRadius(Vector2 worldCenter, Vector2I tileSize, float worldRadius, int damageAmount = 1) {
        if (damageAmount <= 0)
            return new Godot.Collections.Array<Vector2I>();

        return DamageWallsInWorldRadius(worldCenter, tileSize, worldRadius, DamageType.Explosive, damageAmount);
    }

    public Godot.Collections.Array<Vector2I> DamageWallsInWorldRadius(Vector2 worldCenter, Vector2I tileSize, float worldRadius, DamageType damageType, float damageAmount) {
        if (damageAmount <= 0.0f)
            return new Godot.Collections.Array<Vector2I>();

        return DamageWallsInWorldRadius(worldCenter, tileSize, worldRadius, DamageContainer.FromDamage(damageType, damageAmount));
    }

    public Godot.Collections.Array<Vector2I> DamageWallsInWorldRadius(Vector2 worldCenter, Vector2I tileSize, float worldRadius, DamageContainer damageContainer) {
        var centerTile = WorldToTile(worldCenter, tileSize);
        var tileRadius = Mathf.CeilToInt(worldRadius / Mathf.Max(1, tileSize.X));
        return DamageWallsInRadius(centerTile, tileRadius, damageContainer);
    }

    public bool DestroyWallTile(Vector2I position) {
        if (!_wallTiles.Contains(position))
            return false;

        AddFloorTile(position);
        ResetWallTiles();
        return true;
    }

    public Vector2I WorldToTile(Vector2 worldPosition, Vector2I tileSize) {
        var safeTileSize = new Vector2(
            Mathf.Max(1, tileSize.X),
            Mathf.Max(1, tileSize.Y));

        return new Vector2I(
            Mathf.FloorToInt(worldPosition.X / safeTileSize.X),
            Mathf.FloorToInt(worldPosition.Y / safeTileSize.Y));
    }

    public Godot.Collections.Array<Vector2I> GetTilesInRadius(Vector2I centerTile, int radius) {
        var tiles = new Godot.Collections.Array<Vector2I>();
        if (radius < 0)
            return tiles;

        var radiusSquared = radius * radius;
        for (var x = centerTile.X - radius; x <= centerTile.X + radius; x++) {
            for (var y = centerTile.Y - radius; y <= centerTile.Y + radius; y++) {
                var tilePosition = new Vector2I(x, y);
                var delta = tilePosition - centerTile;
                if ((delta.X * delta.X) + (delta.Y * delta.Y) > radiusSquared)
                    continue;

                tiles.Add(tilePosition);
            }
        }

        return tiles;
    }

    private float GetRadiusDamageMultiplier(Vector2I centerTile, Vector2I tilePosition, int radius) {
        if (radius <= 0)
            return tilePosition == centerTile ? 1.0f : 0.0f;

        var distance = new Vector2(centerTile.X, centerTile.Y).DistanceTo(new Vector2(tilePosition.X, tilePosition.Y));
        return Mathf.Clamp(1.0f - (distance / radius), 0.0f, 1.0f);
    }

    private int CalculateWallDamageStage(int damage, int maxDamage) {
        if (damage <= 0 || maxDamage <= 1)
            return 0;

        var healthRatio = (maxDamage - damage) / (float)maxDamage;
        if (healthRatio < 0.5f)
            return 2;

        return healthRatio < 0.9f ? 1 : 0;
    }

    private Vector2I GetWallDamageAtlasCoords(int damageStage) {
        return damageStage >= 2 ? HeavyWallDamageAtlasCoords : LightWallDamageAtlasCoords;
    }

    private void PruneInvalidWallDamage() {
        var invalidPositions = new List<Vector2I>();

        foreach (var hitWallTile in _hitWallTiles) {
            if (_wallTiles.Contains(hitWallTile.Key))
                continue;

            invalidPositions.Add(hitWallTile.Key);
        }

        foreach (var invalidPosition in invalidPositions)
            _hitWallTiles.Remove(invalidPosition);
    }

    private MapTileData CreateTileData(
        Vector2I position,
        MapTileData.MapTileType tileType,
        MapTileData.MapLayerType layerType,
        int sourceId,
        Vector2I atlasCoords) {
        return new MapTileData {
            Position = position,
            TileType = tileType,
            LayerType = layerType,
            SourceId = sourceId,
            AtlasCoords = atlasCoords,
        };
    }
}
