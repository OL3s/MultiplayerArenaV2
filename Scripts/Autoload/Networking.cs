using Godot;

public partial class Networking : Node
{
    private const int ServerPeerId = 1;

    public enum NetworkMode
    {
        NotSelected,
        LocalOnly,
        ServerLocal,
        ServerOnline,
        DedicatedServer,
        Client,
    }

    public NetworkMode CurrentMode { get; private set; } = NetworkMode.NotSelected;

    [Export]
    public MultiplayerData MultiplayerData { get; private set; } = new();

    [Export]
    public LocalLobbyData LocalLobbyData { get; private set; } = new();

    public override void _Ready()
    {
        if (IsHeadlessRun())
        {
            SetDedicatedServer();
        }
    }

    public bool IsLocalOnly => CurrentMode == NetworkMode.LocalOnly;

    public bool IsServer => CurrentMode is NetworkMode.ServerLocal or NetworkMode.ServerOnline or NetworkMode.DedicatedServer;

    public bool IsClient => CurrentMode == NetworkMode.Client;

    public bool IsDedicatedServer => CurrentMode == NetworkMode.DedicatedServer;

    public bool IsOnlineServer => CurrentMode == NetworkMode.ServerOnline;

    public bool HasSelectedMode => CurrentMode != NetworkMode.NotSelected;

    public void SetLocalOnly()
    {
        CurrentMode = NetworkMode.LocalOnly;
    }

    public void SetServerLocal()
    {
        CurrentMode = NetworkMode.ServerLocal;
    }

    public void SetServerOnline()
    {
        CurrentMode = NetworkMode.ServerOnline;
    }

    public void SetDedicatedServer()
    {
        CurrentMode = NetworkMode.DedicatedServer;
    }

    public void SetClient()
    {
        CurrentMode = NetworkMode.Client;
    }

    public void ClearMode()
    {
        CurrentMode = NetworkMode.NotSelected;
    }

    public void RegisterLocalLobbyPlayers()
    {
        if (IsDedicatedServer)
        {
            return;
        }

        var peerId = GetLocalPeerId();
        if (peerId == -1)
        {
            return;
        }

        var activeLocalPlayerCount = GetActiveLocalPlayerCount();
        MultiplayerData.Peers.Clear();
        MultiplayerData.Players.Clear();
        MultiplayerData.SetupConfig.ForceFreeForAllTeams = CurrentMode == NetworkMode.LocalOnly;

        UpdatePeer(peerId, IsServer, global::MultiplayerData.FreeForAllTeamId, activeLocalPlayerCount, 4);

        var globalId = 0;
        foreach (var localPlayerData in LocalLobbyData.LocalPlayers)
        {
            if (!localPlayerData.IsActive)
            {
                continue;
            }

            UpdatePlayer(
                globalId,
                peerId,
                localPlayerData.LocalId,
                localPlayerData.DisplayName,
                true);
            globalId++;
        }
    }

    public void SetLocalPeerTeam(int teamId)
    {
        var peerId = GetRegisteredLocalPeerId();
        if (peerId == -1)
        {
            return;
        }

        SetPeerTeam(peerId, teamId);
    }

    public void SetPeerTeam(int peerId, int teamId)
    {
        var normalizedTeamId = global::MultiplayerData.NormalizeTeamId(teamId);
        MultiplayerData.SetupConfig.ForceFreeForAllTeams = normalizedTeamId == global::MultiplayerData.FreeForAllTeamId;

        var peerData = GetOrCreatePeerData(peerId);
        UpdatePeer(
            peerId,
            peerData.IsHost,
            normalizedTeamId,
            peerData.RequestedLocalPlayerCount,
            peerData.MaxLocalPlayers);
    }

    private int GetRegisteredLocalPeerId()
    {
        foreach (var playerData in MultiplayerData.Players)
        {
            if (playerData.IsLocalPlayer)
            {
                return playerData.PeerId;
            }
        }

        return GetLocalPeerId();
    }

    private int GetLocalPeerId()
    {
        if (CurrentMode is NetworkMode.LocalOnly or NetworkMode.ServerLocal or NetworkMode.ServerOnline)
        {
            return ServerPeerId;
        }

        if (HasNetworkPeer())
        {
            return Multiplayer.GetUniqueId();
        }

        return -1;
    }

    private int GetActiveLocalPlayerCount()
    {
        var count = 0;
        foreach (var localPlayerData in LocalLobbyData.LocalPlayers)
        {
            if (localPlayerData.IsActive)
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsHeadlessRun()
    {
        if (DisplayServer.GetName() == "headless")
        {
            return true;
        }

        foreach (var argument in OS.GetCmdlineArgs())
        {
            if (argument == "--headless")
            {
                return true;
            }
        }

        return false;
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
                isLocalPlayer);
            return;
        }

        RpcUpdatePlayer(globalId, peerId, localId, displayName, isLocalPlayer);
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
        bool isLocalPlayer)
    {
        GetOrCreatePeerData(peerId);

        var playerData = GetOrCreatePlayerData(peerId, localId);
        playerData.GlobalId = globalId;
        playerData.DisplayName = displayName;
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

    private bool HasNetworkPeer()
    {
        return Multiplayer.MultiplayerPeer != null;
    }
}
