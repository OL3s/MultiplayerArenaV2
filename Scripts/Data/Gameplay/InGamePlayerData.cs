using Godot;

[GlobalClass]
public partial class InGamePlayerData : Resource {
    [Export]
    public PlayerArmor Armor { get; set; }

    [Export]
    public Godot.Collections.Array<PlayerInventoryBag> Inventories { get; set; } = new();

    [Export]
    public PlayerEquipable BackStrapItem { get; set; }

    [Export]
    public Godot.Collections.Array<PlayerEquipable> Items { get; set; } = new();

    [Export]
    public PlayerMagazineStorage MagazineCapacity { get; set; } = new();

    [Export]
    public PlayerMagazineStorage StoredMagazines { get; set; } = new();
}
