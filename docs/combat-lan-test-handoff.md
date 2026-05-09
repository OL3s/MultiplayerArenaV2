# Combat And LAN Test Handoff

This document is the current handoff for the shared combat backend, destructible walls, level props, and LAN damage-test player targets.

## Current Branch

- Current branch after the last merge is `feature/player-controls-actions`.
- `main` already contains the completed shared damage resources, destructible props, and LAN player damage target commits.
- The next planned work is player controls/actions on top of the LAN test player/runtime model.

## Shared Combat Backend

Core files live in `Scripts/Data/Combat/`.

- `DamageType`: `Crush`, `Slash`, `Heat`, `Explosive`.
- `StatusEffectType`: currently only `Fire`.
- `DamageResource`: authored damage payload. It contains both typed damage values and typed status effect values.
- `DamageContainer`: runtime/apply wrapper around a `DamageResource`.
- `ArmorResource`: typed damage and status-effect reduction percentages.
- `HealthContainer`: default `100/100` health, owns armor and active status effects, and exposes `ApplyDamage()` / `TakeDamage()`.

Damage should flow through the same path for players, props, and walls:

```text
DamageContainer -> HealthContainer.ApplyDamage() -> ArmorResource reductions -> CurrentHealth
```

Do not duplicate damage math in player/prop/wall code. Add new behavior around the container system instead.

## Player Identity Model

`PlayerData` is persistent match/network identity and is stored under `Networking.MultiplayerData.Players`.

- `GlobalId` is the match-wide gameplay id.
- `PeerId` is the network peer/device owner.
- `LocalId` is the local player slot on that peer/device.
- `DisplayName` is display-only.
- `IsLocalPlayer` is resolved per running instance.

Runtime player objects should keep only `GlobalId` as their identity key. When runtime code needs peer/local/display data, look it up with:

```csharp
var playerData = Networking.MultiplayerData.GetPlayerByGlobalId(GlobalId);
```

This keeps spawned player bodies independent from lobby/network ownership details while still allowing server/client ownership checks later.

## LAN Damage Test Scene

Scene: `Scenes/Tests/TestMapDestructionLogicLAN.tscn`

Script: `Scripts/Data/Map/TestMapDestructionLogicLAN.cs`

The LAN test currently covers:

- Server-authoritative wall damage replication.
- Shared combat damage against wall tiles, props, and player targets.
- Damage type selection with number keys.
- Radius damage falloff, strongest at the explosion center.
- Real `PlayerData` registration through the `Networking` autoload instead of hardcoded mock player ids.
- Quantized player movement and aim tests through `GlobalId -> PlayerData -> LocalPlayerData` ownership/input resolution.

Controls:

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
- Client aim: gamepad right stick. If the right stick is inside the aim deadzone, aim falls back to the current left-stick movement direction for controller convenience. If both sticks are idle, aim keeps the previous valid direction instead of hiding the weapon. Aim is displayed immediately on the local client for responsiveness.

The LAN test seeds local lobby data before hosting/joining:

- Host/local instances register one active keyboard/mouse local player: `LocalId 0`.
- Client instances register two active gamepad local players: `LocalId 0` on device `0` and `LocalId 1` on device `1`.
- The client still joins through the real join flow, so the server assigns `GlobalId`s and syncs `PlayerData` back.

Expected player mapping with one host and one client:

- Host player: `P0 peer 1:local 0`.
- Client first local player: `P1 peer <clientPeer>:local 0`.
- Client second local player: `P2 peer <clientPeer>:local 1`.

The status label shows player health and ownership mapping using `GlobalId -> PlayerData -> PeerId/LocalId`.

Current hitbox/collision/input test structure:

