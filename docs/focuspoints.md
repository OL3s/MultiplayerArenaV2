# Focus Points

This file tracks what to focus on in the next working session.

## Next Session Goal

Continue the simplified player equipment slice inside the dedicated player item/action LAN test scene. The current direction is armor-driven loadout capacity plus armor-modified item reload/refresh cooldowns: no backstrap, no inventory bags, no separate ammo rig, and no separate magazine reserve buckets.

Use `scenes/tests/test_player_item_room_lan.tscn` and `scripts/data/gameplay/TestPlayerItemRoomLAN.cs` for this slice.

## Primary Focus

- Continue from the current `rework-ammo-system` branch unless a new branch is explicitly requested.
- Read `docs/player-items-inventory-plan.md` first.
- Keep `scenes/tests/test_map_destruction_logic_lan.tscn` focused on destructible map and prop damage sync.
- Keep `DamageTestPlayer.GlobalId` as the runtime identity key and resolve ownership through `Networking.MultiplayerData.GetPlayerByGlobalId(GlobalId)`.
- Treat the existing `B` item grid as a temporary equipment/debug menu, not the final purchase UI.
- Keep item use shaped like future server-authoritative commands: local input requests an item action, host/server validates ownership, control/death/recovery state, loaded ammo or gadget readiness, and applies it.
- Use armor as the only capacity and cooldown-modifier provider. Armor decides whether the player can carry a second weapon, how many gadget slots are available, and which percentage multipliers apply to item-defined weapon reloads and gadget refreshes.
- Keep `PlayerWeapon` and `PlayerGadget` as separate resource families. Shared purchasable/display data belongs on `PlayerItem`, which is also used by armor.
- Use `IPlayerUsable` only as a runtime bridge for the common item-use path, not as a shared exported Godot resource base.
- Keep item use simple: single-fire weapons/gadgets use once per press, full-auto weapons repeat while held after recovery, weapon use is gated by loaded ammo, and gadget use is gated by readiness/refresh timers. Do not reintroduce toggled fire modes or burst mode.
- Remove old planning assumptions around backstrap items, inventory providers, separate ammo carriers, and `Small`/`Medium`/`Large`/`Special` magazine buckets.
- Give every item separate visual roles where relevant: showcase/UI image and in-use gameplay image.
- Render equipped armor as an overlay above the root/base player body sprite.
- Add player stats/equipment HUD UI as reusable scenes, not hardcoded controls inside the test room script.

## Current Simplified Rules

- Maximum weapon slots: 2.
- Maximum gadget slots: 3.
- No armor/default capacity: 1 weapon, 1 gadget, `1.0x` weapon reload multiplier, `1.0x` gadget refresh multiplier.
- Light armor currently follows the default capacity.
- Heavy armor currently allows 2 weapons and 2 gadgets, with slower or faster reload/refresh multipliers depending on its protection/capacity tradeoff.
- Weapon loaded ammo is `item.MagazineSize`.
- Pressing reload starts the selected weapon's item-defined reload cooldown after applying the armor reload multiplier.
- Gadget use consumes readiness and starts that gadget's item-defined refresh cooldown after applying the armor refresh multiplier.
- Equipping armor clamps unavailable weapon/gadget slots and changes future reload/refresh cooldown multipliers.

## Modern Items In Scope

- `Pistol-T1`, `Pistol-T2`, `Pistol-T3`
- `Smg-T1`, `Smg-T2`, `Smg-T3`
- `AR-T1`, `AR-T2`, `AR-T3`
- `Rifle-T1`, `Rifle-T2`, `Rifle-T3`
- `Rocketlauncher`
- `Grenadelauncher-T1`, `Grenadelauncher-T2`
- `NadeExplosive`, `NadeIncendiary`, `NadeSmoke`
- `Light Armor`, `Heavy Armor`

## Next Implementation Order

1. Rework `PlayerLoadoutState` around loaded weapon ammo, weapon reload timers, gadget readiness, and gadget refresh timers.
2. Add reload input handling in `TestPlayerItemRoomLAN` so pressing reload starts the selected weapon's item-defined reload cooldown after applying the armor multiplier.
3. Make gadget use start that gadget's item-defined refresh timer after applying the armor multiplier.
4. Improve the temporary `B` menu so assigning weapons/gadgets into armor-limited slots is clearer than simply replacing the last available slot.
5. Add HUD scenes for local player equipment/readability: selected item, weapon slots, gadget slots, armor, health, loaded ammo, reload state, and gadget refresh state.
6. Sync loaded ammo/readiness/cooldown state explicitly if status/HUD readability requires it across peers.
7. Add focused tests or test helpers for reload start/finish, gadget refresh start/finish, slot clamping when changing armor, and invalid use rejection.
8. Run `dotnet build MultiplayerArenaV2.csproj` and `./tools/verify-startup.sh` after implementation.

## First Test Cases

- Host/local player can use a shootable item with keyboard/mouse aim.
- Client gamepad players can use the same item path with their local aim/fallback aim model.
- Local players select test weapons and gadgets through the `B` item grid, then use the selected item with left mouse or Xbox right trigger.
- The `B` item grid keeps local players in `PlayerControlState.Menu` while open.
- Item actions broadcast the exact action direction long enough for remote players to see the held item point where the shot or throw was executed.
- Generic bullet, thrown-item, and launched-projectile scenes each instantiate from item execution code.
- A player without armor can carry 1 weapon and 1 gadget.
- Heavy armor allows a second weapon and a second gadget slot.
- Pressing reload starts a weapon reload cooldown using the selected weapon's base reload duration and the currently equipped armor's reload multiplier.
- Shootable weapons and launchers consume loaded ammo and cannot execute when empty or reloading.
- Grenades consume gadget readiness and cannot execute while their refresh timer is active.
- Changing from higher-capacity armor to lower-capacity armor clamps unavailable slots.
- Armor can affect protection and future movement penalties without being a carried usable item.
- Player stats HUD can eventually display 1, 2, 3, or 4 local player panels at the same time.
- The first item data model does not hardcode modern-only assumptions so medieval-style items can be added later.

## Keep In Mind

- Do not build the full purchase menu first. Build working item actions first.
- Do not rebuild the old inventory/backstrap/ammo-rig model.
- Keep the first item/action content pass modern-only.
- Do build a lightweight stats/equipment HUD early enough to validate readability.
- Spawn/respawn overlap-safe placement is still needed, but the current gameplay slice is player items/actions unless spawn blocking becomes a direct blocker.

## Relevant Docs

- `docs/player-items-inventory-plan.md`
- `docs/player-hud-ui-plan.md`
- `docs/modern-item-content-plan.md`
- `docs/combat-lan-test-handoff.md`
- `docs/test-scenes.md`
- `docs/multiplayer-networking.md`
- `docs/destructible-environment.md`
