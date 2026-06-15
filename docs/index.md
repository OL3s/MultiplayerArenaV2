# Documentation Overview

This directory is the project memory for MultiplayerArenaV2. Keep durable decisions, implementation notes, test-scene behavior, follow-up tasks, and system plans here instead of relying on chat history.

The root `README.md` should stay concise and newcomer-friendly. Detailed technical notes belong in these docs.

## Start Here

| Need | Read |
| --- | --- |
| Project setup, build commands, style rules, broad status | [Development Reference](development-reference.md) |
| High-level game direction | [Game Concept](game-concept.md) |
| Current next-session checklist | [Focus Points](focuspoints.md) |
| Test scene launch commands and controls | [Test Scenes](test-scenes.md) |

## Implementation Docs

| Area | Doc |
| --- | --- |
| Multiplayer, local lobby, network modes, setup sync, overlays | [Multiplayer And Networking](multiplayer-networking.md) |
| Shared runtime logging format and `GameLog` usage | [Game Logging](game-logging.md) |
| Destructible walls, props, tile rendering, combat damage model | [Destructible Environment](destructible-environment.md) |
| Current combat and LAN player/item handoff | [Combat LAN Test Handoff](combat-lan-test-handoff.md) |
| Asset folder ownership and visual role rules | [Asset Organization](asset-organization.md) |
| SVG input icon generation rules | [SVG Input Icon Generation](svg-input-icon-generation.md) |

## Plans

| Plan | Doc |
| --- | --- |
| Player items, simplified armor-driven loadouts, weapons, gadgets, ammo reloads, and gadget refreshes | [Player Items And Loadout Plan](player-items-inventory-plan.md) |
| Local player stats/equipment HUD | [Player HUD UI Plan](player-hud-ui-plan.md) |
| First modern weapon, armor, throwable, and projectile content pass | [Modern Item Content Plan](modern-item-content-plan.md) |

## Current Working Context

- Main scene: `scenes/ui/menus/main_menu.tscn`.
- Primary active gameplay test bed: `scenes/tests/test_player_item_room_lan.tscn`.
- Destruction-focused LAN test bed: `scenes/tests/test_map_destruction_logic_lan.tscn`.
- Current technical direction: build data/resource-driven gameplay systems first, keep UI reusable, and keep multiplayer behavior shaped around server authority.
- Current next work: rework ammo so item `.tres` resources own base weapon reload and gadget refresh cooldowns, while armor applies percentage cooldown modifiers instead of granting extra magazines or gadget uses.

## Maintenance Rules

- Update [Focus Points](focuspoints.md) when the next-session priority changes.
- Update [Test Scenes](test-scenes.md) when controls, launch commands, or test-scene responsibilities change.
- Update [Multiplayer And Networking](multiplayer-networking.md) when lobby, setup sync, network mode, identity, or RPC behavior changes.
- Update [Game Logging](game-logging.md) when log format, scopes, types, or logging policy changes.
- Update [Destructible Environment](destructible-environment.md) when wall, prop, combat, tile, or destruction-authority behavior changes.
- Update [Asset Organization](asset-organization.md) when adding new asset families or changing folder ownership rules.
- Keep plans as plans, and move implemented behavior into the relevant implementation doc once it becomes real.
