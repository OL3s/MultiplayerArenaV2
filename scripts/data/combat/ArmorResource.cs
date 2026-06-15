using Godot;

[GlobalClass]
public partial class ArmorResource : Resource {
    [Export]
    public Godot.Collections.Dictionary<DamageType, float> DamageReductionPercentages { get; set; } = new();

    [Export]
    public Godot.Collections.Dictionary<StatusEffectType, float> StatusEffectReductionPercentages { get; set; } = new();

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float FlatDamageReductionPercentage { get; set; }

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float FlatStatusEffectReductionPercentage { get; set; }

    public float GetDamageMultiplier(DamageType damageType) {
        var reductionPercentage = DamageReductionPercentages.TryGetValue(damageType, out var typedReduction)
            ? typedReduction
            : FlatDamageReductionPercentage;

        return 1.0f - Mathf.Clamp(reductionPercentage, 0.0f, 1.0f);
    }

    public float GetStatusEffectMultiplier(StatusEffectType statusEffectType) {
        var reductionPercentage = StatusEffectReductionPercentages.TryGetValue(statusEffectType, out var typedReduction)
            ? typedReduction
            : FlatStatusEffectReductionPercentage;

        return 1.0f - Mathf.Clamp(reductionPercentage, 0.0f, 1.0f);
    }
}
