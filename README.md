# MultiplayerArenaV2

MultiplayerArenaV2 is an early-stage Godot project for a simple top-down 2D arena shooter.

The goal is to build a fast, easy-to-pick-up arena game with multiple game modes and support for several platforms over time.

## CLI Build

Build the C# project without opening the Godot editor:

```bash
dotnet build MultiplayerArenaV2.csproj
```

Import assets from the CLI when new Godot assets were added:

```bash
godot --headless --path . --import
```

Validate that Godot can start the project without opening the editor window:

```bash
godot --headless --path . --quit
```

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
- `Assets/Tiles/debug_wall_damage_overlay.svg` is a temporary separate SVG atlas for wall-damage overlay visuals.
- The top tile is a light damage overlay at atlas coordinate `(0, 0)`.
- The bottom tile is a heavy damage overlay at atlas coordinate `(0, 1)`.

Current map data classes:

- `ArenaMapData` stores floor tiles and wall tiles as hashsets, and hit walls in a dictionary keyed by `Vector2I`.
- Tile coordinates are signed because they use `Vector2I`, so negative map positions are valid and do not need special handling.
- `ArenaMapData.GenerateMap()` exists as the main generation entry point, but is intentionally empty until the real map algorithm is chosen.
- `ArenaMapData.ResetWallTiles()` rebuilds the wall hashset from current floors using all 8 neighboring cells, so corner walls are included and no smoothing is applied.
- `ArenaMapData.FillWallsFromFloors()` currently delegates to `ResetWallTiles()`.
- `ArenaMapData.DamageWallTile()` tracks damage per wall tile in `WallDamageData` and destroys the wall when max damage is reached.
- `ArenaMapData.DamageWallFromWorldPosition()` converts a world hit position back into a tile coordinate before applying single-tile wall damage.
- `ArenaMapData.WorldToTile()` is the shared world-to-grid conversion helper for destructible wall logic.
- `ArenaMapData.GetTilesInRadius()`, `DamageWallsInRadius()`, and `DamageWallsInWorldRadius()` support tile-accurate explosive damage instead of row-based or merged-physics damage.
- `ArenaMapData.DestroyWallTile()` converts the destroyed wall tile into a floor tile, then rebuilds surrounding walls from the floor hashset so the data stays consistent.
- `ArenaMapData.GenerateLayerTileMapData()` emits `MapTileData` for separate logical layers: `Floor`, `Wall`, and `WallDamage`.
- `MapTileData` now stores both tile type and logical layer type so a renderer can rebuild visible tile layers from data without using the rendered TileMap state as authority.
- `WallDamageData` stores `Damage`, `MaxDamage`, and `DamageStage` for one wall tile.

Current rendering structure for destructible map testing:

- `ArenaMapData` is the source of truth for floor tiles, wall tiles, and wall damage.
- `ArenaTileLayerRenderer` is a Node2D renderer that rebuilds visible Godot `TileMapLayer` nodes from `ArenaMapData`.
- Layer TileSet resources now live in `Assets/Tiles/TileSets/` as separate `.tres` files for floor, wall, and wall-damage overlay rendering.
- The current layer split is:
- `FloorLayer`: floor visuals
- `WallLayer`: wall visuals
- `WallDamageLayer`: visual damage overlay only
- The renderer clears and repaints each `TileMapLayer` from generated `MapTileData`, so the rendered layers are a projection of hashset/dictionary state, not the authority themselves.

Projectile and explosion damage handling rules:

- TileMapLayer collision should not be treated as the authority for destructible walls.
- Godot tile collision is authored per tile in the `TileSet`, but physics can be grouped internally by quadrants for performance.
- Bullet and projectile hits should convert the impact world position back into map/grid coordinates before wall damage is applied.
- Wall damage should be resolved by checking the wall hashset at that grid position, not by treating a merged collision body or a row of tiles as one destructible unit.
- Grenade and explosion damage should iterate tile positions inside the explosion radius and apply damage tile-by-tile.
- Explosion damage should only affect tiles that actually fall within the radius and still exist in the wall hashset.
- A good future pattern is `DamageWallsInRadius(centerTile, radius, damageAmount)` so area damage remains grid-accurate.
- Movement collision can come from the wall `TileMapLayer`, but destructible gameplay state should always be read from `ArenaMapData`.
- Current RTC/networking direction for destructible walls is server-authoritative function replication: the host/server runs the map logic and sends the same damage/destroy function calls outward to clients.
- Clients should not be the authority for wall destruction. For now, they only receive and replay server-approved wall damage updates.
- Late-join map-state catch-up is intentionally deferred for later work. The current focus is authoritative live sync from server to already-connected clients.

Current destruction test scene:

