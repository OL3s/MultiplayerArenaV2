using Godot;

[GlobalClass]
public partial class PlayerItemMelee : PlayerEquipable {
    public PlayerItemMelee() {
        ContainerTypes.Add(PlayerItemSlotType.SmallItem);
    }

    [Export]
    public float Range { get; set; } = 48.0f;

    [Export]
    public float ArcDegrees { get; set; } = 90.0f;

    [Export]
    public DamageResource Damage { get; set; } = new();
}
