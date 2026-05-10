using Godot;

public partial class GenericThrownItem : Node2D {
    private const float BounceDamping = 0.62f;
    private const float MinBounceRemainingDistance = 4.0f;
    private const int MaxBounces = 6;

    private PlayerItemRuntimeContext _context;
    private PlayerItemThrowable _throwable;
    private PlayerItemObjective _objective;
    private Vector2 _startPosition;
    private float _travelSeconds = 0.2f;
    private float _elapsedSeconds;
    private float _fuseSeconds;
    private float _totalTravelDistance;
    private float _remainingTravelDistance;
    private Vector2 _travelDirection = Vector2.Right;
    private int _bounceCount;
    private bool _landed;
    private Polygon2D _shadow;
    private Sprite2D _sprite;

    public override void _Ready() {
        EnsureVisual();
    }

    public static GenericThrownItem Create(
        PackedScene scene,
        PlayerItemRuntimeContext context,
        PlayerItemThrowable throwable,
        PlayerItemObjective objective,
        Vector2 startPosition,
        Vector2 targetPosition) {
        var thrownItem = scene?.Instantiate<GenericThrownItem>();
        if (thrownItem == null)
            return null;

        thrownItem.Initialize(context, throwable, objective, startPosition, targetPosition);
        return thrownItem;
    }

    public override void _PhysicsProcess(double delta) {
        if (_context == null || _throwable == null) {
            QueueFree();
            return;
        }

        _elapsedSeconds += (float)delta;
        if (!_landed) {
            MoveThrownItem(delta);
            var progress = 1.0f - Mathf.Clamp(_remainingTravelDistance / Mathf.Max(_totalTravelDistance, 0.001f), 0.0f, 1.0f);
            UpdateArcVisual(progress);
            if (_remainingTravelDistance <= 0.0f) {
                _landed = true;
                if (_throwable.ActivateOnGroundImpact || _throwable.ExecuteObjectiveOnRest) {
                    ExecuteObjective();
                    return;
                }
            }
        }

        if (_fuseSeconds > 0.0f && _elapsedSeconds >= _fuseSeconds)
            ExecuteObjective();
    }

    public void Initialize(
        PlayerItemRuntimeContext context,
        PlayerItemThrowable throwable,
        PlayerItemObjective objective,
        Vector2 startPosition,
        Vector2 targetPosition) {
        _context = context;
        _throwable = throwable;
        _objective = objective;
        _startPosition = startPosition;
        _fuseSeconds = throwable.FuseSeconds;
        _totalTravelDistance = startPosition.DistanceTo(targetPosition);
        _remainingTravelDistance = _totalTravelDistance;
        _travelDirection = targetPosition.DistanceSquaredTo(startPosition) > 0.001f
            ? (targetPosition - startPosition).Normalized()
            : Vector2.Right;
        _travelSeconds = _totalTravelDistance / Mathf.Max(throwable.ThrowSpeed, 1.0f);
        GlobalPosition = startPosition;
        EnsureVisual();
        UpdateVisual();
    }

    private void MoveThrownItem(double delta) {
        var remainingStepDistance = Mathf.Min(_throwable.ThrowSpeed * (float)delta, _remainingTravelDistance);
        while (remainingStepDistance > 0.0f && _remainingTravelDistance > 0.0f && !_landed) {
            var from = GlobalPosition;
            var to = from + (_travelDirection * remainingStepDistance);
            var hit = _context.FindFirstHit(from, to, 4.0f, null, null);
            if (!hit.HasHit) {
                GlobalPosition = to;
                _remainingTravelDistance -= remainingStepDistance;
                return;
            }

            var hitDistance = Mathf.Clamp(hit.Distance, 0.0f, remainingStepDistance);
            GlobalPosition = hit.Position;
            _remainingTravelDistance -= hitDistance;
            remainingStepDistance -= hitDistance;

            if (!TryBounce(hit)) {
                _remainingTravelDistance = 0.0f;
                _landed = true;
                return;
            }

            _remainingTravelDistance *= BounceDamping;
            remainingStepDistance *= BounceDamping;
            GlobalPosition += _travelDirection * 0.5f;
        }
    }

    private bool TryBounce(ProjectileSweepHit hit) {
        if (_bounceCount >= MaxBounces || _remainingTravelDistance <= MinBounceRemainingDistance)
            return false;

        var normal = GetBounceNormal(hit);
        if (normal.LengthSquared() <= 0.0001f)
            return false;

        _travelDirection = _travelDirection.Bounce(normal).Normalized();
        _bounceCount++;
        return _travelDirection.LengthSquared() > 0.0001f;
    }

    private Vector2 GetBounceNormal(ProjectileSweepHit hit) {
        if (hit.Kind == ProjectileHitKind.Prop && hit.Target is LevelProp prop && GodotObject.IsInstanceValid(prop))
            return (hit.Position - prop.GlobalPosition).Normalized();

        if (hit.Kind == ProjectileHitKind.Player && hit.Target is DamageTestPlayer player && GodotObject.IsInstanceValid(player))
            return (hit.Position - player.GlobalPosition).Normalized();

        if (hit.Kind == ProjectileHitKind.Wall && _context?.ArenaMapData != null) {
            var tileCenter = new Vector2(
                (hit.WallTile.X * _context.TileSize.X) + (_context.TileSize.X * 0.5f),
                (hit.WallTile.Y * _context.TileSize.Y) + (_context.TileSize.Y * 0.5f));
            var normal = (hit.Position - tileCenter).Normalized();
            if (normal.LengthSquared() > 0.0001f)
                return normal;
        }

        return -_travelDirection;
    }

    private void ExecuteObjective() {
        var hit = new ProjectileSweepHit { Position = GlobalPosition };
        _context.ExecuteObjective(_objective ?? _throwable.UseObjective, GlobalPosition, hit, _throwable.Damage);
        QueueFree();
    }

    private void EnsureVisual() {
        if (_sprite != null)
            return;

        _shadow = new Polygon2D {
            Name = "Shadow",
            Color = new Color(0.0f, 0.0f, 0.0f, 0.32f),
            Polygon = CreateEllipsePolygon(6.0f, 2.4f, 18),
        };
        AddChild(_shadow);

        _sprite = new Sprite2D { Name = "ThrownItemSprite", ZIndex = 1 };
        AddChild(_sprite);
    }

    private void UpdateVisual() {
        if (_sprite == null || _throwable == null)
            return;

        _sprite.Texture = _throwable.HeldTexture;
    }

    private void UpdateArcVisual(float progress) {
        if (_sprite == null)
            return;

        var height = Mathf.Sin(progress * Mathf.Pi) * 28.0f;
        _sprite.Position = new Vector2(0.0f, -height);
        _sprite.Scale = Vector2.One * (1.0f + (height / 160.0f));
        _sprite.Rotation += 0.2f;

        if (_shadow != null) {
            var shadowScale = 1.0f - (height / 90.0f);
            _shadow.Scale = new Vector2(Mathf.Clamp(shadowScale, 0.55f, 1.0f), Mathf.Clamp(shadowScale, 0.45f, 1.0f));
        }
    }

    private static Vector2[] CreateEllipsePolygon(float radiusX, float radiusY, int pointCount) {
        var points = new Vector2[Mathf.Max(pointCount, 3)];
        for (var i = 0; i < points.Length; i++) {
            var angle = Mathf.Tau * i / points.Length;
            points[i] = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }

        return points;
    }
}
