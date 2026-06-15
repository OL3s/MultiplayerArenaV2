using Godot;

public interface IPlayerUsable {
    PlayerItemObjective UseObjective { get; }

    float RecoverySeconds { get; }

    float Range { get; }

    float AimDisplayRange { get; }

    float AimMoveSpeedMultiplier { get; }

    float DefaultAccuracy { get; }

    float MovementAccuracy { get; }

    float AccuracyPushback { get; }

    float ShotAccuracyRecovery { get; }

    float MovementAccuracyRecovery { get; }

    float MaxInaccuracy { get; }

    float GetAimDisplayDistance();

}
