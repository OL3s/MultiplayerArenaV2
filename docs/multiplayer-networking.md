# Multiplayer And Networking

This document tracks the multiplayer model, networking direction, lobby setup, and synced data structures.

## Multiplayer Focus

The project should support both local multiplayer and online multiplayer, with split-screen integration treated as a first-class part of the multiplayer model.

Multiplayer should be flexible enough to mix local and online players. The main integration goal is split-screen-first: one device can own multiple active players, and those local players should move cleanly through the same host, join, lobby, and match flows as single-player devices.

The main multiplayer target is up to 4 teams with up to 4 players per team, for team matches with up to 16 total active players.

The player model should be dynamic. A network peer is a device/connection, not necessarily one player. One device should be able to own several local players, and that same device should be able to either host the match or join another host.

## Local Lobby

The main menu should include a lobby system where local players are selected before hosting or joining. The local lobby target is 4 slots per device.

The main menu Host action should always be visible and usable so a device can start a host/server lobby without local players. Join Game and Reset Players should only appear after at least one local player is active in the local lobby config.

On PC, supported local lobby setups should include:

- 1 keyboard/mouse player and up to 3 gamepad players.
- Up to 4 gamepad players.
- 1 touchscreen player by touching/clicking the visible empty player card in the main menu.

Main menu local-player configuration guards enforce at most one keyboard/mouse player and at most one touchscreen player per device. Gamepad uniqueness is still per gamepad device id.

Local lobby slots should be stored as `LocalPlayerData` resources inside `LocalLobbyData`. This keeps local input ownership separate from online player replication and makes it possible for one peer/device to request several in-game players.

In-match local player HUD should follow the same model. UI panels should be created per local player on the current device, up to 4 panels, rather than per network peer. Each panel should use `PlayerData.GlobalId` for runtime gameplay lookup and `PlayerData.LocalId` for local layout/input identity.

## Team Autofill

Non-local host lobbies expose autofill actions for 2, 3, and 4 teams. Autofill assignment is server-authoritative: clients may request autofill through RPC, but only the authoritative server applies the assignment and syncs the resulting peer team updates back to peers.

Autofill assigns whole peer/device groups, not individual players, so all split-screen players from the same peer stay on the same team.

The current autofill algorithm groups players by `PeerId`, sorts larger peer groups first, then places each group onto the team with the lowest assigned player count. Ties prefer the team with fewer peer groups, then the lowest team id. This keeps team sizes as balanced as possible while preserving peer grouping.

Local-only lobbies keep their separate `FFA` and `TEAM` buttons because local-only can intentionally split players from the same process across teams without network peer ownership concerns.

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
- The Local lobby hides connection settings but keeps map/game Match Config available. It uses local-only team mode buttons instead of peer/team assignment: `FFA` assigns each local player to their own team, while `TEAM` assigns local player slots 1 and 3 to Team 1 and slots 2 and 4 to Team 2.
- Entering a Local lobby applies `FFA` immediately so local players do not initially appear stacked on Team 1.
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

## Host Authority And Self Connections

The host/server authority is represented by peer id `1`. Host-owned local players are registered directly under peer `1`; connected clients receive different peer ids and request state changes from the host.

The host should never create an RTC/WebRTC connection to itself. Future RTC integration should only create transport connections for remote peers. Host-local actions should validate and apply through the same authoritative gameplay code path, then broadcast resulting state to remote clients. Client actions should send requests to peer `1`; the host validates, applies, and syncs the result.

Implementation rules:

- `PeerData.IsHost` should be `true` for peer `1` and `false` for remote clients.
- `Networking.IsHostAuthority` means this process can apply authoritative state locally. It is true for local-only mode and for the network host when its active multiplayer peer id is `1`.
- `Networking.IsRemoteClient` means this process should send authority requests to peer `1` instead of applying them locally.
- Shared sync methods may use Godot RPC `CallLocal` or explicit local apply helpers, but transport code must not create a host-to-host loopback connection.
- Gameplay and lobby code should stay transport-agnostic. Replacing ENet with Easy Networking + RTC later should not require changing the lobby/team/player ownership model.

