using System;
using Godot;

public partial class HostServerActionsOverlay : Control {
    public event Action NextGameModeRequested;
    public event Action RestartMatchRequested;
    public event Action BackToMainMenuRequested;

    public override void _Ready() {
        UiInputActions.EnsureConfigured();
        GetNode<Button>("Panel/Margin/Layout/Actions/NextGameModeButton").Pressed += OnNextGameModePressed;
        GetNode<Button>("Panel/Margin/Layout/Actions/RestartMatchButton").Pressed += OnRestartMatchPressed;
        GetNode<Button>("Panel/Margin/Layout/Actions/BackToMainMenuButton").Pressed += OnBackToMainMenuPressed;
        GetNode<Button>("Panel/Margin/Layout/Actions/CloseButton").Pressed += QueueFree;
        CallDeferred(MethodName.FocusDefaultButton);
    }

    public override void _UnhandledInput(InputEvent inputEvent) {
        if (!inputEvent.IsActionPressed("ui_cancel"))
            return;

        GetViewport()?.SetInputAsHandled();
        QueueFree();
    }

    private void FocusDefaultButton() {
        GetNode<Button>("Panel/Margin/Layout/Actions/NextGameModeButton").GrabFocus();
    }

    private void OnNextGameModePressed() {
        NextGameModeRequested?.Invoke();
        QueueFree();
    }

    private void OnRestartMatchPressed() {
        RestartMatchRequested?.Invoke();
        QueueFree();
    }

    private void OnBackToMainMenuPressed() {
        BackToMainMenuRequested?.Invoke();
        QueueFree();
    }
}
