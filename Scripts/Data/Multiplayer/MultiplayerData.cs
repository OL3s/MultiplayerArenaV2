using System;
using Godot;

[GlobalClass]
public partial class MultiplayerData : Resource
{
    public const int DefaultTeamId = 0;

    [Export]
    public Godot.Collections.Array<PeerData> Peers { get; set; } = new();

    [Export]
    public Godot.Collections.Array<PlayerData> Players { get; set; } = new();

    [Export]
    public SetupConfig SetupConfig { get; set; } = new();

    public int GetTeam(PlayerData playerData)
    {
        if (playerData == null)
        {
            return DefaultTeamId;
        }

        var peerData = GetPeer(playerData.PeerId);
        return NormalizeTeamId(peerData?.TeamId ?? DefaultTeamId);
    }

    public int GetTeam(int peerId, int localId)
    {
        foreach (var playerData in Players)
        {
            if (playerData.PeerId == peerId && playerData.LocalId == localId)
            {
                return GetTeam(playerData);
            }
        }

        return DefaultTeamId;
    }

    public static int NormalizeTeamId(int teamId)
    {
        return Math.Clamp(teamId, DefaultTeamId, 4);
    }

    private PeerData GetPeer(int peerId)
    {
        foreach (var peerData in Peers)
        {
            if (peerData.PeerId == peerId)
            {
                return peerData;
            }
        }

        return null;
    }
}
