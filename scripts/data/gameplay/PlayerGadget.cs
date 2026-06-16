using Godot;

[GlobalClass]
public abstract partial class PlayerGadget : PlayerItem, IPlayerUsable {
    [Export]
    public PlayerItemObjective UseObjective { get; set; }

    [Export]
    public float ReloadRecoverySeconds { get; set; } = 1.0f;

    [Export]
    public float Range { get; set; } = 160.0f;

    [Export]
    public float AimDisplayRange { get; set; }

    [Export]
    public float AimMoveSpeedMultiplier { get; set; } = 0.9f;

    [Export]
    public float DefaultAccuracy { get; set; } = 0.03f;

    [Export]
    public float MovementAccuracy { get; set; } = 0.06f;

    [Export]
    public float AccuracyPushback { get; set; } = 0.01f;

    [Export]
    public float ShotAccuracyRecovery { get; set; } = 0.08f;

    [Export]
    public float MovementAccuracyRecovery { get; set; } = 0.18f;

    [Export]
    public float MaxInaccuracy { get; set; } = 0.25f;

    protected PlayerGadget() {
        ContainerTypes.Add(PlayerItemSlotType.Gadget);
    }

    public float GetAimDisplayDistance() {
        return AimDisplayRange > 0.0f ? AimDisplayRange : Range;
    }

}