- `Scenes/Tests/TestMapDestructionLogic.tscn` is a temporary root scene for destructible map backend testing.
- `TestMapDestructionLogic` creates mock floor hashset data, calls `ResetWallTiles()`, then applies a few sample wall-damage values after normal wall generation.
- Left-clicking a wall applies bullet-style single-tile damage from the current mouse world position.
- `Shift + Left Click` applies explosive area damage using the current mouse world position and a larger test radius.
- A debug radius is drawn under the mouse cursor so explosive sampling is visible while testing.
- Right-clicking resets the mock test arena.

Current LAN destruction test scene:

- `Scenes/Tests/TestMapDestructionLogicLAN.tscn` is a temporary LAN/RTC-focused test scene for server-authoritative wall destruction sync.
- `TestMapDestructionLogicLAN` uses the same scene-local mock map flow as `TestMapDestructionLogic`, then forwards host damage/reset RPCs to connected clients.
- The scene includes a status label that shows waiting/connection state while testing host/client behavior.
- `Networking` owns the shared `--role` CLI override and maps it to `NetworkMode`; this scene reads `Networking.CurrentMode` instead of keeping a separate role enum.
- `TestMapDestructionLogicLAN` still supports scene-local CLI overrides through Godot user args: `--address` and `--port` for the direct client target.
- The host instance is the only peer allowed to apply wall damage input.
- The client instance is a read-only viewer that applies and re-renders scene-local RPC updates sent by the host.
- Current controls on the host are the same as the local test scene: `Left Click` for bullet-style damage, `Shift + Left Click` for explosive radius damage, and `Right Click` to rebuild/reset the mock arena.
- The LAN test scene currently focuses on live sync for already-connected peers. Start both peers first, then perform destruction tests from the host side.
- Initial map construction and late-join catch-up are still not fully synchronized yet. Those are deferred follow-up tasks, not part of the current networking slice.

Example CLI usage:

- Host: `godot --path . res://Scenes/Tests/TestMapDestructionLogicLAN.tscn -- --role host`
- Client: `godot --path . res://Scenes/Tests/TestMapDestructionLogicLAN.tscn -- --role client --address 127.0.0.1 --port 7700`
- Launch one host and one client from the same terminal:

```bash
godot --path . res://Scenes/Tests/TestMapDestructionLogicLAN.tscn -- --role host & \
godot --path . res://Scenes/Tests/TestMapDestructionLogicLAN.tscn -- --role client --address 127.0.0.1 --port 7700 & \
disown
```

- If the host log says it picked a port other than `7700`, use that port for the client. This can happen if another old test instance is still holding `7700`.
- Supported shared role values are `local`, `lan`, `host`, `server`, `server-local`, `client`, `online`, `online-host`, and `server-online`.

Runtime print/logging standards:

- Multiplayer runtime prints should use a clear bracket prefix so multi-instance terminal output stays searchable.
- General networking logs use `[Multiplayer][Mode=<NetworkMode>] ...` and should include the current `NetworkMode` enum value.
- Scene-specific LAN destruction logs use `[LANDestructionTest][Mode=<NetworkMode>] ...`.
- Keep live gameplay prints short and event-based: host/client start, connect/fail/disconnect, peer count changes, and RPC send/apply events.
- Do not print every frame or every `_Process()` tick.

## Planned Game Modes

- `Deathmatch`
- `CaptureTheFlag`
- Free-for-all is not a separate mode in the current design. A deathmatch setup with one player per team covers the same gameplay shape for now.
- More modes may be added as the core gameplay develops

## Multiplayer Focus

The project should support both local multiplayer and online multiplayer, with split-screen integration treated as a first-class part of the multiplayer model.

Multiplayer should be flexible enough to mix local and online players. The main integration goal is split-screen-first: one device can own multiple active players, and those local players should move cleanly through the same host, join, lobby, and match flows as single-player devices. Example targets include two players sharing one machine while joining an online match, or one player hosting while additional local players join from the same device.

The main multiplayer target is up to 4 teams with up to 4 players per team, for team matches with up to 16 total active players.

The player model should be dynamic. A network peer is a device/connection, not necessarily one player. One device should be able to own several local players, and that same device should be able to either host the match or join another host.

The main menu should include a lobby system, similar in spirit to Fortnite's party/lobby flow, where local players are selected before hosting or joining. The local lobby target is 4 slots per device.

On PC, supported local lobby setups should include:

- 1 keyboard/mouse player and up to 3 gamepad players
- Up to 4 gamepad players

Local lobby slots should be stored as `LocalPlayerData` resources inside `LocalLobbyData`. This keeps local input ownership separate from online player replication and makes it possible for one peer/device to request several in-game players. The active local lobby is owned by the `Networking` autoload so selected local players survive scene changes from the main menu into host/join menus and the match lobby.

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

Match limits should track peers and players separately. For example, a match can target up to 16 active players while using fewer than 16 network peers if some devices have multiple local players.

