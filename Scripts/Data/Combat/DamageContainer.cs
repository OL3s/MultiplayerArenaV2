using Godot;

[GlobalClass]
public partial class DamageContainer : Resource {
    [Export]
    public DamageResource Damage { get; set; } = new();

    public float ApplyDamage(HealthContainer healthContainer) {
        return healthContainer?.ApplyDamage(this) ?? 0.0f;
    }

    public static DamageContainer FromDamage(DamageType damageType, float damageValue) {
        var damageContainer = new DamageContainer();
        damageContainer.Damage.AddDamageValue(damageType, damageValue);
        return damageContainer;
    }
}
