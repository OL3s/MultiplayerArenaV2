# Multiplayer And Networking

This document tracks the multiplayer model, networking direction, lobby setup, and synced data structures.

## Multiplayer Focus

The project should support both local multiplayer and online multiplayer, with split-screen integration treated as a first-class part of the multiplayer model.

Multiplayer should be flexible enough to mix local and online players. The main integration goal is split-screen-first: one device can own multiple active players, and those local players should move cleanly through the same host, join, lobby, and match flows as single-player devices.

The main multiplayer target is up to 4 teams with up to 4 players per team, for team matches with up to 16 total active players.

The player model should be dynamic. A network peer is a device/connection, not necessarily one player. One device should be able to own several local players, and that same device should be able to either host the match or join another host.

## Local Lobby

The main menu should include a lobby system where local players are selected before hosting or joining. The local lobby target is 4 slots per device.

On PC, supported local lobby setups should include:

- 1 keyboard/mouse player and up to 3 gamepad players.
- Up to 4 gamepad players.

Local lobby slots should be stored as `LocalPlayerData` resources inside `LocalLobbyData`. This keeps local input ownership separate from online player replication and makes it possible for one peer/device to request several in-game players.

In-match local player HUD should follow the same model. UI panels should be created per local player on the current device, up to 4 panels, rather than per network peer. Each panel should use `PlayerData.GlobalId` for runtime gameplay lookup and `PlayerData.LocalId` for local layout/input identity.

## Identity Rules

- `LocalId` is the local player number on one device, usually matching the local lobby slot: `0`, `1`, `2`, or `3`.
- `PeerId` is the network peer/device that owns or represents that local player.
- `GlobalId` is the match-wide player id assigned to an accepted player.
- `PeerId + LocalId` identify ownership.
- `GlobalId` is for match ordering, scoreboards, spawn order, team assignment, kill feeds, and gameplay/UI systems that need a simple player number.
- `-1` is used as the unset/invalid value for ids. Do not use `PeerId = -1` to mean host.
- In Godot networking, the server/host normally uses peer id `1`, while connected clients receive their own peer ids.

Examples:

- Host peer `1`, local player `0` can be `(GlobalId: 0, PeerId: 1, LocalId: 0)`.
- Host peer `1`, second split-screen player can be `(GlobalId: 1, PeerId: 1, LocalId: 1)`.
- Client peer `3`, first local player can be `(GlobalId: 2, PeerId: 3, LocalId: 0)`.
- Client peer `3`, second split-screen player can be `(GlobalId: 3, PeerId: 3, LocalId: 1)`.

## Supported Setup Goals

- One device hosting with one local player.
- One device hosting with multiple local split-screen players.
- One device hosting without playing, acting as host-only/server authority.
- One client device joining with one local player.
- One client device joining with multiple local split-screen players.
- Mixed matches where total players are spread across several host/client devices.

Match limits should track peers and players separately. For example, a match can target up to 16 active players while using fewer than 16 network peers if some devices have multiple local players.

## Network Modes

Networking is managed through the `Networking` autoload node. This node is responsible for tracking the current network mode before and during a match.

Current network mode state:

- `NotSelected`: no network mode has been chosen yet.
- `Local`: no network peer, no ports opened, current running instance only.
- `Lan`: network host/client mode for direct local-network or direct address connections.
- `Online`: network host/client mode intended for internet discovery/listing, UPnP, relay, or matchmaking.
- `Client`: this instance is connected to a host.

The host menu should expose `Local`, `Lan`, and `Online`. A separate dedicated-server mode is not needed yet; running headless without local players is treated as a normal host/server process.

Current mode distinction:

- `Local` means the match is contained inside this one running process. It is not LAN and should not create a network peer or open a port.
- `Lan` and `Online` are both real network modes. For now they use the same direct host/client transport behavior.
- `Lan` is the default private/direct mode.
- `Online` is reserved for public/internet-facing host flow.
- Direct address joins should stay transport-agnostic: if the target address and port are reachable, they can connect to either a LAN or manually port-forwarded host.

