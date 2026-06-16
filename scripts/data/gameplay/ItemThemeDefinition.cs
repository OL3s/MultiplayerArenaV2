using Godot;

[GlobalClass]
public partial class ItemThemeDefinition : Resource {
    [Export]
    public string Id { get; set; } = string.Empty;

    [Export]
    public string DisplayName { get; set; } = "Theme";

    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } = string.Empty;

    [Export]
    public Texture2D Icon { get; set; }

    [Export(PropertyHint.Dir)]
    public string RootFolder { get; set; } = string.Empty;

    [Export]
    public PlayerItem DefaultStarterItem { get; set; }

    [Export]
    public Godot.Collections.Array<ItemBuyMenuGroup> BuyMenuGroups { get; set; } = new();

    [Export]
    public int SortOrder { get; set; }
}
