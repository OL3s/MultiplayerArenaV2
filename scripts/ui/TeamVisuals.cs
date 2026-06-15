using Godot;

public static class TeamVisuals {
    private const string PlayerCardStylePath = "res://assets/ui/styles/lobby_player_card.tres";
    private const string EmptyPlayerSlotStylePath = "res://assets/ui/styles/lobby_empty_player_slot.tres";

    private static readonly Color[] TeamColors = {
        new(0.95f, 0.22f, 0.26f),
        new(0.20f, 0.55f, 1.00f),
        new(0.22f, 0.78f, 0.38f),
        new(1.00f, 0.72f, 0.18f),
    };

    private static readonly string[] TeamPanelStylePaths = {
        "res://assets/ui/styles/lobby_team_1_panel.tres",
        "res://assets/ui/styles/lobby_team_2_panel.tres",
        "res://assets/ui/styles/lobby_team_3_panel.tres",
        "res://assets/ui/styles/lobby_team_4_panel.tres",
    };

    public static Color GetTeamColor(int teamId) {
        if (teamId < 1 || teamId > TeamColors.Length)
            return new Color(0.42f, 0.46f, 0.52f);

        return TeamColors[teamId - 1];
    }

    public static Color GetTeamBackgroundColor(int teamId) {
        var color = GetTeamColor(teamId);
        return new Color(color.R * 0.24f, color.G * 0.24f, color.B * 0.24f, 0.92f);
    }

    public static Color GetTeamBorderColor(int teamId) {
        var color = GetTeamColor(teamId);
        return new Color(color.R, color.G, color.B, 0.95f);
    }

    public static StyleBoxFlat GetTeamPanelStyle(int teamId) {
        return LoadStyle(GetTeamPanelStylePath(teamId));
    }

    public static StyleBoxFlat GetPlayerCardStyle(int teamId) {
        var style = LoadStyle(PlayerCardStylePath);
        style.BorderColor = GetTeamBorderColor(teamId);
        return style;
    }

    public static StyleBoxFlat GetEmptyPlayerSlotStyle(int teamId) {
        var style = LoadStyle(EmptyPlayerSlotStylePath);
        var borderColor = GetTeamBorderColor(teamId);
        borderColor.A = 0.36f;
        style.BorderColor = borderColor;
        return style;
    }

    private static string GetTeamPanelStylePath(int teamId) {
        if (teamId < 1 || teamId > TeamPanelStylePaths.Length)
            return TeamPanelStylePaths[0];

        return TeamPanelStylePaths[teamId - 1];
    }

    private static StyleBoxFlat LoadStyle(string stylePath) {
        var style = GD.Load<StyleBoxFlat>(stylePath);
        return (StyleBoxFlat)style.Duplicate();
    }
}
