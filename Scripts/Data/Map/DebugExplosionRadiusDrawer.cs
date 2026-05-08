using System;
using Godot;

public partial class DebugExplosionRadiusDrawer : Node2D
{
    public float Radius { get; set; }

    public Color DrawColor { get; set; } = Colors.Orange;

    public Func<bool> CanDraw { get; set; } = () => true;

    public override void _Draw()
    {
        if (!CanDraw())
        {
            return;
        }

        var localMousePosition = GetLocalMousePosition();
        DrawArc(localMousePosition, Radius, 0.0f, Mathf.Tau, 48, DrawColor, 2.0f);
        DrawCircle(localMousePosition, 3.0f, DrawColor);
    }
}
