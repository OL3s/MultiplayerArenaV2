# Focus Points

This file tracks what to focus on in the next working session.

## Next Session Goal

Continue the player item/action slice inside the dedicated player item/action LAN test scene, with the next focus on expanding and hardening real item execution scenes.

The first generic runtime `.tscn` pieces now exist: one generic bullet scene, one generic thrown-item scene, and one generic launched-projectile scene. Use `Scenes/Tests/TestPlayerItemRoomLAN.tscn` for this slice instead of continuing to overload the LAN destruction test scene. Reuse LAN runtime patterns where useful, but keep item/action testing separate before building final UI or purchase flow.

## Primary Focus

- Continue from `main` unless a new feature branch is created for the item slice.
- Read `docs/player-items-inventory-plan.md` first.
- Use `Scenes/Tests/TestPlayerItemRoomLAN.tscn` and `Scripts/Data/Gameplay/TestPlayerItemRoomLAN.cs` as the primary player item/action test bed.
- Keep `Scenes/Tests/TestMapDestructionLogicLAN.tscn` focused on destructible map and prop damage sync; it no longer spawns player targets for item testing.
- Keep `DamageTestPlayer.GlobalId` as the runtime identity key and resolve ownership through `Networking.MultiplayerData.GetPlayerByGlobalId(GlobalId)`.
- Implement every modern item that currently has an SVG image, including all weapon tiers and all grenade types listed in `docs/modern-item-content-plan.md`.
- Keep item use shaped like future server-authoritative commands: local input requests an item action, host/server validates and applies it, clients display the result.

## Modern Items To Implement

- `Pistol-T1`, `Pistol-T2`, `Pistol-T3`
- `Smg-T1`, `Smg-T2`, `Smg-T3`
- `AR-T1`, `AR-T2`, `AR-T3`
- `Rifle-T1`, `Rifle-T2`, `Rifle-T3`
- `Rocketlauncher`
- `Grenadelauncher-T1`, `Grenadelauncher-T2`
- `NadeExplosive`, `NadeIncendiary`, `NadeSmoke`

## Next Implementation Order

1. Test and tune the generic bullet scene and script for shootable weapons. It uses exact aim plus `PlayerItemAccuracyState.CurrentAccuracy` spread, sweeps movement over physics ticks, tracks already-hit objects/wall tiles to prevent duplicate hits, resolves collision against players, props, and destructible walls, and routes damage through `DamageContainer -> HealthContainer`.
2. Test and tune the generic thrown-item scene and script for hand-thrown items. It uses throw strength/range projection, a simple up/down arc animation, fuse/rest behavior, and fallback explosive/incendiary/smoke objectives.
3. Test and tune the generic launched-projectile scene and script for launcher-style items. It stays separate from carried launcher item resources and supports rocket/grenade-launcher projectile behavior through `PlayerProjectileData`.
4. Expand authored projectile/objective data resources beyond the first shared modern profiles if individual tiers need different bullet/projectile speed, width, color, damage, radius, or collision behavior.
5. Add stronger runtime validation and replicated damage-state catch-up after the basic server-authoritative request/sync path is stable.
6. Add temporary runtime item ownership on `DamageTestPlayer` or a small player runtime data object before building full inventory.
10. After all currently imaged modern items execute, add the inventory/armor model: equipped armor, inventory providers, carried equipables, typed slots, and validation.
11. Add magazine/ammo reserve dependencies after shootable weapons exist, so reload capacity is tested against real item use.
12. Run `dotnet build MultiplayerArenaV2.csproj` and `godot --headless --path . --quit` after implementation. Run `godot --headless --path . --import` when adding or changing assets.

## First Test Cases

- Host/local player can use a simple shootable item with keyboard/mouse aim.
- Client gamepad players can use the same item path with their local aim/fallback aim model.
- Generic bullet, thrown-item, and launched-projectile scenes can each be instantiated from item execution code.
- Every currently imaged modern weapon tier can be selected in `TestPlayerItemRoomLAN` and executes through the same item-use path.
- Every currently imaged modern grenade can be selected in `TestPlayerItemRoomLAN` and executes through the same throwable path.
- A thrown/grenade item can apply radius damage to players, props, and destructible walls through the shared damage backend.
- The new item/action test scene can be launched directly without relying on `TestMapDestructionLogicLAN.tscn` as the active scene.
- Item actions use exact aim at action time, not only the quantized estimated aim state.
- Shootable weapons apply spread around exact aim using item accuracy stats, and sustained fire naturally becomes less accurate based on pushback versus recovery.
- Shot inaccuracy recovers separately from movement inaccuracy. Movement penalty snaps worse instantly, then recovers by item-specific movement recovery when slowing down or stopping.
- Local aiming shows an aim line and crosshair/circle derived from the same current accuracy value used for firing spread.
- Crosshair/circle radius changes with projection distance and current accuracy, instead of using a fixed screen/world radius.
- Gun aim indicators use item range with a readable display cap for long-range weapons that reach beyond the screen, and stop at sampled collision so hit/miss readability is clearer.
- Throwable aim indicators use predicted collision or throw endpoint, with gamepad aim-vector strength scaling throw distance.
- Dead players cannot use items until respawn.
- The first item data model does not hardcode modern-only assumptions so medieval-style items can be added later.

## Keep In Mind

- Do not build the full purchase menu first. Build working item actions first.
- Keep the first item/action content pass modern-only. The target is all currently imaged modern items, including each tier, before expanding into UI or future themes.
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
