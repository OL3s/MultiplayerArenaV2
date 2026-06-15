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
- Builds a simple square floor/wall room with one center barrel prop.
- Spawns players through the structure-driven `GameplaySpawnManager`; team assignment decides which team spawn tiles are used.
- Uses the same `DamageTestPlayer.GlobalId -> PlayerData` ownership lookup pattern as the old LAN player test path.
- Player movement is currently server-authoritative and intentionally simple: clients send movement input vectors when quantized movement state changes, only the host/server simulates movement, and clients directly apply server movement-state plus every-physics-tick moving-position updates without interpolation or local prediction.
- Player visuals use temporary SVG player body sprites in `assets/players/` plus the `Pistol-T1` item image in `assets/items/modern/weapons/`.
- Player held-item visuals now come from the selected modern item `.tres` resource's `HeldTexture`.
- The temporary `B` item grid displays each item's `ShowcaseTexture` when present and falls back to `HeldTexture` for older resources.
- The temporary `B` item grid also includes `Light Armor` and `Heavy Armor`; their buttons use store/showcase art, while selection applies the armor overlay texture on top of the player body.
- Planned next UI slice: add reusable `scenes/ui/player_stats_panel.tscn` and `scenes/ui/local_players_hud.tscn` so the test room can display name, avatar, kills, health, selected item, armor, weapon slots, gadget slots, loaded ammo, reload/refresh cooldowns, and empty slots for up to 4 local players.
- The item room uses a simplified armor-driven loadout model. Armor decides whether a second weapon is available, how many gadget slots are available, and which percentage multipliers apply to item-defined weapon reload and gadget refresh cooldowns.
- The old backstrap, inventory-provider, ammo-rig, and separate magazine-bucket model is intentionally not used.
- The selected map structure controls the test room area layout, team objective centers, team spawn tiles around each objective, and temporary item spawn marker positions through `StructureGenerationData`. `Arena` uses a fixed plus-shape layout; `Plains` uses a larger open layout; `Square` uses a simple square room for mode/test iteration.
- The player/item LAN test currently forces `Square`, starts one host/server and one client by default, auto-assigns two teams, and spawns the two players on opposite left/right team bases.
- Team bases use `scenes/gameplay/objectives/team_spawn_base_marker.tscn`. The scene is centered on the team objective/core, owns a larger spawn `Area2D`, owns a smaller objective `Area2D`, and packs the four labeled spawn platforms in a `+` around the core.
- Spawn platforms map to team-local player slots `1-4`, only show platforms for players currently on that team, and hide the whole team base marker when the team has no players.
- The room has one separate core neutral center objective from `scenes/gameplay/objectives/neutral_objective.tscn`. It owns a wider outer `Area2D` and a smaller inner `Area2D`; the inner area currently shows occupancy/contest state only. It does not award score; game modes should own scoring behavior. This mirrors the intended runtime contract: neutral objectives can exist in every game mode, even when ignored, and modes decide how or whether to use them.
- Future secondary neutral objectives should use the same neutral objective scene but be placed at spread-out structure-generated spots. These are candidate/random objective points for modes such as future hold-the-zone behavior, not active by default.
- Player death currently runs through a first respawn flow: 1-second dead timer, reset health/ammo/recovery, teleport to team spawn, 1-second immobilized invulnerable spawn state, then normal gameplay.
- The room detects team wipes through a first `TeamWiped` event/log hook. Actual game-mode-specific wipe behavior is still deferred.
- Players controlled by the local process show a yellow SVG arrow marker above the body and an `L#` label, where `#` is the backend local player id `0-3`.
- The scene now has a local-player debug aim indicator: transparent line, dotted line, and crosshair/circle whose radius comes from dynamic current accuracy and item-aware aim projection distance.
- Gun aim indicators are capped through item `AimDisplayRange` for readability when gameplay range extends beyond the screen, and stop at sampled collision so the player can see whether the aim line intersects an object. Throwable indicators project toward sampled collision or throw endpoint, using gamepad aim-vector strength for throw distance.

