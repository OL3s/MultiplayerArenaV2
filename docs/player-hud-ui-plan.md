# Player HUD UI Plan

This document tracks the planned in-match player stats and equipment HUD.

## Goal

Add a reusable player HUD that shows local player identity, combat stats, armor, weapon slots, gadget slots, remaining uses, and empty capacity while supporting up to 4 local players on one device.

The first implementation should be test-scene friendly and reusable. Build it as `.tscn` scenes instead of constructing the whole HUD directly inside `TestPlayerItemRoomLAN.cs`.

## Scene Structure

Planned reusable scene split:

- `scenes/ui/player_stats_panel.tscn`: one local player's stats/equipment panel.
- `scenes/ui/local_players_hud.tscn`: parent HUD/container that owns and lays out 1-4 `PlayerStatsPanel` instances.

`PlayerStatsPanel` should expose a script API that accepts simple runtime data or direct setters for the current display state. The game/test scene should not need to know internal label/icon node names.

`LocalPlayersHud` should be responsible for arranging panels for the current local players. It should support 1, 2, 3, and 4 local panels without overlapping core gameplay readability or the temporary item/equipment menu.

## Per-Player Panel Content

Each player panel should show:

- Player name from `PlayerData.DisplayName`.
- Avatar image or placeholder avatar.
- Kills.
- Health/status, including dead/recovering states when available.
- Current equipped/selected weapon or active item.
- Remaining weapon uses/ammo.
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
- Runtime equipment data for selected item, armor, weapon slots, gadget slots, remaining uses, and empty slots.
- Match scoring state for kills once available.

The HUD should tolerate missing data while the item system is still being built. Unknown values should show placeholders like empty slots, `0`, or `--` instead of crashing or hiding entire sections.

## Test Scene Integration

First integration target:

- Scene: `scenes/tests/test_player_item_room_lan.tscn`
- Script: `scripts/data/gameplay/TestPlayerItemRoomLAN.cs`

The test room should instantiate `local_players_hud.tscn` or include it under its `CanvasLayer`. Runtime code should update the HUD when players spawn/despawn, item selection changes, armor changes, health changes, item uses change, or scoring changes.

The temporary `B` item grid remains a debug/equipment menu, not the final buy wheel. The new HUD is a passive status display that should remain useful while the menu is closed and should not consume gameplay input.

## First Pass Acceptance

- HUD uses `.tscn` scenes for reusable UI structure.
- HUD displays up to 4 local player panels at once.
- Each panel shows name, avatar placeholder, kills placeholder, health, selected item, armor, weapon slots, gadget slots, remaining uses, and empty slots.
- HUD updates when selecting weapons or armor in `TestPlayerItemRoomLAN`.
- HUD does not break LAN host/client testing or local-only scene startup.
- HUD remains readable at desktop resolution and does not overlap the active aim indicator in the center of the screen.
