# SVG Input Icon Generation

Godot's SVG importer does not reliably render SVG `<text>` elements. SVGs that preview correctly in VS Code or a browser can import in Godot with the text missing.

For input button icons, do not use SVG `<text>`. Generate labels as real vector geometry instead.

## Current Approach

The generated icons in `assets/inputicons/` use a small pixel-font map in Python. Each label character is defined as a 5x7 bitmap, then written into the SVG as many small `<rect>` elements.

This keeps the files as SVGs, but avoids font resolution entirely:

- Works in Godot after import.
- Does not depend on system fonts.
- Looks consistent across editor, game, and source previews.
- Keeps labels editable by changing the Python glyph map and regenerating.

## Minimal Pattern

Use this pattern when generating new labeled SVG buttons:

```python
GLYPHS = {
    "A": ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
    "B": ["11110", "10001", "10001", "11110", "10001", "10001", "11110"],
    "X": ["10001", "10001", "01010", "00100", "01010", "10001", "10001"],
}

def pixel_label(label, center_x=64, center_y=64, max_width=72, max_height=36, fill="#E8EDF2"):
    glyphs = [GLYPHS[ch] for ch in label.upper() if ch in GLYPHS]
    cols = len(glyphs) * 5 + max(0, len(glyphs) - 1)
    rows = 7
    cell = min(max_width / cols, max_height / rows)
    start_x = center_x - (cols * cell) / 2
    start_y = center_y - (rows * cell) / 2
    rects = []
    cursor = 0

    for glyph in glyphs:
        for row_index, row in enumerate(glyph):
            for col_index, bit in enumerate(row):
                if bit == "1":
                    x = start_x + (cursor + col_index) * cell
                    y = start_y + row_index * cell
                    rects.append(
                        f'<rect x="{x:.2f}" y="{y:.2f}" '
                        f'width="{cell * 0.86:.2f}" height="{cell * 0.86:.2f}" '
                        f'rx="{cell * 0.14:.2f}" fill="{fill}"/>'
                    )
        cursor += 6

    return "\n  ".join(rects)
```

Then insert the returned string into the SVG body:

```python
label_geometry = pixel_label("X")

svg = f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128">
  <title>X button icon</title>
  <circle cx="64" cy="64" r="50" fill="#2563EB" stroke="#E8EDF2" stroke-width="8"/>
  {label_geometry}
</svg>'''
```

## Regeneration Checklist

1. Generate the SVGs with label geometry, not `<text>`.
2. Confirm no generated icon contains SVG text:

```bash
rg '<text\b' assets/InputIcons
```

3. Reimport assets for Godot:

```bash
godot --headless --import
```

4. If Godot creates unrelated `.cs.uid` files during the scan, do not include them unless the project intentionally starts tracking those files.
