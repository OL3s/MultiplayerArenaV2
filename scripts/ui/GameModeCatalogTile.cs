using System;
using Godot;

public partial class GameModeCatalogTile : Button {
    private GameModeConfig.GameModeType _modeType;

    public event Action<GameModeConfig.GameModeType> ModeSelected;

    public override void _Ready() {
        Pressed += OnPressed;
    }

    public void Configure(GameModeConfig.GameModeType modeType, string displayName, string iconPath) {
        _modeType = modeType;
        GetNode<Label>("Content/NameLabel").Text = displayName;
        GetNode<TextureRect>("Content/Icon").Texture = UiResourceLoader.LoadIconTexture(iconPath);
    }

    private void OnPressed() {
        ModeSelected?.Invoke(_modeType);
    }
}
