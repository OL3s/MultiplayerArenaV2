using Godot;

public partial class ScoreboardPlayerRow : PanelContainer {
    private Label _playerLabel;
    private Label _peerLabel;
    private Label _localLabel;
    private Label _idLabel;
    private Label _teamLabel;
    private Label _scoreLabel;
    private Label _killsLabel;
    private Label _deathsLabel;
    private Label _assistsLabel;

    public override void _Ready() {
        EnsureNodes();
    }

    public void SetValues(
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
        EnsureNodes();
        _playerLabel.Text = playerName;
        _peerLabel.Text = peerId;
        _localLabel.Text = localId;
        _idLabel.Text = globalId;
        _teamLabel.Text = teamId;
        _scoreLabel.Text = score;
        _killsLabel.Text = kills;
        _deathsLabel.Text = deaths;
        _assistsLabel.Text = assists;
        ApplyStyle(teamColor, isLocalPlayer);
    }

    private void EnsureNodes() {
        if (_playerLabel != null)
            return;

        _playerLabel = GetNode<Label>("Row/PlayerLabel");
        _peerLabel = GetNode<Label>("Row/PeerLabel");
        _localLabel = GetNode<Label>("Row/LocalLabel");
        _idLabel = GetNode<Label>("Row/IdLabel");
        _teamLabel = GetNode<Label>("Row/TeamLabel");
        _scoreLabel = GetNode<Label>("Row/ScoreLabel");
        _killsLabel = GetNode<Label>("Row/KillsLabel");
        _deathsLabel = GetNode<Label>("Row/DeathsLabel");
        _assistsLabel = GetNode<Label>("Row/AssistsLabel");
    }

    private void ApplyStyle(Color teamColor, bool isLocalPlayer) {
        var borderColor = isLocalPlayer
            ? Colors.White
            : new Color(teamColor.R, teamColor.G, teamColor.B, 0.55f);
        var borderWidth = isLocalPlayer ? 3 : 1;

        AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = new Color(teamColor.R * 0.18f, teamColor.G * 0.18f, teamColor.B * 0.18f, 0.92f),
            BorderColor = borderColor,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ContentMarginLeft = 10.0f,
            ContentMarginRight = 10.0f,
        });
    }
}
