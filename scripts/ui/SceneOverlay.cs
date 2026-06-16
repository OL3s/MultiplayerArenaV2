using Godot;
using System.Collections.Generic;

public partial class SceneOverlay : CanvasLayer {
    private const string BlurBackdropName = "BlurBackdrop";
    private const string OverlayScenePath = "res://scenes/ui/overlays/scene_overlay.tscn";
    private const string BlurShaderPath = "res://assets/shaders/OverlayBlurBackdrop.gdshader";

    private static readonly PackedScene OverlayScene = ResourceLoader.Load<PackedScene>(OverlayScenePath);
    private static readonly Shader BlurShader = ResourceLoader.Load<Shader>(BlurShaderPath);

    private ColorRect _blurBackdrop;
    private readonly Dictionary<ulong, Control> _overlayFocusRestoreTargets = new();
    private Control _pendingRestoreFocus;

    public override void _Ready() {
        EnsureBlurBackdrop();
    }

    public override void _Process(double delta) {
        if (!IsInsideTree())
            return;

        EnsureTopOverlayOwnsFocus();
    }

    public static SceneOverlay Get(Node context) {
        var owner = GetOverlayOwner(context);
        return owner?.GetNodeOrNull<SceneOverlay>(nameof(SceneOverlay));
    }

    public static SceneOverlay GetOrCreate(Node context) {
        var owner = GetOverlayOwner(context);
        if (owner == null)
            return null;

        var existing = owner.GetNodeOrNull<SceneOverlay>(nameof(SceneOverlay));
        if (existing != null)
            return existing;

        if (OverlayScene == null) {
            GD.PushError($"Failed to load overlay scene at '{OverlayScenePath}'.");
            return null;
        }

        var overlay = OverlayScene.Instantiate<SceneOverlay>();
        owner.AddChild(overlay);
        return overlay;
    }

    public void AddOverlay(Control overlay) {
        if (overlay == null) {
            GD.PushWarning("Overlay control is null, cannot add.");
            return;
        }

        EnsureBlurBackdrop();

        var overlayId = overlay.GetInstanceId();
        _overlayFocusRestoreTargets[overlayId] = GetViewport()?.GuiGetFocusOwner();

        AddChild(overlay);
        RefreshBlurBackdrop();
        overlay.TreeExited += () => OnOverlayTreeExited(overlayId);
        CallDeferred(MethodName.FocusTopOverlay);
    }

    public void AddOverlay(PackedScene overlayScene) {
        if (overlayScene == null) {
            GD.PushWarning("Overlay scene is null, cannot add.");
            return;
        }

        if (overlayScene.Instantiate() is not Control overlay) {
            GD.PushWarning("Overlay scene root must be a Control.");
            return;
        }

        AddOverlay(overlay);
    }

    public void CloseTopOverlay() {
        for (var i = GetChildCount() - 1; i >= 0; i--) {
            var child = GetChild(i);
            if (child == _blurBackdrop)
                continue;

            child.QueueFree();
            break;
        }

        RefreshBlurBackdrop();
    }

    public void CloseAllOverlays() {
        foreach (var child in GetChildren()) {
            if (child == _blurBackdrop)
                continue;

            child.QueueFree();
        }

        RefreshBlurBackdrop();
    }

    private void EnsureBlurBackdrop() {
        if (GodotObject.IsInstanceValid(_blurBackdrop))
            return;

        _blurBackdrop = new ColorRect {
            Name = BlurBackdropName,
            MouseFilter = Control.MouseFilterEnum.Stop,
            Color = Colors.White,
            Visible = false,
        };
        _blurBackdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        if (BlurShader != null) {
            _blurBackdrop.Material = new ShaderMaterial { Shader = BlurShader };
        }

        AddChild(_blurBackdrop);
        MoveChild(_blurBackdrop, 0);
    }

    private void FocusTopOverlay() {
        if (!IsInsideTree())
            return;

        var topOverlay = GetTopOverlay();
        if (topOverlay == null)
            return;

        var focusTarget = FindFirstFocusableControl(topOverlay);
        focusTarget?.GrabFocus();
    }

    private void OnOverlayTreeExited(ulong overlayId) {
        if (_overlayFocusRestoreTargets.TryGetValue(overlayId, out var previousFocus)) {
            _pendingRestoreFocus = previousFocus;
            _overlayFocusRestoreTargets.Remove(overlayId);
        }

        CallDeferred(MethodName.RefreshFocusAfterOverlay);
        CallDeferred(MethodName.RefreshBlurBackdrop);
    }

    private void RefreshFocusAfterOverlay() {
        if (!IsInsideTree())
            return;

        var topOverlay = GetTopOverlay();
        if (topOverlay != null) {
            var overlayFocusTarget = FindFirstFocusableControl(topOverlay);
            overlayFocusTarget?.GrabFocus();
            return;
        }

        if (GodotObject.IsInstanceValid(_pendingRestoreFocus) && _pendingRestoreFocus.IsInsideTree())
            _pendingRestoreFocus.GrabFocus();

        _pendingRestoreFocus = null;
    }

    private void EnsureTopOverlayOwnsFocus() {
        if (!IsInsideTree())
            return;

        var topOverlay = GetTopOverlay();
        if (topOverlay == null)
            return;

        var focusOwner = GetViewport()?.GuiGetFocusOwner();
        if (focusOwner != null && OwnsFocus(topOverlay, focusOwner))
            return;

        var focusTarget = FindFirstFocusableControl(topOverlay);
        focusTarget?.GrabFocus();
    }

    private Control GetTopOverlay() {
        for (var i = GetChildCount() - 1; i >= 0; i--) {
            if (GetChild(i) is Control overlay && overlay != _blurBackdrop)
                return overlay;
        }

        return null;
    }

    private static bool OwnsFocus(Control overlay, Control focusOwner) {
        return focusOwner == overlay || overlay.IsAncestorOf(focusOwner);
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
        if (control == null || !control.IsInsideTree())
            return false;

        if (control is BaseButton button && button.Disabled)
            return false;

        return control.FocusMode != Control.FocusModeEnum.None
            && control.IsVisibleInTree();
    }

    private void RefreshBlurBackdrop() {
        if (!GodotObject.IsInstanceValid(_blurBackdrop))
            return;

        var hasOverlay = false;

        for (var i = 0; i < GetChildCount(); i++) {
            var child = GetChild(i);
            if (child == _blurBackdrop || child.IsQueuedForDeletion())
                continue;

            hasOverlay = true;
            break;
        }

        _blurBackdrop.Visible = hasOverlay;
        MoveChild(_blurBackdrop, 0);
    }

    private static Node GetOverlayOwner(Node context) {
        if (context == null)
            return null;

        var sceneTree = context.GetTree();
        return sceneTree?.CurrentScene ?? sceneTree?.Root;
    }
}
