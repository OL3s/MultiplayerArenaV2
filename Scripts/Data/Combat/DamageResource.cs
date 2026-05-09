using Godot;

[GlobalClass]
public partial class DamageResource : Resource {
    [Export]
    public Godot.Collections.Dictionary<DamageType, float> DamageValues { get; set; } = new();

    [Export]
    public Godot.Collections.Dictionary<StatusEffectType, float> StatusEffectValues { get; set; } = new();

    public float GetDamageValue(DamageType damageType) {
        return DamageValues.TryGetValue(damageType, out var damageValue) ? damageValue : 0.0f;
    }

    public float GetStatusEffectValue(StatusEffectType statusEffectType) {
        return StatusEffectValues.TryGetValue(statusEffectType, out var statusEffectValue) ? statusEffectValue : 0.0f;
    }

    public void AddDamageValue(DamageType damageType, float damageValue) {
        if (damageValue == 0.0f)
            return;

        DamageValues[damageType] = GetDamageValue(damageType) + damageValue;
    }

    public void AddStatusEffectValue(StatusEffectType statusEffectType, float statusEffectValue) {
        if (statusEffectValue == 0.0f)
            return;

        StatusEffectValues[statusEffectType] = GetStatusEffectValue(statusEffectType) + statusEffectValue;
    }

    public DamageResource Scaled(float multiplier) {
        var scaledDamage = new DamageResource();

        foreach (var damageValue in DamageValues)
            scaledDamage.DamageValues[damageValue.Key] = damageValue.Value * multiplier;

        foreach (var statusEffectValue in StatusEffectValues)
            scaledDamage.StatusEffectValues[statusEffectValue.Key] = statusEffectValue.Value * multiplier;

        return scaledDamage;
    }
}
