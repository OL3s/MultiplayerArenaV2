using Godot;

[GlobalClass]
public partial class GameplayScoring : Resource {
    [Export]
    public int BestOfRoundsPerGameMode { get; set; } = 3;

    [Export]
    public int BestOfGameModes { get; set; } = 3;

    [Export]
    public bool RandomizeGameModeOrder { get; set; }

    [Export]
    public Godot.Collections.Dictionary<int, int> TeamRoundScores { get; set; } = new();

    [Export]
    public Godot.Collections.Dictionary<int, int> TeamGameScores { get; set; } = new();

    public GameplayScoring Clone() {
        var clone = new GameplayScoring {
            BestOfRoundsPerGameMode = BestOfRoundsPerGameMode,
            BestOfGameModes = BestOfGameModes,
            RandomizeGameModeOrder = RandomizeGameModeOrder,
        };

        foreach (var pair in TeamRoundScores)
            clone.TeamRoundScores[pair.Key] = pair.Value;

        foreach (var pair in TeamGameScores)
            clone.TeamGameScores[pair.Key] = pair.Value;

        return clone;
    }
}
