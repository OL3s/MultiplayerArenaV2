using Godot;

public static class UiFocusHelper {
    public static bool FocusFirstAvailable(Control root, params NodePath[] preferredPaths) {
        if (root == null || !root.IsInsideTree())
            return false;

        foreach (var preferredPath in preferredPaths) {
            var preferredControl = root.GetNodeOrNull<Control>(preferredPath);
            if (TryGrabFocus(preferredControl))
                return true;
        }

        return TryGrabFocus(FindFirstFocusableControl(root));
    }

    public static bool EnsureFocusWithin(Control root, params NodePath[] preferredPaths) {
        if (root == null || !root.IsInsideTree())
            return false;

        var focusOwner = root.GetViewport()?.GuiGetFocusOwner();
        if (focusOwner != null && (focusOwner == root || root.IsAncestorOf(focusOwner)) && IsFocusable(focusOwner))
            return true;

        return FocusFirstAvailable(root, preferredPaths);
    }

    private static bool TryGrabFocus(Control control) {
        if (!IsFocusable(control))
            return false;

        control.GrabFocus();
        return true;
    }

    private static Control FindFirstFocusableControl(Control root) {
        if (root == null || !root.IsInsideTree())
            return null;

        if (IsFocusable(root))
            return root;

        foreach (var child in root.GetChildren()) {
            if (child is not Control controlChild)
                continue;

            var focusableDescendant = FindFirstFocusableControl(controlChild);
            if (focusableDescendant != null)
                return focusableDescendant;
        }

        return null;
    }

    private static bool IsFocusable(Control control) {
        if (control == null || !control.IsInsideTree() || !control.IsVisibleInTree())
            return false;

        if (control is BaseButton button && button.Disabled)
            return false;

        return control.FocusMode != Control.FocusModeEnum.None;
    }
}
