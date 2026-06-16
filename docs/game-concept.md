# Game Concept

This document tracks the high-level game direction.

## Core Concept

- Top-down 2D arena shooter.
- Simple movement and combat controls.
- Small arena-based matches.
- Multiple game modes.
- Fully destructible environments.
- Quick rounds and multiplayer-focused gameplay.

## Planned Game Modes

- `Deathmatch`
- `CaptureTheFlag`
- `KingOfTheHill`
- `Headquarters`
- Free-for-all is not a separate mode in the current design. A deathmatch setup with one player per team covers the same gameplay shape for now.

## Game Mode Contracts

Neutral objectives and team spawn bases can exist on every match map. Game modes decide whether to ignore them, read their occupancy, or attach mode-specific behavior. Objective scenes should not hardcode scoring, capture timing, flag rules, or round endings.

- `Deathmatch`: uses normal team/player spawns and ignores neutral objectives by default.
- `CaptureTheFlag`: uses team spawn bases as flag/base anchors. The center neutral objective can exist but is ignored unless a specific CTF variant uses it.
- `KingOfTheHill`: uses the wide/outer center neutral objective area. A team wins or scores by holding the center core for `30` seconds straight. More teammates inside should make progress faster.
- `Headquarters`: activates one neutral objective, using a random neutral objective spawn/candidate when available. A team must hold it for `3` seconds to capture. After capture, the owning team gains points while holding it. Another team can remove/take it by holding for `3` seconds after it is capped. Capture speed is based on `owning contest players - other team players` in the objective area.

## Current Direction

The game should prioritize fast readable combat, local/online multiplayer flexibility, and destructible arena play. Systems should remain generic enough for multiple item themes. Modern is ranged-weapon focused. Medieval has started as a small ranged/gadget/armor data slice and should eventually become more melee-focused when melee runtime work is intentionally implemented.
