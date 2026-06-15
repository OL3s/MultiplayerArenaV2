using System.Collections.Generic;
using Godot;

public partial class LocalPlayersHud : HBoxContainer {
    private const string PlayerHudCardScenePath = "res://scenes/ui/hud/player_hud_card.tscn";
    private readonly Dictionary<int, PlayerHudCard> _cardsByGlobalId = new();
    private readonly Dictionary<int, PanelContainer> _teamGroupsByTeamId = new();
    private readonly Dictionary<int, HBoxContainer> _teamRowsByTeamId = new();
    private PackedScene _cardScene;
    private bool _useTeamGroups;

    public override void _Ready() {
        _cardScene = GD.Load<PackedScene>(PlayerHudCardScenePath);
        AddThemeConstantOverride("separation", 8);
    }

    public void BeginRefresh(bool useTeamGroups) {
        _useTeamGroups = useTeamGroups;
        foreach (var card in _cardsByGlobalId.Values)
            card.Visible = false;
        foreach (var group in _teamGroupsByTeamId.Values)
            group.Visible = false;
    }

    public void SetPlayerState(
        int globalId,
        int localId,
        int backendTeamId,
        int displayTeamId,
        string displayName,
        string statusText,
        int currentHealth,
        int maxHealth,
        Texture2D itemIcon,
        int currentAmmo,
        int maxAmmo,
        PlayerItem.AmmoCaliberType ammoCaliber,
        string gadgetText,
        Color teamColor) {
        var card = GetOrCreateCard(globalId, backendTeamId, displayTeamId, teamColor);
        card.Visible = true;
        card.SetPlayerState(localId, displayName, statusText, currentHealth, maxHealth, itemIcon, currentAmmo, maxAmmo, ammoCaliber, gadgetText, teamColor);
    }

    public void EndRefresh() {
        var staleGlobalIds = new List<int>();
        foreach (var cardEntry in _cardsByGlobalId) {
            if (!cardEntry.Value.Visible)
                staleGlobalIds.Add(cardEntry.Key);
        }

        foreach (var globalId in staleGlobalIds) {
            var card = _cardsByGlobalId[globalId];
            card.GetParent()?.RemoveChild(card);
            card.QueueFree();
            _cardsByGlobalId.Remove(globalId);
        }

        var staleTeamIds = new List<int>();
        foreach (var groupEntry in _teamGroupsByTeamId) {
            if (!groupEntry.Value.Visible)
                staleTeamIds.Add(groupEntry.Key);
        }

        foreach (var teamId in staleTeamIds) {
            var group = _teamGroupsByTeamId[teamId];
            RemoveChild(group);
            group.QueueFree();
            _teamGroupsByTeamId.Remove(teamId);
            _teamRowsByTeamId.Remove(teamId);
        }
    }

    private PlayerHudCard GetOrCreateCard(int globalId, int backendTeamId, int displayTeamId, Color teamColor) {
        var parent = _useTeamGroups ? GetOrCreateTeamRow(backendTeamId, displayTeamId, teamColor) : (Node)this;
        if (_cardsByGlobalId.TryGetValue(globalId, out var card) && IsInstanceValid(card)) {
            if (card.GetParent() != parent) {
                card.GetParent()?.RemoveChild(card);
                parent.AddChild(card);
            }

            return card;
        }

        _cardScene ??= GD.Load<PackedScene>(PlayerHudCardScenePath);
        card = _cardScene?.Instantiate<PlayerHudCard>() ?? new PlayerHudCard();
        card.Name = $"PlayerHudCard{globalId}";
        parent.AddChild(card);
        _cardsByGlobalId[globalId] = card;
        return card;
    }

    private HBoxContainer GetOrCreateTeamRow(int backendTeamId, int displayTeamId, Color teamColor) {
        if (_teamRowsByTeamId.TryGetValue(backendTeamId, out var row) && IsInstanceValid(row)) {
            _teamGroupsByTeamId[backendTeamId].Visible = true;
            ApplyTeamGroupStyle(_teamGroupsByTeamId[backendTeamId], teamColor);
            UpdateTeamLabel(_teamGroupsByTeamId[backendTeamId], displayTeamId);
            return row;
        }

        var group = new PanelContainer {
            Name = $"TeamHudGroup{backendTeamId}",
            Visible = true,
        };
        ApplyTeamGroupStyle(group, teamColor);
        AddChild(group);

        var margin = new MarginContainer { Name = "Margin" };
        margin.AddThemeConstantOverride("margin_left", 6);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        group.AddChild(margin);

        var groupRow = new HBoxContainer { Name = "TeamRow" };
        groupRow.AddThemeConstantOverride("separation", 8);
        margin.AddChild(groupRow);

        row = new HBoxContainer {
            Name = "Cards",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        row.AddThemeConstantOverride("separation", 6);
        groupRow.AddChild(row);

        var teamLabel = new Label {
            Name = "TeamLabel",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(34.0f, 0.0f),
        };
        teamLabel.AddThemeFontSizeOverride("font_size", 13);
        groupRow.AddChild(teamLabel);
        UpdateTeamLabel(group, displayTeamId);

        _teamGroupsByTeamId[backendTeamId] = group;
        _teamRowsByTeamId[backendTeamId] = row;
        return row;
    }

    private static void UpdateTeamLabel(PanelContainer group, int displayTeamId) {
        var label = group.GetNodeOrNull<Label>("Margin/TeamRow/TeamLabel");
        if (label != null)
            label.Text = $"T{displayTeamId}";
    }

    private static void ApplyTeamGroupStyle(PanelContainer group, Color teamColor) {
        var style = new StyleBoxFlat {
            BgColor = new Color(teamColor.R * 0.28f, teamColor.G * 0.28f, teamColor.B * 0.28f, 0.82f),
            BorderColor = new Color(teamColor.R, teamColor.G, teamColor.B, 0.9f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 20,
            CornerRadiusTopRight = 20,
            CornerRadiusBottomLeft = 20,
            CornerRadiusBottomRight = 20,
        };
        group.AddThemeStyleboxOverride("panel", style);
    }
}
