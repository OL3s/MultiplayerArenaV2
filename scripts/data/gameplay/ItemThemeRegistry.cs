using System.Collections.Generic;
using Godot;

public static class ItemThemeRegistry {
    public const string CatalogPath = "res://assets/items/item_theme_catalog.tres";

    public static ItemThemeCatalog LoadCatalog() {
        return GD.Load<ItemThemeCatalog>(CatalogPath);
    }

    public static List<ItemThemeDefinition> GetAvailableThemes(ItemThemeCatalog catalog = null) {
        catalog ??= LoadCatalog();
        var themes = new List<ItemThemeDefinition>();
        if (catalog == null)
            return themes;

        foreach (var theme in catalog.ThemeDefinitions) {
            if (theme != null && !themes.Contains(theme))
                themes.Add(theme);
        }

        themes.Sort(CompareThemes);
        return themes;
    }

    public static List<string> GetDefaultEnabledThemePaths(ItemThemeCatalog catalog = null) {
        catalog ??= LoadCatalog();
        var paths = new List<string>();
        if (catalog == null)
            return paths;

        foreach (var theme in catalog.DefaultEnabledThemes)
            AddThemePath(paths, theme);

        if (paths.Count == 0)
            AddThemePath(paths, catalog.FallbackTheme);

        return paths;
    }

    public static List<ItemThemeDefinition> ResolveThemes(IEnumerable<string> themePaths, ItemThemeCatalog catalog = null) {
        catalog ??= LoadCatalog();
        var resolved = new List<ItemThemeDefinition>();
        var availableThemes = GetAvailableThemes(catalog);
        var requestedPaths = new HashSet<string>();

        if (themePaths != null) {
            foreach (var path in themePaths) {
                if (!string.IsNullOrWhiteSpace(path))
                    requestedPaths.Add(path);
            }
        }

        foreach (var theme in availableThemes) {
            if (requestedPaths.Contains(theme.ResourcePath))
                resolved.Add(theme);
        }

        if (resolved.Count == 0 && catalog?.FallbackTheme != null)
            resolved.Add(catalog.FallbackTheme);

        return resolved;
    }

    public static List<PlayerItem> LoadThemeItems(ItemThemeDefinition theme) {
        var items = new List<PlayerItem>();
        if (theme == null || string.IsNullOrWhiteSpace(theme.RootFolder))
            return items;

        LoadThemeItemsRecursive(NormalizeDirectoryPath(theme.RootFolder), items);
        return items;
    }

    public static string GetThemeIconPath(ItemThemeDefinition theme) {
        return theme?.Icon?.ResourcePath ?? string.Empty;
    }

    private static void LoadThemeItemsRecursive(string folderPath, List<PlayerItem> items) {
        using var directory = DirAccess.Open(folderPath);
        if (directory == null) {
            GameLog.Error(GameLogScope.PlayerItemRoom, "ItemThemeFolderOpenFailed", $"folder={folderPath}");
            return;
        }

        foreach (var fileName in directory.GetFiles()) {
            if (!fileName.EndsWith(".tres"))
                continue;

            var resourcePath = $"{folderPath}/{fileName}";
            var item = GD.Load<PlayerItem>(resourcePath);
            if (item != null && !items.Contains(item))
                items.Add(item);
        }

        foreach (var subfolder in directory.GetDirectories()) {
            if (subfolder.StartsWith("."))
                continue;

            LoadThemeItemsRecursive($"{folderPath}/{subfolder}", items);
        }
    }

    private static string NormalizeDirectoryPath(string folderPath) {
        return folderPath.EndsWith("/") ? folderPath.TrimEnd('/') : folderPath;
    }

    private static void AddThemePath(List<string> paths, ItemThemeDefinition theme) {
        if (theme == null || string.IsNullOrWhiteSpace(theme.ResourcePath) || paths.Contains(theme.ResourcePath))
            return;

        paths.Add(theme.ResourcePath);
    }

    private static int CompareThemes(ItemThemeDefinition a, ItemThemeDefinition b) {
        var sortCompare = a.SortOrder.CompareTo(b.SortOrder);
        if (sortCompare != 0)
            return sortCompare;

        return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
    }
}
