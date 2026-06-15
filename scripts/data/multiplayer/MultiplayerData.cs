using System;
using Godot;

[GlobalClass]
public partial class MultiplayerData : Resource {
    public const int DefaultTeamId = -1;
    public const int MinTeamId = 0;
    public const int MaxTeamId = 3;

    [Export]
    public Godot.Collections.Array<PeerData> Peers { get; set; } = new();

    [Export]
    public Godot.Collections.Array<PlayerData> Players { get; set; } = new();

    [Export]
    public SetupConfig SetupConfig { get; set; } = new();

    public int GetTeam(PlayerData playerData) {
        if (playerData == null)
            return DefaultTeamId;

        var playerTeamId = NormalizeTeamId(playerData.TeamId);
        if (playerTeamId != DefaultTeamId)
            return playerTeamId;

        var peerData = GetPeer(playerData.PeerId);
        return NormalizeTeamId(peerData?.TeamId ?? DefaultTeamId);
    }

    public int GetTeam(int peerId, int localId) {
        foreach (var playerData in Players) {
            if (playerData.PeerId == peerId && playerData.LocalId == localId)
                return GetTeam(playerData);
        }

        return DefaultTeamId;
    }

    public PlayerData GetPlayerByGlobalId(int globalId) {
        foreach (var playerData in Players) {
            if (playerData.GlobalId == globalId)
                return playerData;
        }

        return null;
    }

    public static int NormalizeTeamId(int teamId) {
        return teamId < MinTeamId ? DefaultTeamId : Math.Clamp(teamId, MinTeamId, MaxTeamId);
    }

    private PeerData GetPeer(int peerId) {
        foreach (var peerData in Peers) {
            if (peerData.PeerId == peerId)
                return peerData;
        }

        return null;
    }
}
