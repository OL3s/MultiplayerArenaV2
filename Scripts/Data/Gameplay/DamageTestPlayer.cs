using Godot;

public partial class DamageTestPlayer : Node2D {
    private static readonly Vector2 DefaultSize = new(16.0f, 16.0f);

    private Area2D _hitbox;
    private CollisionShape2D _collisionShape;
    private Label _label;
    private bool _isAlive = true;

    public int GlobalId { get; private set; } = -1;

    public Vector2 Size { get; private set; } = DefaultSize;

    public HealthContainer Health { get; private set; } = new();

    public bool IsAlive => _isAlive;

    public Rect2 WorldHitbox => new(GlobalPosition - (Size * 0.5f), Size);

    public override void _Ready() {
        EnsureNodes();
        UpdateLabel();
    }

    public override void _Draw() {
        var rect = new Rect2(Size * -0.5f, Size);
        DrawRect(rect, GetBodyColor(), true);
        DrawRect(rect, Colors.Black, false, 1.0f);

        var healthRatio = Health.MaxHealth <= 0 ? 0.0f : Health.CurrentHealth / (float)Health.MaxHealth;
        DrawRect(new Rect2(new Vector2(-8.0f, -13.0f), new Vector2(16.0f, 2.0f)), Colors.DarkRed, true);
        DrawRect(new Rect2(new Vector2(-8.0f, -13.0f), new Vector2(16.0f * healthRatio, 2.0f)), Colors.LimeGreen, true);
    }

    public void Initialize(int globalId, Vector2 worldPosition) {
        GlobalId = globalId;
        Position = worldPosition;
        Health = CreateDefaultHealth();
        EnsureNodes();
        SetAlive(true);
        UpdateLabel();
        QueueRedraw();
    }

    public bool ContainsWorldPosition(Vector2 worldPosition) {
        return !IsDead() && WorldHitbox.HasPoint(worldPosition);
    }

    public bool IsInsideWorldRadius(Vector2 worldCenter, float radius) {
        return !IsDead() && GlobalPosition.DistanceTo(worldCenter) <= radius;
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
        if (Health == null || !_isAlive)
            return false;

        Health.ApplyDamage(damageContainer);
        if (Health.IsDead())
            SetAlive(false);

        UpdateLabel();
        QueueRedraw();
        return true;
    }

    public void Respawn(Vector2 worldPosition) {
        Position = worldPosition;
        Health = CreateDefaultHealth();
        SetAlive(true);
        UpdateLabel();
        QueueRedraw();
    }

    public bool IsDead() {
        return !_isAlive || Health == null || Health.IsDead();
    }

    private static HealthContainer CreateDefaultHealth() {
        return new HealthContainer {
            MaxHealth = 100,
            CurrentHealth = 100,
            Armor = new ArmorResource(),
        };
    }

    private void EnsureNodes() {
        if (_label != null)
            return;

        _hitbox = new Area2D { Name = "Hitbox" };
        AddChild(_hitbox);

        _collisionShape = new CollisionShape2D { Name = "CollisionShape2D" };
        _collisionShape.Shape = new RectangleShape2D { Size = Size };
        _hitbox.AddChild(_collisionShape);

        _label = new Label {
            Name = "Label",
            Position = new Vector2(-18.0f, -30.0f),
            Size = new Vector2(64.0f, 16.0f),
        };
        _label.AddThemeFontSizeOverride("font_size", 8);
        AddChild(_label);
    }

    private void SetAlive(bool alive) {
        _isAlive = alive;

        if (_hitbox != null) {
            _hitbox.Monitoring = alive;
            _hitbox.Monitorable = alive;
        }

        if (_collisionShape != null)
            _collisionShape.Disabled = !alive;

        SetProcess(alive);
        SetPhysicsProcess(alive);
        SetProcessInput(alive);
    }

    private void UpdateLabel() {
        if (_label == null || Health == null)
            return;

        var stateText = _isAlive ? string.Empty : " DEAD";
        _label.Text = $"P{GlobalId} {Health.CurrentHealth}/{Health.MaxHealth}{stateText}";
    }

    private Color GetBodyColor() {
        if (IsDead())
            return new Color(0.25f, 0.25f, 0.25f);

        return new Color(0.2f, 0.45f, 1.0f);
    }
}
