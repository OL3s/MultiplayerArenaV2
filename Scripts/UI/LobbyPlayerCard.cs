using Godot;

public partial class LobbyPlayerCard : PanelContainer
{
    public void SetPlayer(PlayerData playerData, int teamId)
    {
        GetNode<Label>("Content/Info/NameLabel").Text = playerData.DisplayName;
        GetNode<Label>("Content/Info/DetailLabel").Text = $"Global {playerData.GlobalId} | Peer {playerData.PeerId} | Local {playerData.LocalId} | Team {FormatTeamName(teamId)}";
    }

    private static string FormatTeamName(int teamId)
    {
        return $"Team {teamId}";
    }
}
