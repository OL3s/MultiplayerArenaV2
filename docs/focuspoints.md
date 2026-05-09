# Focus Points

This file tracks what to focus on in the next working session.

## Next Session Goal

Create a dedicated player item/action test scene, then start the player item/action slice there.

The next target is to make players use simple working items first, then grow that into inventory, armor, movement-speed, magazine, and dependency rules. Build a new focused test scene for this slice instead of continuing to overload the LAN destruction test scene. Reuse the LAN test runtime patterns where useful, but keep the item/action test bed separate before building final UI or purchase flow.

## Primary Focus

- Continue from `main` unless a new feature branch is created for the item slice.
- Read `docs/player-items-inventory-plan.md` first.
- Create a new test scene under `Scenes/Tests/` for player items/actions, with a matching script under `Scripts/Data/Map/` or `Scripts/Data/Gameplay/` depending on the final scene responsibility.
- Use `Scenes/Tests/TestMapDestructionLogicLAN.tscn` as a reference for networking, damage targets, input ownership, and destructible wall/prop interaction, not as the primary item/action test scene.
- Keep `DamageTestPlayer.GlobalId` as the runtime identity key and resolve ownership through `Networking.MultiplayerData.GetPlayerByGlobalId(GlobalId)`.
- Keep item use shaped like future server-authoritative commands: local input requests an item action, host/server validates and applies it, clients display the result.

## Next Implementation Order

1. Create the dedicated player item/action test scene and wire it into the existing networking/local test startup pattern.
2. Bring over only the required runtime pieces from the LAN destruction test: player spawning, ownership resolution, aim/movement input, damage targets, props, and destructible wall interaction.
3. Add the smallest useful player item/action data model: base item, equipable item, and objective/effect data.
4. Add one simple working shootable weapon and one simple throwable grenade-style item.
5. Route weapon/grenade damage through the existing `DamageContainer -> HealthContainer` backend.
6. Use exact aim vectors for actual shot/throw actions, while keeping estimated aim for normal remote display.
7. Add temporary runtime item ownership on `DamageTestPlayer` or a small player runtime data object before building full inventory.
8. After items work, add the inventory/armor model: equipped armor, inventory providers, carried equipables, typed slots, and validation.
9. Add movement-speed effects from armor, carried weight, or item loadout after the item/inventory state exists.
10. Add magazine/ammo reserve dependencies after shootable weapons exist, so reload capacity is tested against real item use.
11. Run `dotnet build MultiplayerArenaV2.csproj` and `godot --headless --path . --quit` after implementation. Run `godot --headless --path . --import` when adding or changing assets.

## First Test Cases

- Host/local player can use a simple shootable item with keyboard/mouse aim.
- Client gamepad players can use the same item path with their local aim/fallback aim model.
- A thrown/grenade item can apply radius damage to players, props, and destructible walls through the shared damage backend.
- The new item/action test scene can be launched directly without relying on `TestMapDestructionLogicLAN.tscn` as the active scene.
- Item actions use exact aim at action time, not only the quantized estimated aim state.
- Dead players cannot use items until respawn.
- The first item data model does not hardcode modern-only assumptions so medieval-style items can be added later.

## Keep In Mind

- Do not build the full purchase menu first. Build working item actions first.
- Keep the first item/action content pass modern-only. Use `docs/modern-item-content-plan.md` for the planned weapon, launcher, and grenade list.
- Do not build the full inventory UI first. Add inventory validation after items exist.
- Keep magazine reserves separate from normal carried item slots.
- Keep armor protection and inventory capacity separate: armor can provide protection, movement penalties, and slot/provider rules.
- Spawn/respawn overlap-safe placement is still needed, but the next gameplay slice is player items/actions unless spawn blocking becomes a direct blocker.

## Relevant Docs

- `docs/player-items-inventory-plan.md`
- `docs/modern-item-content-plan.md`
- `docs/combat-lan-test-handoff.md`
- `docs/test-scenes.md`
- `docs/multiplayer-networking.md`
- `docs/destructible-environment.md`
