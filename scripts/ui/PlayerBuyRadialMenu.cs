using System;
using System.Collections.Generic;
using Godot;

public partial class PlayerBuyRadialMenu : Control {
    private const string SegmentScenePath = "res://scenes/ui/buy/buy_radial_segment.tscn";
    private const float SegmentRadius = 112.0f;

    public enum EntryKind {
        Category,
        Item,
        Back,
        Close,
    }

    public sealed class Entry {
        public string Id { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public Texture2D Icon { get; init; }
        public EntryKind Kind { get; init; }
        public bool Enabled { get; init; } = true;
    }

    private readonly List<Entry> _entries = new();
    private readonly List<BuyRadialSegment> _segments = new();
    private PackedScene _segmentScene;
    private Label _centerLabel;
    private int _selectedIndex;

    public event Action<Entry> EntrySelected;

    public override void _Ready() {
        _segmentScene = GD.Load<PackedScene>(SegmentScenePath);
        _centerLabel = GetNode<Label>("Center/CenterLabel");
        SetCategoryEntries();
    }

    public void SetCategoryEntries() {
        SetEntries("BUY", new[] {
            new Entry { Id = "weapons", Label = "Weapons", Kind = EntryKind.Category },
            new Entry { Id = "gadgets", Label = "Gadgets", Kind = EntryKind.Category },
            new Entry { Id = "armor", Label = "Armor", Kind = EntryKind.Category },
            new Entry { Id = "cancel", Label = "Cancel", Kind = EntryKind.Close },
        });
    }

    public void SetEntries(string title, IReadOnlyList<Entry> entries) {
        _entries.Clear();
        _entries.AddRange(entries);
        _selectedIndex = 0;
        _centerLabel.Text = title;
        RebuildSegments();
    }

    public void SelectDirection(Vector2 direction) {
        if (_entries.Count == 0 || direction.LengthSquared() <= 0.1f)
            return;

        var angle = Mathf.PosMod(Mathf.Atan2(direction.Y, direction.X) + Mathf.Pi / 2.0f, Mathf.Tau);
        _selectedIndex = Mathf.Clamp(Mathf.RoundToInt(angle / Mathf.Tau * _entries.Count), 0, _entries.Count - 1);
        RefreshSegmentSelection();
    }

    public void ConfirmSelection() {
        if (_selectedIndex < 0 || _selectedIndex >= _entries.Count)
            return;

        var entry = _entries[_selectedIndex];
        if (entry.Enabled)
            EntrySelected?.Invoke(entry);
    }

    private void RebuildSegments() {
        ClearSegments();
        if (_entries.Count == 0)
            return;

        for (var i = 0; i < _entries.Count; i++) {
            var segment = _segmentScene?.Instantiate<BuyRadialSegment>() ?? new BuyRadialSegment();
            AddChild(segment);
            _segments.Add(segment);
            PositionSegment(segment, i, _entries.Count);
        }

        RefreshSegmentSelection();
    }

    private void RefreshSegmentSelection() {
        for (var i = 0; i < _segments.Count; i++) {
            var entry = _entries[i];
            _segments[i].SetEntry(entry.Label, entry.Icon, i == _selectedIndex, entry.Enabled, entry.Kind == EntryKind.Close);
        }
    }

    private static void PositionSegment(Control segment, int index, int count) {
        var angle = (-Mathf.Pi / 2.0f) + (Mathf.Tau * index / count);
        var center = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * SegmentRadius;
        segment.Position = center - (segment.CustomMinimumSize * 0.5f);
    }

    private void ClearSegments() {
        foreach (var segment in _segments) {
            RemoveChild(segment);
            segment.QueueFree();
        }

        _segments.Clear();
    }
}