Networking should be managed through a `Networking` autoload node. This node is responsible for tracking the current network mode before and during a match.

For destructible map state, `Networking` should also act as the authoritative bridge between gameplay and transport. The current target is one-way server-to-client map update flow: the server/host executes the wall-damage logic and sends the same function call to clients so they stay in sync. Full late-join map snapshot/catch-up is planned later and is not the current focus.

Planned network mode state:

- Not selected: no network mode has been chosen yet
- Local: no network peer, no ports opened, current running instance only
- LAN: network host/client mode for direct local-network or direct address connections
- Online: network host/client mode intended for internet discovery/listing, UPnP, relay, or matchmaking
- Client: this instance is connected to a host

Possible structure:

```csharp
public enum NetworkMode
{
    NotSelected,
    Local,
    Lan,
    Online,
    Client,
}

public NetworkMode CurrentMode { get; private set; } = NetworkMode.NotSelected;
```

The host menu should expose `Local`, `Lan`, and `Online`. A separate dedicated-server mode is not needed yet; running headless without local players is treated as a normal host/server process.

The rest of the game should check this shared state instead of guessing whether it is running as local, LAN, online, or client.

Runtime network mode debug UI:

- The `Networking` autoload creates a small always-on-top network mode icon in the top-left corner for debug builds/runs.
- The icon reflects `NetworkMode.NotSelected`, `Local`, `Lan`, `Online`, or `Client` using SVG assets in `Assets/Debug/NetworkModes/`.
- Non-client modes also show a small peer-count label beside the icon so host/server peer state is readable while developing.
- `SettingsConfig.ShowNetworkDebugOverlay` controls whether the network debug overlay is visible.
- The setting is exposed in the main menu Settings screen under the `Online` tab.
- A separate connection-lost icon is shown when a client connection fails or an already-connected client loses the server. This is a debug/display state exposed through `Networking.HasLostConnection`, not a separate `NetworkMode`.
- `ConnectionFailed` and `ServerDisconnected` from Godot multiplayer are the current signals used to detect failed or lost client connections.
- The overlay is skipped in headless runs.

Settings menu structure:

- `Scenes/UI/SettingsMenu.tscn` is the current settings entry point from the main menu.
- `SettingsConfig` is the shared settings resource owned by the `Networking` autoload for now.
- `SettingsConfig.LoadOrCreate()` loads `user://settings_config.tres` or returns defaults, and `SettingsConfig.Save()` persists the current resource to the same path.
- The settings menu currently has placeholder tabs for `Video`, `Sound`, `Controls`, and `Gameplay`, plus an `Online` tab with the network debug overlay toggle and Apply button.

Current mode distinction:

- `Local` means the match is contained inside this one running process. It is not LAN and should not create a network peer or open a port.
- `Lan` and `Online` are both real network modes. For now they use the same direct host/client transport behavior.
- `Lan` is the default private/direct mode. A LAN server can still be reached from outside the local network if the user manually port-forwards and another player connects with a direct address and port.
- `Online` is reserved for public/internet-facing host flow. The main future difference is that online hosts should broadcast/register with an online service so they appear in an online search list. LAN hosts should not register with that online service.
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

`MultiplayerData` should describe the active synced match setup. It owns connected peers/devices, accepted match players, and setup config so the game can support pure local play, pure online play, hosted split-screen play, and split-screen clients joining online sessions through the same shared model.

Peers and players should be separate arrays. `PeerData` describes the connected device and its requested local-player capacity. `PlayerData` describes an actual accepted in-game player and links back to the device through `PeerId`. This keeps the player list easy for gameplay systems while still supporting several players on one connection.

Important identity rule: `PlayerData` is looked up by `(PeerId, LocalId)`, not by `GlobalId`. `LocalId` can repeat across peers, because every machine has its own local player `0`, `1`, `2`, etc. `PeerId` disambiguates which device that local player belongs to. `GlobalId` is still stored on the player, but it is the accepted match player number, not the ownership key.

Real team ids currently run from `1` to `4`. Team `0` is treated as an auto-assign request, not a persistent gameplay team. When a peer joins, or when the lobby `Auto-Assign` action is pressed, the server resolves that peer to the least-populated real team.

Team resolution is peer-based for the current lobby model. `PeerData.TeamId` is the authoritative team for that peer/device, so split-screen players owned by the same peer move teams as a group. Gameplay code should call `MultiplayerData.GetTeam(...)` instead of reading team fields directly.

The match lobby should show a small top-left setup summary, a centered players section, and a right-side config section. Players are rendered through reusable `LobbyPlayerCard` scene instances and grouped under clickable team headers like `[Auto-Assign]`, `[Team 1]`, `[Team 2]`, and `[Team 3]`. Clicking a team header moves all players owned by the local peer to that team. `Auto-Assign` immediately reassigns that peer to the least-populated real team.

