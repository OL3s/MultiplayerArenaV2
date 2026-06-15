using Godot;

public partial class HostServerMenu : Control {
    private const string MatchLobbyScenePath = "res://scenes/ui/lobby/match_lobby.tscn";
    private const string MainMenuScenePath = "res://scenes/ui/menus/main_menu.tscn";
    private const string NetworkIconLocalPath = "res://assets/network/networkmodes/network_local.svg";
    private const string NetworkIconLanPath = "res://assets/network/networkmodes/network_lan.svg";
    private const string NetworkIconOnlinePath = "res://assets/network/networkmodes/network_online.svg";
    private const string BackIconPath = "res://assets/ui/back_arrow.svg";

    public override void _Ready() {
        UiInputActions.EnsureConfigured();
        GetNode<Button>("MainLayout/Actions/LocalButton").Pressed += OnLocalPressed;
        GetNode<Button>("MainLayout/Actions/LanButton").Pressed += OnLanPressed;
        GetNode<Button>("MainLayout/Actions/OnlineButton").Pressed += OnOnlinePressed;
        GetNode<Button>("MainLayout/BackButton").Pressed += OnBackPressed;
        ApplyButtonIcons();
        RefreshActionAvailability();
        CallDeferred(MethodName.FocusDefaultButton);
    }

    public override void _UnhandledInput(InputEvent inputEvent) {
        if (!inputEvent.IsActionPressed("ui_cancel"))
            return;

        GetViewport().SetInputAsHandled();
        OnBackPressed();
    }

    private void OnLocalPressed() {
        if (GetActiveLocalPlayerCount() < 2)
            return;

        GetNetworking().SetLocal();
        OpenMatchLobby();
    }

    private void OnLanPressed() {
        GetNetworking().SetLan();
        OpenMatchLobby();
    }

    private void OnOnlinePressed() {
        GetNetworking().SetOnline();
        OpenMatchLobby();
    }

    private void OnBackPressed() {
        GetTree().ChangeSceneToFile(MainMenuScenePath);
    }

    private void OpenMatchLobby() {
        if (!GetNetworking().BeginHostingSession()) {
            GD.PushError(GetNetworking().LastConnectionError);
            return;
        }

        GetTree().ChangeSceneToFile(MatchLobbyScenePath);
    }

    private void FocusDefaultButton() {
        var localButton = GetNode<Button>("MainLayout/Actions/LocalButton");
        if (!localButton.Disabled) {
            localButton.GrabFocus();
            return;
        }

        GetNode<Button>("MainLayout/Actions/LanButton").GrabFocus();
    }

    private void RefreshActionAvailability() {
        GetNode<Button>("MainLayout/Actions/LocalButton").Disabled = GetActiveLocalPlayerCount() < 2;
    }

    private int GetActiveLocalPlayerCount() {
        var count = 0;
        foreach (var localPlayer in GetNetworking().LocalLobbyData.LocalPlayers) {
            if (localPlayer.IsActive)
                count++;
        }

        return count;
    }

    private void ApplyButtonIcons() {
        GetNode<Button>("MainLayout/Actions/LocalButton").Icon = GD.Load<Texture2D>(NetworkIconLocalPath);
        GetNode<Button>("MainLayout/Actions/LanButton").Icon = GD.Load<Texture2D>(NetworkIconLanPath);
        GetNode<Button>("MainLayout/Actions/OnlineButton").Icon = GD.Load<Texture2D>(NetworkIconOnlinePath);
        GetNode<Button>("MainLayout/BackButton").Icon = GD.Load<Texture2D>(BackIconPath);
    }

    private Networking GetNetworking() {
        return GetNode<Networking>("/root/Networking");
    }
}
