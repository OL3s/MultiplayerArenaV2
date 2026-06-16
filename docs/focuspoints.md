# Focus Points

This file tracks what to focus on in the next working session.

## Next Session Goal

Move from test-room iteration toward a real lobby-launched match slice. The current priority is to make lobby-started gameplay consume resolved setup correctly, generate real structure/objective/prop layouts from the authoritative seed, and make the configured game modes actually playable.

## Current Handoff

- Lobby Start Match, resolved setup sync, item-theme loading, radial buy menu, Credit-based first-pass buying, local HUD, scoreboard, and direct-test wrapper separation are already implemented enough to treat them as the baseline.
- `MapGeneratorController` currently needs attention: it should consume the host-resolved selected structure, not fall back to the first enabled candidate when a candidate pool exists.
- `Plains` is still not a real generator. It should become the first true random/open-field structure generator.
- `Arena` and `Square` should stay useful as stable/debug-friendly structures, but they still need seed-driven placement layers for props, side objectives, and item spawn candidates.
- The game modes are selectable/configured, but the runtime rules still need to be made real.

## Primary Focus

- Use the main menu host/join flow and `scenes/ui/lobby/match_lobby.tscn` as the primary entry point, not only direct test-scene launchers.
- Keep `scenes/tests/test_player_item_room_lan.tscn` and square mode test scenes as developer test beds.
- Keep lobby-selected `SetupConfig` authoritative when entering gameplay: game mode playlist, loadout mode pool, teams, structure pool, biome pool, item theme pool, host-resolved active structure/biome/theme/loadout, and server-resolved map seed should survive the transition.
- Keep direct LAN test bootstrap behavior separate from lobby-started match behavior. Test scenes may force square/fixed seed; the real match scene should not.
- Make configured game modes actually work in gameplay. `Deathmatch`, `CaptureTheFlag`, `KingOfTheHill`, and `Headquarters` need runtime scoring/capture/flag/round/win behavior, not only lobby selection and test-scene labels.
- Make `MapGeneratorController` the central structure generation entry point. It should consume the host-resolved active structure and authoritative synced seed, then route to the correct structure generator.
- Build `Plains` as the first true random/open-field map generator with rule-based placement constraints for team spawn/core objects, neutral core objectives, side objectives, item spawn candidates, and props.
- Keep `Arena` and `Square` wall/playable-space layouts non-random.
- `Arena`: keep standard team spawn/core object placements and standard main/core objective placement; randomize side objective placement, item spawn candidates, and props from the synced seed.
- `Square`: keep non-random walls/playable-space; randomize team spawn/core object placement, side objective placement, item spawn candidates, and props from the synced seed.
- Random placement must respect minimum distances between team spawn/core placements, objective placements, side objectives, and props. It must avoid blocking spawns, objectives, critical paths, and immediate exits from spawn areas.
- Build real biome output next. Biomes should affect visuals/content such as wall/floor tile choice, prop set, prop density, and biome-specific environmental dressing, not gameplay spawn rules.
- Add a local-player-aware match camera. It should center on the local players owned by the current device and zoom in/out to keep those players readable and on screen as they spread out or regroup.
- Replace in-game SVG placeholder imagery with proper production-direction assets where gameplay readability depends on it: players, map tiles, props, objectives, item pickups, and held/world items.
- Follow `docs/asset-organization.md`: player bodies in `assets/players/`, carried items in `assets/items/`, spawned projectiles in `assets/projectiles/`, props in `assets/props/`, tiles in `assets/tiles/`.

## Current Working Scene Split

- Lobby-started gameplay scene: `scenes/gameplay/arena_match.tscn`.
- Shared gameplay runtime script: `scripts/data/gameplay/ArenaMatch.cs`.
- Direct LAN test wrapper script: `scripts/data/gameplay/TestPlayerItemRoomLAN.cs`.
- Direct player/item LAN test scene: `scenes/tests/test_player_item_room_lan.tscn`.
- Mode-specific square LAN tests: `scenes/tests/test_deathmatch_square_lan.tscn`, `scenes/tests/test_capture_the_flag_square_lan.tscn`, `scenes/tests/test_king_of_the_hill_square_lan.tscn`, and `scenes/tests/test_headquarters_square_lan.tscn`.