- `DamageTestPlayer` is a `CharacterBody2D` with a direct circular `CollisionShape2D`. Click damage still uses its circular radius, and movement collision is resolved by Godot physics through `MoveAndSlide()`.
- `DamageTestPlayer` creates a simple child `Line2D` named `Weapon`. The weapon is offset from the body and rotated toward the active aim display vector.
- `DamageTestPlayer` stores separate local and estimated aim vectors. Owned/local players display their locally calculated exact aim immediately. Remote/non-owned player display uses the replicated quantized estimated aim.
- `LevelProp` is a `StaticBody2D` with a direct circular `CollisionShape2D` derived from `LevelPropData.Size`. Click damage still uses the same circular prop radius.
- Wall damage is tile-data authoritative: world positions are converted to `Vector2I` tile coordinates and checked against `ArenaMapData` wall tiles. Wall tiles currently behave as full `16x16` damage cells.
- LAN test movement uses Godot physics bodies: players are `CharacterBody2D`, props are `StaticBody2D`, and wall movement collision comes from the rendered `WallLayer` `TileMapLayer`. `ArenaTileLayerRenderer` adds a runtime physics layer/polygon to the loaded wall `TileSet`, while `ArenaMapData.WallTiles` remains the gameplay authority for wall damage/destruction checks.
- Local input first resolves to generic movement and aim vectors, then those vectors are quantized into 16 direction buckets and three strength states: `None`, `Some`, and `Full`. Keyboard/mouse and gamepad only differ at the vector-read step.
- Movement state changes only replicate when the direction bucket or strength state changes. Direction and strength use hysteresis so analog stick input does not flicker rapidly at bucket/threshold edges. The host/server simulates movement from the latest state instead of receiving movement every physics tick.
- Aim state changes replicate independently from movement state changes. Aiming does not force movement updates. For gamepad players with no active right-stick aim, the aim state follows movement-state direction/strength changes.
- Local aim display is allowed to be more exact than replicated aim state. Future shoot/throw actions should send their exact aim vector at action time and can use that exact vector to update the acting object's local aim display.
- Client movement and aim input are sent to the host/server as state-change requests. The host validates ownership by comparing the requested player's `PeerId` with the RPC sender and then syncs the accepted state back to clients.
- Position correction is not applied on every movement-state change. Client position is sent with movement changes as a drift hint, and the server only includes a correction when the difference is currently over `48px`. Future shot/throw actions should send their exact aim vector or coordinate at action time instead of relying only on the quantized display aim.

## Damage Test Player Runtime

File: `Scripts/Data/Gameplay/DamageTestPlayer.cs`

`DamageTestPlayer` is a temporary LAN-test runtime player body for combat and upcoming controls work.

- It owns `GlobalId` and a `HealthContainer`.
- It draws a simple body, health bar, and label.
- It is a `CharacterBody2D` with a circular `CollisionShape2D`.
- On death it disables hitbox monitoring, monitorable state, collision shape, processing, physics processing, and input processing.
- Dead players ignore further damage.
- `Respawn(worldPosition)` resets health, restores alive state, re-enables disabled features, and moves the body to a spawn/test position.

For actual gameplay, keep this principle:

```text
PlayerData persists for the match.
Runtime player body persists through death.
On death: disable conflicting systems.
On respawn: reset health, teleport, and re-enable systems.
```

Do not create a new `PlayerData` on death. If a future runtime body is recreated, it must keep the same `GlobalId`.

## Walls

Files:

- `Scripts/Data/Map/ArenaMapData.cs`
- `Scripts/Data/Map/WallDamageData.cs`

Wall damage now uses the shared combat backend.

- `WallDamageData` owns a `HealthContainer`.
- Default wall health is `500`.
- Default wall armor has `Heat` immunity, `Slash` 95% reduction, `Crush` 0% reduction, `Explosive` 0% reduction, and `Fire` status immunity.
- Wall armor is selected through a biome switch hook in `WallDamageData.ConfigureForBiome(...)`; currently all biomes fall through to default values.
- Wall visual damage stages are health-ratio based: no decal above or at 90% health, light decal below 90%, heavy decal below 50%.
- Radius wall damage scales per tile by distance from the radius center.

## Props

Files:

- `Scripts/Data/Map/LevelProp.cs`
- `Scripts/Data/Map/LevelPropData.cs`
- `Scripts/Data/Map/LevelPropType.cs`
- `Assets/Props/barrel.svg`
- `Assets/Props/rock.svg`
- `Assets/Props/tree.svg`

Current prop types:

- Barrel: `16x16`, lower health, vulnerable to heat/explosive.
- Rock: `16x16`, high health, full heat and slash resistance.
- Tree: `16x32`, medium health, vulnerable to heat/fire.

Props use `HealthContainer` and `ArmorResource` directly. They should follow the same combat path as players and walls.

## Next Work: Player Controls And Actions

The current branch is ready for player controls/actions work.

Recommended next steps:

1. Keep `DamageTestPlayer.GlobalId` as the only ownership key on the runtime body.
2. Use `Networking.MultiplayerData.GetPlayerByGlobalId(GlobalId)` to resolve `PeerId` and `LocalId`.
3. Keep input ownership checks based on `PeerId + LocalId` instead of duplicating ownership data on the runtime player.
4. Continue hardening quantized movement/aim in the LAN test scene before moving it into a final gameplay scene.
5. Keep server-authoritative direction in mind: client input should remain requests/commands, not direct authority over shared state.

## Verification Commands

Use these after changes:

```bash
dotnet build MultiplayerArenaV2.csproj
godot --headless --path . --quit
```

Run import when adding or changing Godot assets:

```bash
godot --headless --path . --import
```
