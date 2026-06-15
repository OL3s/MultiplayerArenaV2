using Godot;

public partial class TeamSpawnBaseMarker : Node2D {
    private const int MaxVisibleSlots = 4;
    private Area2D _spawnArea;
    private CollisionShape2D _spawnCollisionShape;
    private Area2D _objectiveArea;
    private CollisionShape2D _objectiveCollisionShape;
    private Color _teamColor = Colors.White;

    [Export]
    public int TeamId { get; set; }

    [Export]
    public int VisibleSlotCount { get; set; } = MaxVisibleSlots;

    [Export]
    public float SpawnRadius { get; set; } = 32.0f;

    [Export]
    public float ObjectiveRadius { get; set; } = 12.0f;

    public void Configure(int teamId, int visibleSlotCount, float spawnRadius, float objectiveRadius) {
        if (teamId == MultiplayerData.DefaultTeamId)
            GameLog.Warn(GameLogScope.PlayerSpawn, "UnassignedTeamUsed", "context=TeamSpawnBaseMarker.Configure team=-1 result=fallbackColor");

        TeamId = teamId;
        _teamColor = GetRuntimeTeamColor(teamId);
        SpawnRadius = spawnRadius;
        ObjectiveRadius = objectiveRadius;
        SetVisibleSlotCount(visibleSlotCount);
        ApplyAreaRadii();
        ApplyTeamColor();
    }

    public override void _Ready() {
        ApplySlotVisibility();
        ApplyAreaRadii();
        _teamColor = GetRuntimeTeamColor(TeamId);
        ApplyTeamColor();
    }

    public void SetVisibleSlotCount(int visibleSlotCount) {
        VisibleSlotCount = Mathf.Clamp(visibleSlotCount, 0, MaxVisibleSlots);
        ApplySlotVisibility();
    }

    private void ApplySlotVisibility() {
        for (var i = 1; i <= MaxVisibleSlots; i++) {
            var slot = GetNodeOrNull<Node2D>($"Slot{i}");
            if (slot != null)
                slot.Visible = i <= VisibleSlotCount;
        }
    }

    private void ApplyAreaRadii() {
        _spawnArea ??= GetNode<Area2D>("SpawnArea");
        _spawnCollisionShape ??= _spawnArea.GetNode<CollisionShape2D>("CollisionShape2D");
        if (_spawnCollisionShape.Shape is CircleShape2D spawnCircle)
            spawnCircle.Radius = SpawnRadius;

        _objectiveArea ??= GetNode<Area2D>("ObjectiveArea");
        _objectiveCollisionShape ??= _objectiveArea.GetNode<CollisionShape2D>("CollisionShape2D");
        if (_objectiveCollisionShape.Shape is CircleShape2D objectiveCircle)
            objectiveCircle.Radius = ObjectiveRadius;
    }

    private void ApplyTeamColor() {
        var core = GetNodeOrNull<Sprite2D>("Core");
        if (core != null)
            core.Modulate = _teamColor;

        for (var i = 1; i <= MaxVisibleSlots; i++) {
            var platform = GetNodeOrNull<Sprite2D>($"Slot{i}/Platform");
            if (platform != null)
                platform.Modulate = _teamColor;
        }
    }

    private static Color GetRuntimeTeamColor(int teamId) {
        return TeamVisuals.GetTeamColor(teamId + 1);
    }
}
