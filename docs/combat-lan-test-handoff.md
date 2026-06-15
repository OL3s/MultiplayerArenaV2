# Combat And LAN Test Handoff

This document is the current handoff for the shared combat backend, destructible walls, level props, and LAN damage-test player targets.

## Current Branch

- Current working branch is usually `main` unless a new feature branch is created for a focused slice.
- `main` contains the shared damage resources, destructible props, LAN player damage targets, and player movement/aim test runtime.
- The next planned work is the player item/action slice tracked in `docs/focuspoints.md` and `docs/player-items-inventory-plan.md`.

## Shared Combat Backend

Core files live in `scripts/data/combat/`.

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

Scene: `scenes/tests/test_map_destruction_logic_lan.tscn`

Script: `scripts/data/map/TestMapDestructionLogicLAN.cs`

The LAN destruction test currently covers:

- Server-authoritative wall damage replication.
- Shared combat damage against wall tiles and props.
- Damage type selection with number keys.
- Radius damage falloff, strongest at the explosion center.
- Real `PlayerData` registration through the `Networking` autoload instead of hardcoded mock player ids.
- Godot physics collision for props and wall TileMap collision.
- Player targets were moved to `scenes/tests/test_player_item_room_lan.tscn`.

Controls:

- `1`: select `Crush` damage.
- `2`: select `Slash` damage.
- `3`: select `Heat` damage.
- `4`: select `Explosive` damage.
- Left click: damage the first prop or wall target under the cursor. Priority is prop, then wall.
- `Shift + Left Click`: radius damage against props and walls.
- Right click: rebuild/reset the arena.

Player/item controls now live in `scenes/tests/test_player_item_room_lan.tscn`.

The LAN destruction test still seeds local lobby data before hosting/joining so host/client setup and RPC flow use the real `Networking` autoload, but it does not spawn player targets.

Current hitbox/collision/input test structure:

- `LevelProp` is a `StaticBody2D` with a direct circular `CollisionShape2D` derived from `LevelPropData.Size`. Click damage uses the same circular prop radius.
- Wall damage is tile-data authoritative: world positions are converted to `Vector2I` tile coordinates and checked against `ArenaMapData` wall tiles. Wall tiles currently behave as full `16x16` damage cells.
- Props are `StaticBody2D`, and wall movement/collision shape data comes from the rendered `WallLayer` `TileMapLayer`. `ArenaTileLayerRenderer` adds a runtime physics layer/polygon to the loaded wall `TileSet`, while `ArenaMapData.WallTiles` remains the gameplay authority for wall damage/destruction checks.

## LAN Player Item Room Test Scene

Scene: `scenes/tests/test_player_item_room_lan.tscn`

Script: `scripts/data/gameplay/TestPlayerItemRoomLAN.cs`

This is now the dedicated player movement, aim, and item/action test bed.

- Builds a square floor/wall room with one center barrel prop.
- Host/local instances register one active keyboard/mouse local player: `LocalId 0`.
- Client instances register one active keyboard/mouse local player: `LocalId 0`.
- Runtime player targets are built from `Networking.MultiplayerData.Players`, keyed only by `GlobalId`, and resolve `PeerId`/`LocalId` through `MultiplayerData.GetPlayerByGlobalId(...)`.
- Expected player mapping with one host and one client is host `P0 peer 1:local 0` and client `P1 peer <clientPeer>:local 0`.
- `DamageTestPlayer` creates temporary SVG visual children: `BodySprite` for front/back body images from `assets/players/` and a `Pistol-T1` weapon `Sprite2D` from `assets/items/modern/weapons/`. The weapon is offset from the body and rotated toward the active aim display vector.
- `DamageTestPlayer` stores separate local and estimated aim vectors. Owned/local players display their locally calculated exact aim immediately. Remote/non-owned player display uses the replicated quantized estimated aim.
- The player body sprite flips only by `BodySprite.Scale.X`; root scale, collision, label, and weapon positioning are not flipped through the root node. The body switches to the back SVG only when aim is sufficiently upward.
- Player room movement uses Godot physics bodies: players are `CharacterBody2D`, the center prop is `StaticBody2D`, and wall movement collision comes from the rendered `WallLayer` `TileMapLayer`.
- Local input first resolves to generic movement and aim vectors, then those vectors are quantized into 16 direction buckets and three strength states: `None`, `Some`, and `Full`. Keyboard/mouse and gamepad only differ at the vector-read step.
- Local input also resolves an explicit active-aiming state. Keyboard/mouse is actively aiming while `Ctrl` or right mouse button is held. Gamepad is actively aiming while the right stick is outside the aim deadzone.
- The debug aim indicator only draws while the local player is actively aiming, and server movement applies the selected item's `AimMoveSpeedMultiplier` while aiming.
- For the current prototype, clients do not predict or simulate player movement locally. Clients send movement input vectors only when their quantized movement state changes, the host/server validates ownership, quantizes the vector, and the host/server is the only peer that simulates movement.
- Aim state changes replicate independently from movement state changes. Aiming does not force movement updates. For gamepad players with no active right-stick aim, the aim state follows movement-state direction/strength changes.
- Local aim display is allowed to be more exact than replicated aim state. Future shoot/throw actions should send their exact aim vector at action time and can use that exact vector to update the acting object's local aim display.
- Accepted movement-state syncs include the server position, and the host/server also broadcasts moving player positions every server physics tick while movement continues. Clients directly apply server positions with no interpolation or local prediction for now.
- `B` opens a temporary test item grid for equipping one of the currently loaded modern item resources on the local player. Keyboard/mouse and controller players use the same menu path instead of comma/period cycling or spacebar use testing.
- `DamageTestPlayer` now exposes a reusable `PlayerControlState`. The item grid switches local players to `Menu` state while open, which blocks movement, aim, and item-use input so controller UI navigation does not also move the player.
- Spawn/respawn placement is not yet overlap-safe. `MoveAndSlide()` should not be relied on to push a player out of an initial overlap; add a circular physics-space spawn query and nearby free-floor fallback before relying on respawns in real gameplay.

