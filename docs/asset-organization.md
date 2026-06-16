# Asset Organization

This document tracks project asset folder rules so temporary and production assets do not drift into unrelated folders.

## Top-Level Folders

- `assets/players/`: player body, character, and player-specific visual parts.
- `assets/items/`: player-carried, bought, equipped, or usable item visuals.
- `assets/projectiles/`: spawned projectile visuals and other moving world objects created by item use.
- `assets/props/`: arena/world props such as barrels, rocks, trees, and other environmental objects.
- `assets/inputicons/`: keyboard, mouse, and gamepad UI input icons.
- `assets/network/`: network/debug UI icons and related network visuals.
- `assets/tiles/`: tile atlases and TileSet resources.
- `assets/shaders/`: shader resources.
- `assets/ui/`: shared menu, HUD, lobby, match-config, team, mode, loadout, and buy-menu icons.

## Items Versus Projectiles

Use `assets/items/` for the thing a player owns, carries, equips, buys, or holds.

Each item should have two item-level SVG roles:

- Showcase visual: the readable presentation image used in store, buy-wheel, inventory, debug menu, tooltip, and item list UI.
- In-use visual: the smaller gameplay image used when the item is held, equipped, worn, thrown from the hand, or otherwise visible during play.

Store item `.tres` data resources beside their item visuals when they describe that item directly. The resource should reference both visual roles where relevant instead of assuming one SVG can work for both UI and gameplay. For example, a pistol resource should be able to reference a readable showcase pistol image and a smaller held pistol image.

Use `assets/projectiles/` for the thing spawned into the world by using an item.

Examples:

- `assets/items/modern/weapons/rocketlauncher.svg`: the carried rocket launcher item.
- `assets/projectiles/rockets/rocket.svg`: the rocket fired by the launcher.
- `assets/items/modern/weapons/pistol_t1.svg`: the `Pistol-T1` visual currently used by `DamageTestPlayer`.
- `assets/items/modern/weapons/pistol_t0.svg`: the weak modern default starter pistol.
- `assets/projectiles/bullets/bullet.svg`: a visible bullet or tracer if one is added later.
- `assets/items/modern/throwables/nade_explosive.svg`: a carried grenade item.
- `assets/projectiles/grenades/thrown_grenade.svg`: the spawned grenade body if the thrown world object needs a separate visual.

## Current Item Subfolders

- `assets/items/modern/weapons/`: modern pistols, SMGs, ARs, rifles, rocket launchers, grenade launchers, and similar carried/equipped weapons.
- `assets/items/modern/throwables/`: modern hand grenades and similar carried throwable items.
- `assets/items/modern/armor/`: modern armor item visuals that overlay the base player body.
- `assets/items/medieval/weapons/`: medieval bows, crossbows, and future melee weapons.
- `assets/items/medieval/throwables/`: medieval thrown gadgets such as bombs.
- `assets/items/medieval/armor/`: medieval armor item visuals that overlay the base player body.
- `assets/items/modern/inventory/`: future modern backpacks, holsters, pouches, straps, and other carry-equipment visuals.
- `assets/items/modern/consumables/`: future modern stims, medkits, and other instant-use carried items.

Each item theme owns its own folder plus one theme definition under `assets/items/themes/`. The global catalog is `assets/items/item_theme_catalog.tres`. Theme definitions point to their item root folders, default starter items, and buy menu group resources; actual item membership is discovered from folder contents.

Theme buy group resources live under `assets/items/themes/buy_groups/`. They describe the radial buy hierarchy and item filtering rules for a theme, not a single player-owned item.

Buy menu category/action icons live under `assets/ui/buy/`. These icons are generic UI concepts such as weapons, gadgets, armor, launchers, bows, and cancel. They are distinct from item showcase art, which stays beside item resources under `assets/items/`.

## Current Modern Item SVGs

Existing modern weapon and throwable SVGs are currently treated as in-use visuals. As item UI is expanded, add matching showcase images for each item instead of scaling the in-use SVG up for store/inventory presentation. Use `_showcase.svg` as the preferred suffix for new showcase images. The existing armor presentation images currently use `_store.svg`; those can be kept for now or renamed in a later cleanup if consistency becomes worth the churn.

