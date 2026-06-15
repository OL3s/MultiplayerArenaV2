# Player Items And Loadout Plan

This document tracks the simplified in-game player item, armor, ammo, and loadout model.

## Core Goal

Keep the game fast and readable. A player should not manage backpacks, backstraps, magazine pouches, or separate inventory providers. The equipped armor is the only loadout-capacity provider.

The simplified player shape is:

```csharp
public partial class InGamePlayerData : Resource {
    public HealthContainer Health { get; set; }
    public PlayerArmor Armor { get; set; }
    public Godot.Collections.Array<PlayerWeapon> Weapons { get; set; }
    public Godot.Collections.Array<PlayerGadget> Gadgets { get; set; }
}
```

`PlayerItem` is the shared purchasable/display base for weapons, gadgets, and armor. It owns item id, display name, theme, cost, weight, held texture, and showcase texture. Weapons and gadgets are intentionally separate resource families instead of sharing a single equipable base class.

## Armor-Driven Capacity

`PlayerArmor` owns both protection and loadout capacity:

- `AllowsSecondWeapon`: whether the player may carry a second weapon.
- `GadgetSlotCount`: how many gadget slots are available, clamped to `0-3`.
- `WeaponMagazineCount`: how many magazines each equipped ammo weapon gets when ammo is reset.
- `GadgetUseCount`: how many uses each equipped gadget gets when ammo is reset.

There are no standalone inventory bags, ammo rigs, pouches, holsters, or backstrap items in the current model. If a later design needs more capacity, add another armor variant instead of adding a second equipment layer.

Current first-pass armor tuning:

- No armor or light armor style baseline: 1 weapon, 1 gadget, 2 weapon magazines, 1 use per gadget.
- Heavy armor: 2 weapons, 2 gadgets, 3 weapon magazines, 2 uses per gadget.

## Weapons And Gadgets

Items are split into separate resource families:

- `PlayerWeapon`: base for shootable weapons, launchers, melee weapons, and other active combat weapons.
- `PlayerGadget`: base for grenades, instant-use items, and future small utility items.
- `PlayerArmor`: base for armor and loadout-capacity data.

`PlayerWeapon` and `PlayerGadget` both implement `IPlayerUsable` for the runtime use path, but they do not inherit from a shared `PlayerEquipable` resource class. This keeps weapon and gadget data separated while preserving one item-use execution path where needed. Item use is intentionally simple: single-fire weapons and gadgets use once per press, full-auto weapons repeat while held after `RecoverySeconds`, and every use is gated by remaining uses. There are no toggled fire modes, burst modes, or fire-mode cycling controls.

The player can carry at most 2 weapons and at most 3 gadgets. The active armor decides how many of those slots are available.

Weapon ammo is intentionally simple:

```text
max weapon uses = item.MagazineSize * armor.WeaponMagazineCount
```

Gadget count is intentionally simple:

```text
max gadget uses = armor.GadgetUseCount
```

There are no `Small`, `Medium`, `Large`, or `Special` reserve buckets. Launchers use the same weapon ammo rule as guns. Grenades use the same gadget count rule as other gadgets.

Weapon firing behavior is fixed by the weapon resource:

- `IsFullAuto = false`: one shot per press.
- `IsFullAuto = true`: hold to repeat after `RecoverySeconds`.

The current modern SMGs and ARs are full-auto. Pistols, rifles, and launchers are single-fire.

## Runtime State

`PlayerLoadoutState` is the current runtime helper for the test scene. It stores equipped armor, up to 2 weapon slots, up to 3 gadget slots, selected item, and current item uses by item id.

Important API:

- `EquipArmor(PlayerArmor armor)`: applies armor capacity, clamps unavailable slots, and resets item uses to max.
- `EquipItem(PlayerItem item)`: equips a `PlayerWeapon` or `PlayerGadget`, selects it, and resets item uses to max.
- `ResetUsesToMax()`: resets all equipped weapons/gadgets to the current armor-derived maximum.
- `TryConsumeUse(PlayerItem item)`: validates and consumes one use before item execution.

The test room also exposes this flow through `ResetPlayerUsesToMax(globalId)` so future respawn, buy-zone, or round-start code can restock from the same armor-derived rules.

## Test Room Behavior

Primary test bed:

- Scene: `scenes/tests/test_player_item_room_lan.tscn`
- Script: `scripts/data/gameplay/TestPlayerItemRoomLAN.cs`

The temporary `B` item grid remains a debug equipment menu, not the final buy UI.

Current behavior:

- Selecting a weapon equips it into the simplified weapon slots and makes it active.
- Selecting a grenade/gadget equips it into the simplified gadget slots and makes it active.
- Selecting armor applies the armor overlay, clamps unavailable weapon/gadget slots, and resets item uses to the armor-derived maximum.
- Item use is server-authoritative in the LAN test path: clients request use, the host validates ownership, recovery, death/control state, and remaining uses, then executes and syncs the result.
- If an item has no remaining uses, the host rejects execution.

## Item Execution

The execution side remains generic and data-driven:

- `scenes/gameplay/projectiles/generic_bullet.tscn` uses `GenericBullet.cs`.
- `scenes/gameplay/projectiles/generic_thrown_item.tscn` uses `GenericThrownItem.cs`.
- `scenes/gameplay/projectiles/generic_launched_projectile.tscn` uses `GenericLaunchedProjectile.cs`.
- `PlayerProjectileData` owns projectile profile fields such as runtime scene, texture, speed, range, width, color, lifetime, penetration, stop-on-hit behavior, damage, and collision objective.
- `PlayerItemObjective` routes direct damage, explosions, effects, and future objective behavior through `PlayerItemRuntimeContext`.

Damage still routes through `DamageContainer -> HealthContainer` for players, props, and destructible walls.

## Visual Roles

Every item can have separate visuals:

- `HeldTexture`: the in-use sprite for held/equipped/worn gameplay rendering.
- `ShowcaseTexture`: the clearer UI/store/debug-menu image.

Armor uses the same split. Its held texture is the player-body overlay, while its showcase texture is the readable store/debug image.

## Purchase Direction

The full buy wheel is deferred. When purchase mode is implemented, validation should use the same simplified armor capacity rules:

- Buying a weapon requires an available weapon slot from armor capacity.
- Buying a gadget requires an available gadget slot from armor capacity.
- Buying/equipping armor may remove excess weapons/gadgets if the new armor has fewer slots.
- Restocking ammo calls the same reset-to-max API used by armor equip.

## Deferred Or Removed

Removed from the active design:

- Backstrap item.
- Inventory bag/provider items.
- Typed physical slots like holster, pouch, strap, and backpack.
- Separate magazine reserve buckets like `Small`, `Medium`, `Large`, and `Special`.
- Separate ammo-carrier worn item.

Deferred future work:

- Final buy wheel UI.
- Full HUD presentation for weapon/gadget slots and remaining uses.
- Additional armor variants with different protection/capacity tradeoffs.
- Non-modern item themes after the modern item/action slice is stable.