## LAN Player Item Room Controls

- Host/local player movement: keyboard `WASD` or arrow keys. Hold `Shift` to emit a half-strength movement vector for walking.
- Client player movement: keyboard `WASD` or arrow keys in each client window. Client `LocalId 0` uses keyboard/mouse input for this LAN test.
- Host/local aim: mouse direction from the player body.
- Client aim: mouse direction from the player body in each client window.
- Active aiming is separate from aim direction. Keyboard/mouse is actively aiming while `Ctrl` or right mouse button is held.
- The debug aim indicator only draws while actively aiming, and movement speed is multiplied by the selected item's `AimMoveSpeedMultiplier` while actively aiming.
- `B`: open or close the test item grid. Keyboard `B` and Xbox controller `B` both toggle it.
- Arrow keys, d-pad, left stick UI navigation, `Enter`, mouse click, or controller `A`: choose an item from the grid and equip it as the local player's current test item.
- Choosing an armor entry from the same grid equips that armor overlay on the local player instead of changing the held item.
- While the item grid is open, the local player's gameplay input is put in `PlayerControlState.Menu`, which stops movement, aim updates, and item use until the menu closes.
- Left mouse button or Xbox right trigger: use the selected item. Single-fire weapons and gadgets use once per press; full-auto weapons repeat while held after `RecoverySeconds`. Shootable weapons spawn `GenericBullet`, throwables spawn `GenericThrownItem`, and launcher weapons spawn `GenericLaunchedProjectile`. Weapon shots consume loaded ammo before execution, gadgets start their refresh timer when used, and unavailable items are rejected by the host/server.
- Reload input: starts the selected weapon's item-defined reload cooldown when the weapon is not full and not already reloading. Armor applies a percentage multiplier to that cooldown.
- Item-use sync includes the exact action direction. The acting player's held item is forced toward that direction for about half a second on other peers so shots and throws read from the same aim direction that executed the action.
- Thrown grenades now travel toward their full throw-distance target and bounce from sampled wall/prop/player collision instead of shortening the throw range to the first obstruction. The thrown visual has a ground shadow under the arc.
- Throwables can activate when they hit the ground through `ActivateOnGroundImpact`. The explosive grenade keeps fuse-timed behavior, while incendiary and smoke grenades currently activate on ground impact.

Current player/item test-scene follow-up:

- Tune and harden the generic bullet, thrown-item, and launched-projectile execution data.
- Add reload input handling for item-defined weapon reload cooldowns with armor multipliers.
- Add item-defined refresh timers for gadgets after use with armor multipliers.
- Add a reusable local player stats HUD scene stack and wire it to the player/item room test runtime.
- Improve the temporary equipment menu so weapon/gadget slot assignment is clearer.
- Replace temporary item spawn markers with real item pickup/spawn behavior.

## Example CLI Usage

Linux/Bash helper scripts live in `tools/testing/`. They are the preferred way to launch multiple local Godot instances during LAN testing.

Player/item room with one host/server and one client, one peer per test team:

```bash
./tools/testing/launch-player-item-room-lan.sh
```

Destruction room with one host/server and two clients:

```bash
./tools/testing/launch-destruction-lan.sh
```

Script defaults:

- `GODOT_BIN=godot`
- `ADDRESS=127.0.0.1`
- `PORT=12000`
- `CLIENTS=1` for `launch-player-item-room-lan.sh`.
- `CLIENTS=2` for `launch-destruction-lan.sh`.
- `START_DELAY=2`

Override any default inline:

```bash
CLIENTS=1 PORT=7800 START_DELAY=3 ./tools/testing/launch-player-item-room-lan.sh
```

The scripts write logs to `.tmp/test-logs/` and keep the terminal attached. Press `Ctrl+C` in that terminal to stop all spawned instances.

Add `--simple` after Godot's user-argument separator when launching manually to shorten `GameLog` lines:

```bash
godot --path . res://scenes/tests/test_player_item_room_lan.tscn -- --role host --simple
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
