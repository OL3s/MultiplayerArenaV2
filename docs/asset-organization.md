# Asset Organization

This document tracks project asset folder rules so temporary and production assets do not drift into unrelated folders.

## Top-Level Folders

- `Assets/Players/`: player body, character, and player-specific visual parts.
- `Assets/Items/`: player-carried, bought, equipped, or usable item visuals.
- `Assets/Projectiles/`: spawned projectile visuals and other moving world objects created by item use.
- `Assets/Props/`: arena/world props such as barrels, rocks, trees, and other environmental objects.
- `Assets/InputIcons/`: keyboard, mouse, and gamepad UI input icons.
- `Assets/Network/`: network/debug UI icons and related network visuals.
- `Assets/Tiles/`: tile atlases and TileSet resources.
- `Assets/Shaders/`: shader resources.

## Items Versus Projectiles

Use `Assets/Items/` for the thing a player owns, carries, equips, buys, or holds.

Use `Assets/Projectiles/` for the thing spawned into the world by using an item.

Examples:

- `Assets/Items/Modern/Weapons/rocketlauncher.svg`: the carried rocket launcher item.
- `Assets/Projectiles/Rockets/rocket.svg`: the rocket fired by the launcher.
- `Assets/Items/Modern/Weapons/pistol_t1.svg`: the `Pistol-T1` visual currently used by `DamageTestPlayer`.
- `Assets/Projectiles/Bullets/bullet.svg`: a visible bullet or tracer if one is added later.
- `Assets/Items/Modern/Throwables/nade_explosive.svg`: a carried grenade item.
- `Assets/Projectiles/Grenades/thrown_grenade.svg`: the spawned grenade body if the thrown world object needs a separate visual.

## Current Item Subfolders

- `Assets/Items/Modern/Weapons/`: modern pistols, SMGs, ARs, rifles, rocket launchers, grenade launchers, and similar carried/equipped weapons.
- `Assets/Items/Modern/Throwables/`: modern hand grenades and similar carried throwable items.
- `Assets/Items/Modern/Armor/`: future modern armor item visuals.
- `Assets/Items/Modern/Inventory/`: future modern backpacks, holsters, pouches, straps, and other carry-equipment visuals.
- `Assets/Items/Modern/Consumables/`: future modern stims, medkits, and other instant-use carried items.

The first item/action content pass is modern-only, so item art is grouped under `Assets/Items/Modern/`. Future themes should get their own theme folder, such as `Assets/Items/Medieval/`, when that content is intentionally started.

## Current Modern Item SVGs

Modern weapons:

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

Modern throwables:

- `Assets/Items/Modern/Throwables/nade_explosive.svg`
- `Assets/Items/Modern/Throwables/nade_incendiary.svg`
- `Assets/Items/Modern/Throwables/nade_smoke.svg`

## Current Projectile Subfolders

- `Assets/Projectiles/Bullets/`: visible bullets, pellets, or tracers.
- `Assets/Projectiles/Rockets/`: rockets and similar launched explosive bodies.
- `Assets/Projectiles/Arrows/`: arrows, bolts, and similar physical shots.
- `Assets/Projectiles/Grenades/`: spawned thrown grenades or grenade-like world bodies.

## SVG Rules

- Avoid SVG `<text>` for Godot-imported visuals, especially labels and icons.
- Prefer vector geometry for labels, following `docs/svg-input-icon-generation.md`.
- Keep item SVG `width` and `height` close to the intended in-game pixel size. Use a larger `viewBox` for drawing detail instead of exporting a huge texture and scaling it down in every scene.
- Current carried-item target sizes are roughly `14-16px` pistols, `18-20px` SMGs, `23-25px` ARs, `27-29px` rifles, `24-30px` launchers, and `10px` grenades.
- Run `godot --headless --path . --import` after adding, moving, or changing Godot assets.