Player item room controls:

- Host/local player movement: keyboard `WASD` or arrow keys. Hold `Shift` to emit a half-strength movement vector for walking.
- Client player movement: keyboard `WASD` or arrow keys in each client window. Client `LocalId 0` uses keyboard/mouse input for this LAN test.
- Host/local aim: mouse direction from the player body.
- Client aim: mouse direction from the player body in each client window.
- Active aiming is separate from aim direction. Keyboard/mouse uses `Ctrl` or right mouse button.
- While actively aiming, movement speed is multiplied by the selected item's `AimMoveSpeedMultiplier`.
- `B`: open or close the item grid. Keyboard `B` and Xbox controller `B` both toggle it.
- Arrow keys, d-pad, left stick UI navigation, `Enter`, mouse click, or controller `A`: select an item from the grid.
- Opening the item grid locks local gameplay input through `PlayerControlState.Menu`; closing it restores `PlayerControlState.Gameplay`.
- Left mouse button or Xbox right trigger: use the currently selected item.
- Item-use replication now also drives a short action-aim visual on `DamageTestPlayer`, so the held item points toward the exact shot/throw direction for about half a second on other devices.

## Damage Test Player Runtime

File: `scripts/data/gameplay/DamageTestPlayer.cs`

`DamageTestPlayer` is a temporary LAN-test runtime player body for combat and upcoming controls work.

- It owns `GlobalId` and a `HealthContainer`.
- It displays temporary SVG body/weapon sprites, draws a health bar, and owns a label.
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

- `scripts/data/map/ArenaMapData.cs`
- `scripts/data/map/WallDamageData.cs`

Wall damage now uses the shared combat backend.

- `WallDamageData` owns a `HealthContainer`.
- Default wall health is `500`.
- Default wall armor has `Heat` immunity, `Slash` 95% reduction, `Crush` 0% reduction, `Explosive` 0% reduction, and `Fire` status immunity.
- Wall armor is selected through a biome switch hook in `WallDamageData.ConfigureForBiome(...)`; currently all biomes fall through to default values.
- Wall visual damage stages are health-ratio based: no decal above or at 90% health, light decal below 90%, heavy decal below 50%.
- Radius wall damage scales per tile by distance from the radius center.

## Props

Files:

- `scripts/data/map/LevelProp.cs`
- `scripts/data/map/LevelPropData.cs`
- `scripts/data/map/LevelPropType.cs`
- `assets/props/barrel.svg`
- `assets/props/rock.svg`
- `assets/props/tree.svg`

Current prop types:

- Barrel: `16x16`, lower health, vulnerable to heat/explosive.
- Rock: `16x16`, high health, full heat and slash resistance.
- Tree: `16x32`, medium health, vulnerable to heat/fire.

Props use `HealthContainer` and `ArmorResource` directly. They should follow the same combat path as players and walls.

Prop visuals use three-frame horizontal damage atlases in `assets/props/`: perfect, touched, and close-to-broken. `LevelProp` selects the frame from current health ratio using wall-style thresholds: no damage frame above or at 90%, touched below 90%, and close-to-broken below 50%.

## Next Work: Player Items And Actions

The current runtime model is ready for player item/action work in the LAN test scene.

Recommended next steps:

1. Keep `DamageTestPlayer.GlobalId` as the only ownership key on the runtime body.
2. Use `Networking.MultiplayerData.GetPlayerByGlobalId(GlobalId)` to resolve `PeerId` and `LocalId`.
3. Keep input ownership checks based on `PeerId + LocalId` instead of duplicating ownership data on the runtime player.
4. Use exact aim vectors for shot/throw/use actions instead of relying only on quantized display aim.
5. Keep server-authoritative direction in mind: client item input should remain requests/commands, not direct authority over shared state.

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
