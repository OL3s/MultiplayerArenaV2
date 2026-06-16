using System.Collections.Generic;
using Godot;

public partial class ScoreboardOverlay : PanelContainer {
    private const string PlayerRowScenePath = "res://scenes/ui/hud/scoreboard_player_row.tscn";
    private VBoxContainer _rows;
    private PackedScene _rowScene;

    public override void _Ready() {
        EnsureNodes();
    }

    public void SetPlayers(IReadOnlyList<PlayerData> players, MultiplayerData multiplayerData) {
        EnsureNodes();
        ClearRows();

        if (players.Count == 0) {
            AddRow("Waiting for players", "-", "-", "-", "-", "0", "0", "0", "0", new Color(0.55f, 0.62f, 0.72f), false);
            return;
        }

        foreach (var playerData in players) {
            var teamId = multiplayerData?.GetTeam(playerData) ?? MultiplayerData.DefaultTeamId;
            var teamText = teamId == MultiplayerData.DefaultTeamId ? "Auto" : (teamId + 1).ToString();
            var teamColor = teamId == MultiplayerData.DefaultTeamId
                ? new Color(0.55f, 0.62f, 0.72f)
                : TeamVisuals.GetTeamColor(Mathf.Clamp(teamId + 1, 1, 4));
            AddRow(
                playerData.DisplayName,
                playerData.PeerId.ToString(),
                playerData.LocalId.ToString(),
                playerData.GlobalId.ToString(),
                teamText,
                playerData.Score.ToString(),
                playerData.Kills.ToString(),
                playerData.Deaths.ToString(),
                playerData.Assists.ToString(),
                teamColor,
                playerData.IsLocalPlayer);
        }
    }

    private void AddRow(
        string playerName,
        string peerId,
        string localId,
        string globalId,
        string teamId,
        string score,
        string kills,
        string deaths,
        string assists,
        Color teamColor,
        bool isLocalPlayer) {
        _rowScene ??= GD.Load<PackedScene>(PlayerRowScenePath);
        var row = _rowScene?.Instantiate<ScoreboardPlayerRow>() ?? new ScoreboardPlayerRow();
        row.SetValues(playerName, peerId, localId, globalId, teamId, score, kills, deaths, assists, teamColor, isLocalPlayer);
        _rows.AddChild(row);
    }

    private void ClearRows() {
        foreach (var child in _rows.GetChildren()) {
            _rows.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void EnsureNodes() {
        if (_rows != null)
            return;

        _rows = GetNode<VBoxContainer>("Margin/Layout/ScoreboardScroll/Rows");
    }
}
