using Godot;

[GlobalClass]
public partial class PlayerItemSlot : Resource
{
    [Export]
    public string SlotId { get; set; } = string.Empty;

    [Export]
    public Godot.Collections.Array<PlayerItemSlotType> AcceptedContainerTypes { get; set; } = new();

    [Export]
    public float MaxItemWeight { get; set; } = 0.0f;

    [Export]
    public PlayerItem StoredItem { get; set; }

    public bool Accepts(PlayerItem item)
    {
        if (item == null)
        {
            return false;
        }

        if (MaxItemWeight > 0.0f && item.Weight > MaxItemWeight)
        {
            return false;
        }

        if (AcceptedContainerTypes.Count == 0 || AcceptedContainerTypes.Contains(PlayerItemSlotType.Generic))
        {
            return true;
        }

        foreach (var containerType in AcceptedContainerTypes)
        {
            if (item.FitsContainerType(containerType))
            {
                return true;
            }
        }

        return false;
    }
}
