# Test Scenes

This document tracks current test scenes, test controls, launch commands, and runtime logging notes. Update this file when test scenes change.

## Main Menu Test Scene Launcher

- The main menu has a top-right test-scenes icon button to the left of Settings.
- The button opens `scenes/ui/overlays/test_scenes_overlay.tscn`.
- `TestScenesOverlay` scans `res://scenes/tests` recursively at runtime and creates one launch button for each `.tscn` file it finds, so the launcher updates automatically when test scenes are added or removed. Button labels use the raw `.tscn` filename because this is a developer launcher.
- Main-menu keyboard player join uses the `C` key and `assets/inputicons/keyboard/key_c.svg` instead of Enter, leaving Enter/Space free for standard UI button activation and arrow-key navigation.
- Empty-card join prompts rotate every 2 seconds across currently available join inputs: keyboard `C`, gamepad `X`, and touch.
- Touching or clicking the visible empty player card in the main menu joins one local touchscreen player using `LocalPlayerData.LocalInputType.Touch` and `assets/inputicons/device_touch.svg`. Main menu lobby API guards allow at most one keyboard/mouse player and at most one touch player.
- Local-only match lobby mode does not open a network peer or bind a server port. Its lobby UI hides connection settings, keeps map/game Match Config editable, and exposes `FFA`/`TEAM` local player team assignment buttons.
- Non-local host lobby mode exposes `Autofill 2 Teams`, `Autofill 3 Teams`, and `Autofill 4 Teams` actions. Autofill keeps all players from the same network peer/device together on one team.
- Match Config groups biome, structure, and item theme in the Map section. It groups game mode and loadout mode in the Game section. Loadout mode now includes first-pass Credit behavior for `BuyOnSpawn` and `PersistentBudget`; random, mirror, and map-pickup runtime behavior is still follow-up work.

## Destruction Logic Test

Scene: `scenes/tests/test_map_destruction_logic.tscn`

Script: `scripts/data/map/TestMapDestructionLogic.cs`

- Temporary root scene for destructible map backend testing.
- Creates mock floor hashset data, calls `ResetWallTiles()`, then applies sample wall-damage values after normal wall generation.
- Left-clicking a wall applies bullet-style single-tile damage from the current mouse world position.
- `Shift + Left Click` applies explosive area damage using the current mouse world position and a larger test radius.
- A debug radius is drawn under the mouse cursor so explosive sampling is visible while testing.
- Right-clicking resets the mock test arena.

## LAN Destruction Test

Scene: `scenes/tests/test_map_destruction_logic_lan.tscn`

Script: `scripts/data/map/TestMapDestructionLogicLAN.cs`

- Temporary LAN/RTC-focused test scene for server-authoritative wall destruction sync.
- Uses the same scene-local map flow as `TestMapDestructionLogic`, then forwards host damage/reset RPCs to connected clients.
- Includes a status label that shows waiting/connection state while testing host/client behavior.
- `Networking` owns the shared `--role` CLI override and maps it to `NetworkMode`; this scene reads `Networking.CurrentMode` instead of keeping a separate role enum.
- Supports scene-local CLI overrides through Godot user args: `--address` and `--port` for the direct client target.
- The host instance is the only peer allowed to apply shared damage input.
- The client instance is a read-only viewer that applies and re-renders scene-local RPC updates sent by the host.
- The LAN test scene currently focuses on live sync for already-connected peers. Start both peers first, then perform destruction tests from the host side.
- Initial map construction and late-join catch-up are still not fully synchronized yet. Those are deferred follow-up tasks, not part of the current networking slice.
- Player targets were removed from this scene. Use `scenes/tests/test_player_item_room_lan.tscn` for player movement, aim, and item/action testing.

## LAN Test Controls

- `1`: select `Crush` damage.
- `2`: select `Slash` damage.
- `3`: select `Heat` damage.
- `4`: select `Explosive` damage.
- Left click: damage the first prop or wall target under the cursor. Priority is prop, then wall.
- `Shift + Left Click`: radius damage against props and walls.
- Right click: rebuild/reset the arena.

