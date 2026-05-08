using System;
using Godot;

public partial class ConfirmationOverlay : Control {
    private Action _onConfirmed;
    private Action _onCancelled;

    public override void _Ready() {
        UiInputActions.EnsureConfigured();
        GetNode<Button>("CenterContainer/PopupPanel/MarginContainer/Content/Actions/ConfirmButton").Pressed += OnConfirmPressed;
        GetNode<Button>("CenterContainer/PopupPanel/MarginContainer/Content/Actions/CancelButton").Pressed += OnCancelPressed;
        CallDeferred(MethodName.FocusDefaultButton);
    }

    public override void _UnhandledInput(InputEvent inputEvent) {
        if (!inputEvent.IsActionPressed("ui_cancel"))
            return;

        GetViewport()?.SetInputAsHandled();
        OnCancelPressed();
    }

    public void Configure(string title, string message, string confirmText, string cancelText, Action onConfirmed, Action onCancelled = null) {
        _onConfirmed = onConfirmed;
        _onCancelled = onCancelled;

        GetNode<Label>("CenterContainer/PopupPanel/MarginContainer/Content/TitleLabel").Text = title;
        GetNode<Label>("CenterContainer/PopupPanel/MarginContainer/Content/MessageLabel").Text = message;
        GetNode<Button>("CenterContainer/PopupPanel/MarginContainer/Content/Actions/ConfirmButton").Text = confirmText;
        GetNode<Button>("CenterContainer/PopupPanel/MarginContainer/Content/Actions/CancelButton").Text = cancelText;
    }

    private void FocusDefaultButton() {
        GetNode<Button>("CenterContainer/PopupPanel/MarginContainer/Content/Actions/CancelButton").GrabFocus();
    }

    private void OnConfirmPressed() {
        _onConfirmed?.Invoke();
        QueueFree();
    }

    private void OnCancelPressed() {
        _onCancelled?.Invoke();
        QueueFree();
    }
}
