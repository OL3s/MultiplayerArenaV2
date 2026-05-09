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

## LAN Player Item Room Controls

- Host/local player movement: keyboard `WASD` or arrow keys. Hold `Shift` to emit a half-strength movement vector for walking.
- Client player movement: gamepad left stick. Client `LocalId 0` uses gamepad device `0`.
- Host/local aim: mouse direction from the player body.
- Client aim: gamepad right stick. If the right stick is inside the aim deadzone, aim falls back to the current left-stick movement direction.
- `1`: set local player item override to `Pistol-T1`.
- `2`: set local player item override to `Smg-T1`.
- `3`: set local player item override to `AR-T1`.
- `4`: set local player item override to `Rifle-T1`.
- `5`: set local player item override to `NadeExplosive`.

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
