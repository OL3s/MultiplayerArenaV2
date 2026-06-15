using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Godot;

public partial class Networking : Node {
    private const int ServerPeerId = 1;
    private const int DefaultServerPort = 12000;
    private const int MinServerPort = 11000;
    private const int MaxServerPort = 13000;
    private const int DiscoveryPort = 7778;
    private const int MaxClients = 8;
    private const string DiscoveryRequestMessage = "MULTIPLAYERARENA_DISCOVER";
    private const string DiscoveryResponsePrefix = "MULTIPLAYERARENA_SERVER";
    private const string NetworkDebugIconNotSelectedPath = "res://assets/network/networkmodes/network_not_selected.svg";
    private const string NetworkDebugIconLocalPath = "res://assets/network/networkmodes/network_local.svg";
    private const string NetworkDebugIconLanPath = "res://assets/network/networkmodes/network_lan.svg";
    private const string NetworkDebugIconOnlinePath = "res://assets/network/networkmodes/network_online.svg";
    private const string NetworkDebugIconClientPath = "res://assets/network/networkmodes/network_client.svg";
    private const string NetworkDebugIconConnectionLostPath = "res://assets/network/networkmodes/network_connection_lost.svg";

    public enum NetworkMode {
        NotSelected,
        Local,
        Lan,
        Online,
        Client,
    }

    public enum JoinType {
        None,
        BrowseLocal,
        BrowseOnline,
        Quickplay,
        DirectAddress,
    }

    private sealed class PeerTeamGroup {
        public int PeerId { get; set; }

        public int PlayerCount { get; set; }
    }

    public sealed class ServerListing {
        public string ListingId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = "Server";

        public string Address { get; set; } = "127.0.0.1";

        public int Port { get; set; } = DefaultServerPort;

        public bool IsOnline { get; set; }

        public int PlayerCount { get; set; }

        public int MaxPlayers { get; set; } = MaxClients;

        public string GameModeId { get; set; } = "deathmatch";
    }

    [Signal]
    public delegate void LobbyStateChangedEventHandler();

    [Signal]
    public delegate void ConnectionStateChangedEventHandler();

    [Signal]
    public delegate void ConfigApplyStateChangedEventHandler();

    [Signal]
    public delegate void ArenaMapChangedEventHandler();

    public NetworkMode CurrentMode { get; private set; } = NetworkMode.NotSelected;

    public JoinType CurrentJoinType { get; private set; } = JoinType.None;

    public string ConnectionStatusText { get; private set; } = "Status: No mode selected.";

    public string LastConnectionError { get; private set; } = string.Empty;

    public string CurrentServerName { get; private set; } = string.Empty;

    public string LastConfigApplyMessage { get; private set; } = string.Empty;

    public bool HasLostConnection { get; private set; }

    [Export]
    public SettingsConfig SettingsConfig { get; private set; } = new();

    [Export]
    public MultiplayerData MultiplayerData { get; private set; } = new();

    [Export]
    public ArenaMapData ArenaMapData { get; private set; } = new();

    public SetupConfig CachedSetupConfig { get; private set; } = new();

    [Export]
    public LocalLobbyData LocalLobbyData { get; private set; } = new();

    private readonly List<ServerListing> _localServerListings = new();
    private PacketPeerUdp _discoveryServer;
    private CanvasLayer _networkModeDebugLayer;
    private TextureRect _networkModeDebugIcon;
    private Label _networkModeDebugPeerLabel;
    private string _lastLoggedRootScenePath = string.Empty;
    private bool _suppressLobbyStateChanged;

    public override void _Ready() {
        GameLog.RegisterNetworking(this);
        GameLog.Print(GameLogScope.Networking, GameLogType.Lifecycle, "NetworkingReady");
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
        SettingsConfig = SettingsConfig.LoadOrCreate();
        SyncCachedSetupConfig();
        CreateNetworkModeDebugController();

        ApplyCommandLineNetworkModeOverrides();

        if (IsHeadlessRun() && !HasSelectedMode)
            SetLan();

        UpdateNetworkModeDebugIcon();
    }

    public override void _Process(double delta) {
        LogRootSceneChange();
        PollDiscoveryRequests();
    }

    public bool IsLocal => CurrentMode == NetworkMode.Local;

    public bool IsServer => CurrentMode is NetworkMode.Lan or NetworkMode.Online;

    public bool IsClient => CurrentMode == NetworkMode.Client;

    public bool IsOnline => CurrentMode == NetworkMode.Online;

    public bool HasSelectedMode => CurrentMode != NetworkMode.NotSelected;

    public bool HasActiveNetworkPeer => HasNetworkPeer();

    public bool IsNetworkedSession => HasNetworkPeer();

    public bool IsRemoteClient => IsClient && HasNetworkPeer();

    public bool IsHostAuthority => IsLocal || IsAuthoritativeServer();

    public int CurrentServerPort => MultiplayerData.SetupConfig.ServerPort;

    public bool HasPendingSetupConfigChanges => HasSelectedMode && !MultiplayerData.SetupConfig.IsEquivalentTo(CachedSetupConfig);

    public SetupConfig GetEditableSetupConfig() {
        return CachedSetupConfig;
    }

    public IReadOnlyList<ServerListing> GetLocalServerListings() {
        var listings = new List<ServerListing>(_localServerListings.Count);
        foreach (var listing in _localServerListings)
            listings.Add(CloneServerListing(listing));

        return listings;
    }

    public IReadOnlyList<ServerListing> GetOnlineServerListings() {
        return Array.Empty<ServerListing>();
    }

    public async Task<IReadOnlyList<ServerListing>> DiscoverLocalServerListingsAsync() {
        _localServerListings.Clear();
        EmitLobbyStateChanged();

        using var discoveryClient = new PacketPeerUdp();
        var bindError = discoveryClient.Bind(0);
        if (bindError != Error.Ok) {
            LastConnectionError = $"Could not bind discovery client: {bindError}.";
            EmitConnectionStateChanged();
            return GetLocalServerListings();
        }

        discoveryClient.SetBroadcastEnabled(true);
        discoveryClient.SetDestAddress("255.255.255.255", DiscoveryPort);
        discoveryClient.PutPacket(Encoding.UTF8.GetBytes(DiscoveryRequestMessage));

        for (var i = 0; i < 8; i++) {
            PollDiscoveryResponses(discoveryClient);
            await ToSignal(GetTree().CreateTimer(0.08), SceneTreeTimer.SignalName.Timeout);
        }

        PollDiscoveryResponses(discoveryClient);
        return GetLocalServerListings();
    }

    public void SetLocal() {
        SetNetworkMode(NetworkMode.Local);
    }

    public void SetLan() {
        SetNetworkMode(NetworkMode.Lan);
    }

    public void SetOnline() {
        SetNetworkMode(NetworkMode.Online);
    }

    public void SetClient() {
        SetNetworkMode(NetworkMode.Client);
    }

    public void ClearMode() {
        SetNetworkMode(NetworkMode.NotSelected);
    }

    public void SetShowNetworkDebugOverlay(bool showNetworkDebugOverlay) {
        SettingsConfig.ShowNetworkDebugOverlay = showNetworkDebugOverlay;
        UpdateNetworkModeDebugIcon();
    }

    public void SaveSettingsConfig() {
        SettingsConfig.Save();
    }

    public void SetNetworkMode(NetworkMode networkMode) {
        if (CurrentMode == networkMode) {
            if (HasLostConnection) {
                GameLog.Print(GameLogScope.Networking, GameLogType.StateChange, "ConnectionLostCleared", $"mode={CurrentMode}");
                HasLostConnection = false;
                UpdateNetworkModeDebugIcon();
                EmitConnectionStateChanged();
            }

            return;
        }

        var previousMode = CurrentMode;
        CurrentMode = networkMode;
        HasLostConnection = false;
        GameLog.Print(GameLogScope.Networking, GameLogType.StateChange, "NetworkModeChanged", $"from={previousMode} to={CurrentMode}");
        UpdateNetworkModeDebugIcon();
        EmitConnectionStateChanged();
    }

