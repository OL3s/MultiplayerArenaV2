# Player Items And Loadout Plan

This document tracks the simplified in-game player item, armor, reload cooldown, and loadout model.

## Core Goal

Keep the game fast and readable. A player should not manage backpacks, backstraps, magazine pouches, or separate inventory providers. Equipped armor is the only loadout-capacity provider, and it modifies item-defined reload/refresh pacing instead of granting extra ammo bundles directly.

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

## Armor-Driven Capacity And Cooldowns

`PlayerArmor` owns protection, loadout capacity, and reload/refresh cooldown modifiers:

- `AllowsSecondWeapon`: whether the player may carry a second weapon.
- `GadgetSlotCount`: how many gadget slots are available, clamped to `0-3`.
- `WeaponReloadCooldownMultiplier`: percentage-style multiplier applied to a weapon's own reload cooldown.
- `GadgetRefreshCooldownMultiplier`: percentage-style multiplier applied to a gadget's own refresh cooldown.

Armor does not define how many magazines a weapon has, how many total uses a gadget has, or the base cooldown length of an item. Weapon `.tres` resources own their magazine/ammo shape and base reload cooldown. Gadget `.tres` resources own their ready/use shape and base refresh cooldown. Armor only applies a percentage modifier that can speed up or slow down those item-defined cooldowns.

There are no standalone inventory bags, ammo rigs, pouches, holsters, or backstrap items in the current model. If a later design needs different carry capacity or reload/refresh modifiers, add another armor variant instead of adding a second equipment layer.

Current first-pass armor tuning target:

- No armor or light armor style baseline: 1 weapon, 1 gadget, `1.0x` weapon reload multiplier, `1.0x` gadget refresh multiplier.
- Heavy armor: 2 weapons, 2 gadgets, slower or faster reload/refresh multipliers depending on its protection/capacity tradeoff.

## Weapons And Gadgets

Items are split into separate resource families:

- `PlayerWeapon`: base for shootable weapons, launchers, melee weapons, and other active combat weapons.
- `PlayerGadget`: base for grenades, instant-use items, and future small utility items.
- `PlayerArmor`: base for armor and loadout-capacity data.

`PlayerWeapon` and `PlayerGadget` both implement `IPlayerUsable` for the runtime use path, but they do not inherit from a shared `PlayerEquipable` resource class. This keeps weapon and gadget data separated while preserving one item-use execution path where needed. Item use is intentionally simple: single-fire weapons and gadgets use once per press, full-auto weapons repeat while held after `RecoverySeconds`, weapon firing is gated by loaded ammo, and gadget use is gated by readiness/refresh state. There are no toggled fire modes, burst modes, or fire-mode cycling controls.

The player can carry at most 2 weapons and at most 3 gadgets. The active armor decides how many of those slots are available.

Weapon ammo is intentionally explicit but still lightweight:

```text
loaded ammo = weapon.MagazineSize
reload duration = weapon.ReloadCooldownSeconds * armor.WeaponReloadCooldownMultiplier
```

Weapon behavior:

- Firing consumes loaded ammo.
- A weapon with no loaded ammo cannot fire.
- Pressing reload starts that weapon's reload cooldown if it is not already full, not already reloading, and the player is allowed to act.
- When the cooldown finishes, the weapon reloads back to its weapon-defined loaded-ammo maximum.
- The weapon resource controls the base reload cooldown duration.
- Armor applies a percentage-style reload cooldown multiplier. Values below `1.0` are faster; values above `1.0` are slower.
- Armor does not grant extra magazines or reserve ammo.

Gadget count is intentionally timer-based:

```text
ready gadget use = 1
refresh duration = gadget.RefreshCooldownSeconds * armor.GadgetRefreshCooldownMultiplier
```

Gadget behavior:

- Using a ready gadget consumes it immediately.
- Consuming a gadget starts that gadget's refresh timer.
- A gadget cannot be used again until its refresh timer completes.
- The gadget resource controls the base refresh cooldown duration.
- Armor applies a percentage-style gadget refresh cooldown multiplier. Values below `1.0` are faster; values above `1.0` are slower.
- Armor does not grant extra gadget uses.

There are no `Small`, `Medium`, `Large`, or `Special` reserve buckets. Launchers use the same reload cooldown rule as guns. Grenades use the same gadget refresh rule as other gadgets.

Weapon firing behavior is fixed by the weapon resource:

- `IsFullAuto = false`: one shot per press.
- `IsFullAuto = true`: hold to repeat after `RecoverySeconds`.

The current modern SMGs and ARs are full-auto. Pistols, rifles, and launchers are single-fire.

## Runtime State

`PlayerLoadoutState` is the current runtime helper for the test scene. It stores equipped armor, up to 2 weapon slots, up to 3 gadget slots, selected item, loaded weapon ammo, weapon reload timers, gadget readiness, and gadget refresh timers. Reload/refresh timers should be calculated from item-owned base cooldowns plus armor multipliers.

Important API:

- `EquipArmor(PlayerArmor armor)`: applies armor capacity, clamps unavailable slots, and changes future reload/refresh cooldown multipliers.
- `EquipItem(PlayerItem item)`: equips a `PlayerWeapon` or `PlayerGadget`, selects it, and initializes that item's ammo/ready state.
- `TryConsumeWeaponAmmo(PlayerWeapon weapon)`: validates and consumes one loaded weapon shot before execution.
- `TryStartWeaponReload(PlayerWeapon weapon)`: starts the weapon-defined reload cooldown after applying the armor reload multiplier.
- `TryConsumeGadgetUse(PlayerGadget gadget)`: validates a ready gadget, consumes it, and starts the gadget-defined refresh cooldown after applying the armor refresh multiplier.

Future respawn, buy-zone, or round-start code should reset loaded ammo and gadget readiness explicitly. It should not use armor as a source of extra magazines or extra gadget uses.

## Test Room Behavior

Primary test bed:

- Scene: `scenes/tests/test_player_item_room_lan.tscn`
- Script: `scripts/data/gameplay/TestPlayerItemRoomLAN.cs`

The temporary `B` item grid remains a debug equipment menu, not the final buy UI.

Current behavior:

- Selecting a weapon equips it into the simplified weapon slots and makes it active.
- Selecting a grenade/gadget equips it into the simplified gadget slots and makes it active.
- Selecting armor applies the armor overlay, clamps unavailable weapon/gadget slots, and changes future reload/refresh cooldown multipliers.
- Item use is server-authoritative in the LAN test path: clients request use, the host validates ownership, recovery, death/control state, loaded ammo or gadget readiness, then executes and syncs the result.
- If a weapon has no loaded ammo, the host rejects firing until reload completes.
- If a gadget is refreshing, the host rejects gadget use until refresh completes.

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
- Restocking at round start, respawn, or buy zones should refill loaded weapon ammo and mark gadgets ready without changing armor's cooldown role.

## Deferred Or Removed

Removed from the active design:

- Backstrap item.
- Inventory bag/provider items.
- Typed physical slots like holster, pouch, strap, and backpack.
- Separate magazine reserve buckets like `Small`, `Medium`, `Large`, and `Special`.
- Separate ammo-carrier worn item.
- Armor-granted magazine counts.
- Armor-granted gadget use counts.

Deferred future work:

- Final buy wheel UI.
- Full HUD presentation for weapon/gadget slots, loaded ammo, reload cooldowns, gadget readiness, and refresh cooldowns.
- Additional armor variants with different protection/capacity tradeoffs.
- Non-modern item themes after the modern item/action slice is stable.
