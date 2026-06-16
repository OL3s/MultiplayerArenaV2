using Godot;

[GlobalClass]
public partial class ItemThemeConfig : Resource {
    [Export]
    public Godot.Collections.Array<string> EnabledThemeDefinitionPaths { get; set; } = new();

    public void AddThemePath(string themeDefinitionPath) {
        if (string.IsNullOrWhiteSpace(themeDefinitionPath) || HasThemePath(themeDefinitionPath))
            return;

        EnabledThemeDefinitionPaths.Add(themeDefinitionPath);
    }

    public void RemoveThemePath(string themeDefinitionPath) {
        for (var i = EnabledThemeDefinitionPaths.Count - 1; i >= 0; i--) {
            if (EnabledThemeDefinitionPaths[i] == themeDefinitionPath)
                EnabledThemeDefinitionPaths.RemoveAt(i);
        }
    }

    public bool HasThemePath(string themeDefinitionPath) {
        foreach (var enabledThemePath in EnabledThemeDefinitionPaths) {
            if (enabledThemePath == themeDefinitionPath)
                return true;
        }

        return false;
    }

    public ItemThemeConfig Clone() {
        var clone = new ItemThemeConfig();
        foreach (var themePath in EnabledThemeDefinitionPaths)
            clone.EnabledThemeDefinitionPaths.Add(themePath);

        return clone;
    }
}
