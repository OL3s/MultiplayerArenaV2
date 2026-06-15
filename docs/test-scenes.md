# Test Scenes

This document tracks current test scenes, test controls, launch commands, and runtime logging notes. Update this file when test scenes change.

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
- Spawns the host player on one side and the client player on the other side when both peers are connected.
- Uses the same `DamageTestPlayer.GlobalId -> PlayerData` ownership lookup pattern as the old LAN player test path.
- Player movement is currently server-authoritative and intentionally simple: clients send movement input vectors when quantized movement state changes, only the host/server simulates movement, and clients directly apply server movement-state plus every-physics-tick moving-position updates without interpolation or local prediction.
- Player visuals use temporary SVG player body sprites in `assets/players/` plus the `Pistol-T1` item image in `assets/items/modern/weapons/`.
- Player held-item visuals now come from the selected modern item `.tres` resource's `HeldTexture`.
- The temporary `B` item grid displays each item's `ShowcaseTexture` when present and falls back to `HeldTexture` for older resources.
- The temporary `B` item grid also includes `Light Armor` and `Heavy Armor`; their buttons use store/showcase art, while selection applies the armor overlay texture on top of the player body.
- Planned next UI slice: add reusable `scenes/ui/player_stats_panel.tscn` and `scenes/ui/local_players_hud.tscn` so the test room can display name, avatar, kills, health, selected item, armor, weapon slots, gadget slots, remaining uses, and empty slots for up to 4 local players.
- The item room uses a simplified armor-driven loadout model. Armor decides whether a second weapon is available, how many gadget slots are available, how many weapon magazines are granted, and how many uses each gadget gets.
- The old backstrap, inventory-provider, ammo-rig, and separate magazine-bucket model is intentionally not used.
- The scene now has a local-player debug aim indicator: transparent line, dotted line, and crosshair/circle whose radius comes from dynamic current accuracy and item-aware aim projection distance.
- Gun aim indicators are capped through item `AimDisplayRange` for readability when gameplay range extends beyond the screen, and stop at sampled collision so the player can see whether the aim line intersects an object. Throwable indicators project toward sampled collision or throw endpoint, using gamepad aim-vector strength for throw distance.

## LAN Player Item Room Controls

- Host/local player movement: keyboard `WASD` or arrow keys. Hold `Shift` to emit a half-strength movement vector for walking.
- Client player movement: gamepad left stick. Client `LocalId 0` uses gamepad device `0`.
- Host/local aim: mouse direction from the player body.
- Client aim: gamepad right stick. If the right stick is inside the aim deadzone, aim falls back to the current left-stick movement direction.
- Active aiming is separate from aim direction. Keyboard/mouse is actively aiming while `Ctrl` or right mouse button is held. Controller is actively aiming while the right stick is outside the aim deadzone.
- The debug aim indicator only draws while actively aiming, and movement speed is multiplied by the selected item's `AimMoveSpeedMultiplier` while actively aiming.
- `B`: open or close the test item grid. Keyboard `B` and Xbox controller `B` both toggle it.
- Arrow keys, d-pad, left stick UI navigation, `Enter`, mouse click, or controller `A`: choose an item from the grid and equip it as the local player's current test item.
- Choosing an armor entry from the same grid equips that armor overlay on the local player instead of changing the held item.
- While the item grid is open, the local player's gameplay input is put in `PlayerControlState.Menu`, which stops movement, aim updates, and item use until the menu closes.
- Left mouse button or Xbox right trigger: use the selected item. Single-fire weapons and gadgets use once per press; full-auto weapons repeat while held after `RecoverySeconds`. Shootable weapons spawn `GenericBullet`, throwables spawn `GenericThrownItem`, and launcher weapons spawn `GenericLaunchedProjectile`. Weapon/gadget uses are consumed before execution and empty items are rejected by the host/server.
- Item-use sync includes the exact action direction. The acting player's held item is forced toward that direction for about half a second on other peers so shots and throws read from the same aim direction that executed the action.
- Thrown grenades now travel toward their full throw-distance target and bounce from sampled wall/prop/player collision instead of shortening the throw range to the first obstruction. The thrown visual has a ground shadow under the arc.
- Throwables can activate when they hit the ground through `ActivateOnGroundImpact`. The explosive grenade keeps fuse-timed behavior, while incendiary and smoke grenades currently activate on ground impact.

Current player/item test-scene follow-up:

- Tune and harden the generic bullet, thrown-item, and launched-projectile execution data.
- Keep `F` fire-mode cycling and selected item recovery behavior active while expanding item execution.
- Add a reusable local player stats HUD scene stack and wire it to the player/item room test runtime.
- Improve the temporary equipment menu so weapon/gadget slot assignment is clearer.

## Example CLI Usage

Linux/Bash helper scripts live in `tools/testing/`. They are the preferred way to launch multiple local Godot instances during LAN testing.

Player/item room with one host/server and two clients:

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
- `PORT=7700`
- `CLIENTS=2`
- `START_DELAY=2`

Override any default inline:

```bash
CLIENTS=3 PORT=7800 START_DELAY=3 ./tools/testing/launch-player-item-room-lan.sh
```

The scripts write logs to `.tmp/test-logs/` and keep the terminal attached. Press `Ctrl+C` in that terminal to stop all spawned instances.

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
godot --path . res://scenes/tests/test_map_destruction_logic_lan.tscn -- --role client --address 127.0.0.1 --port 7700
```

Launch destruction host and client:

```bash
godot --path . res://scenes/tests/test_map_destruction_logic_lan.tscn -- --role host
godot --path . res://scenes/tests/test_map_destruction_logic_lan.tscn -- --role client --address 127.0.0.1 --port 7700
```

Launch player item room host and client:

```bash
godot --path . res://scenes/tests/test_player_item_room_lan.tscn -- --role host
godot --path . res://scenes/tests/test_player_item_room_lan.tscn -- --role client --address 127.0.0.1 --port 7700
```

If the host log says it picked a port other than `7700`, use that port for the client. This can happen if another old test instance is still holding `7700`.

Supported shared role values are `local`, `lan`, `host`, `server`, `server-local`, `client`, `online`, `online-host`, and `server-online`.

## Runtime Logging Standards

- Multiplayer runtime prints should use a clear bracket prefix so multi-instance terminal output stays searchable.
- General networking logs use `[Multiplayer][Mode=<NetworkMode>] ...` and should include the current `NetworkMode` enum value.
- Scene-specific LAN destruction logs use `[LANDestructionTest][Mode=<NetworkMode>] ...`.
- Scene-specific player item room logs use `[PlayerItemRoomTest][Mode=<NetworkMode>] ...`.
- Keep live gameplay prints short and event-based: host/client start, connect/fail/disconnect, peer count changes, and RPC send/apply events.
- Do not print every frame or every `_Process()` tick.
