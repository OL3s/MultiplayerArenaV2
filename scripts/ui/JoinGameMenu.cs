using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class JoinGameMenu : Control {
    private const string MatchLobbyScenePath = "res://scenes/ui/lobby/match_lobby.tscn";
    private const string MainMenuScenePath = "res://scenes/ui/menus/main_menu.tscn";
    private const string ServerBrowserOverlayScenePath = "res://scenes/ui/overlays/server_browser_overlay.tscn";
    private const string NetworkIconLocalPath = "res://assets/network/networkmodes/network_lan.svg";
    private const string NetworkIconOnlinePath = "res://assets/network/networkmodes/network_online.svg";
    private const string NetworkIconClientPath = "res://assets/network/networkmodes/network_client.svg";
    private const string NetworkIconAnyPath = "res://assets/network/networkmodes/network_not_selected.svg";
    private const string BackIconPath = "res://assets/ui/back_arrow.svg";

    private PackedScene _serverBrowserOverlayScene;
    private Control _joinAddressPopup;
    private Node _joinAddressPopupOriginalParent;
    private int _joinAddressPopupOriginalIndex;
    private LineEdit _joinAddressInput;

    public override void _Ready() {
        UiInputActions.EnsureConfigured();
        _serverBrowserOverlayScene = GD.Load<PackedScene>(ServerBrowserOverlayScenePath);
        GetNode<Button>("MainLayout/Actions/QuickmatchButton").Pressed += OnQuickmatchPressed;
        GetNode<Button>("MainLayout/Actions/BrowseLocalButton").Pressed += OnBrowseLocalPressed;
        GetNode<Button>("MainLayout/Actions/BrowseOnlineButton").Pressed += OnBrowseServersPressed;
        GetNode<Button>("MainLayout/Actions/JoinIpButton").Pressed += OnJoinIpPressed;
        GetNode<Button>("MainLayout/BackButton").Pressed += OnBackPressed;
        _joinAddressPopup = GetNode<Control>("JoinAddressPopup");
        _joinAddressPopupOriginalParent = _joinAddressPopup.GetParent();
        _joinAddressPopupOriginalIndex = _joinAddressPopup.GetIndex();
        _joinAddressInput = GetNode<LineEdit>("JoinAddressPopup/CenterContainer/PopupPanel/MarginContainer/Content/AddressInput");
        GetNode<Button>("JoinAddressPopup/CenterContainer/PopupPanel/MarginContainer/Content/Actions/CancelButton").Pressed += HideJoinAddressPopup;
        GetNode<Button>("JoinAddressPopup/CenterContainer/PopupPanel/MarginContainer/Content/Actions/JoinButton").Pressed += OnJoinAddressPressed;
        _joinAddressInput.TextSubmitted += _ => OnJoinAddressPressed();
        ApplyButtonIcons();
        CallDeferred(MethodName.FocusDefaultButton);
    }

    public override void _UnhandledInput(InputEvent inputEvent) {
        if (!inputEvent.IsActionPressed("ui_cancel"))
            return;

        GetViewport().SetInputAsHandled();
        if (_joinAddressPopup.Visible) {
            HideJoinAddressPopup();
            return;
        }

        OnBackPressed();
    }

    private async void OnBrowseLocalPressed() {
        var listings = await GetNetworking().DiscoverLocalServerListingsAsync();
        if (listings.Count == 0)
            GameLog.Print(GameLogScope.UI, GameLogType.ApiCall, "BrowseLocalServersEmpty");

        ShowServerBrowser(
            "Local Servers",
            listings.Count > 0 ? $"{listings.Count} local server(s) found." : "Searching local network.",
            listings,
            "No local servers found.");
    }

    private void OnBrowseServersPressed() {
        var listings = GetNetworking().GetOnlineServerListings();
        if (listings.Count == 0)
            GameLog.Print(GameLogScope.UI, GameLogType.ApiCall, "BrowseOnlineServersEmpty");

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

        GameLog.Print(GameLogScope.UI, GameLogType.ApiCall, "QuickmatchServersEmpty");
        ShowMessageOverlay("Quickplay", "No local or online matches found.");
    }

    private void OnJoinIpPressed() {
        var overlay = SceneOverlay.GetOrCreate(this);
        if (overlay == null)
            return;

        if (_joinAddressPopup.GetParent() != overlay) {
            _joinAddressPopup.GetParent()?.RemoveChild(_joinAddressPopup);
            overlay.AddOverlay(_joinAddressPopup);
        }

        _joinAddressPopup.Visible = true;
        _joinAddressInput.SelectAll();
        _joinAddressInput.GrabFocus();
    }

    private void OnJoinAddressPressed() {
        var addressInput = _joinAddressInput.Text;
        if (!TryParseAddress(addressInput, out var address, out var port)) {
            ShowMessageOverlay("Join Failed", "Enter a valid address like 127.0.0.1:12000.");
            return;
        }

        if (!GetNetworking().BeginDirectClientConnection(address, port)) {
            ShowMessageOverlay("Join Failed", "Could not prepare a direct connection for that address.");
            return;
        }

        OpenMatchLobby();
    }

    private void HideJoinAddressPopup() {
        _joinAddressPopup.Visible = false;
        if (_joinAddressPopup.GetParent() != _joinAddressPopupOriginalParent) {
            _joinAddressPopup.GetParent()?.RemoveChild(_joinAddressPopup);
            _joinAddressPopupOriginalParent.AddChild(_joinAddressPopup);
            _joinAddressPopupOriginalParent.MoveChild(_joinAddressPopup, _joinAddressPopupOriginalIndex);
        }

        GetNode<Button>("MainLayout/Actions/JoinIpButton").GrabFocus();
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
        sceneOverlay.AddOverlay(browserOverlay);
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
        port = 12000;

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
        GetNode<Button>("MainLayout/Actions/QuickmatchButton").GrabFocus();
    }

    private void ApplyButtonIcons() {
        GetNode<Button>("MainLayout/Actions/QuickmatchButton").Icon = UiResourceLoader.LoadIconTexture(NetworkIconAnyPath);
        GetNode<Button>("MainLayout/Actions/BrowseLocalButton").Icon = UiResourceLoader.LoadIconTexture(NetworkIconLocalPath);
        GetNode<Button>("MainLayout/Actions/BrowseOnlineButton").Icon = UiResourceLoader.LoadIconTexture(NetworkIconOnlinePath);
        GetNode<Button>("MainLayout/Actions/JoinIpButton").Icon = UiResourceLoader.LoadIconTexture(NetworkIconClientPath);
        GetNode<Button>("MainLayout/BackButton").Icon = UiResourceLoader.LoadIconTexture(BackIconPath);
    }
}
