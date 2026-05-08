# Focus Points

This file tracks what to focus on in the next working session.

## Next Session Goal

Start implementing the player item, inventory, magazine reserve, and purchase-validation system into the actual game in a working state with a dedicated test scene.

The target is not full gameplay polish. The target is a small vertical slice where item resources can be created, equipped, validated against slots, and displayed/debugged in Godot.

## Primary Focus

- Build a working test scene for player item and inventory logic.
- Create a small set of modern test item resources.
- Validate item purchases against available item slots, backstrap slot, weight, money, and magazine reserve capacity.
- Show clear debug output/UI so the item system can be tested quickly without full gameplay.

## Implementation Order

1. Add core validation methods around `InGamePlayerData`.
2. Add helper methods for collecting slots from armor and inventory bags.
3. Add helper methods for checking whether an item fits a slot through `ContainerTypes` and `AcceptedContainerTypes`.
4. Add helper methods for magazine reserve capacity using `PlayerMagazineStorage`.
5. Add purchase validation that checks money, item slots, backstrap compatibility, weight, and magazine reserve capacity.
6. Create modern `.tres` test resources for armor, inventory bags, weapons, gadgets, projectile items, and magazine capacity bonuses.
7. Create `Scenes/Tests/TestPlayerItemsInventory.tscn` as the first item-system test scene.
8. Add a small C# test scene script that creates or loads test data and prints validation results.
9. Add simple scene controls for buying/equipping sample items if time allows.
10. Run `dotnet build MultiplayerArenaV2.csproj` and `godot --headless --path . --quit` after implementation.

## Test Scene Requirements

The test scene should prove these cases:

- A player with no equipment can carry only the base/default item capacity.
- A holster or similar inventory bag can add `SmallItem` slots.
- A stim/potion pouch can add `SmallGadget` slots.
- A backstrap-compatible item can use the dedicated `BackStrapItem` slot.
- A non-backstrap item is rejected from the backstrap slot.
- Armor and inventory bags do not consume carried item slots.
- Magazine reserve counts are separate from item slots.
- An RPG-style `PlayerItemProjectile` can occupy a `LargeItem` or `BackStrap` slot.
- Extra RPG rockets use `Special` magazine reserve capacity instead of item slots.
- Weight and cost are visible in validation output, even if movement penalties are not implemented yet.

## Current Resource Model To Use

- `PlayerItem` is the base abstract resource for buyable/equippable item data and includes cost, weight, theme, and container categories.
- `PlayerEquipable` is the abstract base for items the player can actively use.
- `PlayerArmor` is equipped separately and can provide item slots and magazine capacity bonuses.
- `PlayerInventoryBag` is equipped separately and can provide item slots and magazine capacity bonuses.
- `PlayerItemSlot` stores accepted container categories, max item weight, and a stored item.
- `PlayerItemSlotType` currently represents shape/container categories: `Generic`, `SmallItem`, `LargeItem`, `SmallGadget`, and `BackStrap`.
- `PlayerMagazineStorage` stores reload reserve counts for `Small`, `Medium`, `Large`, and `Special`.
- `PlayerItemShootable` uses `PlayerProjectileData` for projectile settings.
- `PlayerItemProjectile` is a carried launcher/holder item, such as an RPG.
- `PlayerItemThrowable`, `PlayerItemMelee`, and `PlayerItemInstant` cover thrown, melee, and immediate-use item behavior.
- `PlayerItemObjective` stores reusable effect data.

## Modern Test Items To Create First

- Light armor with no extra slots.
- Tactical armor with medium magazine capacity bonus.
- Small holster with one `SmallItem` slot.
- Stim pouch with three `SmallGadget` slots.
- Weapon strap or backpack with one `LargeItem` slot.
- Pistol as `PlayerItemShootable` with `SmallItem` category.
- Rifle as `PlayerItemShootable` with `LargeItem` and optional `BackStrap` categories.
- RPG as `PlayerItemProjectile` with `LargeItem` and `BackStrap` categories.
- Stim as `PlayerItemInstant` with `SmallGadget` category.
- Grenade as `PlayerItemThrowable` with `SmallGadget` or `LargeItem` depending on desired size.

## Keep Deferred

- Full buy-wheel UI polish.
- Real player movement penalties from weight.
- Full weapon firing, projectile scenes, and collision effects.
- Multiplayer sync for inventory state.
- Medieval item resources beyond proving the model is theme-safe.
- Save/load persistence for bought items.

## Relevant Docs

- `docs/player-items-inventory-plan.md`
- `README.md`
