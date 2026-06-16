using Godot;

public partial class PlayerHudCard : PanelContainer {
    public enum HudOverlayState {
        None,
        Dead,
        Spawning,
        ReloadAvailable,
        ReloadRecovering,
        Reloading,
        GadgetReloadRecovering,
        BuyAvailable,
    }

    private const string StandardCaliberIconPath = "res://assets/ui/ammo_caliber_standard.svg";
    private const string HeavyCaliberIconPath = "res://assets/ui/ammo_caliber_heavy.svg";
    private const string ShellCaliberIconPath = "res://assets/ui/ammo_caliber_shell.svg";
    private Label _nameLabel;
    private Label _statusLabel;
    private Label _healthLabel;
    private TextureRect _itemIcon;
    private HBoxContainer _ammoPips;
    private Label _gadgetLabel;
    private PanelContainer _overlayPanel;
    private TextureRect _overlayIcon;
    private Label _overlayLabel;
    private ProgressBar _overlayProgress;

    public override void _Ready() {
        EnsureNodes();
    }

    public void SetPlayerState(
        int localId,
        string displayName,
        string statusText,
        int currentHealth,
        int maxHealth,
        Texture2D itemIcon,
        int currentAmmo,
        int maxAmmo,
        PlayerItem.AmmoCaliberType ammoCaliber,
        string gadgetText,
        Color teamColor,
        HudOverlayState overlayState = HudOverlayState.None,
        string overlayText = "",
        Texture2D overlayIcon = null,
        float overlayProgress = 0.0f) {
        EnsureNodes();

        _nameLabel.Text = $"L{localId + 1} {displayName}";
        _statusLabel.Text = string.IsNullOrWhiteSpace(statusText) ? "--" : statusText;
        _healthLabel.Text = $"HP {Mathf.Max(currentHealth, 0)}/{Mathf.Max(maxHealth, 0)}";
        _itemIcon.Texture = itemIcon;
        _itemIcon.Visible = itemIcon != null;
        SetAmmoPips(currentAmmo, maxAmmo, ammoCaliber);
        _gadgetLabel.Text = gadgetText;
        ApplyPanelStyle(teamColor);
        ApplyStatusStyle(statusText);
        SetOverlayState(overlayState, overlayText, overlayIcon, overlayProgress);
    }

    private void EnsureNodes() {
        if (_nameLabel != null)
            return;

        _nameLabel = GetNode<Label>("Margin/Row/TextColumn/NameLabel");
        _statusLabel = GetNode<Label>("Margin/Row/TextColumn/StatusRow/StatusLabel");
        _healthLabel = GetNode<Label>("Margin/Row/TextColumn/StatusRow/HealthLabel");
        _gadgetLabel = GetNode<Label>("Margin/Row/TextColumn/GadgetLabel");
        _itemIcon = GetNode<TextureRect>("Margin/Row/ItemColumn/ItemIcon");
        _ammoPips = GetNode<HBoxContainer>("Margin/Row/ItemColumn/AmmoPips");
        _overlayPanel = GetNode<PanelContainer>("OverlayPanel");
        _overlayIcon = GetNode<TextureRect>("OverlayPanel/Center/Content/PromptRow/PromptIcon");
        _overlayLabel = GetNode<Label>("OverlayPanel/Center/Content/PromptRow/PromptLabel");
        _overlayProgress = GetNode<ProgressBar>("OverlayPanel/Center/Content/OverlayProgress");
    }

    private void SetOverlayState(HudOverlayState overlayState, string overlayText, Texture2D overlayIcon, float overlayProgress) {
        _overlayPanel.Visible = overlayState != HudOverlayState.None;
        if (!_overlayPanel.Visible)
            return;

        _overlayLabel.Text = string.IsNullOrWhiteSpace(overlayText) ? GetDefaultOverlayText(overlayState) : overlayText;
        _overlayIcon.Texture = overlayIcon;
        _overlayIcon.Visible = overlayIcon != null;
        _overlayProgress.Visible = overlayState is HudOverlayState.Reloading or HudOverlayState.ReloadRecovering or HudOverlayState.GadgetReloadRecovering;
        _overlayProgress.Value = Mathf.Clamp(overlayProgress, 0.0f, 1.0f) * 100.0f;
        ApplyOverlayStyle(overlayState);
    }

