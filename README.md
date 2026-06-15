# MultiplayerArenaV2

MultiplayerArenaV2 is an early-stage Godot/C# project for a fast, easy-to-pick-up top-down 2D arena shooter with local/LAN multiplayer and destructible arenas.

The project is in the early gameplay and systems phase. Destructible map logic, local/LAN networking flow, lobby setup, damage-test player controls, temporary weapon visuals, generic projectile/throwable execution, simplified armor-driven loadouts, and game-mode configuration have partial working implementations. Full gameplay, production networking, complete weapons, finalized arenas, HUD polish, and complete game modes are still in progress.

## Core Concept

The game is a top-down 2D multiplayer arena shooter built around quick rounds, simple movement/combat controls, and small arena-based matches.

Destructibility is a core pillar: maps should be fully destructible, including walls, arena structures, and props. Destructible gameplay state should stay consistent across movement, bullets, explosions, line-of-sight, rendering, and multiplayer sync.

## Quick Commands

Build the C# project without opening the Godot editor:

```bash
dotnet build MultiplayerArenaV2.csproj
```

Import assets from the CLI when new Godot assets were added:

```bash
./tools/import-assets.sh
```

Validate that Godot can start the project without opening the editor window:

```bash
./tools/verify-startup.sh
```

## Documentation Index

AI assistants working on this project should keep this index and the referenced docs updated as work progresses. Store relevant decisions, current implementation notes, deferred follow-ups, test scene notes, and future-reference context in the appropriate doc instead of leaving that knowledge only in chat history.

Start with [Docs Index](docs/index.md) for the full documentation map.

Use this as the documentation dictionary:

| Doc | Use For |
| --- | --- |
| [Docs Index](docs/index.md) | Starting point for documentation navigation, implementation docs, plans, and maintenance rules. |
| [Development Reference](docs/development-reference.md) | CLI commands, C# style rules, tech stack, target platforms, project status, and broad development goals. |
| [Game Concept](docs/game-concept.md) | High-level game concept, planned game modes, and core design direction. |
| [Destructible Environment](docs/destructible-environment.md) | Destructible map data model, wall damage rules, tile rendering, debug tile assets, props, and destruction authority rules. |
| [Multiplayer And Networking](docs/multiplayer-networking.md) | Local/online multiplayer model, split-screen identity rules, lobby/setup config, network modes, transport direction, debug overlay, settings, and RPC state sync. |
| [Game Logging](docs/game-logging.md) | Shared runtime logging format, `GameLog` API rules, scopes/types, and LAN multi-process terminal logging guidance. |
| [Test Scenes](docs/test-scenes.md) | Current test scenes, how to launch them, controls, test-specific notes, and runtime logging conventions. |
| [Combat LAN Test Handoff](docs/combat-lan-test-handoff.md) | Current combat/LAN handoff for the shared damage backend, LAN damage-test player runtime, and immediate player control/action context. |
| [Player Items And Loadout Plan](docs/player-items-inventory-plan.md) | Simplified player item model: armor-driven weapon/gadget capacity, ammo/use reset rules, theme, and purchase-mode direction. |
| [Player HUD UI Plan](docs/player-hud-ui-plan.md) | Planned local player stats HUD, reusable player panel scene, equipment/ammo/armor display, and split-screen UI scaling rules. |
| [Modern Item Content Plan](docs/modern-item-content-plan.md) | First modern-only item content list, including planned weapon tiers, launchers, and grenades. |
| [Asset Organization](docs/asset-organization.md) | Asset folder ownership rules, including item versus projectile art placement. |
| [Focus Points](docs/focuspoints.md) | Next-session implementation focus and short working checklist. |
| [SVG Input Icon Generation](docs/svg-input-icon-generation.md) | SVG input-icon generation approach and why button labels are generated as vector geometry instead of SVG `<text>`. |

## Current Test Entry Points

Use separate scenes for destruction testing and player/item testing.

On Linux/Bash, use the helper scripts for multi-instance LAN testing. Each script starts one host/server instance and two client instances by default:

```bash
./tools/testing/launch-player-item-room-lan.sh
./tools/testing/launch-destruction-lan.sh
```

Override defaults with environment variables:

```bash
CLIENTS=3 PORT=7800 GODOT_BIN=godot ./tools/testing/launch-player-item-room-lan.sh
```

Logs are written to `.tmp/test-logs/`. Press `Ctrl+C` in the script terminal to stop all spawned instances.

Destruction LAN host/client without the helper script:

```bash
godot --path . res://scenes/tests/test_map_destruction_logic_lan.tscn -- --role host & \
godot --path . res://scenes/tests/test_map_destruction_logic_lan.tscn -- --role client --address 127.0.0.1 --port 12000
```

Player/item LAN host/client without the helper script:

```bash
godot --path . res://scenes/tests/test_player_item_room_lan.tscn -- --role host & \
godot --path . res://scenes/tests/test_player_item_room_lan.tscn -- --role client --address 127.0.0.1 --port 12000
```

Keep detailed test scene notes in `docs/test-scenes.md`.

## Tech Stack

| Area | Details |
| --- | --- |
| Engine | Godot 4.6 |
| Language | C# |
| Renderer | GL Compatibility |
| Project Type | 2D game |
| Primary Platform | PC |

## Prerequisites

- Godot 4.6 with C#/.NET support.
- .NET SDK compatible with the Godot C# project.
- Git for cloning and version control.

## Setup

Clone the repository:

```bash
git clone git@github.com:OL3s/MultiplayerArenaV2.git
cd MultiplayerArenaV2
```

Restore/build the C# project:

```bash
dotnet build MultiplayerArenaV2.csproj
```

Import Godot assets after cloning or after asset changes:

```bash
./tools/import-assets.sh
```

## Running

Open the project in Godot:

```bash
godot --path .
```

Run the player/item LAN test scene directly:

```bash
godot --path . res://scenes/tests/test_player_item_room_lan.tscn -- --role host
```

Validate startup without opening the editor window:

```bash
./tools/verify-startup.sh
```

## Dependencies

- No external Godot addons are currently included in the repository.
- The planned networking direction references Easy Networking and RTC, but those pieces are not yet present.
- Current local multi-instance testing uses Godot's built-in multiplayer transport.

## Development Notes

- Keep README general and keep detailed implementation notes in `docs/`.
- Update the relevant doc when changing test scenes, networking, destructible map logic, item plans, or current focus.
- Follow the C# style rules in [Development Reference](docs/development-reference.md).
