# Item Themes

This document tracks gameplay item themes and their content libraries.

## Theme Selection

The lobby exposes item themes beside biome and structure. `SetupConfig.ItemThemeConfig` syncs enabled themes over the network, and `ArenaMatch` loads item ids/resource paths from theme library resources.

Current theme definitions:

- Catalog: `assets/items/item_theme_catalog.tres`
- Modern: `assets/items/themes/modern.tres`
- Medieval: `assets/items/themes/medieval.tres`

Each theme definition owns metadata, icon, item root folder, and a direct default starter item reference. Buy/debug menus scan the selected theme root folders for `PlayerItem` resources instead of reading duplicated id/path lists.

## Source Of Truth

Resource paths and resource references are the source of truth. Do not maintain parallel string id lists when the information can be derived from a resource path, folder scan, or loaded resource field.

Preferred pattern:

- A catalog points to theme definitions.
- A theme definition points to its root folder and starter item resource.
- Runtime scans the root folder and loads `PlayerItem` resources.
- UI labels/icons come from loaded resources.

Avoid:

- Duplicated `ItemIds` plus `ItemResourcePaths` arrays.
- Hardcoded theme enum arrays for lobby selection.
- Menu labels that duplicate `PlayerItem.DisplayName`.

## Modern

Modern is the current ranged-weapon-heavy theme. It emphasizes pistols, SMGs, ARs, rifles, launchers, grenades, and armor.

`pistol_t0` is the modern default starter weapon. It is intentionally weaker than all purchasable modern weapons: shorter range, worse accuracy, smaller magazine, slower fire rate, and slower reload/recovery. The goal is to give every modern player a fallback weapon while strongly encouraging buying or equipping another weapon.

## Medieval

Medieval is now started as a first data slice, but it is still placeholder ranged content until melee runtime work exists.

Current medieval items:

- `bow_t0`: default starter/training bow.
- `bow_t1`: stronger bow.
- `crossbow_t1`: heavier single-shot crossbow using the heavy ammo caliber display for now.
- `bomb`: throwable gadget.
- `leather_armor`: first armor variant.

Design direction: medieval should eventually be more melee-item focused than modern, with bows/crossbows as supporting ranged weapons. Melee weapons are documented future work and should not be forced into the current projectile/reload slice until there is a deliberate melee implementation pass.

## Biome Relationship

`Medieval` is also available as a biome option. Biomes are visual/content dressing choices and must not own spawn, objective, or game-mode rules.
