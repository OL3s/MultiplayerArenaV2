using Godot;

[GlobalClass]
public partial class PlayerItemThrowable : PlayerEquipable {
    public PlayerItemThrowable() {
        ContainerTypes.Add(PlayerItemSlotType.SmallGadget);
    }

    [Export]
    public PackedScene ThrowableScene { get; set; }

    [Export]
    public float ThrowSpeed { get; set; } = 600.0f;

    [Export]
    public float MinThrowRange { get; set; } = 48.0f;

    [Export]
    public bool ThrowStrengthAffectsRange { get; set; } = true;

    [Export]
    public float FuseSeconds { get; set; } = 2.0f;

    [Export]
    public DamageResource Damage { get; set; } = new();

    [Export]
    public bool ExecuteObjectiveOnRest { get; set; } = true;

    [Export]
    public bool ActivateOnGroundImpact { get; set; }
}
