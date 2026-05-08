using Godot;

[GlobalClass]
public partial class PlayerProjectileData : Resource
{
    [Export]
    public PackedScene ProjectileScene { get; set; }

    [Export]
    public float Speed { get; set; } = 1200.0f;

    [Export]
    public Color Color { get; set; } = Colors.White;

    [Export]
    public int Penetration { get; set; }

    [Export]
    public PlayerItemObjective CollisionObjective { get; set; }
}
