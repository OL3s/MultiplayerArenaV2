using Godot;

public partial class HostServerMenu : Control
{
    private const string MainMenuScenePath = "res://Scenes/UI/MainMenu.tscn";

    public override void _Ready()
    {
        GetNode<Button>("MainLayout/SecondaryActions/LocalOnlyButton").Pressed += OnLocalOnlyPressed;
        GetNode<Button>("MainLayout/PrimaryAction/ServerLocalButton").Pressed += OnServerLocalPressed;
        GetNode<Button>("MainLayout/SecondaryActions/ServerOnlineButton").Pressed += OnServerOnlinePressed;
        GetNode<Button>("MainLayout/BackButton").Pressed += OnBackPressed;
    }

    private void OnLocalOnlyPressed()
    {
        GetNetworking().SetLocalOnly();
    }

    private void OnServerLocalPressed()
    {
        GetNetworking().SetServerLocal();
    }

    private void OnServerOnlinePressed()
    {
        GetNetworking().SetServerOnline();
    }

    private void OnBackPressed()
    {
        GetTree().ChangeSceneToFile(MainMenuScenePath);
    }

    private Networking GetNetworking()
    {
        return GetNode<Networking>("/root/Networking");
    }
}
