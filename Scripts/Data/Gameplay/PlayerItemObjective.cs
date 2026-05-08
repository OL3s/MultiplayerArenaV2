using Godot;

[GlobalClass]
public partial class PlayerItemObjective : Resource {
    public enum ObjectiveType {
        None,
        Damage,
        Heal,
        Buff,
        Explosion,
    }

    [Export]
    public ObjectiveType Type { get; set; } = ObjectiveType.None;

    [Export]
    public float Damage { get; set; }

    [Export]
    public float HealAmount { get; set; }

    [Export]
    public float Radius { get; set; }

    [Export]
    public float DurationSeconds { get; set; }

    [Export]
    public PackedScene EffectScene { get; set; }
}
