# Focus Points

This file tracks what to focus on in the next working session.

## Next Session Goal

Move from test-room iteration toward a real lobby-launched match slice. The current priority is to make Start Match lead into playable gameplay using lobby-selected setup, then replace placeholder map/visual content with actual structure, biome, prop, and gameplay assets.

## Primary Focus

- Use the main menu host/join flow and `scenes/ui/lobby/match_lobby.tscn` as the primary entry point, not only direct test-scene launchers.
- Keep `scenes/tests/test_player_item_room_lan.tscn` and square mode test scenes as developer test beds.
- Keep lobby-selected `SetupConfig` authoritative when entering gameplay: game mode, teams, structure selection, biome selection, and server-resolved map seed should survive the transition.
- Keep direct LAN test bootstrap behavior separate from lobby-started match behavior. Test scenes may force square/fixed seed; the real match scene should not.
- Build real structure generation next. `Arena`, `Plains`, and `Square` should become distinct implemented layouts instead of sharing temporary placeholder output.
- Build real biome output next. Biomes should affect visuals/content such as wall/floor tile choice, prop set, prop density, and biome-specific environmental dressing, not gameplay spawn rules.
- Replace in-game SVG placeholder imagery with proper production-direction assets where gameplay readability depends on it: players, map tiles, props, objectives, item pickups, and held/world items.
- Follow `docs/asset-organization.md`: player bodies in `assets/players/`, carried items in `assets/items/`, spawned projectiles in `assets/projectiles/`, props in `assets/props/`, tiles in `assets/tiles/`.

## Current Working Scene Split

- Lobby-started gameplay scene: `scenes/gameplay/arena_match.tscn`.
- Shared gameplay runtime script: `scripts/data/gameplay/ArenaMatch.cs`.
- Direct LAN test wrapper script: `scripts/data/gameplay/TestPlayerItemRoomLAN.cs`.
- Direct player/item LAN test scene: `scenes/tests/test_player_item_room_lan.tscn`.
- Mode-specific square LAN tests: `scenes/tests/test_deathmatch_square_lan.tscn`, `scenes/tests/test_capture_the_flag_square_lan.tscn`, `scenes/tests/test_king_of_the_hill_square_lan.tscn`, and `scenes/tests/test_headquarters_square_lan.tscn`.

## Next Implementation Order

1. Verify Start Match from Local and LAN lobbies enters `scenes/gameplay/arena_match.tscn` on host and connected clients.
2. Make the match scene consume lobby setup without forcing square/fixed-seed test overrides.
3. Restore or rebuild `Plains` as a real open-field structure distinct from `Square`.
4. Add at least one more production-intent structure layout beyond the debug square/arena shape.
5. Add biome-aware tile/prop selection so `Woods` and `Arena` visibly differ without changing spawn/objective rules.
6. Replace temporary in-game SVG placeholders with proper assets for the first playable slice, prioritizing map tiles, props, player bodies, team bases/objectives, and item pickups.
7. Move hardcoded structure definitions toward reusable data/resources once there is more than one real layout to author.
8. Run `dotnet build MultiplayerArenaV2.csproj`, `./tools/import-assets.sh`, and `./tools/verify-startup.sh` after implementation.

## Keep In Mind

- Do not put game-mode scoring/capture rules into `NeutralObjective`; game modes own those rules.
- Do not let clients resolve independent random map seeds. Generation must use the authoritative server seed from synced setup.
- Do not make biomes responsible for spawn or objective placement. Structures own gameplay layout; biomes own visual/content dressing.
- Do not rebuild the old inventory/backstrap/ammo-rig model.
- Keep the current armor-driven loadout, reload, gadget refresh, and local-player HUD work as implemented baseline, then iterate only where readability or gameplay flow needs it.

## Relevant Docs

- `docs/index.md`
- `docs/multiplayer-networking.md`
- `docs/spawning-and-objectives.md`
- `docs/asset-organization.md`
- `docs/test-scenes.md`
- `docs/player-items-inventory-plan.md`
- `docs/player-hud-ui-plan.md`
- `docs/modern-item-content-plan.md`
- `docs/destructible-environment.md`
