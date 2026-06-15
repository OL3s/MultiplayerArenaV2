using Godot;

[GlobalClass]
public partial class InGamePlayerData : Resource {
    [Export]
    public HealthContainer Health { get; set; } = new();

    [Export]
    public PlayerArmor Armor { get; set; }

    [Export]
    public Godot.Collections.Array<PlayerWeapon> Weapons { get; set; } = new();

    [Export]
    public Godot.Collections.Array<PlayerGadget> Gadgets { get; set; } = new();
}
