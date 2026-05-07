using Godot;

public partial class JoinGameMenu : Control
{
    private const string MainMenuScenePath = "res://Scenes/UI/MainMenu.tscn";

    public override void _Ready()
    {
        GetNode<Button>("MainLayout/JoinOptions/BrowseLocalButton").Pressed += OnBrowseLocalPressed;
        GetNode<Button>("MainLayout/JoinOptions/BrowseServersButton").Pressed += OnBrowseServersPressed;
        GetNode<Button>("MainLayout/JoinOptions/QuickmatchButton").Pressed += OnQuickmatchPressed;
        GetNode<Button>("MainLayout/JoinByAddress/JoinAddressButton").Pressed += OnJoinAddressPressed;
        GetNode<Button>("MainLayout/BackButton").Pressed += OnBackPressed;
        ApplyPlaceholderIcons();
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

    private void ApplyPlaceholderIcons()
    {
        SetPlaceholderIcon(GetNode<Button>("MainLayout/JoinOptions/BrowseLocalButton"), "Network");
        SetPlaceholderIcon(GetNode<Button>("MainLayout/JoinOptions/BrowseServersButton"), "World");
        SetPlaceholderIcon(GetNode<Button>("MainLayout/JoinOptions/QuickmatchButton"), "Play");
        SetPlaceholderIcon(GetNode<Button>("MainLayout/JoinByAddress/JoinAddressButton"), "Forward");
    }

    private void SetPlaceholderIcon(Button button, string iconName)
    {
        button.Icon = GetThemeIcon(iconName, "EditorIcons");
        button.Set("icon_max_width", 26);
    }

    private Networking GetNetworking()
    {
        return GetNode<Networking>("/root/Networking");
    }
}