Modern weapons:

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

Modern throwables:

- `assets/items/modern/throwables/nade_explosive.svg`
- `assets/items/modern/throwables/nade_incendiary.svg`
- `assets/items/modern/throwables/nade_smoke.svg`

Modern armor overlays:

- `assets/items/modern/armor/light_armor.svg`
- `assets/items/modern/armor/heavy_armor.svg`

Modern armor showcase/presentation images:

- `assets/items/modern/armor/light_armor_store.svg`
- `assets/items/modern/armor/heavy_armor_store.svg`

Modern item data resources use matching filenames beside these SVGs, such as `assets/items/modern/weapons/pistol_t1.tres` and `assets/items/modern/throwables/nade_explosive.tres`.

## Item Visual Roles

Every item should separate its showcase/UI visual from its in-use gameplay visual.

Showcase visuals are for store, buy-wheel, inventory, debug menu, tooltip, and item selection UI. They should prioritize readability and can be larger, more front-facing, more detailed, or framed differently than the gameplay version.

In-use visuals are for runtime gameplay. They should fit the player scale, hand position, armor overlay, thrown object start visual, or other active world/equipment usage. They should prioritize correct scale, origin, readability in motion, and compatibility with player/item rendering.

For carried weapons and throwables, the in-use image is the held/equipped image. For armor, the in-use image is the body overlay. Armor is also the current loadout-capacity provider, so standalone backpack, holster, pouch, ammo-rig, and strap visuals are not part of the active simplified model.

Use `_showcase.svg` for new showcase images and the base filename for the in-use visual unless a specific category needs clearer naming. Examples: `pistol_t1.svg` for held gameplay use and `pistol_t1_showcase.svg` for store UI; `light_armor.svg` for equipped overlay and `light_armor_store.svg` for the existing first-pass armor UI image.

## Player Visual Layering

The base player body image belongs under `assets/players/` and should be the root player visual. Equipped armor is a separate overlay image from `assets/items/modern/armor/` rendered above the body at the same origin. The overlay should hide or replace body pixels only where the armor SVG draws opaque/semi-opaque shapes, so armor can be changed independently from player identity/body art.

Initial armor overlay target size is `12x12`, matching the current damage-test player SVGs. Later production player bodies can use the same layering rule with larger or directional armor art.

Armor follows the same item visual role split. The armor in-use visual is the player-body overlay, while the armor showcase visual is the store/inventory image. Presentation images may be larger and more readable than equipped overlays because they are item icons, not body overlays.

## Current Projectile Subfolders

- `assets/projectiles/bullets/`: visible bullets, pellets, or tracers.
- `assets/projectiles/rockets/`: rockets and similar launched explosive bodies.
- `assets/projectiles/arrows/`: arrows, bolts, and similar physical shots.
- `assets/projectiles/grenades/`: spawned thrown grenades or grenade-like world bodies.

## Prop Damage Atlases

Current prop SVGs in `assets/props/` are horizontal three-frame damage atlases. Each atlas uses the prop's intended in-game frame size repeated three times across the width:

- Frame `0`: perfect/undamaged.
- Frame `1`: touched/damaged, used below 90% health.
- Frame `2`: close-to-broken, used below 50% health.

The runtime prop collision size still comes from `LevelPropData.Size`; visual damage frames should not change prop collision unless gameplay rules intentionally change later.

## SVG Rules

- Avoid SVG `<text>` for Godot-imported visuals, especially labels and icons.
- Prefer vector geometry for labels, following `docs/svg-input-icon-generation.md`.
- Keep in-use item SVG `width` and `height` close to the intended in-game pixel size. Use a larger `viewBox` for drawing detail instead of exporting a huge texture and scaling it down in every scene.
- Keep showcase item SVGs sized for UI readability rather than gameplay scale. They may use a larger `width` and `height` than the matching in-use visual.
- Current carried-item target sizes are roughly `14-16px` pistols, `18-20px` SMGs, `23-25px` ARs, `27-29px` rifles, `24-30px` launchers, and `10px` grenades.
- Run `godot --headless --path . --import` after adding, moving, or changing Godot assets.
