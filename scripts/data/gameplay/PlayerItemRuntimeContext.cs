using System;
using System.Collections.Generic;
using Godot;

public sealed class PlayerItemRuntimeContext {
    private const float WallSweepStep = 4.0f;

    public int OwnerGlobalId { get; set; } = -1;

    public Node2D World { get; set; }

    public ArenaMapData ArenaMapData { get; set; }

    public Vector2I TileSize { get; set; } = new(16, 16);

    public IReadOnlyDictionary<int, DamageTestPlayer> PlayersByGlobalId { get; set; }

    public IReadOnlyList<LevelProp> Props { get; set; }

    public Action ArenaChanged { get; set; }

    public Action<int, int> PlayerKilled { get; set; }

    public ProjectileSweepHit FindFirstHit(
        Vector2 from,
        Vector2 to,
        float radius,
        ISet<ulong> ignoredObjectIds,
        ISet<Vector2I> ignoredWallTiles) {
        var bestHit = new ProjectileSweepHit();
        var segment = to - from;
        var length = segment.Length();
        if (length <= 0.001f)
            return bestHit;

        CheckWallHit(from, segment, length, ignoredWallTiles, bestHit);
        CheckPropHits(from, segment, length, radius, ignoredObjectIds, bestHit);
        CheckPlayerHits(from, segment, length, radius, ignoredObjectIds, bestHit);
        return bestHit;
    }

    public void ExecuteObjective(PlayerItemObjective objective, Vector2 position, ProjectileSweepHit hit, DamageResource fallbackDamage) {
        if (objective != null) {
            objective.Execute(this, position, hit, fallbackDamage);
            return;
        }

        if (hit != null && hit.HasHit)
            ApplyDamageToHit(hit, CreateDamageContainer(fallbackDamage));
    }

    public void ApplyDamageToHit(ProjectileSweepHit hit, DamageContainer damageContainer) {
        if (hit == null || !hit.HasHit || damageContainer?.Damage == null)
            return;

        switch (hit.Kind) {
            case ProjectileHitKind.Player:
                if (hit.Target is DamageTestPlayer player && GodotObject.IsInstanceValid(player))
                    ApplyDamageToPlayer(player, damageContainer);
                break;
            case ProjectileHitKind.Prop:
                if (hit.Target is LevelProp prop && GodotObject.IsInstanceValid(prop))
                    prop.ApplyDamage(damageContainer);
                break;
            case ProjectileHitKind.Wall:
                if (ArenaMapData != null && ArenaMapData.DamageWallTile(hit.WallTile, damageContainer))
                    ArenaChanged?.Invoke();
                break;
        }
    }

    public void ApplyRadiusDamage(Vector2 center, float radius, DamageResource damage) {
        if (radius <= 0.0f || damage == null)
            return;

        ApplyRadiusDamageToPlayers(center, radius, damage);
        ApplyRadiusDamageToProps(center, radius, damage);
        ApplyRadiusDamageToWalls(center, radius, damage);
    }

    public void SpawnEffect(PackedScene effectScene, Vector2 position) {
        if (effectScene == null || World == null)
            return;

        var effect = effectScene.Instantiate<Node2D>();
        if (effect == null)
            return;

        World.AddChild(effect);
        effect.GlobalPosition = position;
    }

    public static DamageContainer CreateDamageContainer(DamageResource damage) {
        return new DamageContainer { Damage = damage ?? new DamageResource() };
    }

    private void CheckWallHit(Vector2 from, Vector2 segment, float length, ISet<Vector2I> ignoredWallTiles, ProjectileSweepHit bestHit) {
        if (ArenaMapData == null)
            return;

        for (var distance = 0.0f; distance <= length; distance += WallSweepStep) {
            if (distance >= bestHit.Distance)
                return;

            var samplePosition = from + (segment * (distance / length));
            var tile = ArenaMapData.WorldToTile(samplePosition, TileSize);
            if (ignoredWallTiles != null && ignoredWallTiles.Contains(tile))
                continue;

            if (!ArenaMapData.IsWallTile(tile))
                continue;

            bestHit.Kind = ProjectileHitKind.Wall;
            bestHit.WallTile = tile;
            bestHit.Position = samplePosition;
            bestHit.Distance = distance;
            return;
        }
    }

