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

## LAN Destruction And Player Test

Scene: `Scenes/Tests/TestMapDestructionLogicLAN.tscn`

Script: `Scripts/Data/Map/TestMapDestructionLogicLAN.cs`

- Temporary LAN/RTC-focused test scene for server-authoritative wall destruction sync and damage-test player controls.
- Uses the same scene-local map flow as `TestMapDestructionLogic`, then forwards host damage/reset RPCs to connected clients.
- Includes a status label that shows waiting/connection state while testing host/client behavior.
- `Networking` owns the shared `--role` CLI override and maps it to `NetworkMode`; this scene reads `Networking.CurrentMode` instead of keeping a separate role enum.
- Supports scene-local CLI overrides through Godot user args: `--address` and `--port` for the direct client target.
- The host instance is the only peer allowed to apply shared damage input.
- The client instance is a read-only viewer that applies and re-renders scene-local RPC updates sent by the host.
- The LAN test scene currently focuses on live sync for already-connected peers. Start both peers first, then perform destruction tests from the host side.
- Initial map construction and late-join catch-up are still not fully synchronized yet. Those are deferred follow-up tasks, not part of the current networking slice.

## LAN Test Controls

- `1`: select `Crush` damage.
- `2`: select `Slash` damage.
- `3`: select `Heat` damage.
- `4`: select `Explosive` damage.
- Left click: damage the first target under the cursor. Priority is player, prop, then wall.
- `Shift + Left Click`: radius damage against players, props, and walls.
- Right click: rebuild/reset the arena and respawn player damage targets.
- Host/local player movement: keyboard `WASD` or arrow keys. Hold `Shift` to emit a half-strength movement vector for walking.
- Client player movement: gamepad left stick. Client `LocalId 0` uses gamepad device `0`; client `LocalId 1` uses gamepad device `1`.
- Host/local aim: mouse direction from the player body.
- Client aim: gamepad right stick. If the right stick is inside the aim deadzone, aim falls back to the current left-stick movement direction for controller convenience.

## LAN Test Runtime Notes

- The LAN test seeds real `LocalLobbyData` before hosting/joining.
- Host/local instances register one active local player.
- Client instances register two active local players to test multiple players on one peer.
- Runtime player targets are built from `Networking.MultiplayerData.Players`, keyed only by `GlobalId`, and resolve `PeerId`/`LocalId` through `MultiplayerData.GetPlayerByGlobalId(...)`.
- `DamageTestPlayer` is the temporary runtime player body for damage/control testing. On death, it disables hitbox monitoring, monitorable state, collision shape, processing, physics processing, and input processing. Respawn resets health, teleports back to test spawn, and re-enables those features.
- LAN test movement collision uses Godot physics bodies: `CharacterBody2D` players with circular collision, `StaticBody2D` props with circular collision, and `WallLayer` TileMap collision projected from `ArenaMapData.WallTiles`.
- LAN test movement is currently server-authoritative and intentionally simple: clients send movement input vectors when quantized movement state changes, only the host/server simulates movement, and clients directly apply server movement-state plus every-physics-tick moving-position updates without interpolation or local prediction.
- Player visuals use temporary SVG player body sprites in `Assets/Players/` plus the `Pistol-T1` item image in `Assets/Items/Modern/Weapons/`. The player body sprite flips only on its own X scale, switches to the back sprite only when aiming upward enough, and leaves the root/collision transform unchanged.
- Player aim display keeps separate local exact aim and replicated estimated aim vectors. When a player uses an item, shoots, throws, or performs another exact-aim action, remote displays should briefly show the real action aim for about one second, then return to the estimated aim vector used for normal movement/aim display.
- Spawn and respawn placement is not yet overlap-safe. `MoveAndSlide()` handles movement collision but should not be relied on to push a player out if it spawns inside a wall, prop, or another player; a follow-up should validate spawn positions with a circle physics query and choose a nearby free floor tile when blocked.

## Example CLI Usage

Host:

```bash
godot --path . res://Scenes/Tests/TestMapDestructionLogicLAN.tscn -- --role host
```

Client:

```bash
godot --path . res://Scenes/Tests/TestMapDestructionLogicLAN.tscn -- --role client --address 127.0.0.1 --port 7700
```

Launch one host and one client from the same terminal:

```bash
godot --path . res://Scenes/Tests/TestMapDestructionLogicLAN.tscn -- --role host & \
godot --path . res://Scenes/Tests/TestMapDestructionLogicLAN.tscn -- --role client --address 127.0.0.1 --port 7700 & \
disown
```

If the host log says it picked a port other than `7700`, use that port for the client. This can happen if another old test instance is still holding `7700`.

Supported shared role values are `local`, `lan`, `host`, `server`, `server-local`, `client`, `online`, `online-host`, and `server-online`.

## Runtime Logging Standards

- Multiplayer runtime prints should use a clear bracket prefix so multi-instance terminal output stays searchable.
- General networking logs use `[Multiplayer][Mode=<NetworkMode>] ...` and should include the current `NetworkMode` enum value.
- Scene-specific LAN destruction logs use `[LANDestructionTest][Mode=<NetworkMode>] ...`.
- Keep live gameplay prints short and event-based: host/client start, connect/fail/disconnect, peer count changes, and RPC send/apply events.
- Do not print every frame or every `_Process()` tick.
