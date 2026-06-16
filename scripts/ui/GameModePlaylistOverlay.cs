using System;
using Godot;

public partial class GameModePlaylistOverlay : PanelContainer {
    private const string ConfigGameIconPath = "res://assets/ui/config_game.svg";
    private const string DeathmatchIconPath = "res://assets/ui/game_mode_deathmatch.svg";
    private const string CaptureTheFlagIconPath = "res://assets/ui/game_mode_capture_the_flag.svg";
    private const string KingOfTheHillIconPath = "res://assets/ui/game_mode_king_of_the_hill.svg";
    private const string HeadquartersIconPath = "res://assets/ui/game_mode_headquarters.svg";
    private const string GameModeCatalogTileScenePath = "res://scenes/ui/overlays/game_mode_catalog_tile.tscn";

    private static readonly GameModeConfig.GameModeType[] AvailableGameModes = {
        GameModeConfig.GameModeType.Deathmatch,
        GameModeConfig.GameModeType.CaptureTheFlag,
        GameModeConfig.GameModeType.KingOfTheHill,
        GameModeConfig.GameModeType.Headquarters,
    };

    private Action<GameModeConfig.GameModeType> _onAdd;
    private Action<int> _onMoveUp;
    private Action<int> _onMoveDown;
    private Action<int> _onRemove;
    private Action _onClear;
    private PackedScene _catalogTileScene;
    private bool _hasGameModes;

    public override void _Ready() {
        _catalogTileScene = GD.Load<PackedScene>(GameModeCatalogTileScenePath);
        GetNode<Button>("Content/ListPanel/Footer/AddButton").Pressed += ShowCatalogPanel;
        GetNode<Button>("Content/ListPanel/Footer/ClearButton").Pressed += () => _onClear?.Invoke();
        GetNode<Button>("Content/ListPanel/Footer/CloseButton").Pressed += QueueFree;
        GetNode<Button>("Content/CatalogPanel/CatalogFooter/CatalogBackButton").Pressed += ShowListPanel;
        BuildCatalogGrid();
    }

    public void Configure(
        Action<GameModeConfig.GameModeType> onAdd,
        Action<int> onMoveUp,
        Action<int> onMoveDown,
        Action<int> onRemove,
        Action onClear) {
        _onAdd = onAdd;
        _onMoveUp = onMoveUp;
        _onMoveDown = onMoveDown;
        _onRemove = onRemove;
        _onClear = onClear;
    }

    public void RefreshList(SetupConfig setupConfig) {
        var list = GetNode<VBoxContainer>("Content/ListPanel/Scroll/List");
        _hasGameModes = setupConfig != null && setupConfig.GameModes.Count > 0;
        GetNode<Button>("Content/ListPanel/Footer/ClearButton").Disabled = !_hasGameModes;
        SetBackButtonState("Content/ListPanel/Footer/CloseButton", _hasGameModes);
        SetBackButtonState("Content/CatalogPanel/CatalogFooter/CatalogBackButton", _hasGameModes);

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

    private void BuildCatalogGrid() {
        if (_catalogTileScene == null) {
            GameLog.Error(GameLogScope.UI, "GameModeCatalogTileSceneLoadFailed", $"path={GameModeCatalogTileScenePath}");
            return;
        }

        var grid = GetNode<GridContainer>("Content/CatalogPanel/CatalogGrid");
        foreach (var child in grid.GetChildren()) {
            grid.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var gameMode in AvailableGameModes) {
            var tile = _catalogTileScene.Instantiate<GameModeCatalogTile>();
            tile.Configure(gameMode, GetGameModeDisplayName(gameMode), GetGameModeIconPath(gameMode));
            tile.ModeSelected += selectedMode => {
                _onAdd?.Invoke(selectedMode);
                ShowListPanel();
            };
            grid.AddChild(tile);
        }
    }

    private void ShowCatalogPanel() {
        GetNode<Control>("Content/ListPanel").Visible = false;
        GetNode<Control>("Content/CatalogPanel").Visible = true;
        SetBackButtonState("Content/CatalogPanel/CatalogFooter/CatalogBackButton", _hasGameModes);
        UiFocusHelper.FocusFirstAvailable(GetNode<Control>("Content/CatalogPanel"));
    }

    private void ShowListPanel() {
        GetNode<Control>("Content/CatalogPanel").Visible = false;
        GetNode<Control>("Content/ListPanel").Visible = true;
        GetNode<Button>("Content/ListPanel/Footer/AddButton").GrabFocus();
    }

    private void SetBackButtonState(string buttonPath, bool enabled) {
        var button = GetNode<Button>(buttonPath);
        button.Disabled = !enabled;
        button.Modulate = enabled ? Colors.White : new Color(0.45f, 0.45f, 0.45f);
    }

    private static string GetGameModeDisplayName(GameModeConfig gameMode) {
        if (gameMode == null)
            return "Unknown";

        return string.IsNullOrWhiteSpace(gameMode.DisplayName)
            ? GetGameModeDisplayName(gameMode.ModeType)
            : gameMode.DisplayName;
    }

    private static string GetGameModeDisplayName(GameModeConfig.GameModeType modeType) {
        return modeType switch {
            GameModeConfig.GameModeType.Deathmatch => "Deathmatch",
            GameModeConfig.GameModeType.CaptureTheFlag => "Capture the Flag",
            GameModeConfig.GameModeType.KingOfTheHill => "King of the Hill",
            GameModeConfig.GameModeType.Headquarters => "Headquarters",
            _ => modeType.ToString(),
        };
    }

    private static string GetGameModeIconPath(GameModeConfig.GameModeType modeType) {
        return modeType switch {
            GameModeConfig.GameModeType.Deathmatch => DeathmatchIconPath,
            GameModeConfig.GameModeType.CaptureTheFlag => CaptureTheFlagIconPath,
            GameModeConfig.GameModeType.KingOfTheHill => KingOfTheHillIconPath,
            GameModeConfig.GameModeType.Headquarters => HeadquartersIconPath,
            _ => ConfigGameIconPath,
        };
    }
}
