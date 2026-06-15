using Godot;

[GlobalClass]
public partial class PlayerInventoryBag : PlayerItem {
    [Export]
    public Godot.Collections.Array<PlayerItemSlot> ProvidedSlots { get; set; } = new();

    [Export]
    public PlayerMagazineStorage MagazineCapacityBonus { get; set; } = new();
}
