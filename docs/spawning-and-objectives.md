# Spawning And Objectives

This document tracks the first gameplay-facing spawn and objective model.

## Structure Responsibility

`MapGenerationConfig.StructureType` is the gameplay layout choice. A structure decides:

- Playable area shape.
- Team spawn placement.
- Team objective-zone placement.
- Neutral objective placement.
- Item spawn placement.

Biomes are visual/content theme choices. They should not decide where players, flags, objectives, or pickups spawn.

Current structures:

- `Arena`: fixed non-random plus-shape layout for debug/known-map gameplay.
- `Plains`: generated wider open layout.
- `Square`: simple square-room layout for mode/test iteration, with opposing left/right team bases for the current two-player item LAN test.

Current biomes:

- `Woods`.
- `Arena`.

## Structure Generation API

Structure generation starts in `StructureGenerationData`.

The resource owns:

- A `HashSet<Vector2I>` of generated floor tiles.
- A list of enabled `SpawnPointType` enum values.
- Spawn tile lists keyed by `SpawnPointType`.
- Team-specific objective center tiles.
- Team-specific spawn tiles around each objective center.

Current spawn point types:

- `TeamSpawn`: tiles where players can spawn around their team's objective center.
- `TeamObjective`: team/base objective center tiles.
- `NeutralObjective`: where the one neutral center objective is placed.
- `ItemSpawn`: where item/pickup spawns can be placed later.

Important API:

- `Generate(MapGenerationConfig.StructureType structureType)`: fills floor tiles and spawn point lists for a structure.
- `ApplyToArenaMap(ArenaMapData arenaMapData)`: transforms generated floor tiles into the arena map hash sets and rebuilds wall tiles.
- `ToFloorTileHashSet()`: returns a copy of generated floor tiles as a `HashSet<Vector2I>`.
- `ToGodotFloorLayerTiles(sourceId)`: transforms generated floor tiles into Godot `MapTileData` entries for the floor layer.
- `GetSpawnTiles(SpawnPointType spawnPointType)`: reads generated spawn/objective/item placement tiles.
- `GetTeamObjectiveTile(teamId)`: reads the center objective tile for a team.
- `GetTeamSpawnTiles(teamId)`: reads the tiles where that team's players can spawn around the objective.

`TestPlayerItemRoomLAN` currently consumes this API directly. Later gameplay scenes should consume the same resource instead of hardcoding structure shapes.

## Team Spawn Bases

Team bases use `scenes/gameplay/objectives/team_spawn_base_marker.tscn` with `TeamSpawnBaseMarker.cs`.

Each team spawn base is centered on that team's objective/core. It owns two concentric packed `Area2D` ranges:

- `SpawnArea`: the wider area around the objective center where that team's players can spawn.
- `ObjectiveArea`: the smaller center area for team-owned objective interactions, such as placing, stealing, or returning a flag in capture the flag.

The objective is the center. Spawning happens around it, not on the objective center unless a structure explicitly chooses that tile as a spawn tile.

The team objective scene must also represent the team's spawn platform layout. It should pack the objective/core and the four spawn platforms in the same `.tscn`, arranged as a `+` around the core. This keeps the base/objective/spawn visual readable and avoids rebuilding child nodes in code.

Team spawn slots are generated in a `+` shape around the objective center. Internal slot indices are `0-3`; debug/player-facing labels are `1-4`:

- Slot `0` / label `1`: above the objective.
- Slot `1` / label `2`: right of the objective.
- Slot `2` / label `3`: below the objective.
- Slot `3` / label `4`: left of the objective.

`TestPlayerItemRoomLAN` resolves a player's team-local index and uses that index to choose the spawn slot.

The base marker is packed as scene children instead of built in code. It uses:

- `assets/ui/team_spawn_core.svg` for the objective core.
- `assets/ui/team_spawn_platform.svg` for each spawn station around the core.

The core and platform SVGs are intentionally white-gray/grayscale. Runtime code modulates them through `TeamVisuals.GetTeamColor(backendTeamId + 1)`: gameplay/spawn backend team ids are `0-3`, while network/lobby palette labels are `1-4`.

Spawn station visibility is team-size aware. Team base markers are hidden when that team has no players. Slot/platform `4` is hidden when the team has only three players, slot/platform `3` is hidden when the team has only two players, and so on. Internal slot indices remain `0-3`; visible debug labels remain `1-4`.

