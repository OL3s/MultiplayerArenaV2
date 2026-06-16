using Godot;

public partial class MatchConfigEntry : PanelContainer {
    private TextureRect _icon;
    private Label _categoryLabel;
    private Label _valueLabel;
    private string _iconPath = string.Empty;

    public override void _Ready() {
        EnsureNodes();
    }

    public void SetEntry(string category, string value, string iconPath) {
        EnsureNodes();

        _categoryLabel.Text = string.IsNullOrWhiteSpace(category) ? "Config" : category.ToUpperInvariant();
        _valueLabel.Text = string.IsNullOrWhiteSpace(value) ? "--" : value;
        iconPath = string.IsNullOrWhiteSpace(iconPath) ? string.Empty : iconPath;
        if (_iconPath != iconPath) {
            _iconPath = iconPath;
            _icon.Texture = string.IsNullOrWhiteSpace(_iconPath) ? null : GD.Load<Texture2D>(_iconPath);
        }

        _icon.Visible = _icon.Texture != null;
    }

    private void EnsureNodes() {
        if (_icon != null)
            return;

        _icon = GetNode<TextureRect>("Margin/Stack/Icon");
        _categoryLabel = GetNode<Label>("Margin/Stack/CategoryLabel");
        _valueLabel = GetNode<Label>("Margin/Stack/ValueLabel");
    }
}