## LAN Test Runtime Notes

- The LAN test seeds real `LocalLobbyData` before hosting/joining.
- Destruction LAN test currently spawns props and destructible walls only.

## LAN Player Item Room Test

Scene: `scenes/tests/test_player_item_room_lan.tscn`

Script: `scripts/data/gameplay/TestPlayerItemRoomLAN.cs`

- Dedicated player/item/action LAN test scene.
- Uses `TestPlayerItemRoomLAN.cs` as a thin test wrapper around the shared `scripts/data/gameplay/ArenaMatch.cs` runtime. The wrapper enables direct LAN-test bootstrap plus square/fixed-seed test overrides; lobby-started gameplay uses `scenes/gameplay/arena_match.tscn` directly.
- Builds a simple square floor/wall room with one center barrel prop.
- Spawns players through the structure-driven `GameplaySpawnManager`; team assignment decides which team spawn tiles are used.
- Uses the same `DamageTestPlayer.GlobalId -> PlayerData` ownership lookup pattern as the old LAN player test path.
- Player movement is currently server-authoritative and intentionally simple: clients send movement input vectors when quantized movement state changes, only the host/server simulates movement, and clients directly apply server movement-state plus every-physics-tick moving-position updates without interpolation or local prediction.
- Player visuals use temporary SVG player body sprites in `assets/players/` plus the selected/default item image from the active item theme library.
- Player held-item visuals now come from the selected modern item `.tres` resource's `HeldTexture`.
- The `B` debug buy/equip grid displays each item's `ShowcaseTexture` when present and falls back to `HeldTexture` for older resources.
- The `B` debug buy/equip grid also includes `Light Armor` and `Heavy Armor`; their buttons use store/showcase art, while selection applies the armor overlay texture on top of the player body.
- The old `B` debug buy/equip grid is enabled only by direct test wrapper scenes through `ArenaMatch.EnableDebugBuyMenu`. Lobby-started `scenes/gameplay/arena_match.tscn` uses the radial buy menu instead.
- Planned next UI slice: add reusable `scenes/ui/player_stats_panel.tscn` and `scenes/ui/local_players_hud.tscn` so the test room can display name, avatar, kills, health, selected item, armor, weapon slots, gadget slots, loaded ammo, reload/recovery cooldowns, and empty slots for up to 4 local players.
- The item room uses a simplified armor-driven loadout model. Armor decides whether a second weapon is available, how many gadget slots are available, and which percentage multipliers apply to item-defined weapon reload duration and gadget reload recovery.
- Lobby item theme selection is a candidate pool. Match start resolves one active item theme, and only that selected theme library populates the player default weapon and buy menus. Modern defaults to the intentionally weak `pistol_t0`; medieval defaults to `bow_t0` when selected.
- The old backstrap, inventory-provider, ammo-rig, and separate magazine-bucket model is intentionally not used.
- The selected map structure controls the test room area layout, team objective centers, team spawn tiles around each objective, and temporary item spawn marker positions through `StructureGenerationData`. `Arena` uses a fixed plus-shape layout; `Plains` uses a larger open layout; `Square` uses a simple square room for mode/test iteration.
- The player/item LAN test currently forces `Square`, starts one host/server and one client by default, auto-assigns two teams, and spawns the two players on opposite left/right team bases.
- Team bases use `scenes/gameplay/objectives/team_spawn_base_marker.tscn`. The scene is centered on the team objective/core, owns a larger spawn `Area2D`, owns a smaller objective `Area2D`, and packs the four labeled spawn platforms in a `+` around the core.
- Spawn platforms map to team-local player slots `1-4`, only show platforms for players currently on that team, and hide the whole team base marker when the team has no players.
- The room has one separate core neutral center objective from `scenes/gameplay/objectives/neutral_objective.tscn`. It owns a wider outer `Area2D` and a smaller inner `Area2D`; the inner area currently shows occupancy/contest state only. It does not award score; game modes should own scoring behavior. This mirrors the intended runtime contract: neutral objectives can exist in every game mode, even when ignored, and modes decide how or whether to use them.
- Future secondary neutral objectives should use the same neutral objective scene but be placed at spread-out structure-generated spots. These are candidate/random objective points for modes such as future hold-the-zone behavior, not active by default. The main center neutral objective is a separate single core objective; loadout modes such as `MapPickups` should only use inactive secondary neutral objectives for item spawns, never the center core objective.
- Player death currently runs through a first respawn flow: 1-second dead timer, reset health/ammo/fire interval, teleport to team spawn, 1-second immobilized invulnerable spawn state, then normal gameplay.
- The room detects team wipes through a first `TeamWiped` event/log hook. Actual game-mode-specific wipe behavior is still deferred.
- Players controlled by the local process show a yellow SVG arrow marker above the body and an `L#` label, where `#` displays the backend local player id `0-3` as `1-4`.
- Player/item LAN tests instantiate `scenes/ui/hud/local_players_hud.tscn` in the bottom-left corner. It shows up to four local player pill cards left to right with local id, status, health, selected item ammo pips, and gadget summary. Networked tests wrap the local cards in a team-colored container with the display team id on the right.
- The scene now has a local-player debug aim indicator: transparent line, dotted line, and crosshair/circle whose radius comes from dynamic current accuracy and item-aware aim projection distance.
- Gun aim indicators are capped through item `AimDisplayRange` for readability when gameplay range extends beyond the screen, and stop at sampled collision so the player can see whether the aim line intersects an object. Throwable indicators project toward sampled collision or throw endpoint, using gamepad aim-vector strength for throw distance.

