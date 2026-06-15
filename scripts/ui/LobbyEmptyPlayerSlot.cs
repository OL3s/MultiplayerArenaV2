using Godot;

public partial class LobbyEmptyPlayerSlot : PanelContainer {
    public void SetTeam(int teamId) {
        AddThemeStyleboxOverride("panel", TeamVisuals.GetEmptyPlayerSlotStyle(teamId));
    }
}
