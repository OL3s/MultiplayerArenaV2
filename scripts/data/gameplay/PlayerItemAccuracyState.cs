using Godot;

public sealed class PlayerItemAccuracyState {
    private const float NearBaseRecoveryMultiplier = 0.08f;
    private const float RecoverySnapThreshold = 0.0005f;

    public IPlayerUsable Item { get; private set; }

    public float CurrentAccuracy => Item == null
        ? 0.0f
        : Mathf.Min(Item.DefaultAccuracy + CurrentShotInaccuracy + CurrentMovementInaccuracy, GetMaxCurrentAccuracy());

    public float CurrentSpreadAccuracy => Item == null
        ? 0.0f
        : CurrentAccuracy;

    public float CurrentShotInaccuracy { get; private set; }

    public float CurrentMovementInaccuracy { get; private set; }

    public void SetItem(IPlayerUsable item) {
        if (Item == item)
            return;

        Item = item;
        CurrentShotInaccuracy = 0.0f;
        CurrentMovementInaccuracy = 0.0f;
    }

    public void Update(float movementStrength, double delta) {
        if (Item == null) {
            CurrentShotInaccuracy = 0.0f;
            CurrentMovementInaccuracy = 0.0f;
            return;
        }

        UpdateMovementInaccuracy(movementStrength, delta);
        RecoverShotInaccuracy(delta);
    }

    public void ApplyUsePushback() {
        if (Item == null)
            return;

        CurrentShotInaccuracy += Item.AccuracyPushback;
        if (Item.MaxInaccuracy > 0.0f)
            CurrentShotInaccuracy = Mathf.Min(CurrentShotInaccuracy, Mathf.Max(Item.MaxInaccuracy - Item.DefaultAccuracy - CurrentMovementInaccuracy, 0.0f));
    }

    public float GetBaseAccuracy(float movementStrength) {
        if (Item == null)
            return 0.0f;

        return Item.DefaultAccuracy + (Item.MovementAccuracy * GetMovementAccuracyResponse(movementStrength));
    }

    public static float GetMovementAccuracyResponse(float movementStrength) {
        var clampedStrength = Mathf.Clamp(movementStrength, 0.0f, 1.0f);
        return Mathf.Pow(clampedStrength, 2.5f);
    }

    public static float GetSpreadRadiusAtDistance(float currentAccuracy, float distance) {
        return Mathf.Tan(Mathf.Max(currentAccuracy, 0.0f)) * Mathf.Max(distance, 0.0f);
    }

    private void UpdateMovementInaccuracy(float movementStrength, double delta) {
        var targetMovementInaccuracy = Item.MovementAccuracy * GetMovementAccuracyResponse(movementStrength);
        if (targetMovementInaccuracy > CurrentMovementInaccuracy) {
            CurrentMovementInaccuracy = targetMovementInaccuracy;
            return;
        }

        RecoverMovementInaccuracy(targetMovementInaccuracy, delta);
    }

    private void RecoverShotInaccuracy(double delta) {
        var difference = CurrentShotInaccuracy;
        if (difference <= RecoverySnapThreshold) {
            CurrentShotInaccuracy = 0.0f;
            return;
        }

        var recoveryRange = Item.MaxInaccuracy > Item.DefaultAccuracy + CurrentMovementInaccuracy
            ? Item.MaxInaccuracy - Item.DefaultAccuracy - CurrentMovementInaccuracy
            : Mathf.Max(Item.AccuracyPushback, 0.001f);
        var recoveryRatio = Mathf.Clamp(difference / recoveryRange, NearBaseRecoveryMultiplier, 1.0f);
        var recoveryStep = Item.ShotAccuracyRecovery * recoveryRatio * (float)delta;
        CurrentShotInaccuracy = Mathf.MoveToward(CurrentShotInaccuracy, 0.0f, recoveryStep);
    }

    private void RecoverMovementInaccuracy(float targetMovementInaccuracy, double delta) {
        var difference = CurrentMovementInaccuracy - targetMovementInaccuracy;
        if (difference <= RecoverySnapThreshold) {
            CurrentMovementInaccuracy = targetMovementInaccuracy;
            return;
        }

        var recoveryRange = Mathf.Max(Item.MovementAccuracy, 0.001f);
        var recoveryRatio = Mathf.Clamp(difference / recoveryRange, NearBaseRecoveryMultiplier, 1.0f);
        var recoveryStep = Item.MovementAccuracyRecovery * recoveryRatio * (float)delta;
        CurrentMovementInaccuracy = Mathf.MoveToward(CurrentMovementInaccuracy, targetMovementInaccuracy, recoveryStep);
    }

    private float GetMaxCurrentAccuracy() {
        return Item.MaxInaccuracy > 0.0f ? Item.MaxInaccuracy : float.MaxValue;
    }
}