## Runtime Network Debug UI

- The `Networking` autoload creates a small always-on-top network mode icon in the top-left corner for debug builds/runs.
- The icon reflects `NetworkMode.NotSelected`, `Local`, `Lan`, `Online`, or `Client` using SVG assets in `assets/network/networkmodes/`.
- The debug overlay shows the current peer count and accepted player count beside the network-mode icon so host/server lobby state is readable while developing.
- `SettingsConfig.ShowNetworkDebugOverlay` controls whether the network debug overlay is visible.
- The setting is exposed in the main menu Settings screen under the `Online` tab.
- A separate connection-lost icon is shown when a client connection fails or an already-connected client loses the server. This is a debug/display state exposed through `Networking.HasLostConnection`, not a separate `NetworkMode`.
- `ConnectionFailed` and `ServerDisconnected` from Godot multiplayer are the current signals used to detect failed or lost client connections.
- The overlay is skipped in headless runs.

## Settings Menu

- `scenes/ui/menus/settings_menu.tscn` is the current settings entry point from the main menu.
- `SettingsConfig` is the shared settings resource owned by the `Networking` autoload for now.
- `SettingsConfig.LoadOrCreate()` loads `user://settings_config.tres` or returns defaults, and `SettingsConfig.Save()` persists the current resource to the same path.
- The settings menu currently has placeholder tabs for `Video`, `Sound`, `Controls`, and `Gameplay`, plus an `Online` tab with the network debug overlay toggle and Apply button.

## Match Setup And Lobby

`MultiplayerData` describes the active synced match setup. It owns connected peers/devices, accepted match players, and setup config so the game can support pure local play, pure online play, hosted split-screen play, and split-screen clients joining online sessions through the same shared model.

Peers and players should be separate arrays. `PeerData` describes the connected device and its requested local-player capacity. `PlayerData` describes an accepted in-game player and links back to the device through `PeerId`.

Important identity rule: `PlayerData` is looked up by `(PeerId, LocalId)`, not by `GlobalId`. `LocalId` can repeat across peers, because every machine has its own local player `0`, `1`, `2`, etc. `PeerId` disambiguates which device that local player belongs to.

Real team ids currently run from `1` to `4`. Team `0` is treated as an auto-assign request, not a persistent gameplay team. Team resolution is peer-based for the current lobby model.

The match lobby shows a centered players section and a right-side config section. The network-mode debug overlay owns mode/peer/player summary display, so the match lobby should not duplicate that summary panel. Players are rendered through reusable `LobbyPlayerCard` scene instances and grouped into reusable `LobbyTeamContainer` scene instances for `Team 1` through `Team 4`. The team section uses a generic 2x2 grid so the default 16:9 lobby fits all four teams without vertical scrolling. Each team container represents the current 4-player-per-team cap with a horizontal row of four player slots. Occupied slots use player cards; empty slots use `LobbyEmptyPlayerSlot` scene instances and stay visible as open capacity.

When the host Start Match action is visually grayed out, it should remain clickable and show a popup explaining the blocking reason instead of silently doing nothing. Current blocking reasons include pending config changes, no selected mode, missing biomes, missing structures, or no game modes.

Lobby team UI should stay scene-driven for easier iteration in the Godot editor:

- `scenes/ui/lobby/lobby_team_container.tscn`: one team container, compact team label, small assign action, and player-slot row.
- `scenes/ui/lobby/lobby_player_card.tscn`: one occupied player slot.
- `scenes/ui/lobby/lobby_empty_player_slot.tscn`: one open player slot.
- `assets/ui/styles/lobby_*.tres`: reusable `StyleBoxFlat` resources for lobby panels, team containers, player cards, and empty slots. Put padding/content margins in these resources so containers do not hug their contents or surrounding edges.
- `assets/ui/start_match.svg`: start-match button icon.
- `assets/ui/config_connection.svg`, `config_biome.svg`, `config_structure.svg`, and `config_game.svg`: Match Config category/action icons.
- `assets/ui/biome_plains.svg`, `biome_arena.svg`, and `structure_arena.svg`: option icons used by the map setup selector overlay and selected map setup buttons.
- `assets/ui/styles/lobby_config_category.tres`, `lobby_apply_button.tres`, and `lobby_revert_button.tres`: Match Config section and action-button styles.

