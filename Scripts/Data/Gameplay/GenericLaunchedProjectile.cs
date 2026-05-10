using System.Collections.Generic;
using Godot;

public partial class GenericLaunchedProjectile : Node2D {
    private readonly HashSet<ulong> _hitObjectIds = new();
    private readonly HashSet<Vector2I> _hitWallTiles = new();

    private PlayerItemRuntimeContext _context;
    private PlayerProjectileData _projectileData;
    private PlayerItemObjective _impactObjective;
    private Vector2 _direction = Vector2.Right;
    private float _range;
    private float _distanceTraveled;
    private float _lifeSeconds;
    private Sprite2D _sprite;

    public override void _Ready() {
        EnsureVisual();
    }

    public static GenericLaunchedProjectile Create(
        PackedScene scene,
        PlayerItemRuntimeContext context,
        PlayerProjectileData projectileData,
        PlayerItemObjective impactObjective,
        Vector2 startPosition,
        Vector2 direction,
        float range) {
        var projectile = scene?.Instantiate<GenericLaunchedProjectile>();
        if (projectile == null)
            return null;

        projectile.Initialize(context, projectileData, impactObjective, startPosition, direction, range);
        return projectile;
    }

    public override void _PhysicsProcess(double delta) {
        if (_context == null || _projectileData == null || _range <= 0.0f) {
            QueueFree();
            return;
        }

        _lifeSeconds += (float)delta;
        if (_projectileData.LifetimeSeconds > 0.0f && _lifeSeconds >= _projectileData.LifetimeSeconds) {
            ExecuteAt(GlobalPosition, null);
            return;
        }

        var remainingRange = _range - _distanceTraveled;
        if (remainingRange <= 0.0f) {
            ExecuteAt(GlobalPosition, null);
            return;
        }

        var moveDistance = Mathf.Min(_projectileData.Speed * (float)delta, remainingRange);
        var from = GlobalPosition;
        var to = from + (_direction * moveDistance);
        var hit = _context.FindFirstHit(from, to, GetRadius(), _hitObjectIds, _hitWallTiles);

        if (hit.HasHit) {
            ExecuteAt(hit.Position, hit);
            return;
        }

        GlobalPosition = to;
        _distanceTraveled += moveDistance;
    }

    public void Initialize(
        PlayerItemRuntimeContext context,
        PlayerProjectileData projectileData,
        PlayerItemObjective impactObjective,
        Vector2 startPosition,
        Vector2 direction,
        float range) {
        _context = context;
        _projectileData = projectileData;
        _impactObjective = impactObjective ?? projectileData?.CollisionObjective;
        _direction = direction.LengthSquared() > 0.0001f ? direction.Normalized() : Vector2.Right;
        _range = range > 0.0f ? range : projectileData?.Range ?? 0.0f;
        GlobalPosition = startPosition;
        Rotation = _direction.Angle();
        EnsureVisual();
        UpdateVisual();
    }

    private void ExecuteAt(Vector2 position, ProjectileSweepHit hit) {
        GlobalPosition = position;
        _context.ExecuteObjective(_impactObjective, position, hit, _projectileData.Damage);
        QueueFree();
    }

    private float GetRadius() {
        return Mathf.Max(_projectileData?.Width ?? 4.0f, 1.0f) * 0.5f;
    }

    private void EnsureVisual() {
        if (_sprite != null)
            return;

        _sprite = new Sprite2D { Name = "ProjectileSprite" };
        AddChild(_sprite);
    }

    private void UpdateVisual() {
        if (_sprite == null || _projectileData == null)
            return;

        _sprite.Texture = _projectileData.Texture;
        _sprite.Modulate = _projectileData.Color;
        if (_sprite.Texture == null)
            _sprite.Scale = Vector2.One;
    }
}
