using System.Collections.Generic;
using Godot;

public sealed class PlayerLoadoutState {
    public const int MaxWeaponSlots = 2;
    public const int MaxGadgetSlots = 3;

    private readonly Dictionary<string, int> _loadedAmmoByWeaponId = new();
    private readonly Dictionary<string, float> _reloadSecondsByWeaponId = new();
    private readonly Dictionary<string, float> _reloadRecoverySecondsByWeaponId = new();
    private readonly Dictionary<string, int> _readyUsesByGadgetId = new();
    private readonly Dictionary<string, float> _reloadRecoverySecondsByGadgetId = new();

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
        if (item is PlayerWeapon weapon)
            return TryConsumeWeaponAmmo(weapon);

        if (item is PlayerGadget gadget)
            return TryConsumeGadgetUse(gadget);

        return true;
    }

    public bool TryStartWeaponReload(PlayerWeapon weapon) {
        if (weapon == null || IsWeaponReloading(weapon) || IsWeaponReloadRecovering(weapon) || GetCurrentUses(weapon) >= GetMaxUses(weapon))
            return false;

        var reloadSeconds = GetWeaponReloadTimeSeconds(weapon);
        if (reloadSeconds <= 0.0f) {
            _loadedAmmoByWeaponId[weapon.ItemId] = GetMaxUses(weapon);
            StartWeaponReloadRecovery(weapon);
            return true;
        }

        _reloadSecondsByWeaponId[weapon.ItemId] = reloadSeconds;
        return true;
    }

    public void UpdateTimers(double delta) {
        UpdateWeaponReloadTimers((float)delta);
        UpdateWeaponReloadRecoveryTimers((float)delta);
        UpdateGadgetReloadRecoveryTimers((float)delta);
    }

    public bool IsWeaponReloading(PlayerItem item) {
        return item is PlayerWeapon weapon
            && _reloadSecondsByWeaponId.TryGetValue(weapon.ItemId, out var reloadSeconds)
            && reloadSeconds > 0.0f;
    }

    public float GetWeaponReloadRemainingSeconds(PlayerItem item) {
        return item is PlayerWeapon weapon && _reloadSecondsByWeaponId.TryGetValue(weapon.ItemId, out var reloadSeconds)
            ? Mathf.Max(reloadSeconds, 0.0f)
            : 0.0f;
    }

    public float GetWeaponReloadProgress(PlayerItem item) {
        if (item is not PlayerWeapon weapon)
            return 0.0f;

        var duration = GetWeaponReloadTimeSeconds(weapon);
        if (duration <= 0.0f)
            return 1.0f;

        return Mathf.Clamp(1.0f - (GetWeaponReloadRemainingSeconds(weapon) / duration), 0.0f, 1.0f);
    }

    public float GetGadgetReloadRecoveryRemainingSeconds(PlayerItem item) {
        return item is PlayerGadget gadget && _reloadRecoverySecondsByGadgetId.TryGetValue(gadget.ItemId, out var recoverySeconds)
            ? Mathf.Max(recoverySeconds, 0.0f)
            : 0.0f;
    }

    public float GetGadgetReloadRecoveryProgress(PlayerItem item) {
        if (item is not PlayerGadget gadget)
            return 0.0f;

        var duration = GetGadgetReloadRecoverySeconds(gadget);
        if (duration <= 0.0f)
            return 1.0f;

        return Mathf.Clamp(1.0f - (GetGadgetReloadRecoveryRemainingSeconds(gadget) / duration), 0.0f, 1.0f);
    }

    public bool IsGadgetReloadRecovering(PlayerItem item) {
        return item is PlayerGadget gadget
            && _reloadRecoverySecondsByGadgetId.TryGetValue(gadget.ItemId, out var recoverySeconds)
            && recoverySeconds > 0.0f;
    }

    public bool IsWeaponReloadRecovering(PlayerItem item) {
        return item is PlayerWeapon weapon
            && _reloadRecoverySecondsByWeaponId.TryGetValue(weapon.ItemId, out var recoverySeconds)
            && recoverySeconds > 0.0f;
    }

    public float GetWeaponReloadRecoveryRemainingSeconds(PlayerItem item) {
        return item is PlayerWeapon weapon && _reloadRecoverySecondsByWeaponId.TryGetValue(weapon.ItemId, out var recoverySeconds)
            ? Mathf.Max(recoverySeconds, 0.0f)
            : 0.0f;
    }

    public float GetWeaponReloadRecoveryProgress(PlayerItem item) {
        if (item is not PlayerWeapon weapon)
            return 0.0f;

        var duration = GetWeaponReloadRecoverySeconds(weapon);
        if (duration <= 0.0f)
            return 1.0f;

        return Mathf.Clamp(1.0f - (GetWeaponReloadRecoveryRemainingSeconds(weapon) / duration), 0.0f, 1.0f);
    }

    private bool TryConsumeWeaponAmmo(PlayerWeapon weapon) {
        if (IsWeaponReloading(weapon))
            return false;

        var currentAmmo = GetCurrentUses(weapon);
        if (currentAmmo <= 0)
            return false;

        _loadedAmmoByWeaponId[weapon.ItemId] = currentAmmo - 1;
        return true;
    }

    private bool TryConsumeGadgetUse(PlayerGadget gadget) {
        var currentUses = GetCurrentUses(gadget);
        if (currentUses <= 0)
            return false;

        _readyUsesByGadgetId[gadget.ItemId] = currentUses - 1;
        if (currentUses - 1 < GetMaxUses(gadget) && !IsGadgetReloadRecovering(gadget))
            StartGadgetReloadRecovery(gadget);

        return true;
    }

    public void ResetUsesToMax() {
        _loadedAmmoByWeaponId.Clear();
        _reloadSecondsByWeaponId.Clear();
        _reloadRecoverySecondsByWeaponId.Clear();
        _readyUsesByGadgetId.Clear();
        _reloadRecoverySecondsByGadgetId.Clear();
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

        if (item is PlayerWeapon)
            return _loadedAmmoByWeaponId.TryGetValue(item.ItemId, out var currentAmmo) ? currentAmmo : maxUses;

        if (item is PlayerGadget)
            return _readyUsesByGadgetId.TryGetValue(item.ItemId, out var readyUses) ? readyUses : maxUses;

        return maxUses;
    }

    public int GetMaxUses(PlayerItem item) {
        if (item == null)
            return 0;

        if (item is PlayerGadget)
            return 1;

        var magazineSize = item switch {
            PlayerItemShootable shootable => shootable.MagazineSize,
            PlayerItemProjectile projectile => projectile.MagazineSize,
            _ => 0,
        };
        return Mathf.Max(magazineSize, 0);
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
        if (maxUses <= 0)
            return;

        if (item is PlayerWeapon)
            _loadedAmmoByWeaponId[item.ItemId] = maxUses;
        else if (item is PlayerGadget)
            _readyUsesByGadgetId[item.ItemId] = maxUses;
    }

    private int GetWeaponSlotCount() {
        return Armor?.GetWeaponSlotCount() ?? 1;
    }

    private int GetGadgetSlotCount() {
        return Armor?.GetGadgetSlotCount() ?? 1;
    }

    private float GetWeaponReloadTimeSeconds(PlayerWeapon weapon) {
        return Mathf.Max(weapon.ReloadTimeSeconds, 0.0f) * Mathf.Max(Armor?.WeaponReloadTimeMultiplier ?? 1.0f, 0.0f);
    }

    private float GetWeaponReloadRecoverySeconds(PlayerWeapon weapon) {
        return Mathf.Max(weapon.ReloadRecoverySeconds, 0.0f) * Mathf.Max(Armor?.WeaponReloadRecoveryMultiplier ?? 1.0f, 0.0f);
    }

    private float GetGadgetReloadRecoverySeconds(PlayerGadget gadget) {
        return Mathf.Max(gadget.ReloadRecoverySeconds, 0.0f) * Mathf.Max(Armor?.GadgetReloadRecoveryMultiplier ?? 1.0f, 0.0f);
    }

    private void UpdateWeaponReloadTimers(float delta) {
        var completedWeaponIds = new List<string>();
        var updatedReloadSeconds = new Dictionary<string, float>();
        foreach (var reloadEntry in _reloadSecondsByWeaponId) {
            var remainingSeconds = reloadEntry.Value - delta;
            if (remainingSeconds <= 0.0f)
                completedWeaponIds.Add(reloadEntry.Key);
            else
                updatedReloadSeconds[reloadEntry.Key] = remainingSeconds;
        }

        foreach (var update in updatedReloadSeconds)
            _reloadSecondsByWeaponId[update.Key] = update.Value;

        foreach (var weaponId in completedWeaponIds) {
            _reloadSecondsByWeaponId.Remove(weaponId);
            var weapon = GetEquippedWeapon(weaponId);
            if (weapon != null) {
                _loadedAmmoByWeaponId[weaponId] = GetMaxUses(weapon);
                StartWeaponReloadRecovery(weapon);
            }
        }
    }

    private void UpdateGadgetReloadRecoveryTimers(float delta) {
        var completedGadgetIds = new List<string>();
        var updatedRecoverySeconds = new Dictionary<string, float>();
        foreach (var recoveryEntry in _reloadRecoverySecondsByGadgetId) {
            var remainingSeconds = recoveryEntry.Value - delta;
            if (remainingSeconds <= 0.0f)
                completedGadgetIds.Add(recoveryEntry.Key);
            else
                updatedRecoverySeconds[recoveryEntry.Key] = remainingSeconds;
        }

        foreach (var update in updatedRecoverySeconds)
            _reloadRecoverySecondsByGadgetId[update.Key] = update.Value;

        foreach (var gadgetId in completedGadgetIds) {
            _reloadRecoverySecondsByGadgetId.Remove(gadgetId);
            var gadget = GetEquippedGadget(gadgetId);
            if (gadget == null)
                continue;

            var currentUses = Mathf.Min(GetCurrentUses(gadget) + 1, GetMaxUses(gadget));
            _readyUsesByGadgetId[gadgetId] = currentUses;
            if (currentUses < GetMaxUses(gadget))
                StartGadgetReloadRecovery(gadget);
        }
    }

    private void UpdateWeaponReloadRecoveryTimers(float delta) {
        var completedWeaponIds = new List<string>();
        var updatedRecoverySeconds = new Dictionary<string, float>();
        foreach (var recoveryEntry in _reloadRecoverySecondsByWeaponId) {
            var remainingSeconds = recoveryEntry.Value - delta;
            if (remainingSeconds <= 0.0f)
                completedWeaponIds.Add(recoveryEntry.Key);
            else
                updatedRecoverySeconds[recoveryEntry.Key] = remainingSeconds;
        }

        foreach (var update in updatedRecoverySeconds)
            _reloadRecoverySecondsByWeaponId[update.Key] = update.Value;

        foreach (var weaponId in completedWeaponIds)
            _reloadRecoverySecondsByWeaponId.Remove(weaponId);
    }

    private void StartWeaponReloadRecovery(PlayerWeapon weapon) {
        var recoverySeconds = GetWeaponReloadRecoverySeconds(weapon);
        if (recoverySeconds > 0.0f)
            _reloadRecoverySecondsByWeaponId[weapon.ItemId] = recoverySeconds;
    }

    private void StartGadgetReloadRecovery(PlayerGadget gadget) {
        var recoverySeconds = GetGadgetReloadRecoverySeconds(gadget);
        if (recoverySeconds <= 0.0f) {
            _readyUsesByGadgetId[gadget.ItemId] = GetMaxUses(gadget);
            return;
        }

        _reloadRecoverySecondsByGadgetId[gadget.ItemId] = recoverySeconds;
    }

    private PlayerWeapon GetEquippedWeapon(string itemId) {
        foreach (var weapon in Weapons) {
            if (weapon?.ItemId == itemId)
                return weapon;
        }

        return null;
    }

    private PlayerGadget GetEquippedGadget(string itemId) {
        foreach (var gadget in Gadgets) {
            if (gadget?.ItemId == itemId)
                return gadget;
        }

        return null;
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
