using Godot;

[GlobalClass]
public abstract partial class PlayerItem : Resource {
    [Export]
    public string ItemId { get; set; } = string.Empty;

    [Export]
    public string DisplayName { get; set; } = "Item";

    [Export]
    public PlayerItemTheme Theme { get; set; } = PlayerItemTheme.Any;

    [Export]
    public int Cost { get; set; }

    [Export]
    public float Weight { get; set; }

    [Export]
    public Texture2D HeldTexture { get; set; }

    [Export]
    public Godot.Collections.Array<PlayerItemSlotType> ContainerTypes { get; set; } = new();

    public bool FitsContainerType(PlayerItemSlotType slotType) {
        return slotType == PlayerItemSlotType.Generic || ContainerTypes.Contains(slotType);
    }
}