## LAN Player Item Room Controls

- Host/local player movement: keyboard `WASD` or arrow keys. Hold `Shift` to emit a half-strength movement vector for walking.
- Client player movement: keyboard `WASD` or arrow keys in each client window. Client `LocalId 0` uses keyboard/mouse input for this LAN test.
- Host/local aim: mouse direction from the player body.
- Client aim: mouse direction from the player body in each client window.
- Active aiming is separate from aim direction. Keyboard/mouse toggles the local player between `PlayerControlState.Gameplay` and `PlayerControlState.Aim` on each `Ctrl` or right mouse button press; the toggle is ignored while in menu/spawn states. Gamepad active aiming is still driven by holding the right stick outside the aim deadzone.
- The debug aim indicator only draws while actively aiming, and movement speed is multiplied by the selected item's `AimMoveSpeedMultiplier` while actively aiming.
- `V` or Xbox controller `Y`: open or close the scene-backed radial buy menu around the first local player. The first ring contains the host-resolved active theme's configured buy groups plus `Cancel`; leaf buy group rings list purchasable items for that selected theme plus `Back`.
- `B`: open or close the debug buy/equip grid. Keyboard `B` and Xbox controller `B` both toggle it.
- `Tab` or Xbox controller select/back: toggle the compact scoreboard overlay with player ids, peer/local ownership, team, score, kills, deaths, and assists. Scoreboard player pills are tinted by team and local-device players use a stronger white outline for accessibility.
- `Esc` on the host/server: open the host-only server actions overlay. `Next Game Mode` advances the playlist and resolves fresh random match setup values before reloading gameplay; `Restart Current Match` reloads the room with the current resolved setup and seed; `Back To Main Menu` closes the server/session, clears multiplayer state, and loads the main menu.
- `R` or Xbox controller `X`: start reloading the selected weapon when it is not full and not already reloading.
- The scoreboard uses editable HUD scenes: `scenes/ui/hud/scoreboard_overlay.tscn` and `scenes/ui/hud/scoreboard_player_row.tscn`.
- The objective state is shown through `scenes/ui/hud/objective_status_hud.tscn` at the top of the screen, with panel color changing for neutral, contested, or team-owned states.
- Player HUD pills use their own overlay layer for local player state prompts such as `DEAD`, `SPAWNING`, `RELOAD`, `RELOADING`, and recovery `COOLDOWN`, instead of showing those prompts in the global status label. The base status label is also color-coded so `ALIVE`, `SPAWN`, and `RELOAD` are visible when no overlay covers the card.
- The buy menu is scene-backed by `scenes/ui/buy/player_buy_radial_menu.tscn` and `scenes/ui/buy/buy_radial_segment.tscn`. It anchors around one local player and uses nested group/item rings instead of a global screen-blocking overlay.
- Buy group hierarchy comes from `ItemThemeDefinition.BuyMenuGroups` and `ItemBuyMenuGroup` resources under `assets/items/themes/buy_groups/`. Buy group/action SVGs live under `assets/ui/buy/`; item entries use item showcase art.
- Radial item entries show cost and current Credits from `LoadoutModeConfig.StartingCredits` in `BuyOnSpawn` and `PersistentBudget`. Entries the player cannot afford are disabled, and selection is rejected if the player cannot afford the item at confirmation time. `BuyOnSpawn` currently awards `CreditsPerKill` on player kills and `CreditsPerSpawn` when respawn finishes; `PersistentBudget` is finite and does not award kill/spawn Credits. `RandomOnRespawn`, `MirrorLoadout`, and `MapPickups` do not use Credits for affordability. Cancel entries use a red-tinted segment style.
- In lobby-started `arena_match.tscn`, keyboard `B` also opens the radial buy menu. The radial buy menu can only open and accept purchases while the local player is inside the wide spawn range of that player's team spawn/base marker.
- Arrow keys, d-pad, left stick UI navigation, mouse direction, `Enter`, mouse click, or controller `A`: choose an item from the radial buy menu or debug grid and equip it as the local player's current item.
- Choosing an armor entry from the radial buy menu or debug grid equips that armor overlay on the local player instead of changing the held item.
- While either buy UI is open, the local player's gameplay input is put in `PlayerControlState.Menu`, which stops movement, aim updates, and item use until the menu closes.
- Left mouse button or Xbox right trigger: use the selected item. Single-fire weapons and gadgets use once per press; full-auto weapons repeat according to `ShotsPerSecond`. Shootable weapons spawn `GenericBullet`, throwables spawn `GenericThrownItem`, and launcher weapons spawn `GenericLaunchedProjectile`. Weapon shots consume loaded ammo before execution, gadgets use `ReloadRecoverySeconds` as their reload recovery timer, and unavailable items are rejected by the host/server.
- Reload input: starts the selected weapon's item-defined reload timer when the weapon is not full, not already reloading, and not in reload recovery. Armor applies `WeaponReloadTimeMultiplier` to reload time. Reload completion starts `ReloadRecoverySeconds` with `WeaponReloadRecoveryMultiplier`, which blocks the next reload but does not block firing.
- Item-use sync includes the exact action direction. The acting player's held item is forced toward that direction for about half a second on other peers so shots and throws read from the same aim direction that executed the action.
- Thrown grenades now travel toward their full throw-distance target and bounce from sampled wall/prop/player collision instead of shortening the throw range to the first obstruction. The thrown visual has a ground shadow under the arc.
- Throwables can activate when they hit the ground through `ActivateOnGroundImpact`. The explosive grenade keeps fuse-timed behavior, while incendiary and smoke grenades currently activate on ground impact.

