# Player Items And Loadout Plan

This document tracks the simplified in-game player item, armor, reload/recovery, and loadout model.

## Core Goal

Keep the game fast and readable. A player should not manage backpacks, backstraps, magazine pouches, or separate inventory providers. Equipped armor is the only loadout-capacity provider, and it modifies item-defined reload/recovery pacing instead of granting extra ammo bundles directly.

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

Theme selection is lobby-configured through `SetupConfig.ItemThemeConfig`. The lobby selection is a candidate pool; match start resolves one active theme and runtime item membership comes from that selected `ItemThemeDefinition` root and folder scanning, not hardcoded duplicate id/path arrays. See `docs/item-themes.md`.

Loadout acquisition is now lobby-configured through `SetupConfig.LoadoutModeConfig`. The config/UI exposes `Buy On Spawn`, `Persistent Budget`, `Random Respawn`, `Mirror Loadout`, and `Map Pickups`. `ArenaMatch` now initializes first-pass per-player Credits from `StartingCredits` and uses item costs to disable/reject unaffordable radial buy entries in Credit-based modes; deeper randomization, mirrored-loadout behavior, and map pickup spawning are still follow-up work. See `docs/loadout-modes.md`.

## Armor-Driven Capacity And Timing

`PlayerArmor` owns protection, loadout capacity, and reload/recovery modifiers:

- `AllowsSecondWeapon`: whether the player may carry a second weapon.
- `GadgetSlotCount`: how many one-use gadget slots are available, clamped to `0-3`.
- `WeaponReloadTimeMultiplier`: percentage-style multiplier applied to a weapon's reload time.
- `WeaponReloadRecoveryMultiplier`: percentage-style multiplier applied to a weapon's reload recovery.
- `GadgetReloadRecoveryMultiplier`: percentage-style multiplier applied to a gadget's own reload recovery.

Armor does not define how many magazines a weapon has, how many total uses a gadget has, or the base timing length of an item. Weapon `.tres` resources own their fire rate, magazine/ammo shape, reload time, and reload recovery. Gadget `.tres` resources own their reload recovery after use. Armor only applies percentage modifiers that can speed up or slow down weapon reload time, weapon reload recovery, and gadget reload recovery.

There are no standalone inventory bags, ammo rigs, pouches, holsters, or backstrap items in the current model. If a later design needs different carry capacity or reload/recovery modifiers, add another armor variant instead of adding a second equipment layer.

Current first-pass armor tuning target:

- No armor or light armor style baseline: 1 weapon, 1 gadget, `1.0x` weapon reload time multiplier, `1.0x` weapon reload recovery multiplier, `1.0x` gadget reload recovery multiplier.
- Heavy armor: 2 weapons, 2 gadgets, slower or faster reload/recovery multipliers depending on its protection/capacity tradeoff.

## Weapons And Gadgets

Items are split into separate resource families:

- `PlayerWeapon`: base for shootable weapons, launchers, melee weapons, and other active combat weapons.
- `PlayerGadget`: base for grenades, instant-use items, and future small utility items.
- `PlayerArmor`: base for armor and loadout-capacity data.

`PlayerWeapon` and `PlayerGadget` both implement `IPlayerUsable` for the runtime use path, but they do not inherit from a shared `PlayerEquipable` resource class. This keeps weapon and gadget data separated while preserving one item-use execution path where needed. Item use is intentionally simple: single-fire weapons and gadgets use once per press, full-auto weapons repeat according to `ShotsPerSecond`, weapon firing is gated by loaded ammo, and gadget use is gated by readiness/reload recovery state. There are no toggled fire modes, burst modes, or fire-mode cycling controls.

The player can carry at most 2 weapons and at most 3 gadgets. The active armor decides how many of those slots are available.

Weapon ammo is intentionally explicit but still lightweight:

```text
loaded ammo = weapon.MagazineSize
fire cadence = weapon.ShotsPerSecond
reload time = weapon.ReloadTimeSeconds * armor.WeaponReloadTimeMultiplier
reload recovery = weapon.ReloadRecoverySeconds * armor.WeaponReloadRecoveryMultiplier
```

Weapon behavior:

- Firing consumes loaded ammo.
- A weapon with no loaded ammo cannot fire.
- Pressing reload starts that weapon's reload timer if it is not already full, not already reloading, not in reload recovery, and the player is allowed to act.
- When the reload timer finishes, the weapon reloads back to its weapon-defined loaded-ammo maximum and starts reload recovery.
- Reload recovery blocks starting another reload, but does not block firing.
- The weapon resource controls fire rate, base reload duration, and post-reload recovery duration.
- Armor applies percentage-style weapon reload time and reload recovery multipliers. Values below `1.0` are faster; values above `1.0` are slower.
- Armor does not grant extra magazines or reserve ammo.
- In the current test room, keyboard `R` and Xbox `X` request reload for the selected local weapon. Network clients request reload from the server, and the server syncs accepted reloads back to peers.

Gadget count is intentionally timer-based:

```text
ready gadget use = 1
reload recovery = gadget.ReloadRecoverySeconds * armor.GadgetReloadRecoveryMultiplier
```

Gadget behavior:

- Using a ready gadget consumes it immediately.
- Consuming a gadget starts that gadget's reload recovery timer immediately.
- A gadget cannot be used again until its reload recovery timer completes.
- The gadget resource controls the base reload recovery duration through `ReloadRecoverySeconds`.
- Armor applies a percentage-style gadget reload recovery multiplier. Values below `1.0` are faster; values above `1.0` are slower.
- Armor does not grant extra gadget uses.

