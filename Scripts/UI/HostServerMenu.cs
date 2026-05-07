using Godot;

public partial class HostServerMenu : Control
{
    private const string MatchLobbyScenePath = "res://Scenes/UI/MatchLobby.tscn";
    private const string MainMenuScenePath = "res://Scenes/UI/MainMenu.tscn";

    public override void _Ready()
    {
        UiInputActions.EnsureConfigured();
        GetNode<Button>("MainLayout/SecondaryActions/LocalOnlyButton").Pressed += OnLocalOnlyPressed;
        GetNode<Button>("MainLayout/PrimaryAction/ServerLocalButton").Pressed += OnServerLocalPressed;
        GetNode<Button>("MainLayout/SecondaryActions/ServerOnlineButton").Pressed += OnServerOnlinePressed;
        GetNode<Button>("MainLayout/BackButton").Pressed += OnBackPressed;
        CallDeferred(MethodName.FocusDefaultButton);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!inputEvent.IsActionPressed("ui_cancel"))
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        OnBackPressed();
    }

    private void OnLocalOnlyPressed()
    {
        GetNetworking().SetLocalOnly();
        OpenMatchLobby();
    }

    private void OnServerLocalPressed()
    {
        GetNetworking().SetServerLocal();
        OpenMatchLobby();
    }

    private void OnServerOnlinePressed()
    {
        GetNetworking().SetServerOnline();
        OpenMatchLobby();
    }

    private void OnBackPressed()
    {
        GetTree().ChangeSceneToFile(MainMenuScenePath);
    }

    private void OpenMatchLobby()
    {
        if (!GetNetworking().BeginHostingSession())
        {
            GD.PushError(GetNetworking().LastConnectionError);
            return;
        }

        GetTree().ChangeSceneToFile(MatchLobbyScenePath);
    }

    private void FocusDefaultButton()
    {
        GetNode<Button>("MainLayout/PrimaryAction/ServerLocalButton").GrabFocus();
    }

    private Networking GetNetworking()
    {
        return GetNode<Networking>("/root/Networking");
    }
}
