using Godot;

[GlobalClass]
public partial class PlayerItemInstant : PlayerEquipable
{
    public PlayerItemInstant()
    {
        ContainerTypes.Add(PlayerItemSlotType.SmallGadget);
    }

    [Export]
    public bool ConsumeOnUse { get; set; } = true;
}
