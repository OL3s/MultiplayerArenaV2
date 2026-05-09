# MultiplayerArenaV2

MultiplayerArenaV2 is an early-stage Godot project for a fast, easy-to-pick-up top-down 2D arena shooter.

The project is in the early gameplay and systems phase. Destructible map logic, local/LAN networking flow, lobby setup, damage-test player controls, temporary weapon visuals, and game-mode configuration have partial working implementations, while full gameplay, production networking, complete weapons, finalized arenas, and complete game modes are still in progress.

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
godot --headless --path . --import
```

Validate that Godot can start the project without opening the editor window:

```bash
godot --headless --path . --quit
```

## Documentation Index

AI assistants working on this project should keep this index and the referenced docs updated as work progresses. Store relevant decisions, current implementation notes, deferred follow-ups, test scene notes, and future-reference context in the appropriate doc instead of leaving that knowledge only in chat history.

Use this as the documentation dictionary:

| Doc | Use For |
| --- | --- |
| [Development Reference](docs/development-reference.md) | CLI commands, C# style rules, tech stack, target platforms, project status, and broad development goals. |
| [Game Concept](docs/game-concept.md) | High-level game concept, planned game modes, and core design direction. |
| [Destructible Environment](docs/destructible-environment.md) | Destructible map data model, wall damage rules, tile rendering, debug tile assets, props, and destruction authority rules. |
| [Multiplayer And Networking](docs/multiplayer-networking.md) | Local/online multiplayer model, split-screen identity rules, lobby/setup config, network modes, transport direction, debug overlay, settings, and RPC state sync. |
| [Test Scenes](docs/test-scenes.md) | Current test scenes, how to launch them, controls, test-specific notes, and runtime logging conventions. |
| [Combat LAN Test Handoff](docs/combat-lan-test-handoff.md) | Current combat/LAN handoff for the shared damage backend, LAN damage-test player runtime, and immediate player control/action context. |
| [Player Items And Inventory Plan](docs/player-items-inventory-plan.md) | Planned player item, inventory, weight, backstrap, magazine reserve, theme, and purchase-mode model. |
| [Modern Item Content Plan](docs/modern-item-content-plan.md) | First modern-only item content list, including planned weapon tiers, launchers, and grenades. |
| [Asset Organization](docs/asset-organization.md) | Asset folder ownership rules, including item versus projectile art placement. |
| [Focus Points](docs/focuspoints.md) | Next-session implementation focus and short working checklist. |
| [SVG Input Icon Generation](docs/svg-input-icon-generation.md) | SVG input-icon generation approach and why button labels are generated as vector geometry instead of SVG `<text>`. |

## Current Test Entry Points

Use separate scenes for destruction testing and player/item testing.

Destruction LAN host/client:

```bash
godot --path . res://Scenes/Tests/TestMapDestructionLogicLAN.tscn -- --role host & \
godot --path . res://Scenes/Tests/TestMapDestructionLogicLAN.tscn -- --role client --address 127.0.0.1 --port 7700
```

Player/item LAN host/client:

```bash
godot --path . res://Scenes/Tests/TestPlayerItemRoomLAN.tscn -- --role host & \
godot --path . res://Scenes/Tests/TestPlayerItemRoomLAN.tscn -- --role client --address 127.0.0.1 --port 7700
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
godot --headless --path . --import
```

## Running

Open the project in Godot:

```bash
godot --path .
```

Run the player/item LAN test scene directly:

```bash
godot --path . res://Scenes/Tests/TestPlayerItemRoomLAN.tscn -- --role host
```

Validate startup without opening the editor window:

```bash
godot --headless --path . --quit
```

## Dependencies

- No external Godot addons are currently included in the repository.
- The planned networking direction references Easy Networking and RTC, but those pieces are not yet present.
- Current local multi-instance testing uses Godot's built-in multiplayer transport.

## Development Notes

- Keep README general and keep detailed implementation notes in `docs/`.
- Update the relevant doc when changing test scenes, networking, destructible map logic, item plans, or current focus.
- Follow the C# style rules in [Development Reference](docs/development-reference.md).
