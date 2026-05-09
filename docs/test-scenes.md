# Test Scenes

This document tracks current test scenes, test controls, launch commands, and runtime logging notes. Update this file when test scenes change.

## Destruction Logic Test

Scene: `Scenes/Tests/TestMapDestructionLogic.tscn`

Script: `Scripts/Data/Map/TestMapDestructionLogic.cs`

- Temporary root scene for destructible map backend testing.
- Creates mock floor hashset data, calls `ResetWallTiles()`, then applies sample wall-damage values after normal wall generation.
- Left-clicking a wall applies bullet-style single-tile damage from the current mouse world position.
- `Shift + Left Click` applies explosive area damage using the current mouse world position and a larger test radius.
- A debug radius is drawn under the mouse cursor so explosive sampling is visible while testing.
- Right-clicking resets the mock test arena.

## LAN Destruction Test

Scene: `Scenes/Tests/TestMapDestructionLogicLAN.tscn`

Script: `Scripts/Data/Map/TestMapDestructionLogicLAN.cs`

- Temporary LAN/RTC-focused test scene for server-authoritative wall destruction sync.
- Uses the same scene-local map flow as `TestMapDestructionLogic`, then forwards host damage/reset RPCs to connected clients.
- Includes a status label that shows waiting/connection state while testing host/client behavior.
- `Networking` owns the shared `--role` CLI override and maps it to `NetworkMode`; this scene reads `Networking.CurrentMode` instead of keeping a separate role enum.
- Supports scene-local CLI overrides through Godot user args: `--address` and `--port` for the direct client target.
- The host instance is the only peer allowed to apply shared damage input.
- The client instance is a read-only viewer that applies and re-renders scene-local RPC updates sent by the host.
- The LAN test scene currently focuses on live sync for already-connected peers. Start both peers first, then perform destruction tests from the host side.
- Initial map construction and late-join catch-up are still not fully synchronized yet. Those are deferred follow-up tasks, not part of the current networking slice.
- Player targets were removed from this scene. Use `Scenes/Tests/TestPlayerItemRoomLAN.tscn` for player movement, aim, and item/action testing.

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

Scene: `Scenes/Tests/TestPlayerItemRoomLAN.tscn`

Script: `Scripts/Data/Gameplay/TestPlayerItemRoomLAN.cs`

- Dedicated player/item/action LAN test scene.
- Builds a simple square floor/wall room with one center barrel prop.
- Spawns the host player on one side and the client player on the other side when both peers are connected.
- Uses the same `DamageTestPlayer.GlobalId -> PlayerData` ownership lookup pattern as the old LAN player test path.
- Player movement is currently server-authoritative and intentionally simple: clients send movement input vectors when quantized movement state changes, only the host/server simulates movement, and clients directly apply server movement-state plus every-physics-tick moving-position updates without interpolation or local prediction.
- Player visuals use temporary SVG player body sprites in `Assets/Players/` plus the `Pistol-T1` item image in `Assets/Items/Modern/Weapons/`.
- Player held-item visuals now come from the selected modern item `.tres` resource's `HeldTexture`.
- The scene now has a local-player debug aim indicator: transparent line, dotted line, and crosshair/circle whose radius comes from dynamic current accuracy and item-aware aim projection distance.
- Gun aim indicators are capped through item `AimDisplayRange` for readability when gameplay range extends beyond the screen, and stop at sampled collision so the player can see whether the aim line intersects an object. Throwable indicators project toward sampled collision or throw endpoint, using gamepad aim-vector strength for throw distance.

## LAN Player Item Room Controls

- Host/local player movement: keyboard `WASD` or arrow keys. Hold `Shift` to emit a half-strength movement vector for walking.
- Client player movement: gamepad left stick. Client `LocalId 0` uses gamepad device `0`.
- Host/local aim: mouse direction from the player body.
- Client aim: gamepad right stick. If the right stick is inside the aim deadzone, aim falls back to the current left-stick movement direction.
- Active aiming is separate from aim direction. Keyboard/mouse is actively aiming while `Ctrl` or right mouse button is held. Controller is actively aiming while the right stick is outside the aim deadzone.
- The debug aim indicator only draws while actively aiming, and movement speed is multiplied by the selected item's `AimMoveSpeedMultiplier` while actively aiming.
- `1`: set local player item override to `Pistol-T1`.
- `2`: set local player item override to `Smg-T1`.
- `3`: set local player item override to `AR-T1`.
- `4`: set local player item override to `Rifle-T1`.
- `5`: set local player item override to `NadeExplosive`.
- `6`: set local player item override to `Rocketlauncher`.
- `7`: set local player item override to `Grenadelauncher-T1`.
- `8`: set local player item override to `Grenadelauncher-T2`.
- `,` / `.`: cycle backward/forward through all modern item resources.
- `F`: cycle the selected item's available fire modes.
- `Space`: use the selected item through the selected fire mode and `RecoverySeconds`. Shootable weapons spawn `GenericBullet`, throwables spawn `GenericThrownItem`, and launcher weapons spawn `GenericLaunchedProjectile`.
- Thrown grenades now travel toward their full throw-distance target and bounce from sampled wall/prop/player collision instead of shortening the throw range to the first obstruction. The thrown visual has a ground shadow under the arc.
- Throwables can activate when they hit the ground through `ActivateOnGroundImpact`. The explosive grenade keeps fuse-timed behavior, while incendiary and smoke grenades currently activate on ground impact.

Current player/item test-scene follow-up:

- Tune and harden the generic bullet, thrown-item, and launched-projectile execution data.
- Keep `F` fire-mode cycling and selected item recovery behavior active while expanding item execution.

## Example CLI Usage

Host:

```bash
godot --path . res://Scenes/Tests/TestMapDestructionLogicLAN.tscn -- --role host
```

Client:

```bash
godot --path . res://Scenes/Tests/TestMapDestructionLogicLAN.tscn -- --role client --address 127.0.0.1 --port 7700
```

Launch destruction host and client:

```bash
godot --path . res://Scenes/Tests/TestMapDestructionLogicLAN.tscn -- --role host
godot --path . res://Scenes/Tests/TestMapDestructionLogicLAN.tscn -- --role client --address 127.0.0.1 --port 7700
```

Launch player item room host and client:

```bash
godot --path . res://Scenes/Tests/TestPlayerItemRoomLAN.tscn -- --role host
godot --path . res://Scenes/Tests/TestPlayerItemRoomLAN.tscn -- --role client --address 127.0.0.1 --port 7700
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
