using Godot;

public partial class NeutralObjective : Node2D {
    private static readonly Color NeutralColor = new(0.7f, 0.7f, 0.8f, 0.28f);
    private static readonly Color ContestedColor = new(1.0f, 0.82f, 0.2f, 0.36f);
    private static readonly Color TeamColor = new(0.25f, 0.85f, 1.0f, 0.36f);

    private Area2D _outerArea;
    private CollisionShape2D _outerCollisionShape;
    private Area2D _innerArea;
    private CollisionShape2D _innerCollisionShape;

    [Export]
    public float OuterRadius { get; set; } = 36.0f;

    [Export]
    public float InnerRadius { get; set; } = 12.0f;

    public int ControllingTeamId { get; private set; } = -1;

    public bool IsContested { get; private set; }

    public override void _Ready() {
        EnsureArea();
    }

    public override void _Draw() {
        var fillColor = GetFillColor();
        DrawCircle(Vector2.Zero, OuterRadius, fillColor);
        DrawArc(Vector2.Zero, OuterRadius, 0.0f, Mathf.Tau, 48, fillColor with { A = 0.55f }, 1.5f);
        DrawCircle(Vector2.Zero, InnerRadius, fillColor with { A = Mathf.Min(fillColor.A + 0.18f, 0.8f) });
        DrawArc(Vector2.Zero, InnerRadius, 0.0f, Mathf.Tau, 32, fillColor with { A = 0.9f }, 2.0f);
    }

    public bool ContainsOuterPosition(Vector2 worldPosition) {
        return GlobalPosition.DistanceTo(worldPosition) <= OuterRadius;
    }

    public bool ContainsInnerPosition(Vector2 worldPosition) {
        return GlobalPosition.DistanceTo(worldPosition) <= InnerRadius;
    }

    public void Configure(float outerRadius, float innerRadius) {
        OuterRadius = outerRadius;
        InnerRadius = innerRadius;
        EnsureArea();
        QueueRedraw();
    }

    public void SetState(int controllingTeamId, bool isContested) {
        if (ControllingTeamId == controllingTeamId && IsContested == isContested)
            return;

        ControllingTeamId = controllingTeamId;
        IsContested = isContested;
        QueueRedraw();
    }

    private void EnsureArea() {
        _outerArea ??= GetNode<Area2D>("OuterArea");
        _outerCollisionShape ??= _outerArea.GetNode<CollisionShape2D>("CollisionShape2D");
        if (_outerCollisionShape.Shape is CircleShape2D outerCircle)
            outerCircle.Radius = OuterRadius;

        _innerArea ??= GetNode<Area2D>("InnerArea");
        _innerCollisionShape ??= _innerArea.GetNode<CollisionShape2D>("CollisionShape2D");
        if (_innerCollisionShape.Shape is CircleShape2D innerCircle)
            innerCircle.Radius = InnerRadius;
    }

    private Color GetFillColor() {
        if (IsContested)
            return ContestedColor;

        if (ControllingTeamId >= 0)
            return TeamColor;

        return NeutralColor;
    }
}