    private static string GetDefaultOverlayText(HudOverlayState overlayState) {
        return overlayState switch {
            HudOverlayState.Dead => "DEAD",
            HudOverlayState.Spawning => "SPAWNING",
            HudOverlayState.ReloadAvailable => "RELOAD",
            HudOverlayState.ReloadRecovering => "COOLDOWN",
            HudOverlayState.Reloading => "RELOADING",
            HudOverlayState.GadgetReloadRecovering => "COOLDOWN",
            HudOverlayState.BuyAvailable => "BUY",
            _ => string.Empty,
        };
    }

    private void ApplyOverlayStyle(HudOverlayState overlayState) {
        var tint = overlayState switch {
            HudOverlayState.Dead => new Color(0.34f, 0.04f, 0.05f, 0.86f),
            HudOverlayState.Spawning => new Color(0.08f, 0.22f, 0.45f, 0.82f),
            HudOverlayState.ReloadAvailable => new Color(0.50f, 0.26f, 0.05f, 0.82f),
            HudOverlayState.ReloadRecovering => new Color(0.30f, 0.20f, 0.08f, 0.84f),
            HudOverlayState.Reloading => new Color(0.12f, 0.16f, 0.22f, 0.86f),
            HudOverlayState.GadgetReloadRecovering => new Color(0.10f, 0.18f, 0.20f, 0.86f),
            HudOverlayState.BuyAvailable => new Color(0.06f, 0.28f, 0.12f, 0.82f),
            _ => new Color(0.02f, 0.02f, 0.025f, 0.78f),
        };

        _overlayPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = tint,
            CornerRadiusTopLeft = 18,
            CornerRadiusTopRight = 18,
            CornerRadiusBottomLeft = 18,
            CornerRadiusBottomRight = 18,
        });
    }

    private void SetAmmoPips(int currentAmmo, int maxAmmo, PlayerItem.AmmoCaliberType ammoCaliber) {
        ClearChildren(_ammoPips);
        maxAmmo = Mathf.Clamp(maxAmmo, 0, 12);
        currentAmmo = Mathf.Clamp(currentAmmo, 0, maxAmmo);
        if (maxAmmo <= 0) {
            _ammoPips.AddChild(new Label { Text = "--" });
            return;
        }

        var texture = GD.Load<Texture2D>(GetAmmoCaliberIconPath(ammoCaliber));
        for (var i = 0; i < maxAmmo; i++) {
            var pip = new TextureRect {
                Texture = texture,
                CustomMinimumSize = new Vector2(5.0f, 14.0f),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Modulate = i < currentAmmo ? Colors.White : new Color(0.02f, 0.02f, 0.025f, 0.8f),
            };
            _ammoPips.AddChild(pip);
        }
    }

    private static string GetAmmoCaliberIconPath(PlayerItem.AmmoCaliberType ammoCaliber) {
        return ammoCaliber switch {
            PlayerItem.AmmoCaliberType.Heavy => HeavyCaliberIconPath,
            PlayerItem.AmmoCaliberType.Shell => ShellCaliberIconPath,
            _ => StandardCaliberIconPath,
        };
    }

    private static void ClearChildren(Node node) {
        foreach (var child in node.GetChildren()) {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void ApplyPanelStyle(Color teamColor) {
        var style = new StyleBoxFlat {
            BgColor = new Color(0.05f, 0.055f, 0.07f, 0.82f),
            BorderColor = new Color(teamColor.R, teamColor.G, teamColor.B, 0.92f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 18,
            CornerRadiusTopRight = 18,
            CornerRadiusBottomLeft = 18,
            CornerRadiusBottomRight = 18,
        };
        AddThemeStyleboxOverride("panel", style);
    }

    private void ApplyStatusStyle(string statusText) {
        var color = statusText switch {
            "DEAD" => new Color(1.0f, 0.32f, 0.32f, 1.0f),
            "SPAWN" => new Color(0.50f, 0.75f, 1.0f, 1.0f),
            "RELOAD" => new Color(1.0f, 0.78f, 0.28f, 1.0f),
            _ => new Color(0.62f, 1.0f, 0.58f, 1.0f),
        };
        _statusLabel.AddThemeColorOverride("font_color", color);
        _statusLabel.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.0f, 0.0f, 0.85f));
        _statusLabel.AddThemeConstantOverride("outline_size", 2);
    }
}
