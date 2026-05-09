# Focus Points

This file tracks what to focus on in the next working session.

## Next Session Goal

Start the player item/action slice.

The next target is to make players use simple working items first, then grow that into inventory, armor, movement-speed, magazine, and dependency rules. Keep this as a working gameplay slice in the LAN test setup before building final UI or purchase flow.

## Primary Focus

- Continue from `main` unless a new feature branch is created for the item slice.
- Read `docs/player-items-inventory-plan.md` first.
- Use `Scenes/Tests/TestMapDestructionLogicLAN.tscn` as the first runtime test bed.
- Keep `DamageTestPlayer.GlobalId` as the runtime identity key and resolve ownership through `Networking.MultiplayerData.GetPlayerByGlobalId(GlobalId)`.
- Keep item use shaped like future server-authoritative commands: local input requests an item action, host/server validates and applies it, clients display the result.

## Next Implementation Order

1. Add the smallest useful player item/action data model: base item, equipable item, and objective/effect data.
2. Add one simple working shootable weapon and one simple throwable grenade-style item.
3. Route weapon/grenade damage through the existing `DamageContainer -> HealthContainer` backend.
4. Use exact aim vectors for actual shot/throw actions, while keeping estimated aim for normal remote display.
5. Add temporary runtime item ownership on `DamageTestPlayer` or a small player runtime data object before building full inventory.
6. After items work, add the inventory/armor model: equipped armor, inventory providers, carried equipables, typed slots, and validation.
7. Add movement-speed effects from armor, carried weight, or item loadout after the item/inventory state exists.
8. Add magazine/ammo reserve dependencies after shootable weapons exist, so reload capacity is tested against real item use.
9. Run `dotnet build MultiplayerArenaV2.csproj` and `godot --headless --path . --quit` after implementation. Run `godot --headless --path . --import` when adding or changing assets.

## First Test Cases

- Host/local player can use a simple shootable item with keyboard/mouse aim.
- Client gamepad players can use the same item path with their local aim/fallback aim model.
- A thrown/grenade item can apply radius damage to players, props, and destructible walls through the shared damage backend.
- Item actions use exact aim at action time, not only the quantized estimated aim state.
- Dead players cannot use items until respawn.
- The first item data model does not hardcode modern-only assumptions so medieval-style items can be added later.

## Keep In Mind

- Do not build the full purchase menu first. Build working item actions first.
- Do not build the full inventory UI first. Add inventory validation after items exist.
- Keep magazine reserves separate from normal carried item slots.
- Keep armor protection and inventory capacity separate: armor can provide protection, movement penalties, and slot/provider rules.
- Spawn/respawn overlap-safe placement is still needed, but the next gameplay slice is player items/actions unless spawn blocking becomes a direct blocker.

## Relevant Docs

- `docs/player-items-inventory-plan.md`
- `docs/combat-lan-test-handoff.md`
- `docs/test-scenes.md`
- `docs/multiplayer-networking.md`
- `docs/destructible-environment.md`