## Next Implementation Order

1. Fix `MapGeneratorController` so generation honors the host-resolved selected structure instead of the first enabled candidate.
2. Verify Start Match from Local and LAN lobbies enters `scenes/gameplay/arena_match.tscn` on host and connected clients with synced structure, biome, item theme, loadout mode, game mode, and seed.
3. Add a constrained random placement layer for props, side objectives, and item spawn candidates using only the authoritative synced seed.
4. Build `Plains` as a real random/open-field structure generator distinct from `Square`.
5. Add `Arena` random side-objective/prop/item-spawn placement while keeping its wall layout, team spawn/core placements, and main/core objective placement stable.
6. Add `Square` random team spawn/core placement, side-objective placement, prop placement, and item-spawn placement while keeping its wall/playable-space layout stable.
7. Implement real `Deathmatch` runtime behavior: scoring, kills/deaths, round/win conditions, and scoreboard/HUD updates.
8. Implement real `CaptureTheFlag` runtime behavior using team spawn/core objects as flag/base anchors.
9. Implement real `KingOfTheHill` runtime behavior using the main/core neutral objective hold rules.
10. Implement real `Headquarters` runtime behavior using active side-objective selection, capture, contesting, and scoring.
11. Add a camera controller that tracks all local players on the current device, centers on their bounds, and zooms smoothly between readable min/max zoom levels.
12. Add biome-aware tile/prop selection so `Woods`, `Arena`, and `Medieval` visibly differ without changing spawn/objective rules.
13. Replace temporary in-game SVG placeholders with proper assets for the first playable slice, prioritizing map tiles, props, player bodies, team bases/objectives, and item pickups.
14. Rename candidate-pool config fields from `Enabled...` to `Available...` and resolved active fields from `Selected...` to `Current...` when the churn is acceptable. Move random current-outcome resolution into the config resources or a central `SetupConfig.ResolveCurrentRuntimeChoices()` helper.
15. Move hardcoded structure definitions toward reusable data/resources once there is more than one real layout to author.
16. Run `dotnet build MultiplayerArenaV2.csproj`, `./tools/import-assets.sh`, and `./tools/verify-startup.sh` after implementation.

## Keep In Mind

- Do not put game-mode scoring/capture rules into `NeutralObjective`; game modes own those rules.
- Do not let clients resolve independent random map seeds. Generation must use the authoritative server seed from synced setup.
- Random generation must be constrained randomness. Do not place team spawn/core objects, objectives, side objectives, item spawns, or props without minimum-distance and obstruction checks.
- Do not randomize `Arena` or `Square` walls/playable-space layout yet. Randomize their placement/content layers only.
- Do not make biomes responsible for spawn or objective placement. Structures own gameplay layout; biomes own visual/content dressing.
- Camera framing should be per device/process and based on local players only, not every networked player in the match.
- Do not rebuild the old inventory/backstrap/ammo-rig model.
- Keep the current armor-driven loadout, reload/recovery, gadget recovery, and local-player HUD work as implemented baseline, then iterate only where readability or gameplay flow needs it.
- Keep the modern starter pistol marked as a modern-only default/starter item; do not make every theme inherit that exact default.
- Keep medieval melee as documented future work until there is an explicit melee runtime slice.
- Resource paths and direct resource references should be the durable keys for theme/catalog/item selection. Avoid parallel enum/string lists for content that already exists as resources or folders.

## Relevant Docs

- `docs/index.md`
- `docs/multiplayer-networking.md`
- `docs/spawning-and-objectives.md`
- `docs/asset-organization.md`
- `docs/test-scenes.md`
- `docs/player-items-inventory-plan.md`
- `docs/player-hud-ui-plan.md`
- `docs/modern-item-content-plan.md`
- `docs/item-themes.md`
- `docs/destructible-environment.md`