Current player/item test-scene follow-up:

- Tune and harden the generic bullet, thrown-item, and launched-projectile execution data.
- Add reload input handling for item-defined weapon reload and recovery timers with armor multipliers.
- Add item-defined reload recovery timers for gadgets after use with armor multipliers.
- Add a reusable local player stats HUD scene stack and wire it to the player/item room test runtime.
- Improve weapon/gadget slot assignment feedback in the HUD and buy flow.
- Replace temporary item spawn markers with real item pickup/spawn behavior.

## Example CLI Usage

Linux/Bash helper scripts live in `tools/testing/`. They are the preferred way to launch multiple local Godot instances during LAN testing.

Player/item room with one host/server and one client, one peer per test team:

```bash
./tools/testing/launch-player-item-room-lan.sh
```

Mode-specific square LAN tests also use one host/server and one client:

```bash
./tools/testing/launch-deathmatch-square-lan.sh
./tools/testing/launch-capture-the-flag-square-lan.sh
./tools/testing/launch-king-of-the-hill-square-lan.sh
./tools/testing/launch-headquarters-square-lan.sh
```

These scripts open:

- `scenes/tests/test_deathmatch_square_lan.tscn`
- `scenes/tests/test_capture_the_flag_square_lan.tscn`
- `scenes/tests/test_king_of_the_hill_square_lan.tscn`
- `scenes/tests/test_headquarters_square_lan.tscn`

