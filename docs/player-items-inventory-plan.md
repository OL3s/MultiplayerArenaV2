# Player Items And Inventory Plan

This document tracks the planned direction for in-game player items, purchase mode, equipment capacity, weight, and theme support.

## Core Goal

The item system should support fast arena gameplay while still feeling grounded. Players should not have an unlimited inventory. What a player can carry should come from the equipment they buy, wear, and attach.

The core rule is that a player has one armor item, one or more inventory-providing items, and an array of carried items. The carried items array is only valid when every item fits into a slot provided by the player's available inventories, armor, or base carry capacity.

The first implementation target is modern-only content. The base structure should still support future themes without rebuilding the inventory model, but medieval content is intentionally deferred until the modern item/action slice works.

## Purchase Mode

Purchase mode is the default planned game mode flow for now.

When purchase mode is enabled:

- Players earn or start with money.
- Players open a buy wheel to spend money before or during a match phase.
- The buy wheel offers weapons, armor, backpacks, holsters, straps, utility bags, stims, grenades, and other theme-specific items.
- Bought items must fit into the player's current equipment and carry-capacity rules.
- Money, item cost, weight, and capacity should all matter.

## Base Player Carry State

A player with no extra equipment should start with only one equipable item slot:

```csharp
Godot.Collections.Array<PlayerEquipable> Items = new();
```

The default capacity is effectively `1`, meaning the player can only carry one active/equipable item without armor, backpack, holster, strap, or another carrying item.

This keeps the default player lightweight and makes equipment purchases meaningful.

Planned in-game player shape:

```csharp
public partial class InGamePlayerData : Resource
{
    public PlayerArmor Armor { get; set; }
    public Godot.Collections.Array<PlayerInventoryBag> Inventories { get; set; } = new();
    public PlayerEquipable BackStrapItem { get; set; }
    public Godot.Collections.Array<PlayerEquipable> Items { get; set; } = new();
}
```

`Items` should not decide its own capacity. Capacity comes from the player's base slot, current armor, and current inventories. If a player removes a backpack, holster, pouch, or armor piece, any items stored in slots from that removed equipment must be dropped, moved, or rejected by validation.

Armor and inventory bags are not usable carried items and do not take up inventory space. They are equipped/configured separately on `InGamePlayerData`. Their job is to provide item slots, magazine capacity, protection, weight, and attachment rules.

`BackStrapItem` is a special optional carry slot for one item that can be worn on the player's back. It does not consume the default hand/equipable slot or a bag slot, but the item must declare the `BackStrap` container category to be valid there.

## Planned Item Type Layers

The current base resource is `PlayerItem`. First-pass hierarchy:

- `PlayerItem`: base abstract item resource.
- `PlayerArmor`: protective item; one equipped armor per player.
- `PlayerInventoryBag`: inventory provider such as backpack, holster, pouch, strap, quiver, or magazine rig.
- `PlayerEquipable`: abstract item that can be equipped or actively used by a player.
- `PlayerItemThrowable`: equipable item that spawns a thrown scene, such as grenade, flask, throwing knife, or potion bomb.
- `PlayerItemShootable`: equipable item that fires direct or mostly hitscan-style projectiles, such as pistol, rifle, bow, or crossbow.
- `PlayerItemProjectile`: equipable item that launches a spawned projectile scene, such as an RPG, grenade launcher, rocket launcher, or magic staff.
- `PlayerItemMelee`: equipable item that resolves a close-range arc or hit shape, such as sword, dagger, club, or knife.
- `PlayerItemInstant`: equipable item that executes immediately, such as stim, potion, powerup, or medkit.
- `PlayerProjectileData`: projectile spawn data used by shootable and projectile-launching items.
- `PlayerItemObjective`: effect data executed by a throwable, projectile, melee hit, or instant item.
- `PlayerArmor`: protective equipment that can also affect weight and attachment slots.

Names can change when implementation starts, but the system should keep item identity, carry rules, and gameplay behavior separate enough that new item families can be added later.

The current implementation keeps `PlayerItem` and `PlayerEquipable` abstract. Concrete category resources like `PlayerItemThrowable`, `PlayerItemShootable`, `PlayerItemProjectile`, `PlayerItemMelee`, `PlayerItemInstant`, `PlayerArmor`, and `PlayerInventoryBag` can be created directly as `.tres` resources.

## Armor And Inventory Providers

The player should have at most one equipped armor item. Armor is not only protection; it can also provide inventory capacity or attachment points.

