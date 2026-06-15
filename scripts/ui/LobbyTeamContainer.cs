using System;
using System.Collections.Generic;
using Godot;

public partial class LobbyTeamContainer : PanelContainer {
    public event Action<int> TeamSelected;

    private const int MaxPlayersPerTeam = 4;

    private int _teamId;

    public override void _Ready() {
        GetNode<Button>("Content/TeamMeta/AssignButton").Pressed += OnAssignPressed;
    }

    public void Configure(
        int teamId,
        IReadOnlyList<PlayerData> players,
        PackedScene playerCardScene,
        PackedScene emptySlotScene,
        bool canAssign) {
        _teamId = teamId;
        AddThemeStyleboxOverride("panel", TeamVisuals.GetTeamPanelStyle(teamId));
        GetNode<Label>("Content/TeamMeta/TeamLabel").Text = $"Team {teamId}";

        var assignButton = GetNode<Button>("Content/TeamMeta/AssignButton");
        assignButton.Disabled = !canAssign;
        assignButton.Modulate = canAssign ? Colors.White : new Color(0.45f, 0.45f, 0.45f);

        var playerCards = GetNode<HBoxContainer>("Content/PlayerCards");
        ClearChildren(playerCards);

        var shownPlayers = 0;
        foreach (var playerData in players) {
            if (shownPlayers >= MaxPlayersPerTeam)
                break;

            var playerCard = playerCardScene.Instantiate<LobbyPlayerCard>();
            playerCard.SetPlayer(playerData, teamId);
            playerCards.AddChild(playerCard);
            shownPlayers++;
        }

        for (var emptySlotIndex = shownPlayers; emptySlotIndex < MaxPlayersPerTeam; emptySlotIndex++) {
            var emptySlot = emptySlotScene.Instantiate<LobbyEmptyPlayerSlot>();
            emptySlot.SetTeam(teamId);
            playerCards.AddChild(emptySlot);
        }
    }

    private void OnAssignPressed() {
        TeamSelected?.Invoke(_teamId);
    }

    private static void ClearChildren(Node node) {
        foreach (var child in node.GetChildren()) {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }
}
