# Focus Points

This file tracks what to focus on in the next working session.

## Next Session Goal

Continue the player equipment slice inside the dedicated player item/action LAN test scene, with the next focus on ammunition, armor, inventory providers, carried item slots, validation, and a scalable local-player stats HUD.

The generic item execution path is now established enough to start simulating real player equipment instead of only test overrides. Use `scenes/tests/test_player_item_room_lan.tscn` for this slice instead of continuing to overload the LAN destruction test scene. Keep the first version test-scene driven and data/resource driven before building final buy-wheel UI or purchase flow.

## Primary Focus

- Continue from `main` unless a new feature branch is created for the item slice.
- Read `docs/player-items-inventory-plan.md` first.
- Use `scenes/tests/test_player_item_room_lan.tscn` and `scripts/data/gameplay/TestPlayerItemRoomLAN.cs` as the primary player item/action test bed.
- Keep `scenes/tests/test_map_destruction_logic_lan.tscn` focused on destructible map and prop damage sync; it no longer spawns player targets for item testing.
- Keep `DamageTestPlayer.GlobalId` as the runtime identity key and resolve ownership through `Networking.MultiplayerData.GetPlayerByGlobalId(GlobalId)`.
- Treat the existing `B` item grid as a temporary equipment/debug menu, not the final purchase UI.
- Keep item use shaped like future server-authoritative commands: local input requests an item action, host/server validates inventory/ammo/control state and applies it, clients display the result.
- Build ammo, armor, and inventory around the model in `docs/player-items-inventory-plan.md`: one armor item, one or more inventory providers, an optional backstrap item, carried equipables, typed slots, and separate magazine reserve buckets.
- Give every item separate visual roles where relevant: a showcase/UI image for store, inventory, buy-wheel, debug menu, tooltip, and item selection, plus an in-use image for held/equipped/worn gameplay rendering.
- Render equipped armor as an overlay above the root/base player body sprite. Start with `assets/items/modern/armor/light_armor.svg` and `assets/items/modern/armor/heavy_armor.svg` overlapping the current 12x12 test player image.
- Give armor a separate presentation/store image for UI instead of reusing the tiny equipped overlay. Start with `assets/items/modern/armor/light_armor_store.svg` and `assets/items/modern/armor/heavy_armor_store.svg`.
- Add player stats/equipment HUD UI as reusable scenes, not hardcoded controls inside the test room script. Start with a `player_stats_panel.tscn`-style scene for one local player and a parent HUD scene/container that can lay out up to 4 local player panels at once.
- The player stats HUD should show player name, avatar image, kills, health/status, equipped weapon, ammunition/magazine reserves, armor, carried items, backstrap item, and empty slots. Missing equipment should render as explicit empty-slot UI, not disappear.

## Modern Items To Implement

- `Pistol-T1`, `Pistol-T2`, `Pistol-T3`
- `Smg-T1`, `Smg-T2`, `Smg-T3`
- `AR-T1`, `AR-T2`, `AR-T3`
- `Rifle-T1`, `Rifle-T2`, `Rifle-T3`
- `Rocketlauncher`
- `Grenadelauncher-T1`, `Grenadelauncher-T2`
- `NadeExplosive`, `NadeIncendiary`, `NadeSmoke`

## Next Implementation Order

