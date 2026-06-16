using Godot;

public partial class BuyRadialSegment : PanelContainer {
    private TextureRect _icon;
    private Label _label;

    public override void _Ready() {
        EnsureNodes();
    }

    public void SetEntry(string label, Texture2D icon, bool selected, bool enabled) {
        EnsureNodes();
        _label.Text = label;
        _icon.Texture = icon;
        _icon.Visible = icon != null;
        Modulate = enabled ? Colors.White : new Color(1.0f, 1.0f, 1.0f, 0.45f);
        AddThemeStyleboxOverride("panel", CreateStyle(selected, enabled));
    }

    private void EnsureNodes() {
        if (_label != null)
            return;

        _icon = GetNode<TextureRect>("Content/Icon");
        _label = GetNode<Label>("Content/Label");
    }

    private static StyleBoxFlat CreateStyle(bool selected, bool enabled) {
        var borderColor = selected
            ? new Color(1.0f, 0.92f, 0.45f, 1.0f)
            : new Color(0.42f, 0.48f, 0.58f, enabled ? 0.95f : 0.45f);
        return new StyleBoxFlat {
            BgColor = selected ? new Color(0.20f, 0.16f, 0.06f, 0.92f) : new Color(0.05f, 0.06f, 0.08f, 0.88f),
            BorderColor = borderColor,
            BorderWidthLeft = selected ? 3 : 1,
            BorderWidthTop = selected ? 3 : 1,
            BorderWidthRight = selected ? 3 : 1,
            BorderWidthBottom = selected ? 3 : 1,
            CornerRadiusTopLeft = 18,
            CornerRadiusTopRight = 18,
            CornerRadiusBottomLeft = 18,
            CornerRadiusBottomRight = 18,
        };
    }
}