    private void CreateNetworkModeDebugController() {
        if (DisplayServer.GetName() == "headless" || _networkModeDebugIcon != null)
            return;

        _networkModeDebugLayer = new CanvasLayer {
            Name = "NetworkModeDebugLayer",
            Layer = 128,
            Visible = SettingsConfig.ShowNetworkDebugOverlay,
        };

        var debugLayout = new HBoxContainer {
            Name = "NetworkModeDebugLayout",
            Position = new Vector2(12.0f, 12.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        debugLayout.AddThemeConstantOverride("separation", 8);

        _networkModeDebugIcon = new TextureRect {
            Name = "NetworkModeDebugIcon",
            CustomMinimumSize = new Vector2(42.0f, 42.0f),
            Size = new Vector2(42.0f, 42.0f),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.75f),
        };

        _networkModeDebugPeerLabel = new Label {
            Name = "NetworkModeDebugPeerLabel",
            VerticalAlignment = VerticalAlignment.Top,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        _networkModeDebugPeerLabel.AddThemeFontSizeOverride("font_size", 14);

        debugLayout.AddChild(_networkModeDebugIcon);
        debugLayout.AddChild(_networkModeDebugPeerLabel);
        _networkModeDebugLayer.AddChild(debugLayout);
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, _networkModeDebugLayer);
        UpdateNetworkModeDebugIcon();
    }

    private void UpdateNetworkModeDebugIcon() {
        if (_networkModeDebugLayer != null)
            _networkModeDebugLayer.Visible = SettingsConfig.ShowNetworkDebugOverlay;

        if (_networkModeDebugIcon == null)
            return;

        var iconPath = GetNetworkModeDebugIconPath();
        _networkModeDebugIcon.Texture = GD.Load<Texture2D>(iconPath);
        if (_networkModeDebugIcon.Texture == null) {
            GD.PushWarning($"Failed to load network debug icon at '{iconPath}'.");
        }

        _networkModeDebugIcon.TooltipText = HasLostConnection
            ? $"Network mode: {CurrentMode} - connection lost"
            : $"Network mode: {CurrentMode}";

        if (_networkModeDebugPeerLabel != null) {
            _networkModeDebugPeerLabel.Visible = HasSelectedMode;
            _networkModeDebugPeerLabel.Text = $"Peers: {GetConnectedPeerCount()}\nPlayers: {MultiplayerData.Players.Count}";
        }
    }

    private string GetNetworkModeDebugIconPath() {
        if (HasLostConnection)
            return NetworkDebugIconConnectionLostPath;

        return CurrentMode switch {
            NetworkMode.Local => NetworkDebugIconLocalPath,
            NetworkMode.Lan => NetworkDebugIconLanPath,
            NetworkMode.Online => NetworkDebugIconOnlinePath,
            NetworkMode.Client => NetworkDebugIconClientPath,
            _ => NetworkDebugIconNotSelectedPath,
        };
    }

    private void ApplyCommandLineNetworkModeOverrides() {
        var arguments = OS.GetCmdlineUserArgs();
        for (var i = 0; i < arguments.Length; i++) {
            var argument = arguments[i];
            if (argument == "--role" && TryGetNextCommandLineArgument(arguments, ref i, out var roleValue)) {
                ApplyCommandLineRole(roleValue);
                continue;
            }

            if (argument.StartsWith("--role="))
                ApplyCommandLineRole(argument[7..]);
        }
    }

    private void ApplyCommandLineRole(string roleValue) {
        if (string.Equals(roleValue, "host", StringComparison.OrdinalIgnoreCase)
            || string.Equals(roleValue, "server", StringComparison.OrdinalIgnoreCase)
            || string.Equals(roleValue, "server-local", StringComparison.OrdinalIgnoreCase)
            || string.Equals(roleValue, "lan", StringComparison.OrdinalIgnoreCase)) {
            SetLan();
            PrintMultiplayerLog(GameLogType.StateChange, "CommandLineRoleSelected", "role=Lan");
            return;
        }

        if (string.Equals(roleValue, "client", StringComparison.OrdinalIgnoreCase)) {
            SetClient();
            PrintMultiplayerLog(GameLogType.StateChange, "CommandLineRoleSelected", "role=Client");
            return;
        }

        if (string.Equals(roleValue, "local", StringComparison.OrdinalIgnoreCase)
            || string.Equals(roleValue, "local-only", StringComparison.OrdinalIgnoreCase)) {
            SetLocal();
            PrintMultiplayerLog(GameLogType.StateChange, "CommandLineRoleSelected", "role=Local");
            return;
        }

        if (string.Equals(roleValue, "online-host", StringComparison.OrdinalIgnoreCase)
            || string.Equals(roleValue, "server-online", StringComparison.OrdinalIgnoreCase)
            || string.Equals(roleValue, "online", StringComparison.OrdinalIgnoreCase)) {
            SetOnline();
            PrintMultiplayerLog(GameLogType.StateChange, "CommandLineRoleSelected", "role=Online");
        }
    }

    private static bool TryGetNextCommandLineArgument(string[] arguments, ref int index, out string value) {
        value = string.Empty;
        if (index + 1 >= arguments.Length)
            return false;

        index++;
        value = arguments[index];
        return true;
    }

    public bool BeginHostingSession() {
        PrintMultiplayerLog(GameLogType.ApiCall, "BeginHostingSession", "requested=true");
        LastConnectionError = string.Empty;
        HasLostConnection = false;
        LastConfigApplyMessage = string.Empty;
        CurrentJoinType = JoinType.None;
        CurrentServerName = string.Empty;
        ClearMultiplayerDataLocal();
        SyncCachedSetupConfig();
        MultiplayerData.SetupConfig.EnsureDefaultSelections();
        SyncCachedSetupConfig();

        if (IsLocal) {
            CloseNetworkPeer();
            StopDiscoveryServer();
            MultiplayerData.SetupConfig.ServerAddress = string.Empty;
            MultiplayerData.SetupConfig.ServerPort = 0;
            MultiplayerData.SetupConfig.OnlineEnabled = false;
            SyncCachedSetupConfig();
            ConnectionStatusText = "Status: Local game ready.";
            RegisterLocalLobbyPlayers();
            SetLocalPlayersFreeForAllTeams();
            PrintMultiplayerLog(GameLogType.Lifecycle, "LocalSessionReady", $"localPlayers={MultiplayerData.Players.Count}");
            EmitConnectionStateChanged();
            return true;
        }

        var peer = new ENetMultiplayerPeer();
        var port = FindAvailableServerPort(peer);
        if (port == -1) {
            LastConnectionError = $"Could not find an available server port from {DefaultServerPort} outward between {MinServerPort} and {MaxServerPort}.";
            ConnectionStatusText = "Status: Failed to start server.";
            PrintMultiplayerLog(GameLogType.Error, "HostStartFailed", LastConnectionError);
            EmitConnectionStateChanged();
            return false;
        }

        Multiplayer.MultiplayerPeer = peer;
        MultiplayerData.SetupConfig.ServerAddress = GetAdvertisedServerAddress();
        MultiplayerData.SetupConfig.ServerPort = port;
        MultiplayerData.SetupConfig.OnlineEnabled = CurrentMode == NetworkMode.Online;
        ConnectionStatusText = $"Status: Hosting on {MultiplayerData.SetupConfig.ServerAddress}:{port}.";
        PrintMultiplayerLog(GameLogType.Lifecycle, "HostingStarted", $"address={MultiplayerData.SetupConfig.ServerAddress} port={port}");
        StartDiscoveryServer();
        RegisterLocalLobbyPlayers();
        EmitConnectionStateChanged();
        return true;
    }

    public bool BeginClientConnection(ServerListing listing, JoinType joinType) {
        if (listing == null) {
            PrintMultiplayerLog(GameLogType.Warning, "ClientConnectionRejected", "reason=nullListing");
            return false;
        }

        PrintMultiplayerLog(GameLogType.ApiCall, "BeginClientConnection", $"target={listing.Address}:{listing.Port} joinType={joinType} name={listing.DisplayName}");

        CloseNetworkPeer();
        StopDiscoveryServer();
        ClearMultiplayerDataLocal();
        SyncCachedSetupConfig();
        LastConnectionError = string.Empty;
        HasLostConnection = false;
        LastConfigApplyMessage = string.Empty;
        CurrentServerName = listing.DisplayName;
        CurrentJoinType = joinType;
        SetClient();

        MultiplayerData.SetupConfig.ServerAddress = listing.Address;
        MultiplayerData.SetupConfig.ServerPort = listing.Port;
        MultiplayerData.SetupConfig.OnlineEnabled = listing.IsOnline;
        MultiplayerData.SetupConfig.GameModeId = listing.GameModeId;
        MultiplayerData.SetupConfig.MaxPlayers = listing.MaxPlayers;
        MultiplayerData.SetupConfig.LocalPlayerCount = GetActiveLocalPlayerCount();
        MultiplayerData.SetupConfig.EnsureDefaultSelections();
        SyncCachedSetupConfig();

        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateClient(listing.Address, listing.Port);
        if (error != Error.Ok) {
            LastConnectionError = $"Could not connect to {listing.Address}:{listing.Port}: {error}.";
            ConnectionStatusText = "Status: Failed to create client connection.";
            HasLostConnection = true;
            UpdateNetworkModeDebugIcon();
            PrintMultiplayerLog(GameLogType.Error, "ClientConnectionCreateFailed", LastConnectionError);
            EmitConnectionStateChanged();
            return false;
        }

        Multiplayer.MultiplayerPeer = peer;
        ConnectionStatusText = $"Status: Connecting to {listing.Address}:{listing.Port}.";
        PrintMultiplayerLog(GameLogType.Lifecycle, "ClientConnecting", $"target={listing.Address}:{listing.Port}");
        EmitConnectionStateChanged();
        return true;
    }

    public bool BeginDirectClientConnection(string address, int port) {
        if (string.IsNullOrWhiteSpace(address) || port <= 0 || port > 65535) {
            PrintMultiplayerLog(GameLogType.Warning, "DirectClientConnectionRejected", $"address={address} port={port}");
            return false;
        }

        return BeginClientConnection(
            new ServerListing {
                ListingId = $"direct-{address}:{port}",
                DisplayName = $"Direct Join ({address}:{port})",
                Address = address,
                Port = port,
            },
            JoinType.DirectAddress);
    }

    public void ResetSessionState() {
        PrintMultiplayerLog(GameLogType.ApiCall, "ResetSessionState");
        CloseNetworkPeer();
        StopDiscoveryServer();
        _localServerListings.Clear();
        LastConnectionError = string.Empty;
        HasLostConnection = false;
        CurrentServerName = string.Empty;
        CurrentJoinType = JoinType.None;
        SetNetworkMode(NetworkMode.NotSelected);
        ConnectionStatusText = "Status: No mode selected.";
        ClearMultiplayerDataLocal();
        SyncCachedSetupConfig();
        LastConfigApplyMessage = string.Empty;
        EmitConnectionStateChanged();
        EmitConfigApplyStateChanged();
    }

    public bool ApplyCachedSetupConfigChanges() {
        if (!HasSelectedMode) {
            PrintMultiplayerLog(GameLogType.Validation, "ApplyCachedSetupConfigRejected", "reason=noSelectedMode");
            return false;
        }

        if (!HasPendingSetupConfigChanges) {
            LastConfigApplyMessage = "No config changes to apply.";
            PrintMultiplayerLog(GameLogType.Validation, "ApplyCachedSetupConfigRejected", "reason=noPendingChanges");
            EmitConfigApplyStateChanged();
            return false;
        }

        if (!IsClient) {
            PrintMultiplayerLog(GameLogType.ApiCall, "ApplyCachedSetupConfigLocal", $"gameMode={CachedSetupConfig.GameModeId} maxPlayers={CachedSetupConfig.MaxPlayers}");
            SyncAuthoritativeSetupConfig(CachedSetupConfig);
            LastConfigApplyMessage = "Config settings changed by host.";
            EmitConfigApplyStateChanged();
            return true;
        }

        if (!IsNetworkedSession) {
            PrintMultiplayerLog(GameLogType.Validation, "ApplyCachedSetupConfigRejected", "reason=clientNoNetworkPeer");
            return false;
        }

        LastConfigApplyMessage = string.Empty;
        PrintMultiplayerLog(GameLogType.RpcSend, "RpcRequestApplyCachedSetupConfig", $"target={ServerPeerId}");
        RpcId(ServerPeerId, nameof(RpcRequestApplyCachedSetupConfig), CachedSetupConfig.SerializeForNetwork());
        EmitConfigApplyStateChanged();
        return true;
    }

    public bool RevertCachedSetupConfigChanges() {
        if (!HasSelectedMode) {
            PrintMultiplayerLog(GameLogType.Validation, "RevertCachedSetupConfigRejected", "reason=noSelectedMode");
            return false;
        }

        PrintMultiplayerLog(GameLogType.ApiCall, "RevertCachedSetupConfigChanges");
        SyncCachedSetupConfig();
        LastConfigApplyMessage = "Config changes reverted.";
        EmitLobbyStateChanged();
        EmitConfigApplyStateChanged();
        return true;
    }

    public void RegisterLocalLobbyPlayers() {
        var peerId = GetLocalPeerId();
        if (peerId == -1) {
            PrintMultiplayerLog(GameLogType.Warning, "RegisterLocalLobbyPlayersRejected", "reason=noLocalPeerId");
            EmitLobbyStateChanged();
            return;
        }

        var activeLocalPlayerCount = GetActiveLocalPlayerCount();
        MultiplayerData.Peers.Clear();
        MultiplayerData.Players.Clear();
        MultiplayerData.SetupConfig.LocalPlayerCount = activeLocalPlayerCount;
        MultiplayerData.SetupConfig.OnlineEnabled = CurrentMode == NetworkMode.Online;
        PrintMultiplayerLog(GameLogType.ApiCall, "RegisterLocalLobbyPlayers", $"peer={peerId} activeLocalPlayers={activeLocalPlayerCount}");

        var isHostPeer = IsHostPeerId(peerId);
        UpdatePeer(peerId, isHostPeer, GetDefaultPeerTeamId(peerId), activeLocalPlayerCount, 4);
        if (isHostPeer)
            PrintMultiplayerLog(GameLogType.StateChange, "HostPeerRegistered", $"peer={peerId} localPlayers={activeLocalPlayerCount} host=true");

        var globalId = 0;
        foreach (var localPlayerData in LocalLobbyData.LocalPlayers) {
            if (!localPlayerData.IsActive)
                continue;

            UpdatePlayer(
                globalId,
                peerId,
                localPlayerData.LocalId,
                localPlayerData.DisplayName,
                true);
            globalId++;
        }

        SetPeerTeam(peerId, GetDefaultPeerTeamId(peerId));
    }

    public void SetLocalPeerTeam(int teamId) {
        var peerId = GetRegisteredLocalPeerId();
        if (peerId == -1) {
            PrintMultiplayerLog(GameLogType.Warning, "SetLocalPeerTeamRejected", $"team={teamId} reason=noPeer");
            return;
        }

        if (IsRemoteClient) {
            PrintMultiplayerLog(GameLogType.RpcSend, "RpcRequestSetLocalPeerTeam", $"target={ServerPeerId} team={teamId}");
            RpcId(ServerPeerId, nameof(RpcRequestSetLocalPeerTeam), teamId);
            return;
        }

        PrintMultiplayerLog(GameLogType.Authority, "LocalAuthoritySetPeerTeam", $"peer={peerId} team={teamId}");
        SetPeerTeam(peerId, teamId);
    }

    public void SetLocalPlayersFreeForAllTeams() {
        if (!IsLocal)
            return;

        foreach (var playerData in MultiplayerData.Players)
            playerData.TeamId = global::MultiplayerData.NormalizeTeamId(playerData.LocalId);

        PrintMultiplayerLog(GameLogType.StateChange, "SetLocalPlayersFreeForAllTeams", $"players={MultiplayerData.Players.Count}");
        EmitLobbyStateChanged();
    }

    public void SetLocalPlayersTwoTeams() {
        if (!IsLocal)
            return;

        foreach (var playerData in MultiplayerData.Players)
            playerData.TeamId = playerData.LocalId % 2 == 0 ? 0 : 1;

        PrintMultiplayerLog(GameLogType.StateChange, "SetLocalPlayersTwoTeams", $"players={MultiplayerData.Players.Count}");
        EmitLobbyStateChanged();
    }

    public void AutoAssignPeerTeams(int teamCount) {
        if (IsLocal) {
            PrintMultiplayerLog(GameLogType.Validation, "AutoAssignPeerTeamsRejected", $"teams={teamCount} reason=localMode");
            return;
        }

        if (IsRemoteClient) {
            PrintMultiplayerLog(GameLogType.RpcSend, "RpcRequestAutoAssignPeerTeams", $"target={ServerPeerId} teams={teamCount}");
            RpcId(ServerPeerId, nameof(RpcRequestAutoAssignPeerTeams), teamCount);
            return;
        }

        if (!IsHostAuthority) {
            PrintMultiplayerLog(GameLogType.Authority, "AutoAssignPeerTeamsRejected", $"teams={teamCount} reason=notHostAuthority");
            return;
        }

        PrintMultiplayerLog(GameLogType.Authority, "LocalAuthorityAutoAssignPeerTeams", $"teams={teamCount}");
        ApplyAutoAssignPeerTeams(teamCount);
    }

    private void ApplyAutoAssignPeerTeams(int teamCount) {
        if (!IsHostAuthority) {
            PrintMultiplayerLog(GameLogType.Authority, "ApplyAutoAssignPeerTeamsRejected", $"teams={teamCount} reason=notHostAuthority");
            return;
        }

        var normalizedTeamCount = Math.Clamp(teamCount, 2, 4);
        var peerGroups = GetPeerTeamGroups();
        peerGroups.Sort((a, b) => {
            var playerCountCompare = b.PlayerCount.CompareTo(a.PlayerCount);
            return playerCountCompare != 0 ? playerCountCompare : a.PeerId.CompareTo(b.PeerId);
        });

        var teamPlayerCounts = new Dictionary<int, int>();
        var teamPeerCounts = new Dictionary<int, int>();
        for (var teamId = 0; teamId < normalizedTeamCount; teamId++) {
            teamPlayerCounts[teamId] = 0;
            teamPeerCounts[teamId] = 0;
        }

        foreach (var playerData in MultiplayerData.Players)
            playerData.TeamId = global::MultiplayerData.DefaultTeamId;

        _suppressLobbyStateChanged = true;
        try {
            foreach (var peerGroup in peerGroups) {
                var teamId = GetBestAutofillTeam(teamPlayerCounts, teamPeerCounts);
                var peerData = GetOrCreatePeerData(peerGroup.PeerId);
                UpdatePeer(
                    peerGroup.PeerId,
                    peerData.IsHost,
                    teamId,
                    peerData.RequestedLocalPlayerCount,
                    peerData.MaxLocalPlayers);
                teamPlayerCounts[teamId] += peerGroup.PlayerCount;
                teamPeerCounts[teamId]++;
            }
        }
        finally {
            _suppressLobbyStateChanged = false;
        }

        PrintMultiplayerLog(GameLogType.StateChange, "AutoAssignPeerTeams", $"teams={normalizedTeamCount} peerGroups={peerGroups.Count} players={MultiplayerData.Players.Count}");
        EmitLobbyStateChanged();
    }

    public void SetPeerTeam(int peerId, int teamId) {
        var normalizedTeamId = global::MultiplayerData.NormalizeTeamId(teamId);
        if (normalizedTeamId == global::MultiplayerData.DefaultTeamId)
            normalizedTeamId = GetLeastPopulatedTeamId(peerId);

        var peerData = GetOrCreatePeerData(peerId);
        PrintMultiplayerLog(GameLogType.ApiCall, "SetPeerTeam", $"peer={peerId} team={normalizedTeamId}");
        UpdatePeer(
            peerId,
            peerData.IsHost,
            normalizedTeamId,
            peerData.RequestedLocalPlayerCount,
            peerData.MaxLocalPlayers);
        SetPeerPlayersTeam(peerId, normalizedTeamId);
    }

    public void UpdateSetupConfig(
        int maxPlayers,
        int localPlayerCount,
        bool onlineEnabled,
        string serverAddress,
        int serverPort,
        string gameModeId) {
        PrintMultiplayerLog(GameLogType.ApiCall, "UpdateSetupConfig", $"maxPlayers={maxPlayers} localPlayers={localPlayerCount} online={onlineEnabled} address={serverAddress} port={serverPort} gameMode={gameModeId}");
        if (IsNetworkedSession) {
            PrintMultiplayerLog(GameLogType.RpcSend, "RpcUpdateSetupConfig", "target=all");
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

        PrintMultiplayerLog(GameLogType.Authority, "ApplySetupConfigLocal", $"maxPlayers={maxPlayers} gameMode={gameModeId}");
        RpcUpdateSetupConfig(maxPlayers, localPlayerCount, onlineEnabled, serverAddress, serverPort, gameModeId);
    }

    public void SyncAuthoritativeSetupConfig(SetupConfig setupConfig) {
        if (setupConfig == null)
            return;

        PrintMultiplayerLog(GameLogType.Sync, "SyncAuthoritativeSetupConfig", $"gameMode={setupConfig.GameModeId} maxPlayers={setupConfig.MaxPlayers}");
        MultiplayerData.SetupConfig.CopyFrom(setupConfig);
        SyncCachedSetupConfig();

        UpdateSetupConfig(
            setupConfig.MaxPlayers,
            setupConfig.LocalPlayerCount,
            setupConfig.OnlineEnabled,
            setupConfig.ServerAddress,
            setupConfig.ServerPort,
            setupConfig.GameModeId);

        var serializedSetupConfig = setupConfig.SerializeForNetwork();
        if (IsNetworkedSession) {
            PrintMultiplayerLog(GameLogType.RpcSend, "RpcReplaceFullSetupConfig", "target=all");
            Rpc(nameof(RpcReplaceFullSetupConfig), serializedSetupConfig);
        }
        else {
            PrintMultiplayerLog(GameLogType.Authority, "ApplyFullSetupConfigLocal");
            RpcReplaceFullSetupConfig(serializedSetupConfig);
        }

        EmitConfigApplyStateChanged();
    }

    public void UpdatePlayer(
        int globalId,
        int peerId,
        int localId,
        string displayName,
        bool isLocalPlayer) {
        PrintMultiplayerLog(GameLogType.ApiCall, "UpdatePlayer", $"global={globalId} peer={peerId} local={localId} localPlayer={isLocalPlayer} name={displayName}");
        if (IsNetworkedSession) {
            PrintMultiplayerLog(GameLogType.RpcSend, "RpcUpdatePlayer", $"target=all global={globalId}");
            Rpc(
                nameof(RpcUpdatePlayer),
                globalId,
                peerId,
                localId,
                displayName,
                isLocalPlayer);
            return;
        }

        PrintMultiplayerLog(GameLogType.Authority, "ApplyPlayerUpdateLocal", $"global={globalId} peer={peerId} local={localId}");
        RpcUpdatePlayer(globalId, peerId, localId, displayName, isLocalPlayer);
    }

    public void UpdatePeer(int peerId, bool isHost, int teamId, int requestedLocalPlayerCount, int maxLocalPlayers) {
        PrintMultiplayerLog(GameLogType.ApiCall, "UpdatePeer", $"peer={peerId} host={isHost} team={teamId} requestedLocalPlayers={requestedLocalPlayerCount} maxLocalPlayers={maxLocalPlayers}");
        if (IsNetworkedSession) {
            PrintMultiplayerLog(GameLogType.RpcSend, "RpcUpdatePeer", $"target=all peer={peerId}");
            Rpc(nameof(RpcUpdatePeer), peerId, isHost, teamId, requestedLocalPlayerCount, maxLocalPlayers);
            return;
        }

        PrintMultiplayerLog(GameLogType.Authority, "ApplyPeerUpdateLocal", $"peer={peerId} host={isHost} team={teamId}");
        RpcUpdatePeer(peerId, isHost, teamId, requestedLocalPlayerCount, maxLocalPlayers);
    }

    public void RemovePeer(int peerId) {
        PrintMultiplayerLog(GameLogType.ApiCall, "RemovePeer", $"peer={peerId}");
        if (IsNetworkedSession) {
            PrintMultiplayerLog(GameLogType.RpcSend, "RpcRemovePeer", $"target=all peer={peerId}");
            Rpc(nameof(RpcRemovePeer), peerId);
            return;
        }

        PrintMultiplayerLog(GameLogType.Authority, "ApplyPeerRemoveLocal", $"peer={peerId}");
        RpcRemovePeer(peerId);
    }

    public void RemovePlayer(int peerId, int localId) {
        PrintMultiplayerLog(GameLogType.ApiCall, "RemovePlayer", $"peer={peerId} local={localId}");
        if (IsNetworkedSession) {
            PrintMultiplayerLog(GameLogType.RpcSend, "RpcRemovePlayer", $"target=all peer={peerId} local={localId}");
            Rpc(nameof(RpcRemovePlayer), peerId, localId);
            return;
        }

        PrintMultiplayerLog(GameLogType.Authority, "ApplyPlayerRemoveLocal", $"peer={peerId} local={localId}");
        RpcRemovePlayer(peerId, localId);
    }

    public void ClearPlayers() {
        PrintMultiplayerLog(GameLogType.ApiCall, "ClearPlayers");
        if (IsNetworkedSession) {
            PrintMultiplayerLog(GameLogType.RpcSend, "RpcClearPlayers", "target=all");
            Rpc(nameof(RpcClearPlayers));
            return;
        }

        PrintMultiplayerLog(GameLogType.Authority, "ApplyPlayersClearLocal");
        RpcClearPlayers();
    }

    public bool DamageAuthoritativeWallTile(Vector2I position, float damageAmount = 1.0f, DamageType damageType = DamageType.Crush) {
        if (!CanApplyAuthoritativeArenaMapChange()) {
            PrintMultiplayerLog(GameLogType.Authority, "DamageWallTileRejected", $"tile={position} reason=notAuthority");
            return false;
        }

        if (!ArenaMapData.IsWallTile(position) || damageAmount <= 0.0f) {
            PrintMultiplayerLog(GameLogType.Validation, "DamageWallTileRejected", $"tile={position} damage={damageAmount} wall={ArenaMapData.IsWallTile(position)}");
            return false;
        }

        if (IsNetworkedSession) {
            PrintMultiplayerLog(GameLogType.RpcSend, "RpcDamageArenaWallTile", $"tile={position} type={damageType} damage={damageAmount}");
            Rpc(nameof(RpcDamageArenaWallTile), position.X, position.Y, (int)damageType, damageAmount);
            return true;
        }

        PrintMultiplayerLog(GameLogType.Authority, "ApplyWallDamageLocal", $"tile={position} type={damageType} damage={damageAmount}");
        RpcDamageArenaWallTile(position.X, position.Y, (int)damageType, damageAmount);
        return true;
    }

    public bool DamageAuthoritativeWallFromWorldPosition(Vector2 worldPosition, Vector2I tileSize, float damageAmount = 1.0f, DamageType damageType = DamageType.Crush) {
        return DamageAuthoritativeWallTile(ArenaMapData.WorldToTile(worldPosition, tileSize), damageAmount, damageType);
    }

    public bool DamageAuthoritativeWallsInRadius(Vector2I centerTile, int radius, float damageAmount = 1.0f, DamageType damageType = DamageType.Explosive) {
        if (!CanApplyAuthoritativeArenaMapChange()) {
            PrintMultiplayerLog(GameLogType.Authority, "DamageWallsInRadiusRejected", $"center={centerTile} reason=notAuthority");
            return false;
        }

        if (damageAmount <= 0.0f || radius < 0) {
            PrintMultiplayerLog(GameLogType.Validation, "DamageWallsInRadiusRejected", $"center={centerTile} radius={radius} damage={damageAmount}");
            return false;
        }

        if (IsNetworkedSession) {
            PrintMultiplayerLog(GameLogType.RpcSend, "RpcDamageArenaWallsInRadius", $"center={centerTile} radius={radius} type={damageType} damage={damageAmount}");
            Rpc(nameof(RpcDamageArenaWallsInRadius), centerTile.X, centerTile.Y, radius, (int)damageType, damageAmount);
            return true;
        }

        PrintMultiplayerLog(GameLogType.Authority, "ApplyRadiusWallDamageLocal", $"center={centerTile} radius={radius} type={damageType} damage={damageAmount}");
        RpcDamageArenaWallsInRadius(centerTile.X, centerTile.Y, radius, (int)damageType, damageAmount);
        return true;
    }

    public bool DamageAuthoritativeWallsInWorldRadius(Vector2 worldCenter, Vector2I tileSize, float worldRadius, float damageAmount = 1.0f, DamageType damageType = DamageType.Explosive) {
        var centerTile = ArenaMapData.WorldToTile(worldCenter, tileSize);
        var tileRadius = Mathf.CeilToInt(worldRadius / Mathf.Max(1, tileSize.X));
        return DamageAuthoritativeWallsInRadius(centerTile, tileRadius, damageAmount, damageType);
    }

    public void ClearPeers() {
        PrintMultiplayerLog(GameLogType.ApiCall, "ClearPeers");
        if (IsNetworkedSession) {
            PrintMultiplayerLog(GameLogType.RpcSend, "RpcClearPeers", "target=all");
            Rpc(nameof(RpcClearPeers));
            return;
        }

        PrintMultiplayerLog(GameLogType.Authority, "ApplyPeersClearLocal");
        RpcClearPeers();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRequestJoinServer(Godot.Collections.Array<int> localIds, Godot.Collections.Array<string> displayNames) {
        if (!IsAuthoritativeServer()) {
            PrintMultiplayerLog(GameLogType.Authority, "RpcRequestJoinServerRejected", "reason=notAuthoritativeServer");
            return;
        }

        var remotePeerId = Multiplayer.GetRemoteSenderId();
        PrintMultiplayerLog(GameLogType.RpcReceive, "RpcRequestJoinServer", $"from={remotePeerId} localIds={localIds.Count} names={displayNames.Count}");
        RegisterRemotePeerPlayers(remotePeerId, localIds, displayNames);
        SendFullLobbyStateToPeer(remotePeerId);
        ConnectionStatusText = $"Status: Peer {remotePeerId} joined the lobby.";
        EmitConnectionStateChanged();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRequestSetLocalPeerTeam(int teamId) {
        if (!IsAuthoritativeServer()) {
            PrintMultiplayerLog(GameLogType.Authority, "RpcRequestSetLocalPeerTeamRejected", "reason=notAuthoritativeServer");
            return;
        }

        var remotePeerId = Multiplayer.GetRemoteSenderId();
        PrintMultiplayerLog(GameLogType.RpcReceive, "RpcRequestSetLocalPeerTeam", $"from={remotePeerId} team={teamId}");
        SetPeerTeam(remotePeerId, teamId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRequestAutoAssignPeerTeams(int teamCount) {
        if (!IsAuthoritativeServer()) {
            PrintMultiplayerLog(GameLogType.Authority, "RpcRequestAutoAssignPeerTeamsRejected", "reason=notAuthoritativeServer");
            return;
        }

        var remotePeerId = Multiplayer.GetRemoteSenderId();
        PrintMultiplayerLog(GameLogType.RpcReceive, "RpcRequestAutoAssignPeerTeams", $"from={remotePeerId} teams={teamCount}");
        ApplyAutoAssignPeerTeams(teamCount);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRequestApplyCachedSetupConfig(string serializedSetupConfig) {
        if (!IsAuthoritativeServer()) {
            PrintMultiplayerLog(GameLogType.Authority, "RpcRequestApplyCachedSetupConfigRejected", "reason=notAuthoritativeServer");
            return;
        }

        var remotePeerId = Multiplayer.GetRemoteSenderId();
        PrintMultiplayerLog(GameLogType.RpcReceive, "RpcRequestApplyCachedSetupConfig", $"from={remotePeerId}");
        if (!SetupConfig.TryDeserializeForNetwork(serializedSetupConfig, out var requestedSetupConfig)) {
            PrintMultiplayerLog(GameLogType.Validation, "RpcRequestApplyCachedSetupConfigRejected", $"from={remotePeerId} reason=deserializeFailed");
            return;
        }

        requestedSetupConfig.ServerAddress = MultiplayerData.SetupConfig.ServerAddress;
        requestedSetupConfig.ServerPort = MultiplayerData.SetupConfig.ServerPort;
        requestedSetupConfig.OnlineEnabled = MultiplayerData.SetupConfig.OnlineEnabled;
        requestedSetupConfig.LocalPlayerCount = MultiplayerData.SetupConfig.LocalPlayerCount;

        var appliedMessage = $"Config settings changed by peer {remotePeerId}.";
        SyncAuthoritativeSetupConfig(requestedSetupConfig);
        LastConfigApplyMessage = appliedMessage;
        Rpc(nameof(RpcNotifyConfigApplied), appliedMessage);
        EmitConfigApplyStateChanged();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcUpdateSetupConfig(
        int maxPlayers,
        int localPlayerCount,
        bool onlineEnabled,
        string serverAddress,
        int serverPort,
        string gameModeId) {
        PrintMultiplayerLog(GameLogType.RpcReceive, "RpcUpdateSetupConfig", $"maxPlayers={maxPlayers} localPlayers={localPlayerCount} online={onlineEnabled} address={serverAddress} port={serverPort} gameMode={gameModeId}");
        MultiplayerData.SetupConfig.MaxPlayers = maxPlayers;
        MultiplayerData.SetupConfig.LocalPlayerCount = localPlayerCount;
        MultiplayerData.SetupConfig.OnlineEnabled = onlineEnabled;
        MultiplayerData.SetupConfig.ServerAddress = serverAddress;
        MultiplayerData.SetupConfig.ServerPort = serverPort;
        MultiplayerData.SetupConfig.GameModeId = gameModeId;
        SyncCachedSetupConfig();
        EmitLobbyStateChanged();
        EmitConfigApplyStateChanged();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcReplaceFullSetupConfig(string serializedSetupConfig) {
        PrintMultiplayerLog(GameLogType.RpcReceive, "RpcReplaceFullSetupConfig");
        if (!SetupConfig.TryDeserializeForNetwork(serializedSetupConfig, out var deserializedSetupConfig)) {
            PrintMultiplayerLog(GameLogType.Validation, "RpcReplaceFullSetupConfigRejected", "reason=deserializeFailed");
            return;
        }

        MultiplayerData.SetupConfig.CopyFrom(deserializedSetupConfig);
        SyncCachedSetupConfig();
        EmitLobbyStateChanged();
        EmitConfigApplyStateChanged();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcNotifyConfigApplied(string message) {
        PrintMultiplayerLog(GameLogType.RpcReceive, "RpcNotifyConfigApplied", message);
        LastConfigApplyMessage = message;
        SyncCachedSetupConfig();
        EmitConfigApplyStateChanged();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcUpdatePlayer(
        int globalId,
        int peerId,
        int localId,
        string displayName,
        bool isLocalPlayer) {
        PrintMultiplayerLog(GameLogType.RpcReceive, "RpcUpdatePlayer", $"global={globalId} peer={peerId} local={localId} localPlayer={isLocalPlayer} name={displayName}");
        GetOrCreatePeerData(peerId);

        var playerData = GetOrCreatePlayerData(peerId, localId);
        playerData.GlobalId = globalId;
        playerData.DisplayName = displayName;
        playerData.IsLocalPlayer = IsNetworkedSession ? peerId == Multiplayer.GetUniqueId() : isLocalPlayer;
        EmitLobbyStateChanged();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcUpdatePeer(int peerId, bool isHost, int teamId, int requestedLocalPlayerCount, int maxLocalPlayers) {
        PrintMultiplayerLog(GameLogType.RpcReceive, "RpcUpdatePeer", $"peer={peerId} host={isHost} team={teamId} requestedLocalPlayers={requestedLocalPlayerCount} maxLocalPlayers={maxLocalPlayers}");
        var peerData = GetOrCreatePeerData(peerId);
        peerData.IsHost = isHost;
        peerData.TeamId = global::MultiplayerData.NormalizeTeamId(teamId);
        peerData.RequestedLocalPlayerCount = requestedLocalPlayerCount;
        peerData.MaxLocalPlayers = maxLocalPlayers;
        SetPeerPlayersTeam(peerId, peerData.TeamId);
        EmitLobbyStateChanged();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRemovePeer(int peerId) {
        PrintMultiplayerLog(GameLogType.RpcReceive, "RpcRemovePeer", $"peer={peerId}");
        for (var i = MultiplayerData.Peers.Count - 1; i >= 0; i--) {
            if (MultiplayerData.Peers[i].PeerId == peerId) {
                MultiplayerData.Peers.RemoveAt(i);
                RemovePlayersOwnedByPeer(peerId);
                EmitLobbyStateChanged();
                return;
            }
        }

        EmitLobbyStateChanged();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRemovePlayer(int peerId, int localId) {
        PrintMultiplayerLog(GameLogType.RpcReceive, "RpcRemovePlayer", $"peer={peerId} local={localId}");
        for (var i = MultiplayerData.Players.Count - 1; i >= 0; i--) {
            if (MultiplayerData.Players[i].PeerId == peerId && MultiplayerData.Players[i].LocalId == localId) {
                MultiplayerData.Players.RemoveAt(i);
                EmitLobbyStateChanged();
                return;
            }
        }

        EmitLobbyStateChanged();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcClearPlayers() {
        PrintMultiplayerLog(GameLogType.RpcReceive, "RpcClearPlayers");
        MultiplayerData.Players.Clear();
        EmitLobbyStateChanged();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcClearPeers() {
        PrintMultiplayerLog(GameLogType.RpcReceive, "RpcClearPeers");
        MultiplayerData.Peers.Clear();
        MultiplayerData.Players.Clear();
        EmitLobbyStateChanged();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcDamageArenaWallTile(int x, int y, int damageTypeValue, float damageAmount) {
        PrintMultiplayerLog(GameLogType.RpcReceive, "RpcDamageArenaWallTile", $"tile=({x},{y}) type={ToDamageType(damageTypeValue)} damage={damageAmount}");
        ArenaMapData.DamageWallTile(new Vector2I(x, y), ToDamageType(damageTypeValue), damageAmount);
        EmitArenaMapChanged();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcDamageArenaWallsInRadius(int centerX, int centerY, int radius, int damageTypeValue, float damageAmount) {
        PrintMultiplayerLog(GameLogType.RpcReceive, "RpcDamageArenaWallsInRadius", $"center=({centerX},{centerY}) radius={radius} type={ToDamageType(damageTypeValue)} damage={damageAmount}");
        ArenaMapData.DamageWallsInRadius(new Vector2I(centerX, centerY), radius, ToDamageType(damageTypeValue), damageAmount);
        EmitArenaMapChanged();
    }

    private static DamageType ToDamageType(int damageTypeValue) {
        return Enum.IsDefined(typeof(DamageType), damageTypeValue)
            ? (DamageType)damageTypeValue
            : DamageType.Crush;
    }

    private void OnPeerConnected(long peerId) {
        PrintMultiplayerLog(GameLogType.Lifecycle, "PeerConnected", $"peer={peerId} connectedPeers={Multiplayer.GetPeers().Length}");

        if (!IsAuthoritativeServer())
            return;

        ConnectionStatusText = $"Status: Peer {peerId} connected. Waiting for lobby data.";
        EmitConnectionStateChanged();
    }

    private void OnPeerDisconnected(long peerId) {
        PrintMultiplayerLog(GameLogType.Lifecycle, "PeerDisconnected", $"peer={peerId} connectedPeers={Multiplayer.GetPeers().Length}");

        if (!IsAuthoritativeServer())
            return;

        RemovePeer((int)peerId);
        ConnectionStatusText = $"Status: Peer {peerId} disconnected.";
        EmitConnectionStateChanged();
    }

    private void OnConnectedToServer() {
        ConnectionStatusText = "Status: Connected. Syncing lobby data from server.";
        HasLostConnection = false;
        UpdateNetworkModeDebugIcon();
        PrintMultiplayerLog(GameLogType.Lifecycle, "ConnectedToServer", $"localPeer={Multiplayer.GetUniqueId()}");
        EmitConnectionStateChanged();
        PrintMultiplayerLog(GameLogType.RpcSend, "RpcRequestJoinServer", $"target={ServerPeerId} localPlayers={GetActiveLocalPlayerCount()}");
        RpcId(ServerPeerId, nameof(RpcRequestJoinServer), BuildActiveLocalIdArray(), BuildActiveLocalNameArray());
    }

    private void OnConnectionFailed() {
        LastConnectionError = "Connection failed.";
        ConnectionStatusText = "Status: Connection failed.";
        HasLostConnection = true;
        UpdateNetworkModeDebugIcon();
        PrintMultiplayerLog(GameLogType.Error, "ConnectionFailed");
        CloseNetworkPeer();
        EmitConnectionStateChanged();
    }

    private void OnServerDisconnected() {
        LastConnectionError = "Server disconnected.";
        ConnectionStatusText = "Status: Disconnected from server.";
        HasLostConnection = true;
        UpdateNetworkModeDebugIcon();
        PrintMultiplayerLog(GameLogType.Warning, "ServerDisconnected");
        CloseNetworkPeer();
        ClearMultiplayerDataLocal();
        EmitConnectionStateChanged();
    }

    private void RegisterRemotePeerPlayers(int peerId, Godot.Collections.Array<int> localIds, Godot.Collections.Array<string> displayNames) {
        RemovePeer(peerId);

        var requestedLocalPlayerCount = Math.Min(localIds.Count, displayNames.Count);

        var globalId = MultiplayerData.Players.Count;
        for (var i = 0; i < requestedLocalPlayerCount; i++) {
            UpdatePlayer(
                globalId,
                peerId,
                localIds[i],
                displayNames[i],
                false);
            globalId++;
        }

        UpdatePeer(peerId, false, GetDefaultPeerTeamId(peerId), requestedLocalPlayerCount, 4);
        SetPeerTeam(peerId, GetDefaultPeerTeamId(peerId));
    }

    private void SendFullLobbyStateToPeer(int targetPeerId) {
        RpcId(targetPeerId, nameof(RpcClearPeers));

        var setupConfig = MultiplayerData.SetupConfig;
        RpcId(
            targetPeerId,
            nameof(RpcUpdateSetupConfig),
            setupConfig.MaxPlayers,
            setupConfig.LocalPlayerCount,
            setupConfig.OnlineEnabled,
            setupConfig.ServerAddress,
            setupConfig.ServerPort,
            setupConfig.GameModeId);
        RpcId(targetPeerId, nameof(RpcReplaceFullSetupConfig), setupConfig.SerializeForNetwork());

        foreach (var peerData in MultiplayerData.Peers) {
            RpcId(
                targetPeerId,
                nameof(RpcUpdatePeer),
                peerData.PeerId,
                peerData.IsHost,
                peerData.TeamId,
                peerData.RequestedLocalPlayerCount,
                peerData.MaxLocalPlayers);
        }

        foreach (var playerData in MultiplayerData.Players) {
            RpcId(
                targetPeerId,
                nameof(RpcUpdatePlayer),
                playerData.GlobalId,
                playerData.PeerId,
                playerData.LocalId,
                playerData.DisplayName,
                false);
        }

    }

    private void StartDiscoveryServer() {
        StopDiscoveryServer();

        _discoveryServer = new PacketPeerUdp();
        var bindError = _discoveryServer.Bind(DiscoveryPort, "*");
        if (bindError != Error.Ok) {
            _discoveryServer.Dispose();
            _discoveryServer = null;
        }
    }

    private void StopDiscoveryServer() {
        if (_discoveryServer == null)
            return;

        _discoveryServer.Close();
        _discoveryServer.Dispose();
        _discoveryServer = null;
    }

    private void PollDiscoveryRequests() {
        if (_discoveryServer == null || !IsServer || !HasNetworkPeer())
            return;

        while (_discoveryServer.GetAvailablePacketCount() > 0) {
            var packet = _discoveryServer.GetPacket();
            var message = Encoding.UTF8.GetString(packet);
            if (message != DiscoveryRequestMessage)
                continue;

            var senderAddress = _discoveryServer.GetPacketIP();
            var senderPort = _discoveryServer.GetPacketPort();
            if (string.IsNullOrWhiteSpace(senderAddress) || senderPort <= 0)
                continue;

            _discoveryServer.SetDestAddress(senderAddress, senderPort);
            _discoveryServer.PutPacket(Encoding.UTF8.GetBytes(BuildDiscoveryResponse()));
        }
    }

    private void LogRootSceneChange() {
        var currentScene = GetTree()?.CurrentScene;
        if (currentScene == null)
            return;

        var scenePath = currentScene.SceneFilePath;
        if (string.IsNullOrWhiteSpace(scenePath))
            scenePath = currentScene.Name;

        if (scenePath == _lastLoggedRootScenePath)
            return;

        var previousScenePath = string.IsNullOrWhiteSpace(_lastLoggedRootScenePath) ? "none" : _lastLoggedRootScenePath;
        _lastLoggedRootScenePath = scenePath;
        GameLog.Print(GameLogScope.UI, GameLogType.StateChange, "RootSceneChanged", $"from={previousScenePath} to={scenePath}");
    }

    private void PollDiscoveryResponses(PacketPeerUdp discoveryClient) {
        while (discoveryClient.GetAvailablePacketCount() > 0) {
            var packet = discoveryClient.GetPacket();
            var message = Encoding.UTF8.GetString(packet);
            if (!TryParseDiscoveryResponse(message, out var listing))
                continue;

            AddOrReplaceLocalListing(listing);
        }
    }

    private void AddOrReplaceLocalListing(ServerListing listing) {
        for (var i = 0; i < _localServerListings.Count; i++) {
            if (_localServerListings[i].ListingId == listing.ListingId) {
                _localServerListings[i] = listing;
                EmitLobbyStateChanged();
                return;
            }
        }

        _localServerListings.Add(listing);
        EmitLobbyStateChanged();
    }

    private string BuildDiscoveryResponse() {
        return string.Join(
            "\t",
            DiscoveryResponsePrefix,
            System.Environment.MachineName,
            GetAdvertisedServerAddress(),
            GetConfiguredServerPort().ToString(),
            MultiplayerData.Players.Count.ToString(),
            MultiplayerData.SetupConfig.MaxPlayers.ToString(),
            CurrentMode == NetworkMode.Online ? "1" : "0",
            MultiplayerData.SetupConfig.GameModeId);
    }

    private static bool TryParseDiscoveryResponse(string message, out ServerListing listing) {
        listing = null;
        var parts = message.Split('\t');
        if (parts.Length < 8 || parts[0] != DiscoveryResponsePrefix)
            return false;

        if (!int.TryParse(parts[3], out var port)
            || !int.TryParse(parts[4], out var playerCount)
            || !int.TryParse(parts[5], out var maxPlayers)) {
            return false;
        }

        listing = new ServerListing {
            ListingId = $"{parts[2]}:{port}",
            DisplayName = parts[1],
            Address = parts[2],
            Port = port,
            PlayerCount = playerCount,
            MaxPlayers = maxPlayers,
            IsOnline = parts[6] == "1",
            GameModeId = parts[7],
        };
        return true;
    }

    private static ServerListing CloneServerListing(ServerListing listing) {
        return new ServerListing {
            ListingId = listing.ListingId,
            DisplayName = listing.DisplayName,
            Address = listing.Address,
            Port = listing.Port,
            IsOnline = listing.IsOnline,
            PlayerCount = listing.PlayerCount,
            MaxPlayers = listing.MaxPlayers,
            GameModeId = listing.GameModeId,
        };
    }

    private Godot.Collections.Array<int> BuildActiveLocalIdArray() {
        var localIds = new Godot.Collections.Array<int>();
        foreach (var localPlayerData in LocalLobbyData.LocalPlayers) {
            if (localPlayerData.IsActive)
                localIds.Add(localPlayerData.LocalId);
        }

        return localIds;
    }

    private Godot.Collections.Array<string> BuildActiveLocalNameArray() {
        var displayNames = new Godot.Collections.Array<string>();
        foreach (var localPlayerData in LocalLobbyData.LocalPlayers) {
            if (localPlayerData.IsActive)
                displayNames.Add(localPlayerData.DisplayName);
        }

        return displayNames;
    }

    private void ClearMultiplayerDataLocal() {
        MultiplayerData.SetupConfig = new SetupConfig();
        MultiplayerData.Peers.Clear();
        MultiplayerData.Players.Clear();
        ArenaMapData = new ArenaMapData();
        SyncCachedSetupConfig();
        EmitLobbyStateChanged();
        EmitArenaMapChanged();
    }

    private void SyncCachedSetupConfig() {
        CachedSetupConfig = MultiplayerData.SetupConfig?.Clone() ?? new SetupConfig();
    }

    private static int GetDefaultPeerTeamId(int peerId) {
        return global::MultiplayerData.DefaultTeamId;
    }

    private List<PeerTeamGroup> GetPeerTeamGroups() {
        var groupsByPeerId = new Dictionary<int, PeerTeamGroup>();
        foreach (var peerData in MultiplayerData.Peers) {
            if (!groupsByPeerId.ContainsKey(peerData.PeerId)) {
                groupsByPeerId[peerData.PeerId] = new PeerTeamGroup {
                    PeerId = peerData.PeerId,
                };
            }
        }

        foreach (var playerData in MultiplayerData.Players) {
            if (!groupsByPeerId.TryGetValue(playerData.PeerId, out var group)) {
                group = new PeerTeamGroup {
                    PeerId = playerData.PeerId,
                };
                groupsByPeerId[playerData.PeerId] = group;
            }

            group.PlayerCount++;
        }

        var groups = new List<PeerTeamGroup>();
        foreach (var group in groupsByPeerId.Values) {
            if (group.PlayerCount > 0)
                groups.Add(group);
        }

        return groups;
    }

    private static int GetBestAutofillTeam(Dictionary<int, int> teamPlayerCounts, Dictionary<int, int> teamPeerCounts) {
        var bestTeamId = global::MultiplayerData.DefaultTeamId;
        var bestPlayerCount = int.MaxValue;
        var bestPeerCount = int.MaxValue;
        foreach (var teamPlayerCount in teamPlayerCounts) {
            var teamId = teamPlayerCount.Key;
            var playerCount = teamPlayerCount.Value;
            var peerCount = teamPeerCounts[teamId];
            if (playerCount < bestPlayerCount
                || playerCount == bestPlayerCount && peerCount < bestPeerCount
                || playerCount == bestPlayerCount && peerCount == bestPeerCount && teamId < bestTeamId) {
                bestTeamId = teamId;
                bestPlayerCount = playerCount;
                bestPeerCount = peerCount;
            }
        }

        return bestTeamId;
    }

    private void ApplyLocalTeams() {
        var assignedPeerIds = new HashSet<int>();
        foreach (var playerData in MultiplayerData.Players) {
            if (!assignedPeerIds.Add(playerData.PeerId))
                continue;

            var peerData = GetOrCreatePeerData(playerData.PeerId);
            peerData.TeamId = GetLeastPopulatedTeamId(playerData.PeerId);
        }
    }

    private int GetLeastPopulatedTeamId(int excludedPeerId = -1) {
        var teamCounts = new Dictionary<int, int>();
        for (var teamId = 0; teamId < 4; teamId++)
            teamCounts[teamId] = 0;

        foreach (var playerData in MultiplayerData.Players) {
            if (playerData.PeerId == excludedPeerId)
                continue;

            var playerTeamId = MultiplayerData.GetTeam(playerData);
            if (!teamCounts.ContainsKey(playerTeamId))
                continue;

            teamCounts[playerTeamId]++;
        }

        var bestTeamId = global::MultiplayerData.DefaultTeamId;
        var bestCount = int.MaxValue;
        for (var teamId = 0; teamId < 4; teamId++) {
            var count = teamCounts[teamId];
            if (count < bestCount) {
                bestTeamId = teamId;
                bestCount = count;
            }
        }

        return bestTeamId;
    }

    private void SetPeerPlayersTeam(int peerId, int teamId) {
        var normalizedTeamId = global::MultiplayerData.NormalizeTeamId(teamId);
        foreach (var playerData in MultiplayerData.Players) {
            if (playerData.PeerId == peerId)
                playerData.TeamId = normalizedTeamId;
        }
    }

    private void CloseNetworkPeer() {
        if (!HasNetworkPeer())
            return;

        switch (Multiplayer.MultiplayerPeer) {
            case ENetMultiplayerPeer enetPeer:
                enetPeer.Close();
                break;
            case WebSocketMultiplayerPeer webSocketPeer:
                webSocketPeer.Close();
                break;
        }

        Multiplayer.MultiplayerPeer = null;
    }

    private bool IsAuthoritativeServer() {
        return IsServer && IsNetworkedSession && IsHostPeerId(Multiplayer.GetUniqueId());
    }

    private bool CanApplyAuthoritativeArenaMapChange() {
        return !IsNetworkedSession || IsAuthoritativeServer();
    }

    private static bool IsHostPeerId(int peerId) {
        return peerId == ServerPeerId;
    }

    private int GetRegisteredLocalPeerId() {
        foreach (var playerData in MultiplayerData.Players) {
            if (playerData.IsLocalPlayer)
                return playerData.PeerId;
        }

        return GetLocalPeerId();
    }

    private int GetLocalPeerId() {
        if (CurrentMode is NetworkMode.Local or NetworkMode.Lan or NetworkMode.Online)
            return ServerPeerId;

        if (HasNetworkPeer())
            return Multiplayer.GetUniqueId();

        return -1;
    }

    private int GetActiveLocalPlayerCount() {
        var count = 0;
        foreach (var localPlayerData in LocalLobbyData.LocalPlayers) {
            if (localPlayerData.IsActive)
                count++;
        }

        return count;
    }

    private int GetConfiguredServerPort() {
        return MultiplayerData.SetupConfig.ServerPort <= 0 ? DefaultServerPort : MultiplayerData.SetupConfig.ServerPort;
    }

    private static int FindAvailableServerPort(ENetMultiplayerPeer peer) {
        for (var offset = 0; offset <= MaxServerPort - MinServerPort; offset++) {
            var lowerPort = DefaultServerPort - offset;
            if (lowerPort >= MinServerPort && TryCreateServer(peer, lowerPort))
                return lowerPort;

            var upperPort = DefaultServerPort + offset;
            if (offset > 0 && upperPort <= MaxServerPort && TryCreateServer(peer, upperPort))
                return upperPort;
        }

        return -1;
    }

    private static bool TryCreateServer(ENetMultiplayerPeer peer, int port) {
        return peer.CreateServer(port, MaxClients) == Error.Ok;
    }

    private string GetAdvertisedServerAddress() {
        return string.IsNullOrWhiteSpace(MultiplayerData.SetupConfig.ServerAddress)
            ? "127.0.0.1"
            : MultiplayerData.SetupConfig.ServerAddress;
    }

    private static bool IsHeadlessRun() {
        if (DisplayServer.GetName() == "headless")
            return true;

        foreach (var argument in OS.GetCmdlineArgs()) {
            if (argument == "--headless")
                return true;
        }

        return false;
    }

    private PeerData GetOrCreatePeerData(int peerId) {
        foreach (var peerData in MultiplayerData.Peers) {
            if (peerData.PeerId == peerId)
                return peerData;
        }

        var newPeerData = new PeerData {
            PeerId = peerId,
        };

        MultiplayerData.Peers.Add(newPeerData);
        return newPeerData;
    }

    private PlayerData GetOrCreatePlayerData(int peerId, int localId) {
        foreach (var playerData in MultiplayerData.Players) {
            if (playerData.PeerId == peerId && playerData.LocalId == localId)
                return playerData;
        }

        var newPlayerData = new PlayerData {
            PeerId = peerId,
            LocalId = localId,
        };

        MultiplayerData.Players.Add(newPlayerData);
        return newPlayerData;
    }

    private void RemovePlayersOwnedByPeer(int peerId) {
        for (var i = MultiplayerData.Players.Count - 1; i >= 0; i--) {
            if (MultiplayerData.Players[i].PeerId == peerId)
                MultiplayerData.Players.RemoveAt(i);
        }
    }

    private bool HasNetworkPeer() {
        return Multiplayer.MultiplayerPeer != null
            && Multiplayer.MultiplayerPeer is not OfflineMultiplayerPeer;
    }

    private int GetConnectedPeerCount() {
        return HasNetworkPeer() ? Multiplayer.GetPeers().Length : 0;
    }

    private void PrintMultiplayerLog(string message) {
        PrintMultiplayerLog(GameLogType.StateChange, "Multiplayer", message);
    }

    private void PrintMultiplayerLog(GameLogType type, string eventName, string details = "") {
        GameLog.Print(GameLogScope.Networking, type, eventName, details);
    }

    private void EmitLobbyStateChanged() {
        if (_suppressLobbyStateChanged) {
            return;
        }

        UpdateNetworkModeDebugIcon();
        EmitSignal(SignalName.LobbyStateChanged);
    }

    private void EmitConnectionStateChanged() {
        UpdateNetworkModeDebugIcon();
        EmitSignal(SignalName.ConnectionStateChanged);
    }

    private void EmitConfigApplyStateChanged() {
        EmitSignal(SignalName.ConfigApplyStateChanged);
    }

    private void EmitArenaMapChanged() {
        EmitSignal(SignalName.ArenaMapChanged);
    }

}
