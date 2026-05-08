using Godot;

[GlobalClass]
public partial class PlayerMagazineStorage : Resource
{
    [Export]
    public int Small { get; set; }

    [Export]
    public int Medium { get; set; }

    [Export]
    public int Large { get; set; }

    [Export]
    public int Special { get; set; }
}
