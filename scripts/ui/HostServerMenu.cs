using Godot;

public partial class HostServerMenu : Control {
    private const string MatchLobbyScenePath = "res://scenes/ui/match_lobby.tscn";
    private const string MainMenuScenePath = "res://scenes/ui/main_menu.tscn";
    private const string NetworkIconLocalPath = "res://assets/network/networkmodes/network_local.svg";
    private const string NetworkIconLanPath = "res://assets/network/networkmodes/network_lan.svg";
    private const string NetworkIconOnlinePath = "res://assets/network/networkmodes/network_online.svg";

    public override void _Ready() {
        UiInputActions.EnsureConfigured();
        GetNode<Button>("MainLayout/SecondaryActions/LocalButton").Pressed += OnLocalPressed;
        GetNode<Button>("MainLayout/PrimaryAction/LanButton").Pressed += OnLanPressed;
        GetNode<Button>("MainLayout/SecondaryActions/OnlineButton").Pressed += OnOnlinePressed;
        GetNode<Button>("MainLayout/BackButton").Pressed += OnBackPressed;
        ApplyButtonIcons();
        CallDeferred(MethodName.FocusDefaultButton);
    }

    public override void _UnhandledInput(InputEvent inputEvent) {
        if (!inputEvent.IsActionPressed("ui_cancel"))
            return;

        GetViewport().SetInputAsHandled();
        OnBackPressed();
    }

    private void OnLocalPressed() {
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
        GetNode<Button>("MainLayout/PrimaryAction/LanButton").GrabFocus();
    }

    private void ApplyButtonIcons() {
        GetNode<Button>("MainLayout/SecondaryActions/LocalButton").Icon = GD.Load<Texture2D>(NetworkIconLocalPath);
        GetNode<Button>("MainLayout/PrimaryAction/LanButton").Icon = GD.Load<Texture2D>(NetworkIconLanPath);
        GetNode<Button>("MainLayout/SecondaryActions/OnlineButton").Icon = GD.Load<Texture2D>(NetworkIconOnlinePath);
    }

    private Networking GetNetworking() {
        return GetNode<Networking>("/root/Networking");
    }
}
