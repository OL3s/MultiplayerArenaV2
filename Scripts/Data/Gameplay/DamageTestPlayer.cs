using Godot;

public partial class DamageTestPlayer : CharacterBody2D {
    private static readonly Vector2 DefaultSize = new(12.0f, 12.0f);
    private const string FrontTexturePath = "res://Assets/Players/damage_test_player_front.svg";
    private const string BackTexturePath = "res://Assets/Players/damage_test_player_back.svg";
    private const float BackFacingYThreshold = -0.5f;

    private Area2D _hitbox;
    private CollisionShape2D _collisionShape;
    private Sprite2D _bodySprite;
    private Label _label;
    private Sprite2D _weapon;
    private bool _isAlive = true;
    private bool _hasLocalAimDirection;
    private bool _hasEstimatedAimDirection = true;
    private Vector2 _localAimDirection = Vector2.Right;
    private Vector2 _estimatedAimDirection = Vector2.Right;
    private float _bodyFacingScaleX = 1.0f;
    private bool _drawBackBody;
    private Texture2D _heldTexture;

    public int GlobalId { get; private set; } = -1;

    public Vector2 Size { get; private set; } = DefaultSize;

    public float CollisionRadius => Mathf.Min(Size.X, Size.Y) * 0.45f;

    public HealthContainer Health { get; private set; } = new();

    public bool IsAlive => _isAlive;

    public Vector2 DisplayAimDirection => _hasLocalAimDirection ? _localAimDirection : _estimatedAimDirection;

    public Rect2 WorldHitbox => new(GlobalPosition - (Size * 0.5f), Size);

    public override void _Ready() {
        EnsureNodes();
        UpdateLabel();
    }

    public override void _Draw() {
        var healthRatio = Health.MaxHealth <= 0 ? 0.0f : Health.CurrentHealth / (float)Health.MaxHealth;
        DrawRect(new Rect2(new Vector2(-6.0f, -11.0f), new Vector2(12.0f, 2.0f)), Colors.DarkRed, true);
        DrawRect(new Rect2(new Vector2(-6.0f, -11.0f), new Vector2(12.0f * healthRatio, 2.0f)), Colors.LimeGreen, true);
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
        return !IsDead() && GlobalPosition.DistanceTo(worldPosition) <= CollisionRadius;
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

    public bool Move(Vector2 movementDelta) {
        if (IsDead() || movementDelta == Vector2.Zero)
            return false;

        GlobalPosition += movementDelta;
        return true;
    }

    public bool MoveWithVelocity(Vector2 velocity) {
        if (IsDead()) {
            Velocity = Vector2.Zero;
            return false;
        }

        var previousPosition = GlobalPosition;
        Velocity = velocity;
        MoveAndSlide();
        return previousPosition != GlobalPosition;
    }

    public void SetSyncedPosition(Vector2 worldPosition) {
        Velocity = Vector2.Zero;
        GlobalPosition = worldPosition;
    }

    public void SetAimDirection(Vector2 aimDirection, bool hasAim) {
        SetEstimatedAimDirection(aimDirection, hasAim);
    }

    public void SetLocalAimDirection(Vector2 aimDirection, bool hasAim) {
        if (TryNormalizeAimDirection(aimDirection, out var normalizedAimDirection))
            _localAimDirection = normalizedAimDirection;

        _hasLocalAimDirection = hasAim;
        EnsureNodes();
        UpdateWeapon();
    }

    public void SetEstimatedAimDirection(Vector2 aimDirection, bool hasAim) {
        if (TryNormalizeAimDirection(aimDirection, out var normalizedAimDirection))
            _estimatedAimDirection = normalizedAimDirection;

        _hasEstimatedAimDirection = true;
        EnsureNodes();
        UpdateWeapon();
    }

    public void SetHeldTexture(Texture2D heldTexture) {
        _heldTexture = heldTexture;
        EnsureNodes();
        if (_weapon != null)
            _weapon.Texture = _heldTexture;
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
        _collisionShape.Shape = new CircleShape2D { Radius = CollisionRadius };
        AddChild(_collisionShape);

        _bodySprite = new Sprite2D {
            Name = "BodySprite",
            Texture = GD.Load<Texture2D>(FrontTexturePath),
        };
        AddChild(_bodySprite);
        UpdateBodySprite();

        _label = new Label {
            Name = "Label",
            Position = new Vector2(-18.0f, -30.0f),
            Size = new Vector2(64.0f, 16.0f),
        };
        _label.AddThemeFontSizeOverride("font_size", 8);
        AddChild(_label);

        _weapon = new Sprite2D {
            Name = "Weapon",
            Texture = _heldTexture,
            ZIndex = 2,
        };
        AddChild(_weapon);
        UpdateWeapon();
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

        if (_weapon != null)
            _weapon.Visible = alive;

        if (_bodySprite != null) {
            _bodySprite.Visible = true;
            _bodySprite.Modulate = alive ? Colors.White : new Color(0.45f, 0.45f, 0.45f);
        }
    }

    private void UpdateWeapon() {
        if (_weapon == null)
            return;

        var aimDirection = _hasLocalAimDirection ? _localAimDirection : _estimatedAimDirection;
        if (!TryNormalizeAimDirection(aimDirection, out aimDirection))
            aimDirection = Vector2.Right;

        _weapon.Visible = _isAlive;
        _weapon.Position = aimDirection * 9.0f;
        _weapon.Rotation = aimDirection.Angle();
        _weapon.Scale = new Vector2(1.0f, _bodyFacingScaleX);
        UpdateBodyFacing(aimDirection);
    }

    private void UpdateBodyFacing(Vector2 aimDirection) {
        var oldFacingScaleX = _bodyFacingScaleX;
        var oldDrawBackBody = _drawBackBody;

        if (aimDirection.X > 0.01f)
            _bodyFacingScaleX = 1.0f;
        else if (aimDirection.X < -0.01f) {
            _bodyFacingScaleX = -1.0f;
        }

        _drawBackBody = aimDirection.Y < BackFacingYThreshold;

        if (oldFacingScaleX != _bodyFacingScaleX || oldDrawBackBody != _drawBackBody)
            UpdateBodySprite();
    }

    private void UpdateBodySprite() {
        if (_bodySprite == null)
            return;

        _bodySprite.Texture = GD.Load<Texture2D>(_drawBackBody ? BackTexturePath : FrontTexturePath);
        _bodySprite.Scale = new Vector2(_bodyFacingScaleX, 1.0f);
    }

    private static bool TryNormalizeAimDirection(Vector2 aimDirection, out Vector2 normalizedAimDirection) {
        if (aimDirection.LengthSquared() <= 0.0001f) {
            normalizedAimDirection = Vector2.Zero;
            return false;
        }

        normalizedAimDirection = aimDirection.Normalized();
        return true;
    }

    private void UpdateLabel() {
        if (_label == null || Health == null)
            return;

        var stateText = _isAlive ? string.Empty : " DEAD";
        _label.Text = $"P{GlobalId} {Health.CurrentHealth}/{Health.MaxHealth}{stateText}";
    }

}