Current LAN host port behavior:

- LAN/server hosting no longer hard-locks to port `7777`.
- Hosting now scans from `7700` through `8700` and binds to the first available port.
- The selected port is written back into setup state and used for LAN discovery responses and direct joins.
- TODO later: allow choosing and preferring a specific port before falling back to the auto-increment scan.

Match setup should be resource-driven. `SetupConfig` owns the selected/available game modes, map generation settings, biome settings, player limits, address/port, and team behavior. Game modes are represented as `GameModeConfig` resources in an array so multiple modes can be enabled for voting, rotation, quickmatch filtering, or future playlist logic. Map and biome setup are separate resources so procedural generation can grow without turning `SetupConfig` into a large flat object. The match lobby config UI should edit these resources directly through grouped sections for internet settings, map/biome settings, and game settings.

Overlay UI should be managed through a reusable `SceneOverlay` scene, and it is not an autoload in this project. Instead, game code should call `SceneOverlay.GetOrCreate(context)` so the overlay layer is created inside the current room/current scene only when needed. `SceneOverlay` can add overlays from a `Control` instance or `PackedScene`, close the top overlay, close all overlays, and optionally enable a blur backdrop for any overlay, not just popup panels.

The `Networking` autoload should expose simple RPC update methods for shared multiplayer state. These methods should use basic arguments instead of sending complex objects directly, which keeps the netcode easier to reason about and compatible with Godot's RPC system.

Initial update targets:

- `UpdateSetupConfig(...)`: syncs match setup like max players, local player count, online enabled, address, port, and game mode.
- `UpdatePeer(...)`: adds or updates one connected peer/device using primitive values for peer id, host state, team id, requested local player count, and max local players.
- `UpdatePlayer(...)`: adds or updates one accepted player using primitive values for global id, peer id, local id, name, and local-player status.
- `RemovePeer(...)`: removes one connected peer/device and its players.
- `RemovePlayer(...)`: removes one player from a specific peer.
- `ClearPlayers()`: clears the accepted match player list.
- `ClearPeers()`: clears all connected peers/devices.

The public `UpdateXYZ` methods are the preferred API for game code. They should call the RPC version when a network peer exists, or apply the same change locally when running without a network peer.

Current data structure:

```csharp
public partial class MultiplayerData : Resource
{
    public const int DefaultTeamId = 0;
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
    public bool IsLocalPlayer { get; set; }
}

public partial class SetupConfig : Resource
{
    public int MaxPlayers { get; set; }
    public int LocalPlayerCount { get; set; }
    public bool OnlineEnabled { get; set; }
    public string ServerAddress { get; set; }
    public int ServerPort { get; set; }
    public string GameModeId { get; set; }
    public Godot.Collections.Array<GameModeConfig> GameModes { get; set; } = new();
    public MapGenerationConfig MapConfig { get; set; } = new();
    public BiomeConfig BiomeConfig { get; set; } = new();

    public void AddGameMode(GameModeConfig gameModeConfig) { ... }
    public void RemoveGameMode(GameModeConfig.GameModeType modeType) { ... }
    public bool HasGameMode(GameModeConfig.GameModeType modeType) { ... }
}

public partial class GameModeConfig : Resource
{
    public enum GameModeType
    {
        Deathmatch,
        CaptureTheFlag,
    }

    public GameModeType ModeType { get; set; }
    public string DisplayName { get; set; }
    public bool IsEnabled { get; set; }
}

public partial class MapGenerationConfig : Resource
{
    public enum MapType
    {
        Arena,
        Rooms,
        Caves,
        Islands,
    }

    public enum SeedMode
    {
        AlwaysRandom,
        FixedSeed,
        SeedPool,
    }

    public MapType SelectedMapType { get; set; }
    public SeedMode SelectedSeedMode { get; set; }
    public int FixedSeed { get; set; }
    public Godot.Collections.Array<int> SeedPool { get; set; } = new();
}

public partial class BiomeConfig : Resource
{
    public enum BiomeType
    {
        Arena,
        Forest,
        Desert,
        Snow,
        Industrial,
    }

    public BiomeType SelectedBiome { get; set; }
    public bool AllowRandomBiome { get; set; }
    public Godot.Collections.Array<BiomeType> EnabledBiomes { get; set; } = new();
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
- Treat split-screen integration as a core multiplayer requirement across local and online flows
- Support multiple local players per device for both hosts and clients
- Use a 4-slot local lobby on the main menu before hosting or joining
- Target up to 4 teams with up to 16 active players total
- Use a `Networking` autoload as the single place for network mode state
- Create reusable arena and game mode systems
- Build a consistent destructible environment system where tile logic and visuals stay in sync
- Keep controls simple across keyboard, controller, touch, and future platform targets
- Expand with more weapons, maps, and match rules over time
