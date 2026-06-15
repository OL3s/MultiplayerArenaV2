using Godot;

[GlobalClass]
public partial class PlayerItemProjectile : PlayerWeapon {
    [Export]
    public PlayerProjectileData Projectile { get; set; }

    [Export]
    public int MagazineSize { get; set; } = 1;

    [Export]
    public float ShotsPerSecond { get; set; } = 1.0f;

}
