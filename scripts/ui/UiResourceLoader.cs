using Godot;
using System.Collections.Generic;

public static class UiResourceLoader {
    private static readonly Dictionary<string, Texture2D> IconTexturesByPath = new();

    public static Texture2D LoadIconTexture(string iconPath) {
        if (string.IsNullOrWhiteSpace(iconPath))
            return null;

        if (IconTexturesByPath.TryGetValue(iconPath, out var cachedTexture))
            return cachedTexture;

        try {
            var texture = iconPath.EndsWith(".svg", System.StringComparison.OrdinalIgnoreCase)
                ? LoadSvgTexture(iconPath)
                : LoadImageTexture(iconPath);
            if (texture != null) {
                IconTexturesByPath[iconPath] = texture;
                return texture;
            }
        }
        catch (System.Exception exception) {
            GameLog.Error(GameLogScope.UI, "IconTextureLoadFailed", $"path={iconPath} error={exception.GetType().Name}");
            return null;
        }

        GameLog.Error(GameLogScope.UI, "IconTextureLoadFailed", $"path={iconPath} error=nullTexture");
        return null;
    }

    private static Texture2D LoadSvgTexture(string iconPath) {
        if (!FileAccess.FileExists(iconPath)) {
            GameLog.Error(GameLogScope.UI, "IconTextureLoadFailed", $"path={iconPath} error=fileMissing");
            return null;
        }

        var svgBytes = FileAccess.GetFileAsBytes(iconPath);
        if (svgBytes == null || svgBytes.Length == 0) {
            GameLog.Error(GameLogScope.UI, "IconTextureLoadFailed", $"path={iconPath} error=emptyFile");
            return null;
        }

        var image = new Image();
        var error = image.LoadSvgFromBuffer(svgBytes);
        if (error != Error.Ok) {
            GameLog.Error(GameLogScope.UI, "IconTextureLoadFailed", $"path={iconPath} error={error}");
            return null;
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static Texture2D LoadImageTexture(string iconPath) {
        var image = Image.LoadFromFile(iconPath);
        if (image == null || image.IsEmpty()) {
            GameLog.Error(GameLogScope.UI, "IconTextureLoadFailed", $"path={iconPath} error=imageLoadFailed");
            return null;
        }

        return ImageTexture.CreateFromImage(image);
    }
}
