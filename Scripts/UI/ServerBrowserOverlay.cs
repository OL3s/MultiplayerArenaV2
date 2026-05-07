using System;
using System.Collections.Generic;
using Godot;

public partial class ServerBrowserOverlay : Control
{
    private Action<Networking.ServerListing> _onConnect;

    public override void _Ready()
    {
        GetNode<Button>("CenterContainer/PopupPanel/MarginContainer/Content/Header/CloseButton").Pressed += OnClosePressed;
    }

    public void Configure(
        string title,
        string statusText,
        IReadOnlyList<Networking.ServerListing> listings,
        string emptyMessage,
        Action<Networking.ServerListing> onConnect)
    {
        _onConnect = onConnect;
        GetNode<Label>("CenterContainer/PopupPanel/MarginContainer/Content/Header/TitleLabel").Text = title;

        var statusLabel = GetNode<Label>("CenterContainer/PopupPanel/MarginContainer/Content/StatusLabel");
        statusLabel.Text = statusText;
        statusLabel.Visible = !string.IsNullOrWhiteSpace(statusText);

        var listingsContainer = GetNode<VBoxContainer>("CenterContainer/PopupPanel/MarginContainer/Content/ListingsScroll/Listings");
        ClearChildren(listingsContainer);

        foreach (var listing in listings)
        {
            listingsContainer.AddChild(CreateListingCard(listing));
        }

        var hasListings = listings.Count > 0;
        GetNode<ScrollContainer>("CenterContainer/PopupPanel/MarginContainer/Content/ListingsScroll").Visible = hasListings;

        var emptyLabel = GetNode<Label>("CenterContainer/PopupPanel/MarginContainer/Content/EmptyLabel");
        emptyLabel.Text = emptyMessage;
        emptyLabel.Visible = !hasListings && !string.IsNullOrWhiteSpace(emptyMessage);
    }

    private Control CreateListingCard(Networking.ServerListing listing)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 96),
        };

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);

        var info = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        info.AddThemeConstantOverride("separation", 4);

        var nameLabel = new Label
        {
            Text = listing.DisplayName,
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 20);

        var detailsLabel = new Label
        {
            Text = $"{listing.Address}:{listing.Port}  |  Players {listing.PlayerCount}/{listing.MaxPlayers}  |  {GetMatchTypeLabel(listing)}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        detailsLabel.AddThemeFontSizeOverride("font_size", 12);

        var connectButton = new Button
        {
            Text = "Connect",
            CustomMinimumSize = new Vector2(140, 44),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        connectButton.Pressed += () => _onConnect?.Invoke(listing);

        info.AddChild(nameLabel);
        info.AddChild(detailsLabel);
        row.AddChild(info);
        row.AddChild(connectButton);
        margin.AddChild(row);
        panel.AddChild(margin);
        return panel;
    }

    private static string GetMatchTypeLabel(Networking.ServerListing listing)
    {
        return listing.IsOnline ? "Online" : "LAN";
    }

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void OnClosePressed()
    {
        QueueFree();
    }
}
