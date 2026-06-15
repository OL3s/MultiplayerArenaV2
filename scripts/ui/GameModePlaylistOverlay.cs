using System;
using Godot;

public partial class GameModePlaylistOverlay : PanelContainer {
    private Action<GameModeConfig.GameModeType> _onAdd;
    private Action<int> _onMoveUp;
    private Action<int> _onMoveDown;
    private Action<int> _onRemove;

    public override void _Ready() {
        GetNode<Button>("Content/Footer/AddDeathmatchButton").Pressed += () => _onAdd?.Invoke(GameModeConfig.GameModeType.Deathmatch);
        GetNode<Button>("Content/Footer/AddCaptureTheFlagButton").Pressed += () => _onAdd?.Invoke(GameModeConfig.GameModeType.CaptureTheFlag);
        GetNode<Button>("Content/Footer/CloseButton").Pressed += QueueFree;
    }

    public void Configure(
        Action<GameModeConfig.GameModeType> onAdd,
        Action<int> onMoveUp,
        Action<int> onMoveDown,
        Action<int> onRemove) {
        _onAdd = onAdd;
        _onMoveUp = onMoveUp;
        _onMoveDown = onMoveDown;
        _onRemove = onRemove;
    }

    public void RefreshList(SetupConfig setupConfig) {
        var list = GetNode<VBoxContainer>("Content/Scroll/List");
        foreach (var child in list.GetChildren()) {
            list.RemoveChild(child);
            child.QueueFree();
        }

        if (setupConfig == null || setupConfig.GameModes.Count == 0) {
            list.AddChild(new Label {
                Text = "No game modes in the list.",
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            return;
        }

        for (var i = 0; i < setupConfig.GameModes.Count; i++) {
            var index = i;
            var gameMode = setupConfig.GameModes[i];
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var label = new Label {
                Text = $"{i + 1}. {GetGameModeDisplayName(gameMode)}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var moveUpButton = new Button {
                Text = "Up",
                Disabled = i == 0,
                CustomMinimumSize = new Vector2(72, 36),
            };
            moveUpButton.Pressed += () => _onMoveUp?.Invoke(index);

            var moveDownButton = new Button {
                Text = "Down",
                Disabled = i == setupConfig.GameModes.Count - 1,
                CustomMinimumSize = new Vector2(72, 36),
            };
            moveDownButton.Pressed += () => _onMoveDown?.Invoke(index);

            var removeButton = new Button {
                Text = "Remove",
                CustomMinimumSize = new Vector2(90, 36),
            };
            removeButton.Pressed += () => _onRemove?.Invoke(index);

            row.AddChild(label);
            row.AddChild(moveUpButton);
            row.AddChild(moveDownButton);
            row.AddChild(removeButton);
            list.AddChild(row);
        }
    }

    private static string GetGameModeDisplayName(GameModeConfig gameMode) {
        if (gameMode == null)
            return "Unknown";

        return string.IsNullOrWhiteSpace(gameMode.DisplayName)
            ? gameMode.ModeType.ToString()
            : gameMode.DisplayName;
    }
}
