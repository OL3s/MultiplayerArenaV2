using Godot;

public partial class GameplaySpawnMarker : Node2D {
    [Export]
    public float Radius { get; set; } = 7.0f;

    [Export]
    public Color MarkerColor { get; set; } = new(0.35f, 1.0f, 0.65f, 0.45f);

    public override void _Draw() {
        DrawCircle(Vector2.Zero, Radius, MarkerColor);
        var outlineColor = MarkerColor;
        outlineColor.A = 0.9f;
        DrawArc(Vector2.Zero, Radius, 0.0f, Mathf.Tau, 24, outlineColor, 1.5f);
    }
}
