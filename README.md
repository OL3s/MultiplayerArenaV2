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

- `docs/development-reference.md`: CLI commands, C# style rules, tech stack, target platforms, project status, and broad development goals.
- `docs/game-concept.md`: high-level game concept, planned game modes, and core design direction.
- `docs/destructible-environment.md`: destructible map data model, wall damage rules, tile rendering, debug tile assets, props, and destruction authority rules.
- `docs/multiplayer-networking.md`: local/online multiplayer model, split-screen identity rules, lobby/setup config, network modes, transport direction, debug overlay, settings, and RPC state sync.
- `docs/test-scenes.md`: current test scenes, how to launch them, controls, test-specific notes, and runtime logging conventions.
- `docs/combat-lan-test-handoff.md`: current combat/LAN handoff for the shared damage backend, LAN damage-test player runtime, and immediate player control/action context.
- `docs/player-items-inventory-plan.md`: planned player item, inventory, weight, backstrap, magazine reserve, theme, and purchase-mode model.
- `docs/focuspoints.md`: next-session implementation focus and short working checklist.
- `docs/svg-input-icon-generation.md`: SVG input-icon generation approach and why button labels are generated as vector geometry instead of SVG `<text>`.

## Current Test Entry Point

The main active runtime test bed is `Scenes/Tests/TestMapDestructionLogicLAN.tscn`.

Start one host and one client from the same terminal:

```bash
godot --path . res://Scenes/Tests/TestMapDestructionLogicLAN.tscn -- --role host & \
godot --path . res://Scenes/Tests/TestMapDestructionLogicLAN.tscn -- --role client --address 127.0.0.1 --port 7700 & \
disown
```

Keep detailed test scene notes in `docs/test-scenes.md`.