    private void CheckPropHits(Vector2 from, Vector2 segment, float length, float radius, ISet<ulong> ignoredObjectIds, ProjectileSweepHit bestHit) {
        if (Props == null)
            return;

        foreach (var prop in Props) {
            if (prop == null || !GodotObject.IsInstanceValid(prop) || prop.IsDestroyed())
                continue;

            if (ignoredObjectIds != null && ignoredObjectIds.Contains(prop.GetInstanceId()))
                continue;

            CheckCircularTarget(from, segment, length, radius + prop.CollisionRadius, prop.GlobalPosition, prop, ProjectileHitKind.Prop, bestHit);
        }
    }

    private void CheckPlayerHits(Vector2 from, Vector2 segment, float length, float radius, ISet<ulong> ignoredObjectIds, ProjectileSweepHit bestHit) {
        if (PlayersByGlobalId == null)
            return;

        foreach (var playerEntry in PlayersByGlobalId) {
            if (playerEntry.Key == OwnerGlobalId)
                continue;

            var player = playerEntry.Value;
            if (player == null || !GodotObject.IsInstanceValid(player) || player.IsDead())
                continue;

            if (ignoredObjectIds != null && ignoredObjectIds.Contains(player.GetInstanceId()))
                continue;

            CheckCircularTarget(from, segment, length, radius + player.CollisionRadius, player.GlobalPosition, player, ProjectileHitKind.Player, bestHit);
        }
    }

    private static void CheckCircularTarget(
        Vector2 from,
        Vector2 segment,
        float length,
        float radius,
        Vector2 targetPosition,
        GodotObject target,
        ProjectileHitKind kind,
        ProjectileSweepHit bestHit) {
        var segmentLengthSquared = segment.LengthSquared();
        if (segmentLengthSquared <= 0.001f)
            return;

        var projection = Mathf.Clamp((targetPosition - from).Dot(segment) / segmentLengthSquared, 0.0f, 1.0f);
        var closestPoint = from + (segment * projection);
        if (closestPoint.DistanceTo(targetPosition) > radius)
            return;

        var distance = length * projection;
        if (distance >= bestHit.Distance)
            return;

        bestHit.Kind = kind;
        bestHit.Target = target;
        bestHit.Position = closestPoint;
        bestHit.Distance = distance;
    }

    private void ApplyRadiusDamageToPlayers(Vector2 center, float radius, DamageResource damage) {
        if (PlayersByGlobalId == null)
            return;

        foreach (var playerEntry in PlayersByGlobalId) {
            var player = playerEntry.Value;
            if (player == null || !GodotObject.IsInstanceValid(player) || player.IsDead() || !player.IsInsideWorldRadius(center, radius))
                continue;

            ApplyDamageToPlayer(player, CreateDamageContainer(damage.Scaled(player.GetRadiusDamageMultiplier(center, radius))));
        }
    }

    private void ApplyRadiusDamageToProps(Vector2 center, float radius, DamageResource damage) {
        if (Props == null)
            return;

        foreach (var prop in Props) {
            if (prop == null || !GodotObject.IsInstanceValid(prop) || prop.IsDestroyed() || !prop.IsInsideWorldRadius(center, radius))
                continue;

            prop.ApplyDamage(CreateDamageContainer(damage.Scaled(prop.GetRadiusDamageMultiplier(center, radius))));
        }
    }

    private void ApplyDamageToPlayer(DamageTestPlayer player, DamageContainer damageContainer) {
        if (player == null || !GodotObject.IsInstanceValid(player) || player.IsDead())
            return;

        var victimGlobalId = player.GlobalId;
        var wasAlive = !player.IsDead();
        if (!player.ApplyDamage(damageContainer) || !wasAlive || !player.IsDead())
            return;

        PlayerKilled?.Invoke(OwnerGlobalId, victimGlobalId);
    }

    private void ApplyRadiusDamageToWalls(Vector2 center, float radius, DamageResource damage) {
        if (ArenaMapData == null)
            return;

        var changedTiles = ArenaMapData.DamageWallsInWorldRadius(center, TileSize, radius, CreateDamageContainer(damage));
        if (changedTiles.Count > 0)
            ArenaChanged?.Invoke();
    }
}