Do not rebuild team containers, player cards, or empty slots entirely in `MatchLobby.cs`; use the scenes above and keep `MatchLobby.cs` responsible for data binding and lobby actions.

Team visuals are centralized in `scripts/ui/TeamVisuals.cs`. The first shared team palette is:

- Team 1: red, `Color(0.95, 0.22, 0.26)`.
- Team 2: blue, `Color(0.20, 0.55, 1.00)`.
- Team 3: green, `Color(0.22, 0.78, 0.38)`.
- Team 4: amber, `Color(1.00, 0.72, 0.18)`.

Autofill is not rendered as a team container. It is a separate host lobby action with 2-team, 3-team, and 4-team options. Manual team assignment still uses team container assign buttons for a peer/device.

## LAN Host Port Behavior

- LAN/server hosting no longer hard-locks to a single fixed port.
- Hosting now starts at port `12000`, scans outward within `11000` through `13000`, and binds to the first available port.
- The selected port is written back into setup state and used for LAN discovery responses and direct joins.
- TODO later: allow choosing and preferring a specific port before falling back to the auto-increment scan.

## Setup Config Direction

Match setup should be resource-driven. `SetupConfig` owns the selected/available game modes, map generation settings, biome settings, player limits, address/port, and team behavior.

Game modes are represented as `GameModeConfig` resources in an array so multiple modes can be enabled for voting, rotation, quickmatch filtering, or future playlist logic. Map and biome setup are separate resources so procedural generation can grow without turning `SetupConfig` into a large flat object.

The match lobby config UI should edit these resources directly through grouped sections for internet settings, map/biome settings, and game settings. For the MVP lobby, map seeds are always random and the seed picker is intentionally hidden. `MapGenerationConfig` still keeps seed fields for later debug/custom-match flows, but the normal match lobby should normalize `SelectedSeedMode` to `AlwaysRandom`.

MVP map setup should stay intentionally narrow while the first playable slice is being chased:

- Structures: only `Arena` is exposed in the match lobby.
- Biomes: only `Plains` and `Arena` are exposed in the match lobby.
- The structure and biome enums should only contain implemented/actively targeted values. Add new enum values one at a time when the corresponding map generation/content work starts.
- The structure/biome selector overlay should show option icons, include `All` and `None` actions, and keep `Close` disabled until at least one option is selected.
- Match Config map option buttons show the category label above the icon and the selected value below it. They use the generic category icon for multi-selection/all states, and switch to the selected option icon when exactly one biome or structure is selected.
- The selector overlay action order is `Back`, `Clear`, `All`. `Clear` and game-mode playlist `Clear` use `assets/ui/reset_revert.svg`. `All` uses `assets/ui/select_all.svg`. Back-style overlay actions use `assets/ui/back_arrow.svg`.
- The main menu Reset Players action also uses `assets/ui/reset_revert.svg` so reset/remove-all actions share one visual language.

## Overlay UI

Overlay UI should be managed through a reusable `SceneOverlay` scene, and it is not an autoload in this project. Instead, game code should call `SceneOverlay.GetOrCreate(context)` so the overlay layer is created inside the current room/current scene only when needed.

`SceneOverlay` can add overlays from a `Control` instance or `PackedScene`, close the top overlay, close all overlays, and optionally enable a blur backdrop for any overlay, not just popup panels.

Join IP uses the reusable `SceneOverlay` blur backdrop when its address panel is open.

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
