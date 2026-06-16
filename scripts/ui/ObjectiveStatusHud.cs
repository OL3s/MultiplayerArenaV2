using Godot;

public partial class ObjectiveStatusHud : PanelContainer {
    private Label _titleLabel;
    private Label _stateLabel;

    public override void _Ready() {
        EnsureNodes();
    }

    public void SetObjectiveState(string title, string stateText, Color stateColor) {
        EnsureNodes();
        _titleLabel.Text = title;
        _stateLabel.Text = stateText;
        AddThemeStyleboxOverride("panel", CreatePanelStyle(stateColor));
    }

    private void EnsureNodes() {
        if (_titleLabel != null)
            return;

        _titleLabel = GetNode<Label>("Margin/Content/TitleLabel");
        _stateLabel = GetNode<Label>("Margin/Content/StateLabel");
    }

    private static StyleBoxFlat CreatePanelStyle(Color stateColor) {
        return new StyleBoxFlat {
            BgColor = new Color(stateColor.R * 0.20f, stateColor.G * 0.20f, stateColor.B * 0.20f, 0.88f),
            BorderColor = new Color(stateColor.R, stateColor.G, stateColor.B, 0.95f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 18,
            CornerRadiusTopRight = 18,
            CornerRadiusBottomLeft = 18,
            CornerRadiusBottomRight = 18,
        };
    }
}
