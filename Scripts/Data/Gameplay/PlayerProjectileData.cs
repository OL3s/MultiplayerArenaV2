using Godot;

[GlobalClass]
public partial class PlayerProjectileData : Resource {
    [Export]
    public PackedScene ProjectileScene { get; set; }

    [Export]
    public Texture2D Texture { get; set; }

    [Export]
    public float Speed { get; set; } = 1200.0f;

    [Export]
    public float Range { get; set; }

    [Export]
    public float Width { get; set; } = 2.0f;

    [Export]
    public Color Color { get; set; } = Colors.White;

    [Export]
    public float LifetimeSeconds { get; set; } = 4.0f;

    [Export]
    public int Penetration { get; set; }

    [Export]
    public bool StopsOnFirstHit { get; set; } = true;

    [Export]
    public DamageResource Damage { get; set; } = new();

    [Export]
    public PlayerItemObjective CollisionObjective { get; set; }
}
