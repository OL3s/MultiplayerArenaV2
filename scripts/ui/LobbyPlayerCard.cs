using Godot;

public partial class LobbyPlayerCard : PanelContainer {
    public void SetPlayer(PlayerData playerData, int teamId) {
        GetNode<Label>("Content/Info/NameLabel").Text = playerData.DisplayName;
        GetNode<Label>("Content/Info/DetailLabel").Text = $"P{playerData.GlobalId + 1} | Slot {playerData.LocalId + 1}";
        GetNode<Label>("Content/Info/DeviceLabel").Text = playerData.IsLocalPlayer ? "Local Device" : "Remote Device";
        AddThemeStyleboxOverride("panel", TeamVisuals.GetPlayerCardStyle(teamId));
    }
}
