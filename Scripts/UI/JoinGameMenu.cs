using Godot;

public partial class JoinGameMenu : Control
{
    private const string MainMenuScenePath = "res://Scenes/UI/MainMenu.tscn";

    public override void _Ready()
    {
        GetNode<Button>("MainLayout/SecondaryActions/BrowseLocalButton").Pressed += OnBrowseLocalPressed;
        GetNode<Button>("MainLayout/SecondaryActions/BrowseServersButton").Pressed += OnBrowseServersPressed;
        GetNode<Button>("MainLayout/PrimaryAction/QuickmatchButton").Pressed += OnQuickmatchPressed;
        GetNode<Button>("MainLayout/JoinByAddress/JoinAddressButton").Pressed += OnJoinAddressPressed;
        GetNode<Button>("MainLayout/BackButton").Pressed += OnBackPressed;
    }

    private void OnBrowseLocalPressed()
    {
        GetNetworking().SetClient();
    }

    private void OnBrowseServersPressed()
    {
        GetNetworking().SetClient();
    }

    private void OnQuickmatchPressed()
    {
        GetNetworking().SetClient();
    }

    private void OnJoinAddressPressed()
    {
        GetNetworking().SetClient();
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
