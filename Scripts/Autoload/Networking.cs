using Godot;

public partial class Networking : Node
{
    public enum NetworkRole
    {
        NotSelected,
        Server,
        Client,
    }

    public NetworkRole CurrentRole { get; private set; } = NetworkRole.NotSelected;

    [Export]
    public MultiplayerData MultiplayerData { get; private set; } = new();

    public bool IsServer => CurrentRole == NetworkRole.Server;

    public bool IsClient => CurrentRole == NetworkRole.Client;

    public bool HasSelectedRole => CurrentRole != NetworkRole.NotSelected;

    public void SetServer()
    {
        CurrentRole = NetworkRole.Server;
    }

    public void SetClient()
    {
        CurrentRole = NetworkRole.Client;
    }

    public void ClearRole()
    {
        CurrentRole = NetworkRole.NotSelected;
    }

    public void UpdateSetupConfig(
        int maxPlayers,
        int localPlayerCount,
        bool onlineEnabled,
        string serverAddress,
        int serverPort,
        string gameModeId)
    {
        if (HasNetworkPeer())
        {
            Rpc(
                nameof(RpcUpdateSetupConfig),
                maxPlayers,
                localPlayerCount,
                onlineEnabled,
                serverAddress,
                serverPort,
                gameModeId);
            return;
        }

        RpcUpdateSetupConfig(maxPlayers, localPlayerCount, onlineEnabled, serverAddress, serverPort, gameModeId);
    }

    public void UpdatePlayer(
        int globalId,
        int peerId,
        int localId,
        string displayName,
        int teamId,
        bool isLocalPlayer)
    {
        if (HasNetworkPeer())
        {
            Rpc(
                nameof(RpcUpdatePlayer),
                globalId,
                peerId,
                localId,
                displayName,
                teamId,
                isLocalPlayer);
            return;
        }

        RpcUpdatePlayer(globalId, peerId, localId, displayName, teamId, isLocalPlayer);
    }

    public void UpdatePeer(int peerId, bool isHost, int teamId, int requestedLocalPlayerCount, int maxLocalPlayers)
    {
        if (HasNetworkPeer())
        {
            Rpc(nameof(RpcUpdatePeer), peerId, isHost, teamId, requestedLocalPlayerCount, maxLocalPlayers);
            return;
        }

        RpcUpdatePeer(peerId, isHost, teamId, requestedLocalPlayerCount, maxLocalPlayers);
    }

    public void RemovePeer(int peerId)
    {
        if (HasNetworkPeer())
        {
            Rpc(nameof(RpcRemovePeer), peerId);
            return;
        }

        RpcRemovePeer(peerId);
    }

    public void RemovePlayer(int peerId, int localId)
    {
        if (HasNetworkPeer())
        {
            Rpc(nameof(RpcRemovePlayer), peerId, localId);
            return;
        }

        RpcRemovePlayer(peerId, localId);
    }

    public void ClearPlayers()
    {
        if (HasNetworkPeer())
        {
            Rpc(nameof(RpcClearPlayers));
            return;
        }

        RpcClearPlayers();
    }

    public void ClearPeers()
    {
        if (HasNetworkPeer())
        {
            Rpc(nameof(RpcClearPeers));
            return;
        }

        RpcClearPeers();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcUpdateSetupConfig(
        int maxPlayers,
        int localPlayerCount,
        bool onlineEnabled,
        string serverAddress,
        int serverPort,
        string gameModeId)
    {
        MultiplayerData.SetupConfig.MaxPlayers = maxPlayers;
        MultiplayerData.SetupConfig.LocalPlayerCount = localPlayerCount;
        MultiplayerData.SetupConfig.OnlineEnabled = onlineEnabled;
        MultiplayerData.SetupConfig.ServerAddress = serverAddress;
        MultiplayerData.SetupConfig.ServerPort = serverPort;
        MultiplayerData.SetupConfig.GameModeId = gameModeId;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcUpdatePlayer(
        int globalId,
        int peerId,
        int localId,
        string displayName,
        int teamId,
        bool isLocalPlayer)
    {
        GetOrCreatePeerData(peerId);

        var playerData = GetOrCreatePlayerData(peerId, localId);
        playerData.GlobalId = globalId;
        playerData.DisplayName = displayName;
        playerData.TeamId = global::MultiplayerData.NormalizeTeamId(teamId);
        playerData.IsLocalPlayer = isLocalPlayer;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcUpdatePeer(int peerId, bool isHost, int teamId, int requestedLocalPlayerCount, int maxLocalPlayers)
    {
        var peerData = GetOrCreatePeerData(peerId);
        peerData.IsHost = isHost;
        peerData.TeamId = global::MultiplayerData.NormalizeTeamId(teamId);
        peerData.RequestedLocalPlayerCount = requestedLocalPlayerCount;
        peerData.MaxLocalPlayers = maxLocalPlayers;
        ApplyPeerTeamToPlayers(peerId, peerData.TeamId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRemovePeer(int peerId)
    {
        for (var i = MultiplayerData.Peers.Count - 1; i >= 0; i--)
        {
            if (MultiplayerData.Peers[i].PeerId == peerId)
            {
                MultiplayerData.Peers.RemoveAt(i);
                RemovePlayersOwnedByPeer(peerId);
                return;
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRemovePlayer(int peerId, int localId)
    {
        for (var i = MultiplayerData.Players.Count - 1; i >= 0; i--)
        {
            if (MultiplayerData.Players[i].PeerId == peerId && MultiplayerData.Players[i].LocalId == localId)
            {
                MultiplayerData.Players.RemoveAt(i);
                return;
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcClearPlayers()
    {
        MultiplayerData.Players.Clear();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcClearPeers()
    {
        MultiplayerData.Peers.Clear();
        MultiplayerData.Players.Clear();
    }

    private PeerData GetOrCreatePeerData(int peerId)
    {
        foreach (var peerData in MultiplayerData.Peers)
        {
            if (peerData.PeerId == peerId)
            {
                return peerData;
            }
        }

        var newPeerData = new PeerData
        {
            PeerId = peerId,
        };

        MultiplayerData.Peers.Add(newPeerData);
        return newPeerData;
    }

    private PlayerData GetOrCreatePlayerData(int peerId, int localId)
    {
        foreach (var playerData in MultiplayerData.Players)
        {
            if (playerData.PeerId == peerId && playerData.LocalId == localId)
            {
                return playerData;
            }
        }

        var newPlayerData = new PlayerData
        {
            PeerId = peerId,
            LocalId = localId,
        };

        MultiplayerData.Players.Add(newPlayerData);
        return newPlayerData;
    }

    private void RemovePlayersOwnedByPeer(int peerId)
    {
        for (var i = MultiplayerData.Players.Count - 1; i >= 0; i--)
        {
            if (MultiplayerData.Players[i].PeerId == peerId)
            {
                MultiplayerData.Players.RemoveAt(i);
            }
        }
    }

    private void ApplyPeerTeamToPlayers(int peerId, int teamId)
    {
        if (MultiplayerData.SetupConfig.OnlineEnabled)
        {
            return;
        }

        foreach (var playerData in MultiplayerData.Players)
        {
            if (playerData.PeerId == peerId)
            {
                playerData.TeamId = teamId;
            }
        }
    }

    private bool HasNetworkPeer()
    {
        return Multiplayer.MultiplayerPeer != null;
    }
}
