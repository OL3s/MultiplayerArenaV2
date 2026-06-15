# Modern Item Content Plan

This document tracks the first planned modern item content set. The first implementation pass is modern-only; future themes can be added later through the same generic item, inventory, projectile, and objective systems.

## Scope Rule

- Build the first playable item/action content around modern weapons, launchers, and grenades only.
- Keep code and data names generic where they describe behavior, such as `Shootable`, `Throwable`, `Projectile`, `Armor`, `InventoryBag`, `Consumable`, and `Objective`.
- Do not create medieval content, medieval assets, or medieval balancing during the first item/action slice.
- Do not hardcode modern-only concepts into base systems when a generic behavior or slot category works.

## Carried Items Versus Projectiles

The carried item and the spawned projectile are separate concepts.

- A rocket launcher is a carried/equipped item in `assets/items/modern/weapons/`.
- A rocket is a spawned projectile in `assets/projectiles/rockets/`.
- A grenade launcher is a carried/equipped item in `assets/items/modern/weapons/`.
- A launched grenade projectile belongs under `assets/projectiles/grenades/` if it needs its own visual.
- A hand grenade item belongs under `assets/items/modern/throwables/`.
- The thrown grenade world body belongs under `assets/projectiles/grenades/` if the held item visual is not reused.

The next implementation slice should create shared runtime scenes for the execution side:

- Generic bullet scene for pistol, SMG, AR, and rifle fire.
- Generic thrown-item scene for hand grenades.
- Generic launched-projectile scene for rockets and launched grenades.

Modern item `.tres` resources should reference generic execution scenes or projectile data rather than each item needing its own bespoke runtime scene.

## Generated Modern SVG Assets

The first SVG pass creates carried item in-use visuals only. It does not implement item data, firing behavior, projectile scenes, damage, inventory, or purchase logic.

Modern items should have two SVG visual roles:

- In-use visual: the gameplay-scale image used when the item is held, equipped, worn, thrown from the hand, or otherwise visible during play.
- Showcase visual: the readable UI image used in store, buy-wheel, inventory, debug menu, tooltip, and item selection UI.

The existing weapon and throwable SVG files are currently in-use visuals. Their `width` and `height` are set to approximate in-game pixel sizes so Godot imports them at player-scale dimensions. Future showcase SVGs should be added separately, preferably with a `_showcase.svg` suffix, instead of scaling these tiny held visuals up for UI. Armor already has first-pass in-use overlays plus separate store/presentation images.

Weapon in-use SVGs:

- `assets/items/modern/weapons/pistol_t1.svg`
- `assets/items/modern/weapons/pistol_t2.svg`
- `assets/items/modern/weapons/pistol_t3.svg`
- `assets/items/modern/weapons/smg_t1.svg`
- `assets/items/modern/weapons/smg_t2.svg`
- `assets/items/modern/weapons/smg_t3.svg`
- `assets/items/modern/weapons/ar_t1.svg`
- `assets/items/modern/weapons/ar_t2.svg`
- `assets/items/modern/weapons/ar_t3.svg`
- `assets/items/modern/weapons/rifle_t1.svg`
- `assets/items/modern/weapons/rifle_t2.svg`
- `assets/items/modern/weapons/rifle_t3.svg`
- `assets/items/modern/weapons/rocketlauncher.svg`
- `assets/items/modern/weapons/grenadelauncher_t1.svg`
- `assets/items/modern/weapons/grenadelauncher_t2.svg`

Throwable in-use SVGs:

- `assets/items/modern/throwables/nade_explosive.svg`
- `assets/items/modern/throwables/nade_incendiary.svg`
- `assets/items/modern/throwables/nade_smoke.svg`

## Modern Item Data Resources

The first item data pass stores each modern carried item as a `.tres` resource beside its item SVG visuals. Item resources should eventually reference both the in-use visual and the showcase visual where UI needs a readable item image.

Weapon resources:

