# Modern Item Content Plan

This document tracks the first planned modern item content set. The first implementation pass is modern-only; future themes can be added later through the same generic item, inventory, projectile, and objective systems.

## Scope Rule

- Build the first playable item/action content around modern weapons, launchers, and grenades only.
- Keep code and data names generic where they describe behavior, such as `Shootable`, `Throwable`, `Projectile`, `Armor`, `InventoryBag`, `Consumable`, and `Objective`.
- Do not create medieval content, medieval assets, or medieval balancing during the first item/action slice.
- Do not hardcode modern-only concepts into base systems when a generic behavior or slot category works.

## Carried Items Versus Projectiles

The carried item and the spawned projectile are separate concepts.

- A rocket launcher is a carried/equipped item in `Assets/Items/Modern/Weapons/`.
- A rocket is a spawned projectile in `Assets/Projectiles/Rockets/`.
- A grenade launcher is a carried/equipped item in `Assets/Items/Modern/Weapons/`.
- A launched grenade projectile belongs under `Assets/Projectiles/Grenades/` if it needs its own visual.
- A hand grenade item belongs under `Assets/Items/Modern/Throwables/`.
- The thrown grenade world body belongs under `Assets/Projectiles/Grenades/` if the held item visual is not reused.

## Generated Modern SVG Assets

The first SVG pass creates carried item visuals only. It does not implement item data, firing behavior, projectile scenes, damage, inventory, or purchase logic.

The SVG files use larger `viewBox` values for clean vector drawing, but their `width` and `height` are set to approximate in-game pixel sizes so Godot imports them at player-scale dimensions.

Weapon SVGs:

- `Assets/Items/Modern/Weapons/pistol_t1.svg`
- `Assets/Items/Modern/Weapons/pistol_t2.svg`
- `Assets/Items/Modern/Weapons/pistol_t3.svg`
- `Assets/Items/Modern/Weapons/smg_t1.svg`
- `Assets/Items/Modern/Weapons/smg_t2.svg`
- `Assets/Items/Modern/Weapons/smg_t3.svg`
- `Assets/Items/Modern/Weapons/ar_t1.svg`
- `Assets/Items/Modern/Weapons/ar_t2.svg`
- `Assets/Items/Modern/Weapons/ar_t3.svg`
- `Assets/Items/Modern/Weapons/rifle_t1.svg`
- `Assets/Items/Modern/Weapons/rifle_t2.svg`
- `Assets/Items/Modern/Weapons/rifle_t3.svg`
- `Assets/Items/Modern/Weapons/rocketlauncher.svg`
- `Assets/Items/Modern/Weapons/grenadelauncher_t1.svg`
- `Assets/Items/Modern/Weapons/grenadelauncher_t2.svg`

Throwable SVGs:

- `Assets/Items/Modern/Throwables/nade_explosive.svg`
- `Assets/Items/Modern/Throwables/nade_incendiary.svg`
- `Assets/Items/Modern/Throwables/nade_smoke.svg`

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

Suggested order:

- `Pistol-T1` as the first shootable weapon.
- `NadeExplosive` as the first throwable area-damage item.
- `Rocketlauncher` or `Grenadelauncher-T1` after projectile spawning is ready.
- Remaining currently imaged items after the common item, projectile, reload, cost, weight, and carry-capacity data paths exist: all pistol, SMG, AR, rifle, launcher, and grenade tiers/variants listed above.
