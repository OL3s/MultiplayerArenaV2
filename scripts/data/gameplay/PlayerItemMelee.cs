using Godot;

[GlobalClass]
public partial class PlayerItemMelee : PlayerWeapon {
    public PlayerItemMelee() {
        Range = 48.0f;
    }

    [Export]
    public float ArcDegrees { get; set; } = 90.0f;

    [Export]
    public DamageResource Damage { get; set; } = new();
}
