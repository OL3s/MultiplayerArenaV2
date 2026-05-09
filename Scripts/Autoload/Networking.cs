using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Godot;

public partial class Networking : Node {
    private const int ServerPeerId = 1;
    private const int DefaultServerPort = 7700;
    private const int MaxServerPort = 8700;
    private const int DiscoveryPort = 7778;
    private const int MaxClients = 8;
    private const string DiscoveryRequestMessage = "MULTIPLAYERARENA_DISCOVER";
    private const string DiscoveryResponsePrefix = "MULTIPLAYERARENA_SERVER";
    private const string NetworkDebugIconNotSelectedPath = "res://Assets/Network/NetworkModes/network_not_selected.svg";
    private const string NetworkDebugIconLocalPath = "res://Assets/Network/NetworkModes/network_local.svg";
    private const string NetworkDebugIconLanPath = "res://Assets/Network/NetworkModes/network_lan.svg";
    private const string NetworkDebugIconOnlinePath = "res://Assets/Network/NetworkModes/network_online.svg";
    private const string NetworkDebugIconClientPath = "res://Assets/Network/NetworkModes/network_client.svg";
    private const string NetworkDebugIconConnectionLostPath = "res://Assets/Network/NetworkModes/network_connection_lost.svg";

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

    public override void _Ready() {
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
        PollDiscoveryRequests();
    }

    public bool IsLocal => CurrentMode == NetworkMode.Local;

    public bool IsServer => CurrentMode is NetworkMode.Lan or NetworkMode.Online;

    public bool IsClient => CurrentMode == NetworkMode.Client;

    public bool IsOnline => CurrentMode == NetworkMode.Online;

    public bool HasSelectedMode => CurrentMode != NetworkMode.NotSelected;

