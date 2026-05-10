using Godot;

public sealed class ProjectileSweepHit {
    public ProjectileHitKind Kind { get; set; } = ProjectileHitKind.None;

    public GodotObject Target { get; set; }

    public Vector2I WallTile { get; set; }

    public Vector2 Position { get; set; }

    public float Distance { get; set; } = float.MaxValue;

    public bool HasHit => Kind != ProjectileHitKind.None;
}