Destruction room with one host/server and two clients:

```bash
./tools/testing/launch-destruction-lan.sh
```

Script defaults:

- `GODOT_BIN=godot`
- `ADDRESS=127.0.0.1`
- `PORT=12000`
- `CLIENTS=1` for `launch-player-item-room-lan.sh`.
- `CLIENTS=1` for each mode-specific square LAN launcher.
- `CLIENTS=2` for `launch-destruction-lan.sh`.
- `START_DELAY=2`

Override any default inline:

```bash
CLIENTS=1 PORT=7800 START_DELAY=3 ./tools/testing/launch-player-item-room-lan.sh
```

The scripts write logs to `.tmp/test-logs/` and keep the terminal attached. Press `Ctrl+C` in that terminal to stop all spawned instances.

`GameLog` lines are short by default. Add `--verbose` after Godot's user-argument separator when launching manually to include sequence, timestamp, process id, peer id, source location, and mode context:

```bash
godot --path . res://scenes/tests/test_player_item_room_lan.tscn -- --role host --verbose
```

Use the general tools for import and startup verification:

```bash
./tools/import-assets.sh
./tools/verify-startup.sh
```

Host:

```bash
godot --path . res://scenes/tests/test_map_destruction_logic_lan.tscn -- --role host
```

Client:

```bash
godot --path . res://scenes/tests/test_map_destruction_logic_lan.tscn -- --role client --address 127.0.0.1 --port 12000
```

Launch destruction host and client:

```bash
godot --path . res://scenes/tests/test_map_destruction_logic_lan.tscn -- --role host
godot --path . res://scenes/tests/test_map_destruction_logic_lan.tscn -- --role client --address 127.0.0.1 --port 12000
```

Launch player item room host and client:

```bash
godot --path . res://scenes/tests/test_player_item_room_lan.tscn -- --role host
godot --path . res://scenes/tests/test_player_item_room_lan.tscn -- --role client --address 127.0.0.1 --port 12000
```

If the host log says it picked a port other than `12000`, use that port for the client. This can happen if another old test instance is still holding `12000`.

Supported shared role values are `local`, `lan`, `host`, `server`, `server-local`, `client`, `online`, `online-host`, and `server-online`.

## Runtime Logging Standards

- Runtime logs should use the shared `GameLog` API and the format documented in `docs/game-logging.md`.
- Logs include sequence, timestamp, process id, role, network mode, peer id, scope, type, and event name so host/client output remains readable when multiple Godot instances write into the same terminal.
- Scene-specific LAN destruction logs use `GameLogScope.DestructibleMap` for the existing destruction test events.
- Scene-specific player item room logs use `GameLogScope.PlayerItemRoom` for room lifecycle, player spawn/remove, input state changes, item/armor equip, item use, and projectile spawn events.
- Keep live gameplay prints short and event-based: host/client start, connect/fail/disconnect, peer count changes, and RPC send/apply events.
- Do not print every frame or every `_Process()` tick.
