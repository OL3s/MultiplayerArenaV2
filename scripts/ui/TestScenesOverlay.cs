using Godot;
using System;
using System.Collections.Generic;

public partial class TestScenesOverlay : Control {
    private const string TestScenesDirectoryPath = "res://scenes/tests";

    public override void _Ready() {
        GetNode<Button>("CenterContainer/PopupPanel/MarginContainer/Content/Header/CloseButton").Pressed += OnClosePressed;
        PopulateTestScenes();
    }

    public override void _UnhandledInput(InputEvent inputEvent) {
        if (!inputEvent.IsActionPressed("ui_cancel"))
            return;

        GetViewport()?.SetInputAsHandled();
        OnClosePressed();
    }

    private void PopulateTestScenes() {
        var scenePaths = GetTestScenePaths();
        var list = GetNode<VBoxContainer>("CenterContainer/PopupPanel/MarginContainer/Content/ScenesScroll/Scenes");
        ClearChildren(list);

        foreach (var scenePath in scenePaths)
            list.AddChild(CreateSceneButton(scenePath));

        var hasScenes = scenePaths.Count > 0;
        GetNode<ScrollContainer>("CenterContainer/PopupPanel/MarginContainer/Content/ScenesScroll").Visible = hasScenes;
        GetNode<Label>("CenterContainer/PopupPanel/MarginContainer/Content/EmptyLabel").Visible = !hasScenes;
    }

    private Button CreateSceneButton(string scenePath) {
        var button = new Button {
            Text = scenePath.GetFile(),
            CustomMinimumSize = new Vector2(0, 54),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = scenePath,
        };
        button.Pressed += () => OpenTestScene(scenePath);
        return button;
    }

    private void OpenTestScene(string scenePath) {
        QueueFree();
        GetTree().ChangeSceneToFile(scenePath);
    }

    private static List<string> GetTestScenePaths() {
        var scenePaths = new List<string>();
        AddTestScenePaths(TestScenesDirectoryPath, scenePaths);
        scenePaths.Sort();
        return scenePaths;
    }

    private static void AddTestScenePaths(string directoryPath, List<string> scenePaths) {
        using var directory = DirAccess.Open(directoryPath);
        if (directory == null) {
            GD.PushWarning($"Could not open test scenes directory '{directoryPath}'.");
            return;
        }

        foreach (var fileName in directory.GetFiles()) {
            if (!fileName.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase))
                continue;

            scenePaths.Add($"{directoryPath}/{fileName}");
        }

        foreach (var subdirectoryName in directory.GetDirectories()) {
            if (subdirectoryName.StartsWith('.'))
                continue;

            AddTestScenePaths($"{directoryPath}/{subdirectoryName}", scenePaths);
        }
    }

    private static void ClearChildren(Node node) {
        foreach (var child in node.GetChildren()) {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void OnClosePressed() {
        QueueFree();
    }
}
