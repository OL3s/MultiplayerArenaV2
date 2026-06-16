public partial class DefaultArenaLAN : ArenaMatch {
    public override void _Ready() {
        UseLanTestBootstrap = true;
        ForceTestSetupOverrides = false;
        EnableDebugBuyMenu = false;
        base._Ready();
    }
}