## Networking Transport

The intended networking stack for this project is Easy Networking with RTC-first transport flow.

- Easy Networking should own the higher-level session flow, connection lifecycle, and multiplayer plumbing.
- RTC should be the main realtime transport for match and lobby traffic.
- Local LAN testing can use a simpler direct transport path while the full Easy Networking + RTC stack is still being integrated.
- The `Networking` autoload should stay transport-aware but gameplay/UI-facing code should remain transport-agnostic.
- Join flow should be driven through a `JoinType` enum in `Networking` so quickplay, LAN browsing, direct address join, and future online matchmaking all converge on the same lobby sync path.

Current implementation note:

- This repository does not currently include the Easy Networking addon or RTC signaling layer.
- Until those pieces are added, the join flow uses Godot's built-in multiplayer transport for working local multi-instance testing while preserving the same `Networking` authority and snapshot-sync flow the RTC path should use.

## Runtime Network Debug UI

- The `Networking` autoload creates a small always-on-top network mode icon in the top-left corner for debug builds/runs.
- The icon reflects `NetworkMode.NotSelected`, `Local`, `Lan`, `Online`, or `Client` using SVG assets in `Assets/Network/NetworkModes/`.
- Non-client modes also show a small peer-count label beside the icon so host/server peer state is readable while developing.
- `SettingsConfig.ShowNetworkDebugOverlay` controls whether the network debug overlay is visible.
- The setting is exposed in the main menu Settings screen under the `Online` tab.
- A separate connection-lost icon is shown when a client connection fails or an already-connected client loses the server. This is a debug/display state exposed through `Networking.HasLostConnection`, not a separate `NetworkMode`.
- `ConnectionFailed` and `ServerDisconnected` from Godot multiplayer are the current signals used to detect failed or lost client connections.
- The overlay is skipped in headless runs.

## Settings Menu

- `Scenes/UI/SettingsMenu.tscn` is the current settings entry point from the main menu.
- `SettingsConfig` is the shared settings resource owned by the `Networking` autoload for now.
- `SettingsConfig.LoadOrCreate()` loads `user://settings_config.tres` or returns defaults, and `SettingsConfig.Save()` persists the current resource to the same path.
- The settings menu currently has placeholder tabs for `Video`, `Sound`, `Controls`, and `Gameplay`, plus an `Online` tab with the network debug overlay toggle and Apply button.

## Match Setup And Lobby

`MultiplayerData` describes the active synced match setup. It owns connected peers/devices, accepted match players, and setup config so the game can support pure local play, pure online play, hosted split-screen play, and split-screen clients joining online sessions through the same shared model.

Peers and players should be separate arrays. `PeerData` describes the connected device and its requested local-player capacity. `PlayerData` describes an accepted in-game player and links back to the device through `PeerId`.

Important identity rule: `PlayerData` is looked up by `(PeerId, LocalId)`, not by `GlobalId`. `LocalId` can repeat across peers, because every machine has its own local player `0`, `1`, `2`, etc. `PeerId` disambiguates which device that local player belongs to.

Real team ids currently run from `1` to `4`. Team `0` is treated as an auto-assign request, not a persistent gameplay team. Team resolution is peer-based for the current lobby model.

The match lobby shows a small top-left setup summary, a centered players section, and a right-side config section. Players are rendered through reusable `LobbyPlayerCard` scene instances and grouped under clickable team headers like `[Auto-Assign]`, `[Team 1]`, `[Team 2]`, `[Team 3]`, and `[Team 4]`.

## LAN Host Port Behavior

- LAN/server hosting no longer hard-locks to port `7777`.
- Hosting now scans from `7700` through `8700` and binds to the first available port.
- The selected port is written back into setup state and used for LAN discovery responses and direct joins.
- TODO later: allow choosing and preferring a specific port before falling back to the auto-increment scan.

