using Godot;

public partial class LevelProp : StaticBody2D {
    private Sprite2D _sprite;
    private CollisionShape2D _collisionShape;
    private Area2D _hitbox;

    public LevelPropData Data { get; private set; }

    public float CollisionRadius {
        get {
            var size = Data?.Size ?? Vector2.Zero;
            return Mathf.Min(size.X, size.Y) * 0.5f;
        }
    }

    public Rect2 WorldHitbox {
        get {
            var size = Data?.Size ?? Vector2.Zero;
            return new Rect2(GlobalPosition - (size * 0.5f), size);
        }
    }

    public override void _Ready() {
        EnsureNodes();
        RefreshVisuals();
    }

    public void Initialize(LevelPropData data, Vector2 worldPosition) {
        Data = data;
        Position = worldPosition;
        EnsureNodes();
        RefreshVisuals();
    }

    public bool ContainsWorldPosition(Vector2 worldPosition) {
        return !IsDestroyed() && GlobalPosition.DistanceTo(worldPosition) <= CollisionRadius;
    }

    public bool IsInsideWorldRadius(Vector2 worldCenter, float radius) {
        if (IsDestroyed())
            return false;

        return GlobalPosition.DistanceTo(worldCenter) <= radius;
    }

    public float GetRadiusDamageMultiplier(Vector2 worldCenter, float radius) {
        if (radius <= 0.0f)
            return 0.0f;

        var distance = GlobalPosition.DistanceTo(worldCenter);
        if (distance > radius)
            return 0.0f;

        return Mathf.Clamp(1.0f - (distance / radius), 0.0f, 1.0f);
    }

    public bool ApplyDamage(DamageContainer damageContainer) {
        if (Data?.Health == null || IsDestroyed())
            return false;

        Data.Health.ApplyDamage(damageContainer);
        if (Data.Health.IsDead()) {
            QueueFree();
            return true;
        }

        UpdateDamageStage();
        return true;
    }

    public bool IsDestroyed() {
        return Data?.Health == null || Data.Health.IsDead();
    }

    private void EnsureNodes() {
        if (_sprite != null)
            return;

        _sprite = new Sprite2D { Name = "Sprite" };
        AddChild(_sprite);

        _hitbox = new Area2D { Name = "Hitbox" };
        AddChild(_hitbox);

        _collisionShape = new CollisionShape2D { Name = "CollisionShape2D" };
        AddChild(_collisionShape);
    }

    private void RefreshVisuals() {
        if (Data == null || _sprite == null || _collisionShape == null)
            return;

        if (!string.IsNullOrWhiteSpace(Data.TexturePath))
            _sprite.Texture = GD.Load<Texture2D>(Data.TexturePath);

        if (_sprite.Texture != null) {
            var textureSize = _sprite.Texture.GetSize();
            _sprite.Hframes = Mathf.Max(Data.DamageStageCount, 1);
            _sprite.Vframes = 1;
            var frameSize = new Vector2(textureSize.X / _sprite.Hframes, textureSize.Y);
            if (frameSize.X > 0.0f && frameSize.Y > 0.0f)
                _sprite.Scale = Data.Size / frameSize;
        }

        UpdateDamageStage();
        _collisionShape.Shape = new CircleShape2D { Radius = CollisionRadius };
    }

    private void UpdateDamageStage() {
        if (_sprite == null || Data?.Health == null)
            return;

        _sprite.Frame = Mathf.Clamp(GetDamageStage(), 0, Mathf.Max(_sprite.Hframes - 1, 0));
    }

    private int GetDamageStage() {
        var healthRatio = Data.Health.MaxHealth <= 0
            ? 0.0f
            : Data.Health.CurrentHealth / (float)Data.Health.MaxHealth;

        if (healthRatio < 0.5f)
            return 2;

        return healthRatio < 0.9f ? 1 : 0;
    }
}
