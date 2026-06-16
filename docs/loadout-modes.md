# Loadout Modes

This document tracks the match-level loadout mode config: how players obtain gear and what happens to that gear across spawn and death.

## Purpose

Loadout mode is a match config candidate pool, grouped with game mode in the lobby. It is separate from map-facing setup:

- Map section: biome, structure, and item theme.
- Game section: game mode and loadout mode.

The config is represented in code and lobby UI. `EnabledLoadoutModes` stores the lobby-selected candidate pool. When the host starts a match or a future game-mode iteration, it resolves one authoritative `SelectedLoadoutMode`; `ArenaMatch` consumes only that selected mode. `ArenaMatch` consumes Credits for `BuyOnSpawn` and `PersistentBudget`; mode-specific randomization and mirrored-loadout behavior is not fully enforced yet.

## Current Modes

- `BuyOnSpawn` / `Buy On Spawn`: players buy or equip gear each time they spawn. Credits are used and can be earned while playing. The current first-pass earning rules grant Credits per kill and per completed spawn, so players who die repeatedly still accumulate some buying power.
- `PersistentBudget` / `Persistent Budget`: players get a finite match Credit pool to spend. Bought loadouts persist after death. There are no kill Credit rewards in this mode.
- `RandomOnRespawn` / `Random Respawn`: players receive a random loadout each time they respawn. Credits are not used.
- `MirrorLoadout` / `Mirror Loadout`: every player gets the same loadout. Credits are not used.
- `MapPickups` / `Map Pickups`: weapons, gear, and gadgets spawn on the map occasionally at spread-out secondary neutral objective locations when those locations are not currently active as objectives. The single main center neutral objective must not be used as the pickup source for this mode. Credits are not used.

## Config Data

Code:

- `scripts/data/multiplayer/LoadoutModeConfig.cs`
- `scripts/data/multiplayer/SetupConfig.cs`
- `scripts/ui/MatchLobby.cs`

`LoadoutModeConfig` owns:

- `EnabledLoadoutModes`: the allowed loadout mode pool for the match setup.
- `SelectedLoadoutMode`: the single host-resolved active loadout mode for the current match/game-mode iteration.
- `StartingCredits`: first-pass radial buy menu Credit value, defaulting to `1000`.
- `CreditsPerKill`: first-pass `BuyOnSpawn` kill reward, defaulting to `100`.
- `CreditsPerSpawn`: first-pass `BuyOnSpawn` completed-spawn reward, defaulting to `50`.

`SetupConfig` clones, compares, and serializes `LoadoutModeConfig` with the rest of match setup so host/client lobby state can stay synchronized.

## Lobby UI

`scenes/ui/lobby/match_lobby.tscn` shows loadout mode beside game mode in the Game section.

The loadout selector uses the same reusable `ConfigSelectionOverlay` as structure, biome, and item theme. It supports selecting one mode, all modes, or a custom subset. These selections are a candidate pool; the host resolves one active mode from that pool before gameplay starts. Start Match is blocked when no loadout mode is selected.

Current UI icons:

- Generic category icon: `assets/ui/config_loadout.svg`.
- `BuyOnSpawn`: `assets/ui/loadout_buy_on_spawn.svg`.
- `PersistentBudget`: `assets/ui/loadout_persistent_budget.svg`.
- `RandomOnRespawn`: `assets/ui/loadout_random_respawn.svg`.
- `MirrorLoadout`: `assets/ui/loadout_mirror.svg`.

## Runtime Follow-Up

Current runtime behavior:

- `ArenaMatch` initializes each player's Credits from `LoadoutModeConfig.StartingCredits`.
- Radial buy item entries show item cost and current Credits in `BuyOnSpawn` and `PersistentBudget`.
- Entries above current Credits are disabled, and confirmation re-checks affordability before equipping.
- Successful local buys deduct the item `Cost` from Credits only in `BuyOnSpawn` and `PersistentBudget`.
- `BuyOnSpawn` awards `LoadoutModeConfig.CreditsPerKill` Credits when a player kills another player.
- `BuyOnSpawn` awards `LoadoutModeConfig.CreditsPerSpawn` Credits when a respawn finishes and the player returns to gameplay.
- `PersistentBudget` is finite; kills do not award Credits.
- `RandomOnRespawn`, `MirrorLoadout`, and `MapPickups` do not use Credits and therefore do not disable buy entries based on cost.

The host already resolves one active mode from `EnabledLoadoutModes` at match start. Later runtime work should reuse that resolver whenever the Game Mode playlist advances, then apply mode-specific behavior:

- `BuyOnSpawn`: clear gear on death/respawn and open or allow buy flow during spawn.
- `PersistentBudget`: make Credit/equipment persistence server-authoritative and synced while preserving equipped loadout after death.
- `RandomOnRespawn`: choose an authoritative random loadout on the host/server each respawn and sync it to peers.
- `MirrorLoadout`: choose one authoritative shared loadout and apply it to all players.
- `MapPickups`: spawn item pickup scene instances at secondary neutral objective locations that are not currently active as objectives. This depends on generated secondary neutral objective instances, which are still follow-up work.

Until that runtime pass, current item-room behavior still preserves equipped loadout through death and refills uses on respawn.
