using System;
using Godot;

public partial class ConfigSelectionOverlay : PanelContainer
{
    public void Configure(string title, string[] optionLabels, Func<int, bool> isSelected, Action<int, bool> onToggled)
    {
        GetNode<Label>("Content/TitleLabel").Text = title;

        var options = GetNode<VBoxContainer>("Content/Options");
        foreach (var child in options.GetChildren())
        {
            options.RemoveChild(child);
            child.QueueFree();
        }

        for (var i = 0; i < optionLabels.Length; i++)
        {
            var optionIndex = i;
            var checkBox = new CheckBox
            {
                Text = optionLabels[i],
                ButtonPressed = isSelected(optionIndex),
            };
            checkBox.Toggled += enabled => onToggled(optionIndex, enabled);
            options.AddChild(checkBox);
        }

        GetNode<Button>("Content/CloseButton").Pressed += QueueFree;
    }
}
