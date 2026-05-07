using Godot;

public partial class HostServerMenu : Control
{
    private const string MainMenuScenePath = "res://Scenes/UI/MainMenu.tscn";

    public override void _Ready()
    {
        GetNode<Button>("MainLayout/ServerModeButtons/LocalOnlyButton").Pressed += OnLocalOnlyPressed;
        GetNode<Button>("MainLayout/ServerModeButtons/ServerLocalButton").Pressed += OnServerLocalPressed;
        GetNode<Button>("MainLayout/ServerModeButtons/ServerOnlineButton").Pressed += OnServerOnlinePressed;
        GetNode<Button>("MainLayout/BackButton").Pressed += OnBackPressed;
        ApplyPlaceholderIcons();
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

    private void ApplyPlaceholderIcons()
    {
        SetPlaceholderIcon(GetNode<Button>("MainLayout/ServerModeButtons/LocalOnlyButton"), "Home");
        SetPlaceholderIcon(GetNode<Button>("MainLayout/ServerModeButtons/ServerLocalButton"), "Network");
        SetPlaceholderIcon(GetNode<Button>("MainLayout/ServerModeButtons/ServerOnlineButton"), "World");
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