## Setup Config Direction

Match setup should be resource-driven. `SetupConfig` owns the selected/available game modes, map generation settings, biome settings, player limits, address/port, and team behavior.

Game modes are represented as `GameModeConfig` resources in an array so multiple modes can be enabled for voting, rotation, quickmatch filtering, or future playlist logic. Map and biome setup are separate resources so procedural generation can grow without turning `SetupConfig` into a large flat object.

The match lobby config UI should edit these resources directly through grouped sections for internet settings, map/biome settings, and game settings.

## Overlay UI

Overlay UI should be managed through a reusable `SceneOverlay` scene, and it is not an autoload in this project. Instead, game code should call `SceneOverlay.GetOrCreate(context)` so the overlay layer is created inside the current room/current scene only when needed.

`SceneOverlay` can add overlays from a `Control` instance or `PackedScene`, close the top overlay, close all overlays, and optionally enable a blur backdrop for any overlay, not just popup panels.

## RPC Update Methods

The `Networking` autoload exposes simple RPC update methods for shared multiplayer state. These methods should use basic arguments instead of sending complex objects directly, which keeps the netcode easier to reason about and compatible with Godot's RPC system.

Current public update methods:

- `UpdateSetupConfig(...)`: syncs match setup like max players, local player count, online enabled, address, port, and game mode.
- `UpdatePeer(...)`: adds or updates one connected peer/device using primitive values for peer id, host state, team id, requested local player count, and max local players.
- `UpdatePlayer(...)`: adds or updates one accepted player using primitive values for global id, peer id, local id, name, and local-player status.
- `RemovePeer(...)`: removes one connected peer/device and its players.
- `RemovePlayer(...)`: removes one player from a specific peer.
- `ClearPlayers()`: clears the accepted match player list.
- `ClearPeers()`: clears all connected peers/devices.

The public `UpdateXYZ` methods are the preferred API for game code. They should call the RPC version when a network peer exists, or apply the same change locally when running without a network peer.

## Current Data Structure Summary

```csharp
public partial class MultiplayerData : Resource
{
    public const int DefaultTeamId = 0;
    public Godot.Collections.Array<PeerData> Peers { get; set; } = new();
    public Godot.Collections.Array<PlayerData> Players { get; set; } = new();
    public SetupConfig SetupConfig { get; set; } = new();

    public int GetTeam(PlayerData playerData) { ... }
    public int GetTeam(int peerId, int localId) { ... }
    public PlayerData GetPlayerByGlobalId(int globalId) { ... }
    public static int NormalizeTeamId(int teamId) { ... }
}

public partial class SetupConfig : Resource
{
    public int MaxPlayers { get; set; }
    public int LocalPlayerCount { get; set; }
    public bool OnlineEnabled { get; set; }
    public string ServerAddress { get; set; }
    public int ServerPort { get; set; }
    public string GameModeId { get; set; }
    public GameplayScoring GameplayScoring { get; set; } = new();
    public Godot.Collections.Array<GameModeConfig> GameModes { get; set; } = new();
    public MapGenerationConfig MapConfig { get; set; } = new();
    public BiomeConfig BiomeConfig { get; set; } = new();
}

public partial class MapGenerationConfig : Resource
{
    public enum StructureType { Arena, Rooms, Narrow, Islands, Plain }
    public enum SeedMode { AlwaysRandom, FixedSeed, SeedPool }

    public SeedMode SelectedSeedMode { get; set; }
    public int FixedSeed { get; set; }
    public Godot.Collections.Array<int> SeedPool { get; set; } = new();
    public Godot.Collections.Array<StructureType> EnabledStructureTypes { get; set; } = new();
}

public partial class BiomeConfig : Resource
{
    public enum BiomeType { Plains, Arena, Tundra, Urban, Jungle, Forest, Desert, Snow, Industrial }
    public Godot.Collections.Array<BiomeType> EnabledBiomes { get; set; } = new();
}
```
