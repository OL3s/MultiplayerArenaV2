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

    [Export]
    public DamageResource DamageResource { get; set; }

    public void Execute(PlayerItemRuntimeContext context, Vector2 position, ProjectileSweepHit hit, DamageResource fallbackDamage) {
        if (context == null)
            return;

        var damage = DamageResource ?? fallbackDamage ?? CreateDamageResourceFromValue();
        switch (Type) {
            case ObjectiveType.Damage:
                if (hit != null && hit.HasHit)
                    context.ApplyDamageToHit(hit, PlayerItemRuntimeContext.CreateDamageContainer(damage));
                break;
            case ObjectiveType.Explosion:
                context.ApplyRadiusDamage(position, Radius, damage);
                break;
        }

        context.SpawnEffect(EffectScene, position);
    }

    private DamageResource CreateDamageResourceFromValue() {
        if (Damage <= 0.0f)
            return null;

        var damageResource = new DamageResource();
        damageResource.AddDamageValue(Type == ObjectiveType.Explosion ? DamageType.Explosive : DamageType.Crush, Damage);
        return damageResource;
    }
}
