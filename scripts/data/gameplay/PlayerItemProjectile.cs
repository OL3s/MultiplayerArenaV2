using Godot;

[GlobalClass]
public partial class PlayerItemProjectile : PlayerEquipable {
    public PlayerItemProjectile() {
        ContainerTypes.Add(PlayerItemSlotType.LargeItem);
    }

    [Export]
    public PlayerProjectileData Projectile { get; set; }

    [Export]
    public int MagazineSize { get; set; } = 1;

    [Export]
    public float ShotsPerSecond { get; set; } = 1.0f;

    [Export]
    public bool UsesSpecialMagazineReserve { get; set; } = true;
}
