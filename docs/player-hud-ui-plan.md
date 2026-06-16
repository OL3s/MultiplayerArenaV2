# Player HUD UI Plan

This document tracks the planned in-match player stats and equipment HUD.

## Goal

Add a reusable player HUD that shows local player identity, combat stats, armor, weapon slots, gadget slots, loaded ammo, reload/recovery cooldowns, gadget readiness, and empty capacity while supporting up to 4 local players on one device.

The first implementation should be test-scene friendly and reusable. Build it as `.tscn` scenes instead of constructing the whole HUD directly inside `TestPlayerItemRoomLAN.cs`.

## Scene Structure

Planned reusable scene split:

- `scenes/ui/player_stats_panel.tscn`: one local player's stats/equipment panel.
- `scenes/ui/local_players_hud.tscn`: parent HUD/container that owns and lays out 1-4 `PlayerStatsPanel` instances.

Current first-pass implementation:

- `scenes/ui/hud/player_hud_card.tscn`: one compact pill-style local player card.
- `scenes/ui/hud/local_players_hud.tscn`: bottom-left `HBoxContainer` that lays out up to 4 local cards left to right.
- `scripts/ui/PlayerHudCard.cs`: card display API for identity, status, health, selected item/ammo, and gadget summary.
- `scripts/ui/LocalPlayersHud.cs`: card creation/removal and refresh API keyed by `GlobalId`.

In networked modes, local player cards are wrapped in a shared team pill container. The team wrapper uses the team color as its background tint and shows the display team id (`T1-T4`) on the right of the cards. In local-only mode, cards stay unwrapped so local split-screen/FFA setups remain visually independent.

`PlayerStatsPanel` should expose a script API that accepts simple runtime data or direct setters for the current display state. The game/test scene should not need to know internal label/icon node names.

`LocalPlayersHud` should be responsible for arranging panels for the current local players. It should support 1, 2, 3, and 4 local panels without overlapping core gameplay readability or the temporary item/equipment menu.

## Per-Player Panel Content

Each player panel should show:

- Player name from `PlayerData.DisplayName`.
- Avatar image or placeholder avatar.
- Kills.
- Health/status, including dead/recovering states when available.
- Current equipped/selected weapon or active item.
- Loaded weapon ammo and reload state.
- Gadget readiness and recovery state.
- Equipped armor and armor status.
- Weapon slots.
- Gadget slots.
- Empty slot placeholders for available but unfilled item capacity.

Empty slots should be visible as intentional placeholders. Do not hide empty capacity, because the player needs to understand what they can still buy, carry, or assign.

## Local Multiplayer Layout

The HUD must be designed around local split-screen/player sharing from the start.

Rules:

- One running instance can own up to 4 local players.
- The HUD should create one `PlayerStatsPanel` per local player, not one panel per connected peer.
- Panel data should resolve through `PlayerData` using `GlobalId`, while local ownership comes from `PlayerData.IsLocalPlayer` and `LocalId`.
- Layout should remain readable with 4 panels active.
- The same scene should work in local-only, host-local, and client-local modes.

Suggested first layout:

- 1 local player: one wider panel in a corner or bottom edge.
- 2 local players: two compact panels along the bottom or opposite corners.
- 3-4 local players: compact grid or corner layout, keeping the center clear for gameplay.

Final placement can change, but the scene API should not assume only one local player.

## Data Sources

Initial data can come from the player/item LAN test runtime:

- `Networking.MultiplayerData.GetPlayerByGlobalId(GlobalId)` for name, local id, peer id, and local ownership.
- `DamageTestPlayer.Health` for health/status.
- Runtime equipment data for selected item, armor, weapon slots, gadget slots, loaded ammo, weapon reload/recovery timers, gadget readiness, gadget recovery timers, and empty slots.
- Match scoring state for kills once available.

The HUD should tolerate missing data while the item system is still being built. Unknown values should show placeholders like empty slots, `0`, or `--` instead of crashing or hiding entire sections.

## Test Scene Integration

First integration target:

- Scene: `scenes/tests/test_player_item_room_lan.tscn`
- Script: `scripts/data/gameplay/TestPlayerItemRoomLAN.cs`

The test room instantiates `scenes/ui/hud/local_players_hud.tscn` under its `CanvasLayer`. Runtime code updates the HUD when player status text updates, which covers player spawn/despawn, item selection, armor changes, health changes, item uses, dead state, and spawning state in the current test scenes.

The `B` item grid remains a debug/equipment menu. The primary buy UI is the `V`/Xbox `Y` radial buy menu. The HUD is a passive status display that should remain useful while buy menus are closed and should not consume gameplay input.

## Match Config Strip

`scenes/ui/hud/scoreboard_overlay.tscn` includes `scenes/ui/hud/match_config_strip.tscn` at the bottom of the Tab scoreboard overlay. The strip is intentionally short and scene-driven, with `match_config_entry.tscn` instances for mode, loadout, structure, biome, item theme, and seed. `ArenaMatch` binds the host-resolved active setup values and existing SVG icons into the strip through `ScoreboardOverlay.SetMatchConfig()`.

## First Pass Acceptance

- HUD uses `.tscn` scenes for reusable UI structure.
- HUD displays up to 4 local player panels at once in a bottom-left horizontal row.
- Each first-pass card shows local id, display name, alive/dead/spawning state, health, selected item icon, selected item ammo/use pips, and first gadget uses/empty state.
- The base status label is color-coded for readability, while urgent states still use the card overlay prompt/progress layer.
- Ammo display uses repeated vertical caliber SVG pips instead of numbers. Available rounds use the caliber's normal color; spent rounds are blacked out. Current calibers are `Standard`, `Heavy`, and `Shell`, with item data defaulting to `Standard` until specific items are categorized.
- HUD updates when selecting weapons or armor in `TestPlayerItemRoomLAN`.
- HUD does not break LAN host/client testing or local-only scene startup.
- HUD remains readable at desktop resolution and does not overlap the active aim indicator in the center of the screen.
