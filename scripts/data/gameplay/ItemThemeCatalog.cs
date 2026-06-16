using Godot;

[GlobalClass]
public partial class ItemThemeCatalog : Resource {
    [Export]
    public Godot.Collections.Array<ItemThemeDefinition> ThemeDefinitions { get; set; } = new();

    [Export]
    public Godot.Collections.Array<ItemThemeDefinition> DefaultEnabledThemes { get; set; } = new();

    [Export]
    public ItemThemeDefinition FallbackTheme { get; set; }
}
