# Focus Points

This file tracks what to focus on in the next working session.

## Next Session Goal

Move from test-room iteration toward a real lobby-launched match slice. The current priority is to make Start Match lead into playable gameplay using lobby-selected setup, then replace placeholder map/visual content with actual structure, biome, prop, and gameplay assets.

## Recent Session Handoff

- Implemented the resource-root item theme system. `assets/items/item_theme_catalog.tres` points to theme definitions under `assets/items/themes/`; each theme definition owns metadata, icon, item root folder, and direct default starter item reference.
- `SetupConfig.ItemThemeConfig` now syncs selected theme definition resource paths instead of enum values. Lobby theme selection is catalog-driven and acts as a candidate pool, not simultaneous active themes.
- The host resolves one active structure, biome, item theme, and loadout mode from lobby-selected candidate pools when starting a match. `ArenaMatch` scans only the resolved active theme root recursively for `PlayerItem` resources. Buy/debug menus populate from that loaded theme and its default starter item.
- Added modern `pistol_t0` as the intentionally weak starter pistol. Added the medieval first-pass items: `bow_t0`, `bow_t1`, `crossbow_t1`, `bomb`, and `leather_armor`.
- Added `Medieval` as a biome option and documented that medieval should eventually be more melee-focused, while current medieval content is a ranged/gadget/armor placeholder slice.
- Added the match-level loadout mode config structure and lobby UI placement. Map config now groups biome, structure, and item theme together; Game config groups game mode and loadout mode together. Runtime loadout-mode behavior is still a follow-up.
- The production radial buy menu now uses theme-owned `ItemBuyMenuGroup` resources, category/action icons under `assets/ui/buy/`, red cancel styling, per-item cost labels, first-pass Credits initialization from `LoadoutModeConfig.StartingCredits`, and disabled/rejected unaffordable entries in Credit-based modes. `BuyOnSpawn` currently awards `CreditsPerKill` on player kills; `PersistentBudget` is finite. Lobby-started `arena_match.tscn` uses the radial buy menu only, while direct test wrappers can still enable the old debug grid.
- Verification passed after the refactor: `dotnet build MultiplayerArenaV2.csproj`, `./tools/import-assets.sh`, `./tools/verify-startup.sh`, and short headless startup of `test_player_item_room_lan.tscn`. The headless item-room test equipped `pistol_t0` through the new catalog/theme-folder path.

## Primary Focus

- Use the main menu host/join flow and `scenes/ui/lobby/match_lobby.tscn` as the primary entry point, not only direct test-scene launchers.
- Keep `scenes/tests/test_player_item_room_lan.tscn` and square mode test scenes as developer test beds.
- Keep lobby-selected `SetupConfig` authoritative when entering gameplay: game mode playlist, loadout mode pool, teams, structure pool, biome pool, item theme pool, host-resolved active structure/biome/theme/loadout, and server-resolved map seed should survive the transition.
- Keep direct LAN test bootstrap behavior separate from lobby-started match behavior. Test scenes may force square/fixed seed; the real match scene should not.
- Build real structure generation next. `Arena`, `Plains`, and `Square` should become distinct implemented layouts instead of sharing temporary placeholder output.
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

1. Verify Start Match from Local and LAN lobbies enters `scenes/gameplay/arena_match.tscn` on host and connected clients.
2. Confirm host-resolved item theme reaches `ArenaMatch` in lobby-started play, especially modern-only, medieval-only, and both-themes candidate pools with the dynamic buy group hierarchy.
3. Confirm host-resolved loadout mode reaches `ArenaMatch` in lobby-started play before implementing mode-specific gear persistence, server-authoritative Credits sync, randomization, or mirrored-loadout behavior.
4. Make the match scene consume lobby setup without forcing square/fixed-seed test overrides.
5. Rename candidate-pool config fields from `Enabled...` to `Available...` and resolved active fields from `Selected...` to `Current...` when the churn is acceptable. Move random current-outcome resolution into the config resources or a central `SetupConfig.ResolveCurrentRuntimeChoices()` helper.
6. Add a camera controller that tracks all local players on the current device, centers on their bounds, and zooms smoothly between readable min/max zoom levels.
7. Restore or rebuild `Plains` as a real open-field structure distinct from `Square`.
8. Add at least one more production-intent structure layout beyond the debug square/arena shape.
9. Add biome-aware tile/prop selection so `Woods`, `Arena`, and `Medieval` visibly differ without changing spawn/objective rules.
10. Replace temporary in-game SVG placeholders with proper assets for the first playable slice, prioritizing map tiles, props, player bodies, team bases/objectives, and item pickups.
11. Move hardcoded structure definitions toward reusable data/resources once there is more than one real layout to author.
12. Run `dotnet build MultiplayerArenaV2.csproj`, `./tools/import-assets.sh`, and `./tools/verify-startup.sh` after implementation.

## Keep In Mind

- Do not put game-mode scoring/capture rules into `NeutralObjective`; game modes own those rules.
- Do not let clients resolve independent random map seeds. Generation must use the authoritative server seed from synced setup.
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
