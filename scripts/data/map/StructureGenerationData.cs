using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class StructureGenerationData : Resource {
    public enum SpawnPointType {
        TeamSpawn,
        TeamObjective,
        NeutralObjective,
        ItemSpawn,
    }

    private readonly HashSet<Vector2I> _floorTiles = new();
    private readonly Dictionary<SpawnPointType, List<Vector2I>> _spawnTilesByType = new();
    private readonly Dictionary<int, List<Vector2I>> _teamSpawnTilesByTeam = new();
    private readonly Dictionary<int, Vector2I> _teamObjectiveTileByTeam = new();

    public IReadOnlySet<Vector2I> FloorTiles => _floorTiles;

    public IReadOnlyDictionary<SpawnPointType, List<Vector2I>> SpawnTilesByType => _spawnTilesByType;

    public List<SpawnPointType> EnabledSpawnTypes { get; } = new();

    public void Clear() {
        _floorTiles.Clear();
        _spawnTilesByType.Clear();
        _teamSpawnTilesByTeam.Clear();
        _teamObjectiveTileByTeam.Clear();
        EnabledSpawnTypes.Clear();
    }

    public void Generate(MapGenerationConfig.StructureType structureType) {
        Clear();
        switch (structureType) {
            case MapGenerationConfig.StructureType.Square:
                GenerateSquareArena();
                break;
            case MapGenerationConfig.StructureType.Plains:
                GenerateOpenField();
                break;
            case MapGenerationConfig.StructureType.Arena:
            default:
                GenerateArenaPlus();
                break;
        }
    }

    public void ApplyToArenaMap(ArenaMapData arenaMapData) {
        if (arenaMapData == null)
            return;

        arenaMapData.Clear();
        foreach (var floorTile in _floorTiles)
            arenaMapData.AddFloorTile(floorTile);

        arenaMapData.ResetWallTiles();
    }

    public HashSet<Vector2I> ToFloorTileHashSet() {
        return new HashSet<Vector2I>(_floorTiles);
    }

    public Godot.Collections.Array<MapTileData> ToGodotFloorLayerTiles(int sourceId = 0) {
        var tiles = new Godot.Collections.Array<MapTileData>();
        foreach (var floorTile in _floorTiles) {
            tiles.Add(new MapTileData {
                Position = floorTile,
                TileType = MapTileData.MapTileType.Floor,
                LayerType = MapTileData.MapLayerType.Floor,
                SourceId = sourceId,
                AtlasCoords = ArenaMapData.FloorAtlasCoords,
            });
        }

        return tiles;
    }

    public IReadOnlyList<Vector2I> GetSpawnTiles(SpawnPointType spawnPointType) {
        return _spawnTilesByType.TryGetValue(spawnPointType, out var spawnTiles) ? spawnTiles : System.Array.Empty<Vector2I>();
    }

    public IEnumerable<int> GetTeamIds() {
        return _teamObjectiveTileByTeam.Keys;
    }

    public IReadOnlyList<Vector2I> GetTeamSpawnTiles(int teamId) {
        return _teamSpawnTilesByTeam.TryGetValue(teamId, out var spawnTiles) ? spawnTiles : System.Array.Empty<Vector2I>();
    }

    public Vector2I GetTeamObjectiveTile(int teamId) {
        return _teamObjectiveTileByTeam.TryGetValue(teamId, out var objectiveTile) ? objectiveTile : Vector2I.Zero;
    }

    private void GenerateArenaPlus() {
        AddFloorRectangle(new Rect2I(11, 5, 7, 11));
        AddFloorRectangle(new Rect2I(5, 9, 19, 3));
        AddTeamBase(0, new Vector2I(7, 10), 1);
        AddTeamBase(1, new Vector2I(21, 10), 1);
        AddTeamBase(2, new Vector2I(14, 7), 1);
        AddTeamBase(3, new Vector2I(14, 13), 1);
        AddSpawnType(SpawnPointType.NeutralObjective, new Vector2I(14, 10));
        AddSpawnType(SpawnPointType.ItemSpawn, new Vector2I(10, 10), new Vector2I(18, 10), new Vector2I(14, 8), new Vector2I(14, 12));
    }

    private void GenerateOpenField() {
        AddFloorRectangle(new Rect2I(4, 4, 22, 14));
        AddTeamBase(0, new Vector2I(7, 10), 2);
        AddTeamBase(1, new Vector2I(22, 10), 2);
        AddTeamBase(2, new Vector2I(15, 6), 2);
        AddTeamBase(3, new Vector2I(15, 15), 2);
        AddSpawnType(SpawnPointType.NeutralObjective, new Vector2I(15, 10));
        AddSpawnType(SpawnPointType.ItemSpawn, new Vector2I(11, 8), new Vector2I(18, 8), new Vector2I(11, 13), new Vector2I(18, 13));
    }

    private void GenerateSquareArena() {
        AddFloorRectangle(new Rect2I(5, 5, 20, 20));
        AddTeamBase(0, new Vector2I(8, 15), 2);
        AddTeamBase(1, new Vector2I(22, 15), 2);
        AddTeamBase(2, new Vector2I(15, 8), 2);
        AddTeamBase(3, new Vector2I(15, 22), 2);
        AddSpawnType(SpawnPointType.NeutralObjective, new Vector2I(15, 15));
        AddSpawnType(SpawnPointType.NeutralObjective, new Vector2I(10, 10), new Vector2I(20, 10), new Vector2I(10, 20), new Vector2I(20, 20));
        AddSpawnType(SpawnPointType.ItemSpawn, new Vector2I(12, 15), new Vector2I(18, 15), new Vector2I(15, 12), new Vector2I(15, 18));
    }

    private void AddFloorRectangle(Rect2I rect) {
        for (var x = rect.Position.X; x < rect.End.X; x++) {
            for (var y = rect.Position.Y; y < rect.End.Y; y++)
                _floorTiles.Add(new Vector2I(x, y));
        }
    }

    private void AddSpawnType(SpawnPointType spawnPointType, params Vector2I[] spawnTiles) {
        if (!EnabledSpawnTypes.Contains(spawnPointType))
            EnabledSpawnTypes.Add(spawnPointType);

        if (!_spawnTilesByType.TryGetValue(spawnPointType, out var existingSpawnTiles)) {
            existingSpawnTiles = new List<Vector2I>();
            _spawnTilesByType[spawnPointType] = existingSpawnTiles;
        }

        if (spawnTiles == null)
            return;

        foreach (var spawnTile in spawnTiles)
            existingSpawnTiles.Add(spawnTile);
    }

    private void AddTeamDefinition(int teamId, Vector2I objectiveTile, params Vector2I[] spawnTiles) {
        _teamObjectiveTileByTeam[teamId] = objectiveTile;
        AddSpawnType(SpawnPointType.TeamObjective, objectiveTile);

        if (!_teamSpawnTilesByTeam.TryGetValue(teamId, out var existingSpawnTiles)) {
            existingSpawnTiles = new List<Vector2I>();
            _teamSpawnTilesByTeam[teamId] = existingSpawnTiles;
        }

        if (spawnTiles == null)
            return;

        foreach (var spawnTile in spawnTiles) {
            existingSpawnTiles.Add(spawnTile);
            AddSpawnType(SpawnPointType.TeamSpawn, spawnTile);
        }
    }

    private void AddTeamBase(int teamId, Vector2I objectiveTile, int spawnOffset) {
        spawnOffset = Mathf.Max(spawnOffset, 1);
        AddTeamDefinition(
            teamId,
            objectiveTile,
            objectiveTile + new Vector2I(0, -spawnOffset),
            objectiveTile + new Vector2I(spawnOffset, 0),
            objectiveTile + new Vector2I(0, spawnOffset),
            objectiveTile + new Vector2I(-spawnOffset, 0));
    }
}
