# Loadout Modes

This document tracks the match-level loadout mode config: how players obtain gear and what happens to that gear across spawn and death.

## Purpose

Loadout mode is a match config choice, grouped with game mode in the lobby. It is separate from map-facing setup:

- Map section: biome, structure, and item theme.
- Game section: game mode and loadout mode.

The config is now represented in code and lobby UI, but gameplay behavior is not fully enforced yet. Runtime implementation should consume the selected loadout mode later from `Networking.MultiplayerData.SetupConfig.LoadoutModeConfig`.

## Current Modes

- `BuyOnSpawn` / `Buy On Spawn`: players buy or equip gear each time they spawn. Death clears the active loadout.
- `PersistentBudget` / `Persistent Budget`: players get a match budget to spend. Bought loadouts persist after death.
- `RandomOnRespawn` / `Random Respawn`: players receive a random loadout each time they respawn.
- `MirrorLoadout` / `Mirror Loadout`: every player gets the same loadout. The loadout persists after death.

## Config Data

Code:

- `scripts/data/multiplayer/LoadoutModeConfig.cs`
- `scripts/data/multiplayer/SetupConfig.cs`
- `scripts/ui/MatchLobby.cs`

`LoadoutModeConfig` owns:

- `EnabledLoadoutModes`: the allowed loadout mode pool for the match setup.
- `StartingBudget`: first placeholder budget value for `PersistentBudget`, defaulting to `1000`.

`SetupConfig` clones, compares, and serializes `LoadoutModeConfig` with the rest of match setup so host/client lobby state can stay synchronized.

## Lobby UI

`scenes/ui/lobby/match_lobby.tscn` shows loadout mode beside game mode in the Game section.

The loadout selector uses the same reusable `ConfigSelectionOverlay` as structure, biome, and item theme. It supports selecting one mode, all modes, or a custom subset. Start Match is blocked when no loadout mode is selected.

Current UI icons:

- Generic category icon: `assets/ui/config_loadout.svg`.
- `BuyOnSpawn`: `assets/ui/loadout_buy_on_spawn.svg`.
- `PersistentBudget`: `assets/ui/loadout_persistent_budget.svg`.
- `RandomOnRespawn`: `assets/ui/loadout_random_respawn.svg`.
- `MirrorLoadout`: `assets/ui/loadout_mirror.svg`.

## Runtime Follow-Up

Later runtime work should define how the active mode is chosen from `EnabledLoadoutModes` when several are selected, then apply mode-specific behavior:

- `BuyOnSpawn`: clear gear on death/respawn and open or allow buy flow during spawn.
- `PersistentBudget`: enforce item costs against the configured budget while preserving equipped loadout after death.
- `RandomOnRespawn`: choose an authoritative random loadout on the host/server each respawn and sync it to peers.
- `MirrorLoadout`: choose one authoritative shared loadout and apply it to all players.

Until that runtime pass, current item-room behavior still preserves equipped loadout through death and refills uses on respawn.
