using Godot;

public partial class MainMenu : Control
{
    private const int LocalLobbySlotCount = 4;

    [Export]
    public LocalLobbyData LocalLobbyData { get; private set; } = new();

    public override void _Ready()
    {
        GetNode<Button>("MainLayout/MenuPanel/MenuButtons/ExitGameButton").Pressed += OnExitGamePressed;
        EnsureDefaultLocalLobby();
        RefreshLocalLobbySlots();
    }

    public void ConfigureKeyboardPlayer(int slotIndex)
    {
        ConfigureLocalPlayer(slotIndex, LocalPlayerData.LocalInputType.KeyboardMouse, -1);
    }

    public void ConfigureGamepadPlayer(int slotIndex, int deviceId)
    {
        ConfigureLocalPlayer(slotIndex, LocalPlayerData.LocalInputType.Gamepad, deviceId);
    }

    public void ClearLocalPlayerSlot(int slotIndex)
    {
        var localPlayer = GetLocalPlayer(slotIndex);
        localPlayer.IsActive = false;
        localPlayer.InputType = LocalPlayerData.LocalInputType.None;
        localPlayer.DeviceId = -1;
        RefreshLocalLobbySlots();
    }

    private void ConfigureLocalPlayer(int slotIndex, LocalPlayerData.LocalInputType inputType, int deviceId)
    {
        var localPlayer = GetLocalPlayer(slotIndex);
        localPlayer.IsActive = true;
        localPlayer.InputType = inputType;
        localPlayer.DeviceId = deviceId;
        localPlayer.DisplayName = $"Player {slotIndex + 1}";
        RefreshLocalLobbySlots();
    }

    private void EnsureDefaultLocalLobby()
    {
        for (var slotIndex = LocalLobbyData.LocalPlayers.Count; slotIndex < LocalLobbySlotCount; slotIndex++)
        {
            LocalLobbyData.LocalPlayers.Add(new LocalPlayerData
            {
                LocalId = slotIndex,
                DisplayName = $"Player {slotIndex + 1}",
            });
        }

        ConfigureKeyboardPlayer(0);
    }

    private LocalPlayerData GetLocalPlayer(int slotIndex)
    {
        while (LocalLobbyData.LocalPlayers.Count <= slotIndex)
        {
            LocalLobbyData.LocalPlayers.Add(new LocalPlayerData
            {
                LocalId = LocalLobbyData.LocalPlayers.Count,
                DisplayName = $"Player {LocalLobbyData.LocalPlayers.Count + 1}",
            });
        }

        return LocalLobbyData.LocalPlayers[slotIndex];
    }

    private void RefreshLocalLobbySlots()
    {
        for (var slotIndex = 0; slotIndex < LocalLobbySlotCount; slotIndex++)
        {
            var localPlayer = GetLocalPlayer(slotIndex);
            var slotLabel = GetNode<Label>($"MainLayout/LobbyPanel/LobbySlots/Slot{slotIndex + 1}/SlotLabel");
            slotLabel.Text = FormatSlotText(localPlayer);
        }
    }

    private static string FormatSlotText(LocalPlayerData localPlayer)
    {
        if (!localPlayer.IsActive)
        {
            return $"Slot {localPlayer.LocalId + 1}\nPress Join\nEmpty";
        }

        var inputLabel = localPlayer.InputType switch
        {
            LocalPlayerData.LocalInputType.KeyboardMouse => "Keyboard + Mouse",
            LocalPlayerData.LocalInputType.Gamepad => $"Gamepad {localPlayer.DeviceId}",
            _ => "No Input",
        };

        return $"{localPlayer.DisplayName}\n{inputLabel}\nIn Lobby";
    }

    private void OnExitGamePressed()
    {
        GetTree().Quit();
    }
}