Player body SVGs are also white-gray/grayscale for the same reason. `DamageTestPlayer` applies the resolved team color at runtime so local and remote players share the same team-color source as bases and lobby UI.

The team spawn base is not the neutral center objective. It represents a team's side/base area.

## Neutral Objectives

Neutral objectives use `scenes/gameplay/objectives/neutral_objective.tscn` with `NeutralObjective.cs`.

Neutral objectives are map/runtime fixtures, not game-mode logic. A match map can create them for every game mode, including modes that ignore them. The objective scene owns geometry, range checks, and generic visual state only. It must not hardcode scoring, capture timing, flag rules, round endings, or any single mode's behavior.

There are two intended neutral objective roles:

- Core neutral objective: the map-center objective. There should be one per match map.
- Secondary neutral objectives: spread-out candidate objective spots used by game modes that need random or rotating locations.

Each neutral objective owns two concentric `Area2D` ranges:

- `OuterArea`: the wider nearby/objective-presence area for future game modes.
- `InnerArea`: the smaller center interaction area used for current occupancy/contest checks.

The neutral objective API should stay intentionally small:

- `ContainsOuterPosition(worldPosition)`: lets a game mode ask whether a player/object is near the objective.
- `ContainsInnerPosition(worldPosition)`: lets a game mode ask whether a player/object is inside the interaction area.
- `SetState(controllingTeamId, isContested)`: optional visual/debug state set by the active game mode or test harness.

The current LAN item room uses the core neutral objective inner area only to show occupancy and contested state. It does not award score; game-mode logic should decide whether a neutral objective matters, whether it scores, how often it scores, and which score bucket receives points.

Expected examples:

- Deathmatch can create the neutral objective and ignore it entirely.
- Hold-the-zone can activate one neutral objective, inspect occupancy, and award points from its own game-mode system.
- Capture-the-flag can ignore the center neutral objective while using team spawn bases for flag pickup/return logic.
- A future rotating-objective mode can choose one core or secondary neutral objective as active, call `SetState(...)` for visuals, then move activation without changing the objective scene.

Secondary neutral objectives are not active scoring points by default. They should be generated by structure data as possible locations for game modes such as a future `Hold The Zone`, where the active objective can move to one of several random/spread-out spots. Game-mode logic should decide which secondary objective is active, when it changes, and how it scores.

## Respawn Flow

When a player dies, gameplay should move through this state flow:

```text
Dead -> RespawnDelay -> SpawnState -> Gameplay
```

Rules:

- Dead players wait for the respawn timer. The current debug value is `1.0` second.
- When the timer finishes, the gameplay reset API restores health, clears item recovery, resets loadout ammo/uses, and teleports the player to the correct team spawn location.
- After teleport, the player enters spawn state.
- Spawn state immobilizes the player and makes them invulnerable for a short duration. The current debug value is `1.0` second.
- When spawn state finishes, control returns to normal gameplay and invulnerability is removed.

The first implementation is in `TestPlayerItemRoomLAN` and `DamageTestPlayer`. It should later move behind a reusable match/runtime API when the test room becomes a real game scene.

## Team Wipe Hook

The first team-wipe API lives in `TestPlayerItemRoomLAN`:

- `TeamWiped`: C# event fired with the wiped team id.
- `OnTeamWiped(teamId)`: local hook that invokes the event and logs the wipe.

The current behavior only detects and reports the wipe. Game-mode-specific handling is intentionally deferred. Later, deathmatch, capture the flag, elimination, and custom config settings should decide what a team wipe does, such as ending a round, dropping/returning flags, awarding points, delaying respawns, or doing nothing.

## Current Follow-Ups

- Replace temporary item spawn markers with real item pickup/spawn scene instances.
- Make structure definitions data-driven instead of hardcoded inside `TestPlayerItemRoomLAN`.
- Add game-mode-specific behavior for team spawn bases, such as flag pickup/return.
- Add generated secondary neutral objective placements and game-mode logic that can activate one at random.
- Add game-mode/config-specific team-wipe handling.
- Add explicit HUD/status presentation for dead, respawning, spawn-protected, occupied, and contested states.