    public bool HasActiveNetworkPeer => HasNetworkPeer();

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
                HasLostConnection = false;
                UpdateNetworkModeDebugIcon();
                EmitConnectionStateChanged();
            }

            return;
        }

        CurrentMode = networkMode;
        HasLostConnection = false;
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
            _networkModeDebugPeerLabel.Visible = CurrentMode is NetworkMode.Lan or NetworkMode.Online;
            _networkModeDebugPeerLabel.Text = $"Peers: {GetConnectedPeerCount()}";
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
            PrintMultiplayerLog("Command line role selected Lan.");
            return;
        }

        if (string.Equals(roleValue, "client", StringComparison.OrdinalIgnoreCase)) {
            SetClient();
            PrintMultiplayerLog("Command line role selected Client.");
            return;
        }

        if (string.Equals(roleValue, "local", StringComparison.OrdinalIgnoreCase)
            || string.Equals(roleValue, "local-only", StringComparison.OrdinalIgnoreCase)) {
            SetLocal();
            PrintMultiplayerLog("Command line role selected Local.");
            return;
        }

        if (string.Equals(roleValue, "online-host", StringComparison.OrdinalIgnoreCase)
            || string.Equals(roleValue, "server-online", StringComparison.OrdinalIgnoreCase)
            || string.Equals(roleValue, "online", StringComparison.OrdinalIgnoreCase)) {
            SetOnline();
            PrintMultiplayerLog("Command line role selected Online.");
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
        PrintMultiplayerLog("Begin host session requested.");
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
            ConnectionStatusText = "Status: Local game ready.";
            RegisterLocalLobbyPlayers();
            EmitConnectionStateChanged();
            return true;
        }

        var peer = new ENetMultiplayerPeer();
        var port = FindAvailableServerPort(peer);
        if (port == -1) {
            LastConnectionError = $"Could not find an available server port between {DefaultServerPort} and {MaxServerPort}.";
            ConnectionStatusText = "Status: Failed to start server.";
            EmitConnectionStateChanged();
            return false;
        }

        Multiplayer.MultiplayerPeer = peer;
        MultiplayerData.SetupConfig.ServerAddress = GetAdvertisedServerAddress();
        MultiplayerData.SetupConfig.ServerPort = port;
        MultiplayerData.SetupConfig.OnlineEnabled = CurrentMode == NetworkMode.Online;
        ConnectionStatusText = $"Status: Hosting on {MultiplayerData.SetupConfig.ServerAddress}:{port}.";
        PrintMultiplayerLog($"Hosting on {MultiplayerData.SetupConfig.ServerAddress}:{port}.");
        StartDiscoveryServer();
        RegisterLocalLobbyPlayers();
        EmitConnectionStateChanged();
        return true;
    }

    public bool BeginClientConnection(ServerListing listing, JoinType joinType) {
        if (listing == null)
            return false;

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
            PrintMultiplayerLog(LastConnectionError);
            EmitConnectionStateChanged();
            return false;
        }

        Multiplayer.MultiplayerPeer = peer;
        ConnectionStatusText = $"Status: Connecting to {listing.Address}:{listing.Port}.";
        PrintMultiplayerLog($"Connecting to {listing.Address}:{listing.Port}.");
        EmitConnectionStateChanged();
        return true;
    }

    public bool BeginDirectClientConnection(string address, int port) {
        if (string.IsNullOrWhiteSpace(address) || port <= 0 || port > 65535) {
            PrintMultiplayerLog($"Direct client connection rejected. Address='{address}', Port={port}.");
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
        if (!HasSelectedMode)
            return false;

        if (!HasPendingSetupConfigChanges) {
            LastConfigApplyMessage = "No config changes to apply.";
            EmitConfigApplyStateChanged();
            return false;
        }

        if (!IsClient) {
            SyncAuthoritativeSetupConfig(CachedSetupConfig);
            LastConfigApplyMessage = "Config settings changed by host.";
            EmitConfigApplyStateChanged();
            return true;
        }

        if (!HasNetworkPeer())
            return false;

        LastConfigApplyMessage = string.Empty;
        RpcId(ServerPeerId, nameof(RpcRequestApplyCachedSetupConfig), CachedSetupConfig.SerializeForNetwork());
        EmitConfigApplyStateChanged();
        return true;
    }

    public bool RevertCachedSetupConfigChanges() {
        if (!HasSelectedMode)
            return false;

        SyncCachedSetupConfig();
        LastConfigApplyMessage = "Config changes reverted.";
        EmitLobbyStateChanged();
        EmitConfigApplyStateChanged();
        return true;
    }

    public void RegisterLocalLobbyPlayers() {
        var peerId = GetLocalPeerId();
        if (peerId == -1) {
            EmitLobbyStateChanged();
            return;
        }

        var activeLocalPlayerCount = GetActiveLocalPlayerCount();
        MultiplayerData.Peers.Clear();
        MultiplayerData.Players.Clear();
        MultiplayerData.SetupConfig.LocalPlayerCount = activeLocalPlayerCount;
        MultiplayerData.SetupConfig.OnlineEnabled = CurrentMode == NetworkMode.Online;

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
        if (peerId == -1)
            return;

        if (IsClient && HasNetworkPeer()) {
            RpcId(ServerPeerId, nameof(RpcRequestSetLocalPeerTeam), teamId);
            return;
        }

        SetPeerTeam(peerId, teamId);
    }

    public void SetPeerTeam(int peerId, int teamId) {
        var normalizedTeamId = global::MultiplayerData.NormalizeTeamId(teamId);
        if (normalizedTeamId == global::MultiplayerData.DefaultTeamId)
            normalizedTeamId = GetLeastPopulatedTeamId(peerId);

        var peerData = GetOrCreatePeerData(peerId);
        UpdatePeer(
            peerId,
            peerData.IsHost,
            normalizedTeamId,
            peerData.RequestedLocalPlayerCount,
            peerData.MaxLocalPlayers);
    }

    public void UpdateSetupConfig(
        int maxPlayers,
        int localPlayerCount,
        bool onlineEnabled,
        string serverAddress,
        int serverPort,
        string gameModeId) {
        if (HasNetworkPeer()) {
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

    public void SyncAuthoritativeSetupConfig(SetupConfig setupConfig) {
        if (setupConfig == null)
            return;

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
        if (HasNetworkPeer())
            Rpc(nameof(RpcReplaceFullSetupConfig), serializedSetupConfig);
        else {
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
        if (HasNetworkPeer()) {
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

    public void UpdatePeer(int peerId, bool isHost, int teamId, int requestedLocalPlayerCount, int maxLocalPlayers) {
        if (HasNetworkPeer()) {
            Rpc(nameof(RpcUpdatePeer), peerId, isHost, teamId, requestedLocalPlayerCount, maxLocalPlayers);
            return;
        }

        RpcUpdatePeer(peerId, isHost, teamId, requestedLocalPlayerCount, maxLocalPlayers);
    }

    public void RemovePeer(int peerId) {
        if (HasNetworkPeer()) {
            Rpc(nameof(RpcRemovePeer), peerId);
            return;
        }

        RpcRemovePeer(peerId);
    }

    public void RemovePlayer(int peerId, int localId) {
        if (HasNetworkPeer()) {
            Rpc(nameof(RpcRemovePlayer), peerId, localId);
            return;
        }

        RpcRemovePlayer(peerId, localId);
    }

    public void ClearPlayers() {
        if (HasNetworkPeer()) {
            Rpc(nameof(RpcClearPlayers));
            return;
        }

        RpcClearPlayers();
    }

    public bool DamageAuthoritativeWallTile(Vector2I position, float damageAmount = 1.0f, DamageType damageType = DamageType.Crush) {
        if (!CanApplyAuthoritativeArenaMapChange())
            return false;

        if (!ArenaMapData.IsWallTile(position) || damageAmount <= 0.0f)
            return false;

        if (HasNetworkPeer()) {
            Rpc(nameof(RpcDamageArenaWallTile), position.X, position.Y, (int)damageType, damageAmount);
            return true;
        }

        RpcDamageArenaWallTile(position.X, position.Y, (int)damageType, damageAmount);
        return true;
    }

    public bool DamageAuthoritativeWallFromWorldPosition(Vector2 worldPosition, Vector2I tileSize, float damageAmount = 1.0f, DamageType damageType = DamageType.Crush) {
        return DamageAuthoritativeWallTile(ArenaMapData.WorldToTile(worldPosition, tileSize), damageAmount, damageType);
    }

    public bool DamageAuthoritativeWallsInRadius(Vector2I centerTile, int radius, float damageAmount = 1.0f, DamageType damageType = DamageType.Explosive) {
        if (!CanApplyAuthoritativeArenaMapChange())
            return false;

        if (damageAmount <= 0.0f || radius < 0)
            return false;

        if (HasNetworkPeer()) {
            Rpc(nameof(RpcDamageArenaWallsInRadius), centerTile.X, centerTile.Y, radius, (int)damageType, damageAmount);
            return true;
        }

        RpcDamageArenaWallsInRadius(centerTile.X, centerTile.Y, radius, (int)damageType, damageAmount);
        return true;
    }

    public bool DamageAuthoritativeWallsInWorldRadius(Vector2 worldCenter, Vector2I tileSize, float worldRadius, float damageAmount = 1.0f, DamageType damageType = DamageType.Explosive) {
        var centerTile = ArenaMapData.WorldToTile(worldCenter, tileSize);
        var tileRadius = Mathf.CeilToInt(worldRadius / Mathf.Max(1, tileSize.X));
        return DamageAuthoritativeWallsInRadius(centerTile, tileRadius, damageAmount, damageType);
    }

    public void ClearPeers() {
        if (HasNetworkPeer()) {
            Rpc(nameof(RpcClearPeers));
            return;
        }

        RpcClearPeers();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRequestJoinServer(Godot.Collections.Array<int> localIds, Godot.Collections.Array<string> displayNames) {
        if (!IsAuthoritativeServer())
            return;

        var remotePeerId = Multiplayer.GetRemoteSenderId();
        RegisterRemotePeerPlayers(remotePeerId, localIds, displayNames);
        SendFullLobbyStateToPeer(remotePeerId);
        ConnectionStatusText = $"Status: Peer {remotePeerId} joined the lobby.";
        EmitConnectionStateChanged();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRequestSetLocalPeerTeam(int teamId) {
        if (!IsAuthoritativeServer())
            return;

        var remotePeerId = Multiplayer.GetRemoteSenderId();
        SetPeerTeam(remotePeerId, teamId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRequestApplyCachedSetupConfig(string serializedSetupConfig) {
        if (!IsAuthoritativeServer())
            return;

        var remotePeerId = Multiplayer.GetRemoteSenderId();
        if (!SetupConfig.TryDeserializeForNetwork(serializedSetupConfig, out var requestedSetupConfig))
            return;

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
        if (!SetupConfig.TryDeserializeForNetwork(serializedSetupConfig, out var deserializedSetupConfig))
            return;

        MultiplayerData.SetupConfig.CopyFrom(deserializedSetupConfig);
        SyncCachedSetupConfig();
        EmitLobbyStateChanged();
        EmitConfigApplyStateChanged();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcNotifyConfigApplied(string message) {
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
        GetOrCreatePeerData(peerId);

        var playerData = GetOrCreatePlayerData(peerId, localId);
        playerData.GlobalId = globalId;
        playerData.DisplayName = displayName;
        playerData.IsLocalPlayer = HasNetworkPeer() ? peerId == Multiplayer.GetUniqueId() : isLocalPlayer;
        EmitLobbyStateChanged();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcUpdatePeer(int peerId, bool isHost, int teamId, int requestedLocalPlayerCount, int maxLocalPlayers) {
        var peerData = GetOrCreatePeerData(peerId);
        peerData.IsHost = isHost;
        peerData.TeamId = global::MultiplayerData.NormalizeTeamId(teamId);
        peerData.RequestedLocalPlayerCount = requestedLocalPlayerCount;
        peerData.MaxLocalPlayers = maxLocalPlayers;
        EmitLobbyStateChanged();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRemovePeer(int peerId) {
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
        MultiplayerData.Players.Clear();
        EmitLobbyStateChanged();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcClearPeers() {
        MultiplayerData.Peers.Clear();
        MultiplayerData.Players.Clear();
        EmitLobbyStateChanged();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcDamageArenaWallTile(int x, int y, int damageTypeValue, float damageAmount) {
        ArenaMapData.DamageWallTile(new Vector2I(x, y), ToDamageType(damageTypeValue), damageAmount);
        EmitArenaMapChanged();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcDamageArenaWallsInRadius(int centerX, int centerY, int radius, int damageTypeValue, float damageAmount) {
        ArenaMapData.DamageWallsInRadius(new Vector2I(centerX, centerY), radius, ToDamageType(damageTypeValue), damageAmount);
        EmitArenaMapChanged();
    }

    private static DamageType ToDamageType(int damageTypeValue) {
        return Enum.IsDefined(typeof(DamageType), damageTypeValue)
            ? (DamageType)damageTypeValue
            : DamageType.Crush;
    }

    private void OnPeerConnected(long peerId) {
        PrintMultiplayerLog($"Peer connected: {peerId}. Connected peers: {Multiplayer.GetPeers().Length}.");

        if (!IsAuthoritativeServer())
            return;

        ConnectionStatusText = $"Status: Peer {peerId} connected. Waiting for lobby data.";
        EmitConnectionStateChanged();
    }

    private void OnPeerDisconnected(long peerId) {
        PrintMultiplayerLog($"Peer disconnected: {peerId}. Connected peers: {Multiplayer.GetPeers().Length}.");

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
        PrintMultiplayerLog($"Connected to server. Local peer id: {Multiplayer.GetUniqueId()}.");
        EmitConnectionStateChanged();
        RpcId(ServerPeerId, nameof(RpcRequestJoinServer), BuildActiveLocalIdArray(), BuildActiveLocalNameArray());
    }

    private void OnConnectionFailed() {
        LastConnectionError = "Connection failed.";
        ConnectionStatusText = "Status: Connection failed.";
        HasLostConnection = true;
        UpdateNetworkModeDebugIcon();
        PrintMultiplayerLog("Connection failed.");
        CloseNetworkPeer();
        EmitConnectionStateChanged();
    }

    private void OnServerDisconnected() {
        LastConnectionError = "Server disconnected.";
        ConnectionStatusText = "Status: Disconnected from server.";
        HasLostConnection = true;
        UpdateNetworkModeDebugIcon();
        PrintMultiplayerLog("Server disconnected.");
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
        for (var teamId = 1; teamId <= 4; teamId++)
            teamCounts[teamId] = 0;

        foreach (var playerData in MultiplayerData.Players) {
            if (playerData.PeerId == excludedPeerId)
                continue;

            var playerTeamId = MultiplayerData.GetTeam(playerData);
            if (!teamCounts.ContainsKey(playerTeamId))
                continue;

            teamCounts[playerTeamId]++;
        }

        var bestTeamId = 1;
        var bestCount = int.MaxValue;
        for (var teamId = 1; teamId <= 4; teamId++) {
            var count = teamCounts[teamId];
            if (count < bestCount) {
                bestTeamId = teamId;
                bestCount = count;
            }
        }

        return bestTeamId;
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
        return IsServer && HasNetworkPeer() && Multiplayer.GetUniqueId() == ServerPeerId;
    }

    private bool CanApplyAuthoritativeArenaMapChange() {
        return !HasNetworkPeer() || IsAuthoritativeServer();
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
        for (var port = DefaultServerPort; port <= MaxServerPort; port++) {
            var error = peer.CreateServer(port, MaxClients);
            if (error == Error.Ok)
                return port;
        }

        return -1;
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
        GD.Print($"[Multiplayer][Mode={CurrentMode}] {message}");
    }

    private void EmitLobbyStateChanged() {
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