There are no `Small`, `Medium`, `Large`, or `Special` reserve buckets. Launchers use the same reload and recovery rules as guns. Grenades use the same gadget reload recovery rule as other gadgets.

Weapon firing behavior is fixed by the weapon resource:

- `IsFullAuto = false`: one shot per press.
- `IsFullAuto = true`: hold to repeat according to `ShotsPerSecond`.

The current modern SMGs and ARs are full-auto. Pistols, rifles, and launchers are single-fire.

## Runtime State

`PlayerLoadoutState` is the current runtime helper for the test scene. It stores equipped armor, up to 2 weapon slots, up to 3 gadget slots, selected item, loaded weapon ammo, weapon reload timers, weapon reload recovery timers, gadget readiness, and gadget reload recovery timers. Reload/recovery timers should be calculated from item-owned base values plus armor multipliers.

Important API:

- `EquipArmor(PlayerArmor armor)`: applies armor capacity, clamps unavailable slots, and changes future reload/recovery multipliers.
- `EquipItem(PlayerItem item)`: equips a `PlayerWeapon` or `PlayerGadget`, selects it, and initializes that item's ammo/ready state.
- `TryConsumeWeaponAmmo(PlayerWeapon weapon)`: validates and consumes one loaded weapon shot before execution.
- `TryStartWeaponReload(PlayerWeapon weapon)`: starts the weapon-defined reload timer after applying the armor reload time multiplier when reload recovery is not active.
- `TryConsumeGadgetUse(PlayerGadget gadget)`: validates a ready gadget, consumes it, and starts gadget reload recovery from `ReloadRecoverySeconds` after applying the armor gadget reload recovery multiplier.

Future respawn, buy-zone, or round-start code should reset loaded ammo and gadget readiness explicitly. It should not use armor as a source of extra magazines or extra gadget uses.

## Test Room Behavior

Primary test bed:

- Scene: `scenes/tests/test_player_item_room_lan.tscn`
- Script: `scripts/data/gameplay/TestPlayerItemRoomLAN.cs`

The `B` item grid remains a debug buy/equipment menu, not the primary buy UI. The primary test-room buy UI is the scene-backed radial menu opened with `V` or Xbox controller `Y`.

In lobby-started `scenes/gameplay/arena_match.tscn`, the old debug grid is disabled. Keyboard `B` opens the same radial buy menu as `V`, and buying is allowed only while the player is inside the wide team spawn/base range. If more than one item theme is selected in the lobby, the host resolves one active theme before the match scene loads; the buy menu never asks players to choose a theme at runtime.

Current behavior:

- The selected lobby item themes decide which theme libraries populate the default starter, radial buy menu, and debug buy/equip grid.
- Selecting a weapon equips it into the simplified weapon slots and makes it active.
- Selecting a grenade/gadget equips it into the simplified gadget slots and makes it active.
- Selecting armor applies the armor overlay, clamps unavailable weapon/gadget slots, and changes future reload/recovery multipliers.
- The radial buy menu is theme-aware, but it only sees the host-resolved active theme. It opens at that theme's configured buy groups, then group-specific item rings with `Back`.
- Buy groups are data-owned by `ItemThemeDefinition.BuyMenuGroups` and `ItemBuyMenuGroup` resources. Groups can nest and filter items by accepted kind, item id prefix, resource path prefix, and whether the theme starter item should be included.
- Empty buy groups are disabled for the selected theme. Empty item rings show a disabled `Empty` entry instead of silently presenting a blank menu.
- Radial item entries show item cost and the active local player's current Credits in `BuyOnSpawn` and `PersistentBudget`. Entries above the player's current Credits are disabled and selection is rejected if Credits changed before confirmation.
- Item use is server-authoritative in the LAN test path: clients request use, the host validates ownership, fire interval, death/control state, loaded ammo or gadget readiness, then executes and syncs the result.
- If a weapon has no loaded ammo, the host rejects firing until reload completes.
- If a gadget is in reload recovery, the host rejects gadget use until recovery completes.

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

## Purchase Behavior

The radial buy menu is implemented in the shared `ArenaMatch` runtime. Purchase validation uses the same simplified armor capacity rules:

- Buying a weapon requires an available weapon slot from armor capacity.
- Buying a gadget requires an available gadget slot from armor capacity.
- Buying/equipping armor may remove excess weapons/gadgets if the new armor has fewer slots.
- `BuyOnSpawn` and `PersistentBudget` buys require enough Credits from the local per-player Credit pool initialized from `LoadoutModeConfig.StartingCredits`.
- Successful Credit-mode buys deduct the selected item's `Cost` after the equip request is accepted locally.
- `BuyOnSpawn` awards `LoadoutModeConfig.CreditsPerKill` Credits on player kills and `CreditsPerSpawn` when respawn finishes, so repeated deaths still build buying power over time.
- `PersistentBudget` does not award kill or spawn Credits and is finite.
- `RandomOnRespawn`, `MirrorLoadout`, and `MapPickups` do not use Credits for current buy-menu affordability checks.
- `MapPickups` should eventually spawn weapons, gear, and gadgets at inactive secondary neutral objective locations, never at the single main center objective.
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

- Server-authoritative synchronized Credits state for LAN clients; current Credit enforcement is first-pass local runtime state in `ArenaMatch`.
- Full HUD presentation for weapon/gadget slots, loaded ammo, reload timers, gadget readiness, and reload recovery timers.
- Additional armor variants with different protection/capacity tradeoffs.
- Medieval melee-focused item runtime; current medieval content is placeholder ranged/gadget/armor data until melee is implemented.
