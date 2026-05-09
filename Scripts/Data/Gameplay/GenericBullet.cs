using System.Collections.Generic;
using Godot;

public partial class GenericBullet : Node2D {
    private readonly HashSet<ulong> _hitObjectIds = new();
    private readonly HashSet<Vector2I> _hitWallTiles = new();

    private PlayerItemRuntimeContext _context;
    private PlayerProjectileData _projectileData;
    private PlayerItemObjective _impactObjective;
    private Vector2 _direction = Vector2.Right;
    private float _range;
    private float _distanceTraveled;
    private float _lifeSeconds;
    private int _hitsApplied;
    private Line2D _line;

    public override void _Ready() {
        EnsureVisual();
    }

    public static GenericBullet Create(
        PackedScene scene,
        PlayerItemRuntimeContext context,
        PlayerProjectileData projectileData,
        PlayerItemObjective impactObjective,
        Vector2 startPosition,
        Vector2 direction,
        float range) {
        var bullet = scene?.Instantiate<GenericBullet>();
        if (bullet == null)
            return null;

        bullet.Initialize(context, projectileData, impactObjective, startPosition, direction, range);
        return bullet;
    }

    public override void _PhysicsProcess(double delta) {
        if (_context == null || _projectileData == null || _range <= 0.0f) {
            QueueFree();
            return;
        }

        _lifeSeconds += (float)delta;
        if (_projectileData.LifetimeSeconds > 0.0f && _lifeSeconds >= _projectileData.LifetimeSeconds) {
            QueueFree();
            return;
        }

        var remainingRange = _range - _distanceTraveled;
        if (remainingRange <= 0.0f) {
            QueueFree();
            return;
        }

        var moveDistance = Mathf.Min(_projectileData.Speed * (float)delta, remainingRange);
        var from = GlobalPosition;
        var to = from + (_direction * moveDistance);
        var hit = _context.FindFirstHit(from, to, GetRadius(), _hitObjectIds, _hitWallTiles);

        if (hit.HasHit) {
            GlobalPosition = hit.Position;
            RecordHit(hit);
            _context.ExecuteObjective(_impactObjective, hit.Position, hit, _projectileData.Damage);
            _hitsApplied++;

            if (ShouldStopAfterHit()) {
                QueueFree();
                return;
            }
        }
        else {
            GlobalPosition = to;
        }

        _distanceTraveled += moveDistance;
        if (_distanceTraveled >= _range)
            QueueFree();
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

    private bool ShouldStopAfterHit() {
        if (_projectileData.StopsOnFirstHit)
            return true;

        return _hitsApplied > Mathf.Max(_projectileData.Penetration, 0);
    }

    private void RecordHit(ProjectileSweepHit hit) {
        if (hit.Kind == ProjectileHitKind.Wall)
            _hitWallTiles.Add(hit.WallTile);
        else if (hit.Target != null)
            _hitObjectIds.Add(hit.Target.GetInstanceId());
    }

    private float GetRadius() {
        return Mathf.Max(_projectileData?.Width ?? 1.0f, 1.0f) * 0.5f;
    }

    private void EnsureVisual() {
        if (_line != null)
            return;

        _line = new Line2D { Name = "BulletLine", Antialiased = false };
        _line.AddPoint(Vector2.Zero);
        _line.AddPoint(new Vector2(8.0f, 0.0f));
        AddChild(_line);
    }

    private void UpdateVisual() {
        if (_line == null || _projectileData == null)
            return;

        _line.Width = Mathf.Max(_projectileData.Width, 1.0f);
        _line.DefaultColor = _projectileData.Color;
        _line.SetPointPosition(1, new Vector2(Mathf.Max(_projectileData.Width * 4.0f, 6.0f), 0.0f));
    }
}
