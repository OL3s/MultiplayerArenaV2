using Godot;

[GlobalClass]
public partial class WallDamageData : Resource
{
    [Export]
    public int Damage { get; set; }

    [Export]
    public int MaxDamage { get; set; } = 3;

    [Export]
    public int DamageStage { get; set; }
}
