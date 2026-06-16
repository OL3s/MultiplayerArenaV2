using Godot;

public partial class MatchConfigStrip : PanelContainer {
    private MatchConfigEntry _modeEntry;
    private MatchConfigEntry _loadoutEntry;
    private MatchConfigEntry _structureEntry;
    private MatchConfigEntry _biomeEntry;
    private MatchConfigEntry _themeEntry;
    private MatchConfigEntry _seedEntry;

    public override void _Ready() {
        EnsureNodes();
    }

    public void SetMatchConfig(
        string modeName,
        string modeIconPath,
        string loadoutName,
        string loadoutIconPath,
        string structureName,
        string structureIconPath,
        string biomeName,
        string biomeIconPath,
        string themeName,
        string themeIconPath,
        string seedText) {
        EnsureNodes();

        _modeEntry.SetEntry("Mode", modeName, modeIconPath);
        _loadoutEntry.SetEntry("Loadout", loadoutName, loadoutIconPath);
        _structureEntry.SetEntry("Structure", structureName, structureIconPath);
        _biomeEntry.SetEntry("Biome", biomeName, biomeIconPath);
        _themeEntry.SetEntry("Theme", themeName, themeIconPath);
        _seedEntry.SetEntry("Seed", seedText, "res://assets/ui/config_structure.svg");
    }

    private void EnsureNodes() {
        if (_modeEntry != null)
            return;

        _modeEntry = GetNode<MatchConfigEntry>("Margin/Layout/Entries/ModeEntry");
        _loadoutEntry = GetNode<MatchConfigEntry>("Margin/Layout/Entries/LoadoutEntry");
        _structureEntry = GetNode<MatchConfigEntry>("Margin/Layout/Entries/StructureEntry");
        _biomeEntry = GetNode<MatchConfigEntry>("Margin/Layout/Entries/BiomeEntry");
        _themeEntry = GetNode<MatchConfigEntry>("Margin/Layout/Entries/ThemeEntry");
        _seedEntry = GetNode<MatchConfigEntry>("Margin/Layout/Entries/SeedEntry");
    }
}
