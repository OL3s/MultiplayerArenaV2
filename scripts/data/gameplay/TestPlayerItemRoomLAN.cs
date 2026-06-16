using Godot;

public partial class TestPlayerItemRoomLAN : ArenaMatch {
    public override void _Ready() {
        UseLanTestBootstrap = true;
        ForceTestSetupOverrides = true;
        EnableDebugBuyMenu = true;
        base._Ready();
    }
}
