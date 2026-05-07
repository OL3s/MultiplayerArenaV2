using Godot;

[GlobalClass]
public partial class MultiplayerData : Resource
{
    public const int FreeForAllTeamId = 0;

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
            return FreeForAllTeamId;
        }

        if (SetupConfig.OnlineEnabled)
        {
            return NormalizeTeamId(playerData.TeamId);
        }

        var peerData = GetPeer(playerData.PeerId);
        return NormalizeTeamId(peerData?.TeamId ?? FreeForAllTeamId);
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

        return FreeForAllTeamId;
    }

    public static int NormalizeTeamId(int teamId)
    {
        return teamId <= FreeForAllTeamId ? FreeForAllTeamId : teamId;
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
