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

Controls:

- `1`: select `Crush` damage.
- `2`: select `Slash` damage.
- `3`: select `Heat` damage.
- `4`: select `Explosive` damage.
- Left click: damage the first target under the cursor. Priority is player, prop, then wall.
- `Shift + Left Click`: radius damage against players, props, and walls.
- Right click: rebuild/reset the arena and respawn player damage targets.

The LAN test seeds local lobby data before hosting/joining:

- Host/local instances register one active local player: `LocalId 0`.
- Client instances register two active local players: `LocalId 0` and `LocalId 1`.
- The client still joins through the real join flow, so the server assigns `GlobalId`s and syncs `PlayerData` back.

Expected player mapping with one host and one client:

- Host player: `P0 peer 1:local 0`.
- Client first local player: `P1 peer <clientPeer>:local 0`.
- Client second local player: `P2 peer <clientPeer>:local 1`.

The status label shows player health and ownership mapping using `GlobalId -> PlayerData -> PeerId/LocalId`.

## Damage Test Player Runtime

File: `Scripts/Data/Gameplay/DamageTestPlayer.cs`

`DamageTestPlayer` is a temporary LAN-test runtime player body for combat and upcoming controls work.

- It owns `GlobalId` and a `HealthContainer`.
- It draws a simple body, health bar, and label.
- It has an `Area2D` hitbox and `CollisionShape2D`.
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
3. Add input ownership checks from `PeerId + LocalId` instead of duplicating ownership data on the runtime player.
4. Start with movement in the LAN test scene before moving it into a final gameplay scene.
5. Keep server-authoritative direction in mind: client input should become requests/commands, not direct authority over shared state.

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