Examples:

- Light armor may provide no extra storage but keeps the player fast.
- Tactical armor may provide magazine capacity bonuses, such as `+2` small magazines.
- Heavy armor may provide more protection and more attachment slots, but with a stronger weight penalty.
- Medieval leather armor may provide belt loops or small pouch attachment points.

Inventory providers are any items that add slots to the player. They can be standalone equipment or attached to armor.

Examples:

- Holster: adds one `SmallItem` slot.
- Magazine pouch: adds extra magazine capacity. Magazines are stored as counts, not item slots.
- Stim pouch: adds several `SmallGadget` slots.
- Backpack: adds generic storage slots and higher weight capacity.
- Strap: adds one `LargeItem` carry slot.
- Built-in backstrap: allows one extra back-worn item if that item has the `BackStrap` container category.

The player can have one or more inventories at the same time, but each inventory must be allowed by the current armor, base player rules, or another valid attachment rule.

Inventory limits are based on shape/container categories provided by those inventories. A pouch, bag, holster, or strap should not usually add generic space unless it is meant to be a generic bag. For example, a stim/potion holder can add three slots that accept the `SmallGadget` container category. Those slots allow buying or carrying small gadgets such as stims, potions, or compact tools, but they do not allow extra guns, large throwables, magazines, armor, or inventory bags.

Class type and container category are separate. `PlayerItemThrowable` describes behavior, not size or storage rules. A small throwing knife, hand grenade, and huge explosive barrel could all be throwable behavior-wise, but they should not necessarily fit in the same inventory slot. Each item declares the shape/container categories it fits, and each slot declares the categories it accepts.

Backstrap compatibility is also a container category. A bow, rifle, sword, or other long item can opt into `BackStrap` if it should be carryable on the player's back. Items without that category cannot use the backstrap slot even if they are large weapons.

## Carry Equipment Examples

Carry capacity should come from physical equipment rather than abstract inventory pages.

Examples:

- A holster is low weight and adds `+1` slot for a smaller weapon.
- A magazine pouch can add `+2` small magazine capacity.
- A stim/potion holder can add `+3` `SmallGadget` item slots.
- A weapon strap may allow one larger weapon to be carried without occupying the normal hand slot.
- The player can carry one backstrap-compatible item on their back as an additional special slot.
- A backpack adds more storage but increases total carried weight and may slow the player.
- A stim pouch or potion pouch adds several small consumable slots but only for consumables.
- Armor can add protection, weight, and attachment points for pouches or holsters.

The important rule is that item capacity should be typed by shape/container category. A slot should be able to say what it accepts, such as `SmallItem`, `LargeItem`, `SmallGadget`, or `BackStrap`. Magazines, arrow bundles, and similar reload reserves should not consume item slots; they use a separate magazine storage model.

## Magazine And Reload Reserve Model

Magazines are not normal carried items. A player should not need a separate `PlayerItem` instance for every extra magazine, RPG rocket, or future arrow bundle. The player should store reload reserves as counts.

Planned reload reserve buckets:

- `Small`: pistol magazines, compact battery packs, small arrow bundles if needed.
- `Medium`: rifle magazines, standard quivers, medium ammo bundles.
- `Large`: heavy weapon magazines, large quivers, bulky reload packs.
- `Special`: RPG rockets, rare launcher ammo, special arrows, magic charges, or other unusual reload reserves.

Possible data shape:

```csharp
public partial class PlayerMagazineStorage : Resource
{
    public int Small { get; set; }
    public int Medium { get; set; }
    public int Large { get; set; }
    public int Special { get; set; }
}
```

The player should have current stored magazine counts and maximum magazine capacity. Armor and inventory bags can add magazine capacity bonuses separately from item slots.

Example:

- A basic holster purchase can provide `+2` `SmallItem` slots and `+2` small magazine capacity.
- A tactical vest can provide no extra item slots, but `+3` medium magazine capacity and `+1` special magazine capacity.
- An RPG can be a `LargeItem` or `BackStrap` item, while the player's extra rockets are stored in `Special` magazine reserve counts.
- A medieval bow can be a `LargeItem` or `BackStrap` item, while arrow bundles use `Small`, `Medium`, `Large`, or `Special` reserve counts depending on the chosen balance.

Purchase validation should check item slots and reload reserve capacity separately. Buying an RPG needs a valid item slot for the launcher. Buying extra RPG rockets needs available `Special` reserve capacity, not a normal item slot.

