using Godot;

public partial class HostServerMenu : Control {
    private const string MatchLobbyScenePath = "res://Scenes/UI/MatchLobby.tscn";
    private const string MainMenuScenePath = "res://Scenes/UI/MainMenu.tscn";
    private const string NetworkIconLocalPath = "res://Assets/Network/NetworkModes/network_local.svg";
    private const string NetworkIconLanPath = "res://Assets/Network/NetworkModes/network_lan.svg";
    private const string NetworkIconOnlinePath = "res://Assets/Network/NetworkModes/network_online.svg";

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
