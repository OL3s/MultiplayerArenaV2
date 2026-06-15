# SVG Input Icon Generation

Godot's SVG importer does not reliably render SVG `<text>` elements. SVGs that preview correctly in VS Code, Inkscape, or a browser can import in Godot with the text missing.

For input button icons, do not use SVG `<text>`. Make labels as real vector shapes instead.

## Current Approach

Icon labels should be drawn or converted into vector paths before export. In practice, create the label visually in Inkscape, then convert the text to paths so the SVG contains shape geometry instead of font-dependent text.

This keeps the files as SVGs, but avoids font resolution entirely:

- Works in Godot after import.
- Does not depend on system fonts.
- Looks consistent across editor, game, and source previews.
- Lets labels look intentionally designed instead of generated from blocky placeholder glyphs.

## Inkscape Text Workflow

Use this workflow when making a labeled SVG icon:

1. Open or create the icon in Inkscape.
2. Add the label with the text tool while designing.
3. Choose the final font, size, weight, spacing, and alignment.
4. Select the text object.
5. Convert it to geometry with `Path > Object to Path`.
6. If the letters need to act as one object, use `Path > Union` after converting.
7. Save as plain SVG.
8. Confirm the saved SVG does not contain `<text>` elements.
9. Reimport the asset in Godot.

## Shape-Only Rule

The exported SVG should contain paths, polygons, circles, rectangles, and other supported shapes. It should not contain live text objects like this:

```xml
<text>ABC</text>
```

Prefer converted path data like this:

```xml
<path d="..." fill="#E8EDF2"/>
```

## Regeneration Checklist

1. Convert all icon labels to paths or manually draw letters as shapes.
2. Confirm no icon contains SVG text:

```bash
rg '<text\b' assets/inputicons
```

3. Reimport assets for Godot:

```bash
./tools/import-assets.sh
```

4. If Godot creates unrelated `.cs.uid` files during the scan, do not include them unless the project intentionally starts tracking those files.