## Weight Direction

Items should have weight, and player movement should eventually react to total carried weight.

Planned weight inputs:

- Base item weight.
- Armor weight.
- Backpack and carry-equipment weight.
- Carried weapons, grenades, consumables, ammo, and utility items.

Possible weight outputs:

- Movement speed penalty.
- Dodge or dash penalty.
- Acceleration penalty.
- Stamina cost increase if stamina becomes part of the game.
- Buy-wheel warnings when the player is about to become too heavy.

The first version can store weight data without applying all movement effects immediately.

## Slot And Capacity Model

A clever long-term model is to treat armor and inventories as slot providers. The player's carried item array is a result of these available slots, not a free-form list.

Possible data shape:

```csharp
public partial class PlayerItemSlot : Resource
{
    public string SlotId { get; set; }
    public Godot.Collections.Array<PlayerItemSlotType> AcceptedContainerTypes { get; set; } = new();
    public float MaxItemWeight { get; set; }
    public PlayerItem StoredItem { get; set; }
}

public abstract partial class PlayerItem : Resource
{
    public Godot.Collections.Array<PlayerItemSlotType> ContainerTypes { get; set; } = new();
}

public partial class PlayerInventoryBag : PlayerItem
{
    public Godot.Collections.Array<PlayerItemSlot> ProvidedSlots { get; set; } = new();
    public PlayerMagazineStorage MagazineCapacityBonus { get; set; } = new();
}

public partial class PlayerArmor : PlayerItem
{
    public Godot.Collections.Array<PlayerItemSlot> ProvidedSlots { get; set; } = new();
    public Godot.Collections.Array<PlayerItemSlotType> AllowedInventorySlotTypes { get; set; } = new();
    public PlayerMagazineStorage MagazineCapacityBonus { get; set; } = new();
}

public partial class InGamePlayerData : Resource
{
    public PlayerEquipable BackStrapItem { get; set; }
}
```

This allows one item to provide specific slots. For example, a stim pouch can provide three `SmallGadget` slots, and a holster can provide one `SmallItem` slot. Magazine pouches should provide magazine capacity bonuses instead of item slots.

`PlayerArmor` and `PlayerInventoryBag` should not need `ContainerTypes`, because they are equipped into dedicated player fields instead of stored inside inventory slots. `ContainerTypes` is for carried/equipable items that need slot validation, such as weapons, tools, stims, throwables, and backstrap items.

The player should not need custom logic for every armor, backpack, holster, or pouch. The player can ask equipped armor and inventories which slots they provide, then validate item placement against those slots.

Validation should answer these questions:

- Does the player have an empty slot that accepts this item type?
- Does the item fit the slot's weight or size limit?
- Is this inventory provider allowed by the player's armor or base rules?
- If an armor or inventory is removed, which items no longer have valid slots?

Purchase validation should use the same rules as carry validation. If a player has no empty slot accepting the needed item container category, the buy wheel should reject or disable that item purchase even if the player has money. If the player equips a stim pouch with three slots accepting `SmallGadget`, the player can buy up to three small-gadget items into those slots.

Container categories can overlap. For example, a compact grenade could be categorized as `SmallGadget` if it fits in a gadget pouch. A bulky mine could be categorized as `LargeItem`, making it incompatible with small-gadget slots even though it still uses throwable-style behavior.

Backstrap validation should be simple: the item in `BackStrapItem` must fit the `BackStrap` container category. Examples include a bow, certain rifles, a large sword, or any item that has an actual strap/sheath setup. A pistol, stim, loose grenade, or tiny potion should usually not be `BackStrap` compatible.

## Equipable Behavior Model

Equipable item categories should describe how the item is used. The actual effect should come from reusable objective data where possible.

Shared concept:

- `PlayerEquipable` stores common use data, including an optional `PlayerItemObjective`.
- `PlayerItemObjective` describes what happens when an item use resolves, such as damage, healing, buff, explosion radius, duration, or a visual/effect scene.
- Theme-specific `.tres` resources should tune values rather than requiring new code for every item.

Throwable behavior:

- `PlayerItemThrowable` represents items like grenades, throwing knives, throwable flasks, and potion bombs.
- It has a `ThrowableScene` that is spawned into the world when used.
- The thrown scene handles travel, bounce, fall, fuse timer, or rest detection.
- When the thrown item resolves, it executes the stored `UseObjective`.
- Example: a grenade can execute an explosion objective after its fuse or when it comes to rest.