- `assets/items/modern/weapons/pistol_t1.tres`
- `assets/items/modern/weapons/pistol_t2.tres`
- `assets/items/modern/weapons/pistol_t3.tres`
- `assets/items/modern/weapons/smg_t1.tres`
- `assets/items/modern/weapons/smg_t2.tres`
- `assets/items/modern/weapons/smg_t3.tres`
- `assets/items/modern/weapons/ar_t1.tres`
- `assets/items/modern/weapons/ar_t2.tres`
- `assets/items/modern/weapons/ar_t3.tres`
- `assets/items/modern/weapons/rifle_t1.tres`
- `assets/items/modern/weapons/rifle_t2.tres`
- `assets/items/modern/weapons/rifle_t3.tres`
- `assets/items/modern/weapons/rocketlauncher.tres`
- `assets/items/modern/weapons/grenadelauncher_t1.tres`
- `assets/items/modern/weapons/grenadelauncher_t2.tres`

Throwable resources:

- `assets/items/modern/throwables/nade_explosive.tres`
- `assets/items/modern/throwables/nade_incendiary.tres`
- `assets/items/modern/throwables/nade_smoke.tres`

These resources currently define item id, display name, theme, cost, weight, held/in-use texture, recovery time, range/display range, aim movement multiplier, accuracy handling stats, magazine/fire-rate values for weapons/launchers, base reload cooldown values for weapons/launchers, base refresh cooldown values for gadgets, and throw range values for grenades. Weapon/gadget slot capacity and reload/refresh cooldown multipliers come from equipped armor, not separate inventory providers or magazine reserve buckets. Add a showcase/presentation texture field when store, inventory, buy-wheel, debug menu, or tooltip UI needs readable item images.

Current fixed firing behavior:

- SMGs and ARs are full-auto.
- Pistols, rifles, launchers, and grenades are single-use per press.
- There is no runtime fire-mode toggle.

## Planned Modern Weapons

Tiered hitscan or fast-projectile weapons:

- `Pistol-T1`
- `Pistol-T2`
- `Pistol-T3`
- `Smg-T1`
- `Smg-T2`
- `Smg-T3`
- `AR-T1`
- `AR-T2`
- `AR-T3`
- `Rifle-T1`
- `Rifle-T2`
- `Rifle-T3`

Launcher weapons:

- `Rocketlauncher`
- `Grenadelauncher-T1`
- `Grenadelauncher-T2`

## Planned Modern Throwables

- `NadeExplosive`
- `NadeIncendiary`
- `NadeSmoke`

## Naming Notes

- Keep the user-facing item names above as the current design names until the first data resources are created.
- File and resource ids can use normalized lowercase names, such as `pistol_t1`, `grenadelauncher_t2`, and `nade_explosive`.
- Use `Smg` and `AR` consistently in display names unless the UI naming pass later chooses full names like `SMG` or `Assault Rifle`.
- `Rocketlauncher` and `Grenadelauncher` are launcher item names; their fired rocket or grenade bodies are projectile assets/data, not separate carried weapon items.

## First Implementation Bias

Implement the smallest playable vertical slice first, then expand across every currently imaged modern item. The first complete modern item pass should include all weapon tiers and grenade variants already represented by SVGs.

The full `.tres` item set now includes the weapon handling and range fields described in `docs/player-items-inventory-plan.md`: default accuracy, movement accuracy, accuracy pushback, shot accuracy recovery, movement accuracy recovery, gameplay range, and aim display range. Those stats are required tuning data for modern shootable weapons and launchers, not a later polish pass.

Throwable resources should also include throw range data. Gamepad aim-vector strength should be able to scale throw distance, while keyboard/mouse can default to full throw strength in the current test scene.

Suggested order:

- `Pistol-T1` as the first shootable weapon.
- `NadeExplosive` as the first throwable area-damage item.
- `Rocketlauncher` or `Grenadelauncher-T1` after projectile spawning is ready.
- Remaining currently imaged items after the common item, projectile, reload, cost, weight, and carry-capacity data paths exist: all pistol, SMG, AR, rifle, launcher, and grenade tiers/variants listed above.
