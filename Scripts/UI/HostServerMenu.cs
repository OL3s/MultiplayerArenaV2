using Godot;

public partial class HostServerMenu : Control {
    private const string MatchLobbyScenePath = "res://Scenes/UI/MatchLobby.tscn";
    private const string MainMenuScenePath = "res://Scenes/UI/MainMenu.tscn";

    public override void _Ready() {
        UiInputActions.EnsureConfigured();
        GetNode<Button>("MainLayout/SecondaryActions/LocalButton").Pressed += OnLocalPressed;
        GetNode<Button>("MainLayout/PrimaryAction/LanButton").Pressed += OnLanPressed;
        GetNode<Button>("MainLayout/SecondaryActions/OnlineButton").Pressed += OnOnlinePressed;
        GetNode<Button>("MainLayout/BackButton").Pressed += OnBackPressed;
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

    private Networking GetNetworking() {
        return GetNode<Networking>("/root/Networking");
    }
}
