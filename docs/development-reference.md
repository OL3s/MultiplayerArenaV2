# Development Reference

This document tracks project-level development reference information that should stay stable and easy to find.

## CLI Commands

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

## C# Code Style

Do not follow the standard C# brace style in this project. Follow these rules instead:

- Put the opening brace on the same line as the function, class, control statement, or other block declaration.
- Do not put a newline between the declaration and `{`.
- If a conditional or loop has only one statement in its body, do not use `{}`.

Example:

```csharp
private void DoThing() {
    RunThing();
}

if (a == b)
    c++;
```

## Tech

- Engine: Godot 4.6
- Language: C#
- Renderer: GL Compatibility
- Project type: 2D game

## Target Platforms

- PC: primary target
- Mobile: planned
- Browser: possible later
- Console: possible later

## Project Status

This project is in the early gameplay and systems phase. Destructible map logic, local/LAN networking flow, lobby setup, damage-test player controls, temporary weapon visuals, and game-mode configuration have partial working implementations, while full gameplay, production networking, complete weapons, finalized arenas, and complete game modes are still in progress.

## Development Goals

- Build a solid top-down movement and shooting foundation.
- Add local and online multiplayer support.
- Treat split-screen integration as a core multiplayer requirement across local and online flows.
- Support multiple local players per device for both hosts and clients.
- Use a 4-slot local lobby on the main menu before hosting or joining.
- Target up to 4 teams with up to 16 active players total.
- Use a `Networking` autoload as the single place for network mode state.
- Create reusable arena and game mode systems.
- Build a consistent destructible environment system where tile logic and visuals stay in sync.
- Keep controls simple across keyboard, controller, touch, and future platform targets.
- Expand with more weapons, maps, and match rules over time.
