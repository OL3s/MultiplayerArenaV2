using System;
using Godot;

public partial class ConfigSelectionOverlay : PanelContainer {
    private CheckBox[] _optionCheckBoxes = Array.Empty<CheckBox>();
    private Action<int, bool> _onToggled;

    public void Configure(string title, string[] optionLabels, Func<int, bool> isSelected, Action<int, bool> onToggled) {
        Configure(title, optionLabels, Array.Empty<string>(), isSelected, onToggled);
    }

    public void Configure(string title, string[] optionLabels, string[] optionIconPaths, Func<int, bool> isSelected, Action<int, bool> onToggled) {
        GetNode<Label>("Content/TitleLabel").Text = title;
        _onToggled = onToggled;

        var options = GetNode<GridContainer>("Content/Options");
        foreach (var child in options.GetChildren()) {
            options.RemoveChild(child);
            child.QueueFree();
        }

        _optionCheckBoxes = new CheckBox[optionLabels.Length];
        for (var i = 0; i < optionLabels.Length; i++) {
            var optionIndex = i;
            var tile = new PanelContainer {
                CustomMinimumSize = new Vector2(140.0f, 122.0f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            tile.AddThemeStyleboxOverride("panel", CreateOptionTileStyle());

            var tileRoot = new Control {
                CustomMinimumSize = new Vector2(124.0f, 106.0f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };

            var content = new VBoxContainer {
                Alignment = BoxContainer.AlignmentMode.Center,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };
            content.SetAnchorsPreset(LayoutPreset.FullRect);
            content.OffsetLeft = 6.0f;
            content.OffsetTop = 8.0f;
            content.OffsetRight = -6.0f;
            content.OffsetBottom = -8.0f;
            content.AddThemeConstantOverride("separation", 6);

            var icon = new TextureRect {
                CustomMinimumSize = new Vector2(46.0f, 46.0f),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Texture = GetOptionIcon(optionIconPaths, optionIndex),
            };

            var checkBox = new CheckBox {
                Text = string.Empty,
                ButtonPressed = isSelected(optionIndex),
                CustomMinimumSize = new Vector2(28.0f, 28.0f),
            };
            checkBox.Position = new Vector2(0.0f, 0.0f);
            checkBox.Toggled += enabled => {
                _onToggled?.Invoke(optionIndex, enabled);
                RefreshCloseButtonState();
            };

            var nameLabel = new Label {
                Text = optionLabels[i],
                HorizontalAlignment = HorizontalAlignment.Center,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };

            content.AddChild(icon);
            content.AddChild(nameLabel);
            tileRoot.AddChild(content);
            tileRoot.AddChild(checkBox);
            tile.AddChild(tileRoot);
            ConnectTileToggle(tile, checkBox);
            ConnectTileToggle(tileRoot, checkBox);
            ConnectTileToggle(content, checkBox);
            ConnectTileToggle(icon, checkBox);
            ConnectTileToggle(nameLabel, checkBox);
            options.AddChild(tile);
            _optionCheckBoxes[i] = checkBox;
        }

        GetNode<Button>("Content/Actions/AllButton").Pressed += SelectAll;
        GetNode<Button>("Content/Actions/NoneButton").Pressed += SelectNone;
        GetNode<Button>("Content/Actions/CloseButton").Pressed += QueueFree;
        RefreshCloseButtonState();
    }

    private static Texture2D GetOptionIcon(string[] optionIconPaths, int optionIndex) {
        if (optionIconPaths == null || optionIndex < 0 || optionIndex >= optionIconPaths.Length || string.IsNullOrWhiteSpace(optionIconPaths[optionIndex]))
            return null;

        return GD.Load<Texture2D>(optionIconPaths[optionIndex]);
    }

    private static StyleBoxFlat CreateOptionTileStyle() {
        return new StyleBoxFlat {
            BgColor = new Color(0.08f, 0.09f, 0.11f, 0.55f),
            BorderColor = new Color(0.24f, 0.27f, 0.32f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusBottomLeft = 8,
            ContentMarginLeft = 8,
            ContentMarginTop = 8,
            ContentMarginRight = 8,
            ContentMarginBottom = 8,
        };
    }

    private static void ConnectTileToggle(Control control, CheckBox checkBox) {
        control.MouseFilter = Control.MouseFilterEnum.Stop;
        control.GuiInput += inputEvent => {
            if (inputEvent is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                return;

            checkBox.ButtonPressed = !checkBox.ButtonPressed;
            control.AcceptEvent();
        };
    }

    private void SelectAll() {
        SetAllOptions(true);
    }

    private void SelectNone() {
        SetAllOptions(false);
    }

    private void SetAllOptions(bool selected) {
        for (var i = 0; i < _optionCheckBoxes.Length; i++) {
            if (_optionCheckBoxes[i] == null || _optionCheckBoxes[i].ButtonPressed == selected)
                continue;

            _optionCheckBoxes[i].ButtonPressed = selected;
        }

        RefreshCloseButtonState();
    }

    private void RefreshCloseButtonState() {
        var hasSelection = false;
        foreach (var checkBox in _optionCheckBoxes) {
            if (checkBox != null && checkBox.ButtonPressed) {
                hasSelection = true;
                break;
            }
        }

        var closeButton = GetNode<Button>("Content/Actions/CloseButton");
        closeButton.Disabled = !hasSelection;
        closeButton.Modulate = hasSelection ? Colors.White : new Color(0.45f, 0.45f, 0.45f);
    }
}
