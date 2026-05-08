using Godot;

[GlobalClass]
public abstract partial class PlayerEquipable : PlayerItem {
    [Export]
    public PlayerItemObjective UseObjective { get; set; }

    [Export]
    public float UseCooldownSeconds { get; set; }
}
