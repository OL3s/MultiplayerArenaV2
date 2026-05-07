# MultiplayerArenaV2

MultiplayerArenaV2 is an early-stage Godot project for a simple top-down 2D arena shooter.

The goal is to build a fast, easy-to-pick-up arena game with multiple game modes and support for several platforms over time.

## Game Concept

- Top-down 2D arena shooter
- Simple movement and combat controls
- Small arena-based matches
- Multiple game modes
- Fully destructible environments
- Designed for quick rounds and multiplayer-focused gameplay

## Destructible Environment

The arena should be built around the idea that everything can be destroyed.

Planned logic model:

- Wall tiles are tracked by grid position using a `HashSet<Vector2I>`.
- Floor tiles are tracked by grid position using a separate `HashSet<Vector2I>`.
- Hit wall tiles are tracked in a hashmap/dictionary keyed by `Vector2I`.
- When a wall tile is hit for the first time, it gets an entry in the hit/damage dictionary.
- Tile damage data is stored separately from the base tile existence data.
- The TileSet represents both floor and wall visuals.
- Temporary TileSet coordinates can use `(0, 0)` for floor and `(0, 1)` for wall until the final art pipeline is decided.
- Damage state maps to a visual TileMap layer so cracked, damaged, and destroyed wall states can be represented clearly.
- Destroyed wall tiles are removed from the wall-tile lookup so movement, bullets, and line-of-sight can use the same source of truth.

Possible structure:

```csharp
private readonly HashSet<Vector2I> _wallTiles = new();
private readonly HashSet<Vector2I> _floorTiles = new();
private readonly Dictionary<Vector2I, WallDamageData> _hitWallTiles = new();
```

`WallDamageData` should contain the state needed to decide how damaged a tile is, when it should be destroyed, and which visual damage tile should be shown.

Current debug tile asset:

- `Assets/Tiles/debug_floor_wall_atlas.svg` is a temporary SVG atlas with `32x16` debug tiles.
- The top tile is floor at atlas coordinate `(0, 0)`.
- The bottom tile is wall at atlas coordinate `(0, 1)`.

Current map data classes:

- `ArenaMapData` stores floor tiles and wall tiles as hashsets.
- `ArenaMapData.GenerateMap()` exists as the main generation entry point, but is intentionally empty until the real map algorithm is chosen.
- `ArenaMapData.FillWallsFromFloors()` creates wall tiles around floor tiles using all 8 neighboring cells, so corner walls are included and no smoothing is applied.
- `ArenaMapData.GenerateTileMapData()` converts the floor/wall hashsets into `MapTileData` resources with position, source id, atlas coordinates, tile type, and alternative tile id.

## Planned Game Modes

- Free-for-all
- Team deathmatch
- Objective-based modes
- More modes may be added as the core gameplay develops

## Multiplayer Focus

The project should support both local multiplayer and online multiplayer.

Multiplayer should be flexible enough to mix local and online players. Example targets include two players sharing one machine with split screen while also joining an online match, or one player hosting an online match while additional local players join from the same device.

The main multiplayer target is 4 players per team, for 4v4 matches with up to 8 total active players.

The player model should be dynamic. A network peer is a device/connection, not necessarily one player. One device should be able to own several local players, and that same device should be able to either host the match or join another host.

The main menu should include a lobby system, similar in spirit to Fortnite's party/lobby flow, where local players are selected before hosting or joining. The local lobby target is 4 slots per device.

On PC, supported local lobby setups should include:

- 1 keyboard/mouse player and up to 3 gamepad players
- Up to 4 gamepad players

Local lobby slots should be stored as `LocalPlayerData` resources inside `LocalLobbyData`. This keeps local input ownership separate from online player replication and makes it possible for one peer/device to request several in-game players.

`LocalId` is the local player number on one device. It represents which player this is locally, usually matching the local lobby slot: `0`, `1`, `2`, or `3`. `PeerId` is the network peer/device that owns or represents that local player. Together, `PeerId + LocalId` identify player ownership.

`GlobalId` is the match-wide player id assigned to an accepted player. It should be a small integer, not a UUID, because the id only needs to be stable inside the current match. Use it for match ordering, scoreboards, spawn order, team assignment, kill feeds, and other gameplay/UI systems that need a simple player number.

Examples:

- Host peer `1`, local player `0` can be `(GlobalId: 0, PeerId: 1, LocalId: 0)`.
- Host peer `1`, second split-screen player can be `(GlobalId: 1, PeerId: 1, LocalId: 1)`.
- Client peer `3`, first local player can be `(GlobalId: 2, PeerId: 3, LocalId: 0)`.
- Client peer `3`, second split-screen player can be `(GlobalId: 3, PeerId: 3, LocalId: 1)`.

`-1` is used as the unset/invalid value for ids. Do not use `PeerId = -1` to mean host. In Godot networking, the server/host normally uses peer id `1`, while connected clients receive their own peer ids. Keeping host as peer `1` avoids special cases and leaves `-1` available for uninitialized data.

Current local player data structure:

```csharp
public partial class LocalPlayerData : Resource
{
    public int LocalId { get; set; }
    public bool IsActive { get; set; }
    public LocalInputType InputType { get; set; }
    public int DeviceId { get; set; }
    public string DisplayName { get; set; }
}
```

Supported setup goals:

- One device hosting with one local player
- One device hosting with multiple local split-screen players
- One device hosting without playing, acting as host-only/server authority
- One client device joining with one local player
- One client device joining with multiple local split-screen players
- Mixed matches where total players are spread across several host/client devices

