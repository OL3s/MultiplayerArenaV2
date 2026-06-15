using System.Collections.Generic;
using Godot;

public sealed class PlayerLoadoutState {
    public const int MaxWeaponSlots = 2;
    public const int MaxGadgetSlots = 3;

    private readonly Dictionary<string, int> _usesByItemId = new();

    public PlayerArmor Armor { get; private set; }

    public PlayerWeapon[] Weapons { get; } = new PlayerWeapon[MaxWeaponSlots];

    public PlayerGadget[] Gadgets { get; } = new PlayerGadget[MaxGadgetSlots];

    public PlayerItem SelectedItem { get; private set; }

    public void EquipArmor(PlayerArmor armor) {
        Armor = armor;
        ClampUnavailableSlots();
        ResetUsesToMax();
    }

    public bool EquipItem(PlayerItem item) {
        if (item == null)
            return false;

        var equipped = item switch {
            PlayerWeapon weapon => EquipWeapon(weapon),
            PlayerGadget gadget => EquipGadget(gadget),
            _ => false,
        };
        if (!equipped)
            return false;

        SelectedItem = item;
        ResetUsesToMax();
        return true;
    }

    public bool TryConsumeUse(PlayerItem item) {
        var maxUses = GetMaxUses(item);
        if (maxUses <= 0)
            return true;

        var currentUses = GetCurrentUses(item);
        if (currentUses <= 0)
            return false;

        _usesByItemId[item.ItemId] = currentUses - 1;
        return true;
    }

    public void ResetUsesToMax() {
        _usesByItemId.Clear();
        foreach (var weapon in Weapons)
            ResetItemUses(weapon);
        foreach (var gadget in Gadgets)
            ResetItemUses(gadget);
    }

    public int GetCurrentUses(PlayerItem item) {
        if (item == null)
            return 0;

        var maxUses = GetMaxUses(item);
        if (maxUses <= 0)
            return 0;

        return _usesByItemId.TryGetValue(item.ItemId, out var currentUses) ? currentUses : maxUses;
    }

    public int GetMaxUses(PlayerItem item) {
        if (item == null)
            return 0;

        if (item is PlayerGadget)
            return Mathf.Max(GetArmorGadgetUseCount(), 0);

        var magazineSize = item switch {
            PlayerItemShootable shootable => shootable.MagazineSize,
            PlayerItemProjectile projectile => projectile.MagazineSize,
            _ => 0,
        };
        return Mathf.Max(magazineSize, 0) * Mathf.Max(GetArmorWeaponMagazineCount(), 0);
    }

    public string GetLoadoutText() {
        var weaponText = $"W:{GetItemSlotText(Weapons, GetWeaponSlotCount())}";
        var gadgetText = $"G:{GetItemSlotText(Gadgets, GetGadgetSlotCount())}";
        return $"{weaponText} {gadgetText}";
    }

    private bool EquipWeapon(PlayerWeapon item) {
        return EquipIntoSlots(Weapons, GetWeaponSlotCount(), item);
    }

    private bool EquipGadget(PlayerGadget item) {
        return EquipIntoSlots(Gadgets, GetGadgetSlotCount(), item);
    }

    private static bool EquipIntoSlots<T>(T[] slots, int availableSlots, T item) where T : PlayerItem {
        availableSlots = Mathf.Clamp(availableSlots, 0, slots.Length);
        if (availableSlots <= 0)
            return false;

        for (var i = 0; i < availableSlots; i++) {
            if (slots[i]?.ItemId == item.ItemId) {
                slots[i] = item;
                return true;
            }
        }

        for (var i = 0; i < availableSlots; i++) {
            if (slots[i] == null) {
                slots[i] = item;
                return true;
            }
        }

        slots[availableSlots - 1] = item;
        return true;
    }

    private void ClampUnavailableSlots() {
        for (var i = GetWeaponSlotCount(); i < Weapons.Length; i++)
            Weapons[i] = null;
        for (var i = GetGadgetSlotCount(); i < Gadgets.Length; i++)
            Gadgets[i] = null;

        if (SelectedItem != null && !ContainsEquippedItem(SelectedItem))
            SelectedItem = GetFirstEquippedItem();
    }

    private bool ContainsEquippedItem(PlayerItem item) {
        foreach (var weapon in Weapons) {
            if (weapon?.ItemId == item.ItemId)
                return true;
        }

        foreach (var gadget in Gadgets) {
            if (gadget?.ItemId == item.ItemId)
                return true;
        }

        return false;
    }

    private PlayerItem GetFirstEquippedItem() {
        foreach (var weapon in Weapons) {
            if (weapon != null)
                return weapon;
        }

        foreach (var gadget in Gadgets) {
            if (gadget != null)
                return gadget;
        }

        return null;
    }

    private void ResetItemUses(PlayerItem item) {
        if (item == null)
            return;

        var maxUses = GetMaxUses(item);
        if (maxUses > 0)
            _usesByItemId[item.ItemId] = maxUses;
    }

    private int GetWeaponSlotCount() {
        return Armor?.GetWeaponSlotCount() ?? 1;
    }

    private int GetGadgetSlotCount() {
        return Armor?.GetGadgetSlotCount() ?? 1;
    }

    private int GetArmorWeaponMagazineCount() {
        return Armor?.WeaponMagazineCount ?? 2;
    }

    private int GetArmorGadgetUseCount() {
        return Armor?.GadgetUseCount ?? 1;
    }

    private string GetItemSlotText<T>(T[] slots, int availableSlots) where T : PlayerItem {
        var parts = new string[slots.Length];
        for (var i = 0; i < slots.Length; i++) {
            if (i >= availableSlots)
                parts[i] = "-";
            else if (slots[i] == null)
                parts[i] = "empty";
            else {
                var maxUses = GetMaxUses(slots[i]);
                parts[i] = maxUses > 0
                    ? $"{slots[i].DisplayName} {GetCurrentUses(slots[i])}/{maxUses}"
                    : slots[i].DisplayName;
            }
        }

        return string.Join("|", parts);
    }
}
