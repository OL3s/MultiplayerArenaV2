using Godot;

[GlobalClass]
public partial class PlayerArmor : PlayerItem {
    [Export]
    public float ArmorValue { get; set; }

    [Export]
    public Godot.Collections.Array<PlayerItemSlot> ProvidedSlots { get; set; } = new();

    [Export]
    public Godot.Collections.Array<PlayerItemSlotType> AllowedInventorySlotTypes { get; set; } = new();

    [Export]
    public PlayerMagazineStorage MagazineCapacityBonus { get; set; } = new();
}
