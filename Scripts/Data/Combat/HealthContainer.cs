using Godot;

[GlobalClass]
public partial class HealthContainer : Resource {
    [Export]
    public int MaxHealth { get; set; } = 100;

    [Export]
    public int CurrentHealth { get; set; } = 100;

    [Export]
    public ArmorResource Armor { get; set; } = new();

    [Export]
    public Godot.Collections.Dictionary<StatusEffectType, float> ActiveStatusEffects { get; set; } = new();

    public float ApplyDamage(DamageContainer damageContainer) {
        if (damageContainer?.Damage == null || CurrentHealth <= 0)
            return 0.0f;

        var totalDamage = 0.0f;

        foreach (var damageValue in damageContainer.Damage.DamageValues)
            totalDamage += Mathf.Max(0.0f, damageValue.Value) * Armor.GetDamageMultiplier(damageValue.Key);

        foreach (var statusEffectValue in damageContainer.Damage.StatusEffectValues) {
            var appliedValue = Mathf.Max(0.0f, statusEffectValue.Value) * Armor.GetStatusEffectMultiplier(statusEffectValue.Key);
            if (appliedValue <= 0.0f)
                continue;

            ActiveStatusEffects[statusEffectValue.Key] = GetActiveStatusEffectValue(statusEffectValue.Key) + appliedValue;
        }

        CurrentHealth = Mathf.Clamp(CurrentHealth - Mathf.RoundToInt(totalDamage), 0, MaxHealth);
        return totalDamage;
    }

    public float TakeDamage(DamageContainer damageContainer) {
        return ApplyDamage(damageContainer);
    }

    public int Heal(int healAmount) {
        if (healAmount <= 0 || CurrentHealth <= 0)
            return 0;

        var previousHealth = CurrentHealth;
        CurrentHealth = Mathf.Clamp(CurrentHealth + healAmount, 0, MaxHealth);
        return CurrentHealth - previousHealth;
    }

    public int ApplyStatusEffectTick() {
        var totalDamage = 0;
        var updatedStatusEffects = new Godot.Collections.Dictionary<StatusEffectType, float>();
        var expiredStatusEffects = new Godot.Collections.Array<StatusEffectType>();

        foreach (var activeStatusEffect in ActiveStatusEffects) {
            var tickDamage = GetStatusEffectTickDamage(activeStatusEffect.Key, activeStatusEffect.Value);
            if (tickDamage > 0) {
                CurrentHealth = Mathf.Clamp(CurrentHealth - tickDamage, 0, MaxHealth);
                totalDamage += tickDamage;
            }

            var remainingValue = activeStatusEffect.Value - 1.0f;
            if (remainingValue <= 0.0f)
                expiredStatusEffects.Add(activeStatusEffect.Key);
            else
                updatedStatusEffects[activeStatusEffect.Key] = remainingValue;
        }

        foreach (var updatedStatusEffect in updatedStatusEffects)
            ActiveStatusEffects[updatedStatusEffect.Key] = updatedStatusEffect.Value;

        foreach (var statusEffectType in expiredStatusEffects)
            ActiveStatusEffects.Remove(statusEffectType);

        return totalDamage;
    }

    public bool IsDead() {
        return CurrentHealth <= 0;
    }

    private float GetActiveStatusEffectValue(StatusEffectType statusEffectType) {
        return ActiveStatusEffects.TryGetValue(statusEffectType, out var statusEffectValue) ? statusEffectValue : 0.0f;
    }

    private int GetStatusEffectTickDamage(StatusEffectType statusEffectType, float statusEffectValue) {
        return statusEffectType switch {
            StatusEffectType.Fire => Mathf.FloorToInt(statusEffectValue),
            _ => 0,
        };
    }
}
