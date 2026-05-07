using Godot;

public static class UiInputActions
{
    public static void EnsureConfigured()
    {
        EnsureAction("ui_accept", new InputEventJoypadButton { ButtonIndex = JoyButton.A });
        EnsureAction("ui_cancel", new InputEventJoypadButton { ButtonIndex = JoyButton.B });
        EnsureAction("ui_left", new InputEventJoypadButton { ButtonIndex = JoyButton.DpadLeft });
        EnsureAction("ui_right", new InputEventJoypadButton { ButtonIndex = JoyButton.DpadRight });
        EnsureAction("ui_up", new InputEventJoypadButton { ButtonIndex = JoyButton.DpadUp });
        EnsureAction("ui_down", new InputEventJoypadButton { ButtonIndex = JoyButton.DpadDown });

        EnsureAction("ui_left", new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = -1.0f });
        EnsureAction("ui_right", new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = 1.0f });
        EnsureAction("ui_up", new InputEventJoypadMotion { Axis = JoyAxis.LeftY, AxisValue = -1.0f });
        EnsureAction("ui_down", new InputEventJoypadMotion { Axis = JoyAxis.LeftY, AxisValue = 1.0f });
    }

    private static void EnsureAction(string actionName, InputEvent inputEvent)
    {
        if (!InputMap.HasAction(actionName))
        {
            InputMap.AddAction(actionName);
        }

        foreach (var existingEvent in InputMap.ActionGetEvents(actionName))
        {
            if (existingEvent.IsMatch(inputEvent, true))
            {
                return;
            }
        }

        InputMap.ActionAddEvent(actionName, inputEvent);
    }
}