Match limits should track peers and players separately. For example, a match can target 8 active players while using fewer than 8 network peers if some devices have multiple local players.

Networking should be managed through a `Networking` autoload node. This node is responsible for tracking the current network mode before and during a match.

Planned network mode state:

- Not selected: no network mode has been chosen yet
- Local only: no network peer, no ports opened, same-machine players only
- Server local: host/LAN server, listens locally but does not attempt public exposure
- Server online: online host mode, intended for future public exposure, UPnP, relay, or matchmaking
- Dedicated server: server-only/dev mode without local player ownership, selected automatically for headless runs instead of through the host menu
- Client: this instance is connected to a host

Possible structure:

```csharp
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
```

The host menu should expose `LocalOnly`, `ServerLocal`, and `ServerOnline`. `DedicatedServer` should not be a normal menu option; it is selected at startup when running Godot with `--headless`.

The rest of the game should check this shared state instead of guessing whether it is running as local-only, LAN host, online host, dedicated server, or client.

`MultiplayerData` should describe the active synced match setup. It owns connected peers/devices, accepted match players, and setup config so the game can support pure local play, pure online play, hosted split-screen play, and split-screen clients joining online sessions.

Peers and players should be separate arrays. `PeerData` describes the connected device and its requested local-player capacity. `PlayerData` describes an actual accepted in-game player and links back to the device through `PeerId`. This keeps the player list easy for gameplay systems while still supporting several players on one connection.

Important identity rule: `PlayerData` is looked up by `(PeerId, LocalId)`, not by `GlobalId`. `LocalId` can repeat across peers, because every machine has its own local player `0`, `1`, `2`, etc. `PeerId` disambiguates which device that local player belongs to. `GlobalId` is still stored on the player, but it is the accepted match player number, not the ownership key.

Team id `0` means free-for-all/no-team. Team ids `1` and above are real team ids. `-1` should stay reserved for unset/invalid ids and should be normalized to `0` before gameplay uses it.

Team resolution depends on match type. In local-only games, `PeerData.TeamId` is used so split-screen players owned by the same peer move teams as a group. In online games, `PlayerData.TeamId` is used so the server can assign or change accepted player teams directly. Gameplay code should call `MultiplayerData.GetTeam(...)` instead of reading `TeamId` fields directly.

The `Networking` autoload should expose simple RPC update methods for shared multiplayer state. These methods should use basic arguments instead of sending complex objects directly, which keeps the netcode easier to reason about and compatible with Godot's RPC system.

Initial update targets:

- `UpdateSetupConfig(...)`: syncs match setup like max players, local player count, online enabled, address, port, and game mode.
- `UpdatePeer(...)`: adds or updates one connected peer/device using primitive values for peer id, host state, team id, requested local player count, and max local players.
- `UpdatePlayer(...)`: adds or updates one accepted player using primitive values for global id, peer id, local id, name, team id, and local-player status.
- `RemovePeer(...)`: removes one connected peer/device and its players.
- `RemovePlayer(...)`: removes one player from a specific peer.
- `ClearPlayers()`: clears the accepted match player list.
- `ClearPeers()`: clears all connected peers/devices.

The public `UpdateXYZ` methods are the preferred API for game code. They should call the RPC version when a network peer exists, or apply the same change locally when running without a network peer.

Current data structure:

```csharp
public partial class MultiplayerData : Resource
{
    public const int FreeForAllTeamId = 0;
    public Godot.Collections.Array<PeerData> Peers { get; set; } = new();
    public Godot.Collections.Array<PlayerData> Players { get; set; } = new();
    public SetupConfig SetupConfig { get; set; } = new();

    public int GetTeam(PlayerData playerData) { ... }
    public int GetTeam(int peerId, int localId) { ... }
    public static int NormalizeTeamId(int teamId) { ... }
}

public partial class PeerData : Resource
{
    public int PeerId { get; set; }
    public bool IsHost { get; set; }
    public int TeamId { get; set; }
    public int RequestedLocalPlayerCount { get; set; }
    public int MaxLocalPlayers { get; set; }
}

public partial class PlayerData : Resource
{
    public int GlobalId { get; set; }
    public int LocalId { get; set; }
    public int PeerId { get; set; }
    public string DisplayName { get; set; }
    public int TeamId { get; set; }
    public bool IsLocalPlayer { get; set; }
}

public partial class LocalLobbyData : Resource
{
    public Godot.Collections.Array<LocalPlayerData> LocalPlayers { get; set; } = new();
}
```

## Target Platforms

- PC: primary target
- Mobile: planned
- Browser: possible later
- Console: possible later

## Tech

- Engine: Godot 4.6
- Language: C#
- Renderer: GL Compatibility
- Project type: 2D game

## Project Status

This project is in the early setup phase. Core gameplay, local multiplayer, online networking, destructible arenas, weapons, and game modes are still to be implemented.

## Development Goals

- Build a solid top-down movement and shooting foundation
- Add local and online multiplayer support
- Support split-screen players in online sessions where possible
- Support multiple local players per device for both hosts and clients
- Use a 4-slot local lobby on the main menu before hosting or joining
- Target 4v4 team matches with up to 8 active players
- Use a `Networking` autoload as the single place for network mode state
- Create reusable arena and game mode systems
- Build a consistent destructible environment system where tile logic and visuals stay in sync
- Keep controls simple across keyboard, controller, touch, and future platform targets
- Expand with more weapons, maps, and match rules over time