Shootable behavior:

- `PlayerItemShootable` represents pistols, rifles, bows, crossbows, and similar weapons.
- It references a `PlayerProjectileData` resource.
- The projectile data controls projectile scene, speed, color, penetration, and collision objective.
- The shootable item owns firing rules like magazine size and shots per second.
- When fired, the shootable spawns the projectile path/object. The projectile executes its objective on collision.
- Example: a rifle uses a fast bullet projectile with low visual size, high speed, and penetration; a bow uses an arrow projectile with different speed and collision behavior.

Projectile item behavior:

- `PlayerItemProjectile` represents the carried launcher/holder item, not the projectile itself.
- Examples include RPG, grenade launcher, rocket launcher, mortar tube, wand, or other item that launches a spawned projectile object.
- It references `PlayerProjectileData` for the spawned projectile scene and collision behavior.
- It should usually be a `LargeItem`, `BackStrap`, or both.
- Extra rockets or special ammo should be stored in the separate `Special` magazine reserve bucket, not as item slots.

Melee behavior:

- `PlayerItemMelee` represents swords, knives, clubs, daggers, and similar close-range items.
- It should resolve an arc, range, or hit shape in front of the player.
- On hit, it executes the stored `UseObjective`.

Instant behavior:

- `PlayerItemInstant` represents stims, potions, powerups, medkits, and other immediate-use items.
- It executes the stored `UseObjective` immediately when used.
- It can be consumed on use.

Projectile data distinction:

- `PlayerProjectileData` is not the same as a player-carried item.
- It is the spawned projectile settings used by `PlayerItemShootable` and `PlayerItemProjectile`.
- It should execute on collision, unlike a thrown item that may execute after a timer, when it rests, or when its thrown scene decides it has resolved.

## Theme Support

The system should support multiple item themes through shared base structures, but the first playable content pass is modern-only.

Current content scope:

- Modern

Deferred future themes:

- Medieval or other non-modern themes

Modern examples:

- Pistol
- Rifle
- Grenade
- Stim injector
- Plate carrier
- Backpack
- Weapon strap
- Sidearm holster

Medieval examples:

- Dagger
- Sword
- Bow
- Throwing flask
- Potion
- Leather armor
- Satchel
- Belt loop

The theme should change item content and presentation, not the core inventory rules. For example, a modern stim pouch and a medieval potion pouch can both provide small consumable slots through the same base slot-provider system.

The first planned modern content list is tracked in `docs/modern-item-content-plan.md`.

## Theme Expansion Rule

Theme-specific resources should inherit from the same base item resources. The code should avoid hardcoding modern-only concepts where a generic term works better.

Preferred generic terms:

- `Consumable` instead of only `Stim`
- `Throwable` behavior instead of only `Grenade`
- `CarryEquipment` or `InventoryBag` instead of only `Backpack`
- `SmallItem` and `LargeItem` container categories instead of only `Pistol` and `Rifle`
- `Shootable` behavior for guns, bows, crossbows, and launchers
- `Instant` behavior for stims, potions, medkits, and powerups

Modern is the first content set. The base wall of the system should be generic enough that future non-modern items can be added by creating new resources and theme data rather than rewriting player inventory logic, but that proof pass is not part of the first implementation slice.

## Implementation Order

Recommended first slice:

- Add `PlayerEquipable` as an abstract subclass of `PlayerItem`.
- Add `InGamePlayerData` with one armor field, one inventory array, and one carried/equipable item array.
- Add item weight and cost fields to base item resources.
- Add item slot type enum.
- Add armor and inventory resources that provide typed slots.
- Add throwable, shootable, projectile, melee, instant, and objective resources.
- Add purchase-mode validation that checks money, slot availability, and weight.
- Add every currently imaged modern test item first: all pistol, SMG, AR, rifle, launcher, and grenade tiers/variants listed in `docs/modern-item-content-plan.md`.
- Add future non-modern test items later, after the modern item/action slice is playable, to prove the base model is theme-safe.

Next-session implementation focus is tracked in `docs/focuspoints.md`. That file should be treated as the short checklist for turning this plan into working game code and a dedicated test scene.

## Open Design Questions

- Should inventory slots be resources, plain data classes, or generated runtime state from equipment resources?
- Should item weight use `float` kilograms or an arcade-friendly integer weight value?
- Should players be blocked from buying overweight items, or allowed to buy them with movement penalties?
- Should the buy wheel show all items or only items valid for the current theme and current carry setup?
