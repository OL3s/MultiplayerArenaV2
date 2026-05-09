using Godot;

public partial class PlayerAimIndicator : Node2D {
    private Vector2 _start;
    private Vector2 _end;
    private float _spreadRadius;
    private bool _visible;

    public void SetAim(Vector2 start, Vector2 end, float spreadRadius, bool visible) {
        _start = start;
        _end = end;
        _spreadRadius = Mathf.Max(spreadRadius, 1.0f);
        _visible = visible;
        QueueRedraw();
    }

    public override void _Draw() {
        if (!_visible)
            return;

        var lineColor = new Color(0.45f, 0.82f, 1.0f, 0.32f);
        var dotColor = new Color(0.78f, 0.93f, 1.0f, 0.55f);
        var circleColor = new Color(0.45f, 0.82f, 1.0f, 0.5f);
        DrawLine(_start, _end, lineColor, 1.0f);
        DrawDottedLine(dotColor);
        DrawArc(_end, _spreadRadius, 0.0f, Mathf.Tau, 48, circleColor, 1.0f);
        DrawLine(_end + new Vector2(-3.0f, 0.0f), _end + new Vector2(3.0f, 0.0f), circleColor, 1.0f);
        DrawLine(_end + new Vector2(0.0f, -3.0f), _end + new Vector2(0.0f, 3.0f), circleColor, 1.0f);
    }

    private void DrawDottedLine(Color color) {
        var delta = _end - _start;
        var distance = delta.Length();
        if (distance <= 0.0f)
            return;

        var direction = delta / distance;
        const float step = 8.0f;
        const float dotRadius = 1.0f;
        for (var currentDistance = 0.0f; currentDistance <= distance; currentDistance += step)
            DrawCircle(_start + (direction * currentDistance), dotRadius, color);
    }
}
