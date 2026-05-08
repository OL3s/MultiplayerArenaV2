using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class JoinGameMenu : Control {
    private const string MatchLobbyScenePath = "res://Scenes/UI/MatchLobby.tscn";
    private const string MainMenuScenePath = "res://Scenes/UI/MainMenu.tscn";
    private const string ServerBrowserOverlayScenePath = "res://Scenes/UI/ServerBrowserOverlay.tscn";
    private const string NetworkIconLocalPath = "res://Assets/Network/NetworkModes/network_lan.svg";
    private const string NetworkIconOnlinePath = "res://Assets/Network/NetworkModes/network_online.svg";
    private const string NetworkIconClientPath = "res://Assets/Network/NetworkModes/network_client.svg";
    private const string NetworkIconAnyPath = "res://Assets/Network/NetworkModes/network_not_selected.svg";

    private PackedScene _serverBrowserOverlayScene;

    public override void _Ready() {
        UiInputActions.EnsureConfigured();
        _serverBrowserOverlayScene = GD.Load<PackedScene>(ServerBrowserOverlayScenePath);
        GetNode<Button>("MainLayout/SecondaryActions/BrowseLocalButton").Pressed += OnBrowseLocalPressed;
        GetNode<Button>("MainLayout/SecondaryActions/BrowseServersButton").Pressed += OnBrowseServersPressed;
        GetNode<Button>("MainLayout/PrimaryAction/QuickmatchButton").Pressed += OnQuickmatchPressed;
        GetNode<Button>("MainLayout/JoinByAddress/JoinAddressButton").Pressed += OnJoinAddressPressed;
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

    private async void OnBrowseLocalPressed() {
        var listings = await GetNetworking().DiscoverLocalServerListingsAsync();
        ShowServerBrowser(
            "Local Servers",
            listings.Count > 0 ? $"{listings.Count} local server(s) found." : "Searching local network.",
            listings,
            "No local servers found.");
    }

    private void OnBrowseServersPressed() {
        var listings = GetNetworking().GetOnlineServerListings();
        ShowServerBrowser(
            "Online Servers",
            "Online browsing is waiting on a matchmaking service.",
            listings,
            "No online matches found.");
    }

    private async void OnQuickmatchPressed() {
        var networking = GetNetworking();
        var localListings = await networking.DiscoverLocalServerListingsAsync();
        if (localListings.Count > 0) {
            ConnectToServer(localListings[0], Networking.JoinType.Quickplay);
            return;
        }

        var onlineListings = networking.GetOnlineServerListings();
        if (onlineListings.Count > 0) {
            ConnectToServer(onlineListings[0], Networking.JoinType.Quickplay);
            return;
        }

        ShowMessageOverlay("Quickplay", "No local or online matches found.");
    }

    private void OnJoinAddressPressed() {
        var addressInput = GetNode<LineEdit>("MainLayout/JoinByAddress/AddressInput").Text;
        if (!TryParseAddress(addressInput, out var address, out var port)) {
            ShowMessageOverlay("Join Failed", "Enter a valid address like 127.0.0.1:7777.");
            return;
        }

        if (!GetNetworking().BeginDirectClientConnection(address, port)) {
            ShowMessageOverlay("Join Failed", "Could not prepare a direct connection for that address.");
            return;
        }

        OpenMatchLobby();
    }

    private void OnBackPressed() {
        GetTree().ChangeSceneToFile(MainMenuScenePath);
    }

    private void OpenMatchLobby() {
        GetTree().ChangeSceneToFile(MatchLobbyScenePath);
    }

    private void ShowServerBrowser(
        string title,
        string statusText,
        IReadOnlyList<Networking.ServerListing> listings,
        string emptyMessage) {
        if (_serverBrowserOverlayScene == null) {
            GD.PushError($"Failed to load server browser overlay scene at '{ServerBrowserOverlayScenePath}'.");
            return;
        }

        var sceneOverlay = SceneOverlay.GetOrCreate(this);
        if (sceneOverlay == null)
            return;

        var browserOverlay = _serverBrowserOverlayScene.Instantiate<ServerBrowserOverlay>();
        browserOverlay.Configure(title, statusText, listings, emptyMessage, listing => ConnectToServer(listing, ResolveJoinType(title)));
        sceneOverlay.AddOverlay(browserOverlay, true);
    }

    private void ShowMessageOverlay(string title, string message) {
        ShowServerBrowser(title, message, Array.Empty<Networking.ServerListing>(), string.Empty);
    }

    private void ConnectToServer(Networking.ServerListing listing, Networking.JoinType joinType) {
        if (!GetNetworking().BeginClientConnection(listing, joinType)) {
            ShowMessageOverlay("Connection Failed", "The selected server could not be opened.");
            return;
        }

        OpenMatchLobby();
    }

    private static Networking.JoinType ResolveJoinType(string title) {
        return title switch {
            "Local Servers" => Networking.JoinType.BrowseLocal,
            "Online Servers" => Networking.JoinType.BrowseOnline,
            _ => Networking.JoinType.None,
        };
    }

    private static bool TryParseAddress(string input, out string address, out int port) {
        address = string.Empty;
        port = 7777;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmedInput = input.Trim();
        var separatorIndex = trimmedInput.LastIndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == trimmedInput.Length - 1) {
            address = trimmedInput;
            return true;
        }

        var addressPart = trimmedInput[..separatorIndex].Trim();
        var portPart = trimmedInput[(separatorIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(addressPart) || !int.TryParse(portPart, out port) || port <= 0 || port > 65535)
            return false;

        address = addressPart;
        return true;
    }

    private Networking GetNetworking() {
        return GetNode<Networking>("/root/Networking");
    }

    private void FocusDefaultButton() {
        GetNode<Button>("MainLayout/PrimaryAction/QuickmatchButton").GrabFocus();
    }

    private void ApplyButtonIcons() {
        GetNode<Button>("MainLayout/PrimaryAction/QuickmatchButton").Icon = GD.Load<Texture2D>(NetworkIconAnyPath);
        GetNode<Button>("MainLayout/SecondaryActions/BrowseLocalButton").Icon = GD.Load<Texture2D>(NetworkIconLocalPath);
        GetNode<Button>("MainLayout/SecondaryActions/BrowseServersButton").Icon = GD.Load<Texture2D>(NetworkIconOnlinePath);
        GetNode<Button>("MainLayout/JoinByAddress/JoinAddressButton").Icon = GD.Load<Texture2D>(NetworkIconClientPath);
    }
}
