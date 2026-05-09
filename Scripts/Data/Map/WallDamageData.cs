using Godot;

[GlobalClass]
public partial class WallDamageData : Resource {
    public const int DefaultWallHealth = 500;

    [Export]
    public HealthContainer Health { get; set; } = new() {
        MaxHealth = DefaultWallHealth,
        CurrentHealth = DefaultWallHealth,
    };

    public WallDamageData() {
        ConfigureForBiome(BiomeConfig.BiomeType.Arena);
    }

    public int Damage {
        get {
            EnsureHealth();
            return Mathf.Max(0, MaxDamage - Health.CurrentHealth);
        }
        set {
            EnsureHealth();
            Health.CurrentHealth = Mathf.Clamp(MaxDamage - value, 0, MaxDamage);
        }
    }

    public int MaxDamage {
        get {
            EnsureHealth();
            return Health.MaxHealth;
        }
        set {
            EnsureHealth();
            var clampedValue = Mathf.Max(1, value);
            Health.MaxHealth = clampedValue;
            Health.CurrentHealth = Mathf.Clamp(Health.CurrentHealth, 0, clampedValue);
        }
    }

    [Export]
    public int DamageStage { get; set; }

    public float ApplyDamage(DamageContainer damageContainer) {
        EnsureHealth();
        return Health.ApplyDamage(damageContainer);
    }

    public bool IsDestroyed() {
        EnsureHealth();
        return Health.IsDead();
    }

    public void ConfigureForBiome(BiomeConfig.BiomeType biomeType, int maxHealth = DefaultWallHealth) {
        EnsureHealth();

        Health.MaxHealth = Mathf.Max(1, maxHealth);
        Health.CurrentHealth = Health.MaxHealth;
        Health.Armor = GetArmorForBiome(biomeType);
    }

    private ArmorResource GetArmorForBiome(BiomeConfig.BiomeType biomeType) {
        return biomeType switch {
            _ => CreateDefaultWallArmor(),
        };
    }

    private ArmorResource CreateDefaultWallArmor() {
        var armor = new ArmorResource();
        armor.DamageReductionPercentages[DamageType.Heat] = 1.0f;
        armor.DamageReductionPercentages[DamageType.Slash] = 0.95f;
        armor.DamageReductionPercentages[DamageType.Crush] = 0.0f;
        armor.DamageReductionPercentages[DamageType.Explosive] = 0.0f;
        armor.StatusEffectReductionPercentages[StatusEffectType.Fire] = 1.0f;
        return armor;
    }

    private void EnsureHealth() {
        Health ??= new HealthContainer {
            MaxHealth = DefaultWallHealth,
            CurrentHealth = DefaultWallHealth,
        };
    }
}
