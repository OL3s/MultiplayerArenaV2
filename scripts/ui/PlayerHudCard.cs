using Godot;

public partial class PlayerHudCard : PanelContainer {
    private const string StandardCaliberIconPath = "res://assets/ui/ammo_caliber_standard.svg";
    private const string HeavyCaliberIconPath = "res://assets/ui/ammo_caliber_heavy.svg";
    private const string ShellCaliberIconPath = "res://assets/ui/ammo_caliber_shell.svg";
    private Label _nameLabel;
    private Label _statusLabel;
    private Label _healthLabel;
    private TextureRect _itemIcon;
    private HBoxContainer _ammoPips;
    private Label _gadgetLabel;

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
        Color teamColor) {
        EnsureNodes();

        _nameLabel.Text = $"L{localId + 1} {displayName}";
        _statusLabel.Text = statusText;
        _healthLabel.Text = $"HP {Mathf.Max(currentHealth, 0)}/{Mathf.Max(maxHealth, 0)}";
        _itemIcon.Texture = itemIcon;
        _itemIcon.Visible = itemIcon != null;
        SetAmmoPips(currentAmmo, maxAmmo, ammoCaliber);
        _gadgetLabel.Text = gadgetText;
        ApplyPanelStyle(teamColor);
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
}
