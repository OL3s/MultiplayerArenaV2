using Godot;

public partial class SceneOverlay : CanvasLayer
{
    private const string BlurBackdropName = "BlurBackdrop";
    private const string OverlayScenePath = "res://Scenes/UI/SceneOverlay.tscn";
    private const string BlurShaderPath = "res://Assets/Shaders/OverlayBlurBackdrop.gdshader";

    private static readonly PackedScene OverlayScene = ResourceLoader.Load<PackedScene>(OverlayScenePath);
    private static readonly Shader BlurShader = ResourceLoader.Load<Shader>(BlurShaderPath);

    private ColorRect _blurBackdrop;

    public override void _Ready()
    {
        EnsureBlurBackdrop();
    }

    public static SceneOverlay Get(Node context)
    {
        var owner = GetOverlayOwner(context);
        return owner?.GetNodeOrNull<SceneOverlay>(nameof(SceneOverlay));
    }

    public static SceneOverlay GetOrCreate(Node context)
    {
        var owner = GetOverlayOwner(context);
        if (owner == null)
        {
            return null;
        }

        var existing = owner.GetNodeOrNull<SceneOverlay>(nameof(SceneOverlay));
        if (existing != null)
        {
            return existing;
        }

        if (OverlayScene == null)
        {
            GD.PushError($"Failed to load overlay scene at '{OverlayScenePath}'.");
            return null;
        }

        var overlay = OverlayScene.Instantiate<SceneOverlay>();
        owner.AddChild(overlay);
        return overlay;
    }

    public void AddOverlay(Control overlay, bool useBlur = false)
    {
        if (overlay == null)
        {
            GD.PushWarning("Overlay control is null, cannot add.");
            return;
        }

        EnsureBlurBackdrop();

        if (useBlur)
        {
            overlay.SetMeta("uses_blur_backdrop", true);
            ShowBlurBackdrop();
            overlay.TreeExited += HideBlurBackdropIfUnused;
        }

        AddChild(overlay);
        MoveOverlayToFront(overlay);
    }

    public void AddOverlay(PackedScene overlayScene, bool useBlur = false)
    {
        if (overlayScene == null)
        {
            GD.PushWarning("Overlay scene is null, cannot add.");
            return;
        }

        if (overlayScene.Instantiate() is not Control overlay)
        {
            GD.PushWarning("Overlay scene root must be a Control.");
            return;
        }

        AddOverlay(overlay, useBlur);
    }

    public void CloseTopOverlay()
    {
        for (var i = GetChildCount() - 1; i >= 0; i--)
        {
            var child = GetChild(i);
            if (child == _blurBackdrop)
            {
                continue;
            }

            child.QueueFree();
            break;
        }

        HideBlurBackdropIfUnused();
    }

    public void CloseAllOverlays()
    {
        foreach (var child in GetChildren())
        {
            if (child == _blurBackdrop)
            {
                continue;
            }

            child.QueueFree();
        }

        HideBlurBackdropIfUnused();
    }

    private void EnsureBlurBackdrop()
    {
        if (GodotObject.IsInstanceValid(_blurBackdrop))
        {
            return;
        }

        _blurBackdrop = new ColorRect
        {
            Name = BlurBackdropName,
            MouseFilter = Control.MouseFilterEnum.Stop,
            Color = Colors.White,
            Visible = false,
        };
        _blurBackdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        if (BlurShader != null)
        {
            _blurBackdrop.Material = new ShaderMaterial { Shader = BlurShader };
        }

        AddChild(_blurBackdrop);
        MoveChild(_blurBackdrop, 0);
    }

    private void MoveOverlayToFront(Control overlay)
    {
        MoveChild(overlay, GetChildCount() - 1);
    }

    private void ShowBlurBackdrop()
    {
        if (!GodotObject.IsInstanceValid(_blurBackdrop))
        {
            return;
        }

        _blurBackdrop.Visible = true;
        MoveChild(_blurBackdrop, GetChildCount() - 1);
    }

    private void HideBlurBackdropIfUnused()
    {
        if (!GodotObject.IsInstanceValid(_blurBackdrop))
        {
            return;
        }

        for (var i = 0; i < GetChildCount(); i++)
        {
            var child = GetChild(i);
            if (child != _blurBackdrop && child.HasMeta("uses_blur_backdrop"))
            {
                return;
            }
        }

        _blurBackdrop.Visible = false;
        MoveChild(_blurBackdrop, 0);
    }

    private static Node GetOverlayOwner(Node context)
    {
        if (context == null)
        {
            return null;
        }

        var sceneTree = context.GetTree();
        return sceneTree?.CurrentScene ?? sceneTree?.Root;
    }
}
