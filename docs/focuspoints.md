# Focus Points

This file tracks what to focus on in the next working session.

## Next Session Goal

Start implementing player controls and actions on the LAN damage-test player setup.

The target is not full gameplay polish. The target is a realistic LAN test slice where runtime player bodies are keyed by `GlobalId`, resolve ownership through `Networking.MultiplayerData`, and can be moved or acted through the same player identity model that the real game will use.

## Primary Focus

- Continue from branch `feature/player-controls-actions`.
- Use `Scenes/Tests/TestMapDestructionLogicLAN.tscn` as the first controls/actions test bed.
- Keep runtime player objects keyed only by `GlobalId`.
- Resolve `PeerId`, `LocalId`, display name, and future team data through `Networking.MultiplayerData.GetPlayerByGlobalId(GlobalId)`.
- Add movement/action code in a way that can later become server-authoritative.

## Implementation Order

1. Read `docs/combat-lan-test-handoff.md` first.
2. Inspect `DamageTestPlayer` and `TestMapDestructionLogicLAN` before editing.
3. Add a minimal movement/input path for owned local players in the LAN test.
4. Use `GlobalId -> PlayerData -> PeerId/LocalId` for ownership checks.
5. Keep dead players unable to move or act because `DamageTestPlayer` disables conflicting systems on death.
6. Add clear status/debug output for which player is controlled by which local input.
7. Run `dotnet build MultiplayerArenaV2.csproj` and `godot --headless --path . --quit` after implementation.

## Test Scene Requirements

The LAN test scene should prove these cases next:

- Host player uses real `PlayerData` with `GlobalId 0`, `PeerId 1`, `LocalId 0`.
- First connecting client peer registers two local players using the real join flow.
- Runtime player bodies are created from `Networking.MultiplayerData.Players`, not from hardcoded mock ids.
- Input ownership can be resolved from `GlobalId` through `PlayerData`.
- Dead player bodies cannot move or act until respawn.
- Reset/respawn restores health and disabled hitbox/collision/process features.

## Current Runtime Model To Use

- `PlayerData` persists as match/network identity in `Networking.MultiplayerData.Players`.
- `DamageTestPlayer` is the current temporary runtime player body in the LAN test.
- `DamageTestPlayer.GlobalId` is the runtime body's only identity key.
- `MultiplayerData.GetPlayerByGlobalId(int globalId)` resolves ownership data.
- `HealthContainer`, `DamageContainer`, and `ArmorResource` are shared by players, props, and walls.
- `DamageTestPlayer.Respawn(...)` resets health and restores disabled features.

## Current Test Objects

- Walls use `WallDamageData` with `HealthContainer` and biome-configured armor.
- Props use `LevelPropData` and `LevelProp` with the shared combat backend.
- Player damage targets use `DamageTestPlayer` and real `PlayerData` entries from `Networking`.
- LAN test damage priority is player, prop, then wall.

## Keep Deferred

- Full item/inventory purchase validation. The previous item-system notes remain in `docs/player-items-inventory-plan.md`.
- Final gameplay scene structure.
- Full Easy Networking / RTC transport.
- Full late-join snapshot sync for map, props, and player runtime health.
- Full weapon/projectile scenes.
- Status-effect ticking for props/walls beyond stored `HealthContainer.ActiveStatusEffects`.

## Relevant Docs

- `docs/combat-lan-test-handoff.md`
- `docs/player-items-inventory-plan.md`
- `README.md`
