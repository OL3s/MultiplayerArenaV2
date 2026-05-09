using Godot;

[GlobalClass]
public partial class LevelPropData : Resource {
    [Export]
    public LevelPropType PropType { get; set; }

    [Export]
    public string DisplayName { get; set; } = string.Empty;

    [Export]
    public string TexturePath { get; set; } = string.Empty;

    [Export]
    public Vector2 Size { get; set; } = new(32.0f, 32.0f);

    [Export]
    public HealthContainer Health { get; set; } = new();

    public void Configure(LevelPropType propType) {
        PropType = propType;

        switch (propType) {
            case LevelPropType.Barrel:
                DisplayName = "Barrel";
                TexturePath = "res://Assets/Props/barrel.svg";
                Size = new Vector2(16.0f, 16.0f);
                Health = CreateHealth(150, CreateArmor(0.1f, 0.35f, 0.0f, 0.0f, 0.25f));
                break;

            case LevelPropType.Rock:
                DisplayName = "Rock";
                TexturePath = "res://Assets/Props/rock.svg";
                Size = new Vector2(16.0f, 16.0f);
                Health = CreateHealth(300, CreateArmor(0.0f, 1.0f, 1.0f, 0.15f, 1.0f));
                break;

            case LevelPropType.Tree:
                DisplayName = "Tree";
                TexturePath = "res://Assets/Props/tree.svg";
                Size = new Vector2(16.0f, 32.0f);
                Health = CreateHealth(220, CreateArmor(0.25f, 0.2f, 0.0f, 0.35f, 0.0f));
                break;
        }
    }

    private static HealthContainer CreateHealth(int maxHealth, ArmorResource armor) {
        return new HealthContainer {
            MaxHealth = maxHealth,
            CurrentHealth = maxHealth,
            Armor = armor,
        };
    }

    private static ArmorResource CreateArmor(float crushReduction, float slashReduction, float heatReduction, float explosiveReduction, float fireReduction) {
        var armor = new ArmorResource();
        armor.DamageReductionPercentages[DamageType.Crush] = crushReduction;
        armor.DamageReductionPercentages[DamageType.Slash] = slashReduction;
        armor.DamageReductionPercentages[DamageType.Heat] = heatReduction;
        armor.DamageReductionPercentages[DamageType.Explosive] = explosiveReduction;
        armor.StatusEffectReductionPercentages[StatusEffectType.Fire] = fireReduction;
        return armor;
    }
}
