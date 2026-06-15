# Destructible Environment

This document tracks destructible map, prop, wall, and tile-rendering reference details.

## Core Direction

The arena should be built around the idea that everything can be destroyed.

Planned logic model:

- Wall tiles are tracked by grid position using a `HashSet<Vector2I>`.
- Floor tiles are tracked by grid position using a separate `HashSet<Vector2I>`.
- Hit wall tiles are tracked in a hashmap/dictionary keyed by `Vector2I`.
- When a wall tile is hit for the first time, it gets an entry in the hit/damage dictionary.
- Tile damage data is stored separately from the base tile existence data.
- Damage state maps to a visual TileMap layer so cracked, damaged, and destroyed wall states can be represented clearly.
- Destroyed wall tiles are removed from the wall-tile lookup so movement, bullets, and line-of-sight can use the same source of truth.

Possible structure:

```csharp
private readonly HashSet<Vector2I> _wallTiles = new();
private readonly HashSet<Vector2I> _floorTiles = new();
private readonly Dictionary<Vector2I, WallDamageData> _hitWallTiles = new();
```

## Shared Combat Model

- `DamageType` currently supports `Crush`, `Slash`, `Heat`, and `Explosive`.
- `StatusEffectType` currently supports `Fire`.
- `DamageResource` contains typed direct damage values and typed status effect values.
- `DamageContainer` applies a `DamageResource` to a `HealthContainer`.
- `ArmorResource` stores typed damage/status-effect reduction percentages.
- `HealthContainer` defaults to `100/100` health and owns armor plus active status effects.
- Players, props, and walls should all route damage through `DamageContainer -> HealthContainer.ApplyDamage()`.

## Debug Tile Assets

- `assets/tiles/debug_floor_wall_atlas.svg` is a temporary SVG atlas with `32x16` debug tiles.
- The top tile is floor at atlas coordinate `(0, 0)`.
- The bottom tile is wall at atlas coordinate `(0, 1)`.
- `assets/tiles/debug_wall_damage_overlay.svg` is a temporary separate SVG atlas for wall-damage overlay visuals.
- The top tile is a light damage overlay at atlas coordinate `(0, 0)`.
- The bottom tile is a heavy damage overlay at atlas coordinate `(0, 1)`.
- Layer TileSet resources live in `assets/tiles/tilesets/` as separate `.tres` files for floor, wall, and wall-damage overlay rendering.

## Current Map Data Classes

- `ArenaMapData` stores floor tiles and wall tiles as hashsets, and hit walls in a dictionary keyed by `Vector2I`.
- Tile coordinates are signed because they use `Vector2I`, so negative map positions are valid and do not need special handling.
- `ArenaMapData.GenerateMap()` exists as the main generation entry point, but is intentionally empty until the real map algorithm is chosen.
- `ArenaMapData.ResetWallTiles()` rebuilds the wall hashset from current floors using all 8 neighboring cells, so corner walls are included and no smoothing is applied.
- `ArenaMapData.FillWallsFromFloors()` currently delegates to `ResetWallTiles()`.
- `ArenaMapData.DamageWallTile()` tracks damage per wall tile in `WallDamageData` and destroys the wall when its `HealthContainer` reaches zero.
- `ArenaMapData.DamageWallFromWorldPosition()` converts a world hit position back into a tile coordinate before applying single-tile wall damage.
- `ArenaMapData.WorldToTile()` is the shared world-to-grid conversion helper for destructible wall logic.
- `ArenaMapData.GetTilesInRadius()`, `DamageWallsInRadius()`, and `DamageWallsInWorldRadius()` support tile-accurate radius damage with damage falloff from the radius center.
- `ArenaMapData.DestroyWallTile()` converts the destroyed wall tile into a floor tile, then rebuilds surrounding walls from the floor hashset so the data stays consistent.
- `ArenaMapData.GenerateLayerTileMapData()` emits `MapTileData` for separate logical layers: `Floor`, `Wall`, and `WallDamage`.
- `MapTileData` stores both tile type and logical layer type so a renderer can rebuild visible tile layers from data without using the rendered TileMap state as authority.

## Wall Damage

- `WallDamageData` stores a wall `HealthContainer`, exposes `Damage`, `MaxDamage`, and `DamageStage`, and configures default wall armor through a biome switch hook.
- Default wall health is `500`.
- Default wall armor has `Heat` immunity, `Slash` 95% reduction, `Crush` 0% reduction, `Explosive` 0% reduction, and `Fire` status immunity.
- Wall damage overlays are health-ratio based: no decal above or at 90% health, light decal below 90%, and heavy decal below 50%.

## Level Props

- `LevelPropData` defines prop type, visual path, hitbox size, health, and armor.
- `LevelProp` is the temporary runtime prop node used in test scenes.
- `LevelPropType` currently supports `Barrel`, `Rock`, and `Tree`.
- Prop SVG assets live in `assets/props/` and are horizontal three-frame damage atlases: perfect, touched, and close-to-broken.
- Barrel and rock are `16x16`; tree is `16x32`.
- Props use the same `HealthContainer` and `DamageContainer` path as players and walls.
- Prop damage stages use the same health-ratio thresholds as wall damage overlays: perfect above or at 90% health, touched below 90%, and close-to-broken below 50%.

## Rendering Structure

- `ArenaMapData` is the source of truth for floor tiles, wall tiles, and wall damage.
- `ArenaTileLayerRenderer` is a Node2D renderer that rebuilds visible Godot `TileMapLayer` nodes from `ArenaMapData`.
- The current layer split is `FloorLayer`, `WallLayer`, and `WallDamageLayer`.
- The renderer clears and repaints each `TileMapLayer` from generated `MapTileData`, so the rendered layers are a projection of hashset/dictionary state, not the authority themselves.

## Projectile And Explosion Damage Rules

- TileMapLayer collision should not be treated as the authority for destructible walls.
- Godot tile collision is authored per tile in the `TileSet`, but physics can be grouped internally by quadrants for performance.
- Bullet and projectile hits should convert the impact world position back into map/grid coordinates before wall damage is applied.
- Wall damage should be resolved by checking the wall hashset at that grid position, not by treating a merged collision body or a row of tiles as one destructible unit.
- Grenade and explosion damage should iterate tile positions inside the explosion radius and apply damage tile-by-tile.
- Explosion damage should only affect tiles that actually fall within the radius and still exist in the wall hashset.
- Movement collision can come from the wall `TileMapLayer`, but destructible gameplay state should always be read from `ArenaMapData`.
- Current RTC/networking direction for destructible walls is server-authoritative function replication: the host/server runs the map logic and sends the same damage/destroy function calls outward to clients.
- Clients should not be the authority for wall destruction. For now, they only receive and replay server-approved wall damage updates.
- Late-join map-state catch-up is intentionally deferred for later work. The current focus is authoritative live sync from server to already-connected clients.
