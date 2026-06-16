using Godot;

[GlobalClass]
public partial class PlayerArmor : PlayerItem {
    [Export]
    public ArmorResource Armor { get; set; } = new();

    [Export]
    public bool AllowsSecondWeapon { get; set; }

    [Export]
    public int GadgetSlotCount { get; set; } = 1;

    [Export]
    public float WeaponReloadTimeMultiplier { get; set; } = 1.0f;

    [Export]
    public float WeaponReloadRecoveryMultiplier { get; set; } = 1.0f;

    [Export]
    public float GadgetReloadRecoveryMultiplier { get; set; } = 1.0f;

    public int GetWeaponSlotCount() {
        return AllowsSecondWeapon ? 2 : 1;
    }

    public int GetGadgetSlotCount() {
        return Mathf.Clamp(GadgetSlotCount, 0, 3);
    }
}