1. Add or harden the runtime player equipment data object used by `DamageTestPlayer`, based on `InGamePlayerData`: equipped armor, inventory providers, backstrap item, carried equipables, selected/equipped item index, and magazine reserve state.
2. Add typed slot validation for carried equipables. Start with base carry capacity `1`, armor-provided slots, inventory-provider slots, and `BackStrap` compatibility.
3. Add magazine/ammo reserve buckets separate from carried item slots: `Small`, `Medium`, `Large`, and `Special`. Track current and maximum reserves.
4. Wire shootable and launcher item use through ammo checks and consumption. Failed ammo validation should reject item use on the host/server and leave clients visually consistent.
5. Add armor data/resource behavior to the test flow: one equipped armor item, protection through existing `ArmorResource` where useful, optional slot/magazine bonuses, weight fields, and movement penalty hooks.
6. Add a reusable local player stats HUD scene stack. Use a per-player panel scene plus a parent HUD/container scene so `TestPlayerItemRoomLAN` and future game scenes can instantiate up to 4 local player panels without rebuilding UI in code.
7. Populate the HUD from runtime player/equipment state: player name/avatar, kills, health/status, selected weapon, ammunition/magazine reserves, armor, carried slots, backstrap, and empty slot placeholders.
8. Update the `B` item grid or add a small test equipment menu so it can assign items into valid carried slots/backstrap and adjust magazine reserves without pretending to be the final buy wheel.
9. Keep validation server-authoritative: clients may request equipment changes or item use, but host/server validates slots, armor, inventory providers, ammo, death state, recovery, and control state before syncing.
10. Add focused test cases for invalid carry attempts, invalid backstrap items, ammo-empty behavior, magazine capacity limits, armor protection, inventory removal invalidating stored items, and local HUD layout with 1-4 local players.
11. Run `dotnet build MultiplayerArenaV2.csproj` and `godot --headless --path . --quit` after implementation. Run `godot --headless --path . --import` when adding or changing assets.

## First Test Cases

- Host/local player can use a simple shootable item with keyboard/mouse aim.
- Client gamepad players can use the same item path with their local aim/fallback aim model.
- Local players select test items through the `B` item grid, then use the selected item with left mouse or Xbox right trigger.
- The `B` item grid must keep local players in `PlayerControlState.Menu` while open so controller navigation cannot also move, aim, or fire the player.
- Item actions should keep broadcasting the exact action direction long enough for remote players to see the held item point where the shot or throw was executed.
- Generic bullet, thrown-item, and launched-projectile scenes can each be instantiated from item execution code.
- A player with no extra equipment can only carry one normal equipable item.
- Armor and inventory providers can add typed carried-item slots and magazine capacity without themselves occupying carried-item slots.
- Backstrap-compatible items can be assigned to `BackStrapItem`; incompatible items are rejected.
- Shootable and launcher items consume ammo/reserve data and cannot execute when the needed ammo bucket is empty.
- Magazine reserve capacity is validated separately from carried item slots.
- Armor can affect protection and future movement penalties without being treated as a carried usable item.
- Player stats HUD can display 1, 2, 3, or 4 local player panels at the same time without overlapping the temporary item menu or core aiming/action UI.
- Each local player panel shows name, avatar image, kills, current health/status, equipped weapon, ammunition reserves, armor, carried slots, backstrap slot, and explicit empty slots.
- The player stats HUD is built from reusable `.tscn` scenes rather than constructing all controls directly inside `TestPlayerItemRoomLAN.cs`.
- Every currently imaged modern weapon tier can be selected in `TestPlayerItemRoomLAN` and executes through the same item-use path.
- Every currently imaged modern grenade can be selected in `TestPlayerItemRoomLAN` and executes through the same throwable path.
- A thrown/grenade item can apply radius damage to players, props, and destructible walls through the shared damage backend.
- The new item/action test scene can be launched directly without relying on `test_map_destruction_logic_lan.tscn` as the active scene.
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
- Do not build the full inventory UI first. Build data validation and a temporary test equipment UI first.
- Do build a lightweight stats/equipment HUD early enough to see whether ammo, armor, slots, and local split-screen identity are readable during play.
- Keep magazine reserves separate from normal carried item slots.
- Keep armor protection and inventory capacity separate: armor can provide protection, movement penalties, and slot/provider rules.
- Spawn/respawn overlap-safe placement is still needed, but the next gameplay slice is player items/actions unless spawn blocking becomes a direct blocker.

## Relevant Docs

- `docs/player-items-inventory-plan.md`
- `docs/player-hud-ui-plan.md`
- `docs/modern-item-content-plan.md`
- `docs/combat-lan-test-handoff.md`
- `docs/test-scenes.md`
- `docs/multiplayer-networking.md`
- `docs/destructible-environment.md`
