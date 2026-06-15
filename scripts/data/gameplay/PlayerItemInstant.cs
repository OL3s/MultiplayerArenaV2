using Godot;

[GlobalClass]
public partial class PlayerItemInstant : PlayerGadget {
    [Export]
    public bool ConsumeOnUse { get; set; } = true;

    [Export]
    public DamageResource Damage { get; set; } = new();
}
