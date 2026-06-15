using System.Collections.Generic;
using Godot;

public sealed class GameplaySpawnManager {
    private readonly Dictionary<int, List<Vector2I>> _spawnTilesByTeam = new();
    private readonly List<Vector2I> _itemSpawnTiles = new();

    public void Clear() {
        _spawnTilesByTeam.Clear();
        _itemSpawnTiles.Clear();
    }

    public void SetSpawnTiles(int teamId, params Vector2I[] spawnTiles) {
        teamId = MultiplayerData.NormalizeTeamId(teamId);
        if (teamId == MultiplayerData.DefaultTeamId) {
            GameLog.Warn(GameLogScope.PlayerSpawn, "UnassignedTeamUsed", "context=GameplaySpawnManager.SetSpawnTiles team=-1 result=ignored");
            return;
        }

        if (!_spawnTilesByTeam.TryGetValue(teamId, out var teamSpawnTiles)) {
            teamSpawnTiles = new List<Vector2I>();
            _spawnTilesByTeam[teamId] = teamSpawnTiles;
        }

        teamSpawnTiles.Clear();
        if (spawnTiles == null)
            return;

        foreach (var spawnTile in spawnTiles)
            teamSpawnTiles.Add(spawnTile);
    }

    public Vector2I GetSpawnTile(int teamId, int spawnIndex, ArenaMapData arenaMapData) {
        teamId = MultiplayerData.NormalizeTeamId(teamId);
        if (teamId == MultiplayerData.DefaultTeamId)
            GameLog.Warn(GameLogScope.PlayerSpawn, "UnassignedTeamUsed", "context=GameplaySpawnManager.GetSpawnTile team=-1 result=fallback");

        if (_spawnTilesByTeam.TryGetValue(teamId, out var teamSpawnTiles) && teamSpawnTiles.Count > 0)
            return teamSpawnTiles[Mathf.PosMod(spawnIndex, teamSpawnTiles.Count)];

        foreach (var defaultSpawnTiles in _spawnTilesByTeam.Values) {
            if (defaultSpawnTiles.Count > 0)
                return defaultSpawnTiles[Mathf.PosMod(spawnIndex, defaultSpawnTiles.Count)];
        }

        return GetFirstFloorTile(arenaMapData);
    }

    public void SetItemSpawnTiles(params Vector2I[] itemSpawnTiles) {
        _itemSpawnTiles.Clear();
        if (itemSpawnTiles == null)
            return;

        foreach (var itemSpawnTile in itemSpawnTiles)
            _itemSpawnTiles.Add(itemSpawnTile);
    }

    public IReadOnlyList<Vector2I> GetItemSpawnTiles() {
        return _itemSpawnTiles;
    }

    public IEnumerable<int> GetTeamIds() {
        return _spawnTilesByTeam.Keys;
    }

    public IReadOnlyList<Vector2I> GetTeamSpawnTiles(int teamId) {
        teamId = MultiplayerData.NormalizeTeamId(teamId);
        if (teamId == MultiplayerData.DefaultTeamId) {
            GameLog.Warn(GameLogScope.PlayerSpawn, "UnassignedTeamUsed", "context=GameplaySpawnManager.GetTeamSpawnTiles team=-1 result=empty");
            return System.Array.Empty<Vector2I>();
        }

        return _spawnTilesByTeam.TryGetValue(teamId, out var teamSpawnTiles) ? teamSpawnTiles : System.Array.Empty<Vector2I>();
    }

    private static Vector2I GetFirstFloorTile(ArenaMapData arenaMapData) {
        if (arenaMapData == null)
            return Vector2I.Zero;

        foreach (var floorTile in arenaMapData.FloorTiles)
            return floorTile;

        return Vector2I.Zero;
    }
}
