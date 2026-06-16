using Godot;

[GlobalClass]
public partial class ItemBuyMenuGroup : Resource {
    public enum AcceptedItemKind {
        Any,
        Weapon,
        Gadget,
        Armor,
    }

    [Export]
    public string Id { get; set; } = string.Empty;

    [Export]
    public string DisplayName { get; set; } = "Group";

    [Export]
    public Texture2D Icon { get; set; }

    [Export]
    public Godot.Collections.Array<ItemBuyMenuGroup> ChildGroups { get; set; } = new();

    [Export]
    public Godot.Collections.Array<AcceptedItemKind> AcceptedKinds { get; set; } = new();

    [Export]
    public Godot.Collections.Array<string> ItemIdPrefixes { get; set; } = new();

    [Export]
    public Godot.Collections.Array<string> ResourcePathPrefixes { get; set; } = new();

    [Export]
    public bool IncludeStarterItems { get; set; } = true;

    public bool Matches(PlayerItem item, string resourcePath, ItemThemeDefinition theme) {
        if (item == null)
            return false;

        if (!IncludeStarterItems && theme?.DefaultStarterItem != null && item.ResourcePath == theme.DefaultStarterItem.ResourcePath)
            return false;

        return MatchesKind(item) && MatchesItemId(item) && MatchesResourcePath(resourcePath);
    }

    private bool MatchesKind(PlayerItem item) {
        if (AcceptedKinds.Count == 0 || AcceptedKinds.Contains(AcceptedItemKind.Any))
            return true;

        if (item is PlayerArmor)
            return AcceptedKinds.Contains(AcceptedItemKind.Armor);
        if (item is PlayerGadget)
            return AcceptedKinds.Contains(AcceptedItemKind.Gadget);
        if (item is PlayerWeapon)
            return AcceptedKinds.Contains(AcceptedItemKind.Weapon);

        return false;
    }

    private bool MatchesItemId(PlayerItem item) {
        if (ItemIdPrefixes.Count == 0)
            return true;

        foreach (var prefix in ItemIdPrefixes) {
            if (!string.IsNullOrWhiteSpace(prefix) && item.ItemId.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool MatchesResourcePath(string resourcePath) {
        if (ResourcePathPrefixes.Count == 0)
            return true;

        foreach (var prefix in ResourcePathPrefixes) {
            if (!string.IsNullOrWhiteSpace(prefix) && resourcePath.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
