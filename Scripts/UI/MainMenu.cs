using Godot;

public partial class MainMenu : Control
{
    private const int LocalLobbySlotCount = 4;
    private const string HostServerMenuScenePath = "res://Scenes/UI/HostServerMenu.tscn";
    private const string JoinGameMenuScenePath = "res://Scenes/UI/JoinGameMenu.tscn";

    [Export]
    public LocalLobbyData LocalLobbyData { get; private set; } = new();

    public override void _Ready()
    {
        GetNode<Button>("TopRightButtons/ExitGameButton").Pressed += OnExitGamePressed;
        GetNode<Button>("MainLayout/ActionButtons/HostGameButton").Pressed += OnHostGamePressed;
        GetNode<Button>("MainLayout/ActionButtons/JoinGameButton").Pressed += OnJoinGamePressed;
        ApplyPlaceholderIcons();
        EnsureDefaultLocalLobby();
        RefreshLocalLobbySlots();
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (inputEvent is InputEventKey { Pressed: true, Echo: false } keyEvent
            && (keyEvent.Keycode == Key.Enter || keyEvent.Keycode == Key.KpEnter))
        {
            TryJoinKeyboardPlayer();
            return;
        }

        if (inputEvent is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.A } joypadButtonEvent)
        {
            TryJoinGamepadPlayer(joypadButtonEvent.Device);
        }
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
    }

    private void TryJoinKeyboardPlayer()
    {
        if (HasKeyboardPlayer())
        {
            return;
        }

        var slotIndex = GetFirstOpenSlotIndex();
        if (slotIndex == -1)
        {
            return;
        }

        ConfigureKeyboardPlayer(slotIndex);
    }

    private void TryJoinGamepadPlayer(int deviceId)
    {
        if (HasGamepadPlayer(deviceId))
        {
            return;
        }

        var slotIndex = GetFirstOpenSlotIndex();
        if (slotIndex == -1)
        {
            return;
        }

        ConfigureGamepadPlayer(slotIndex, deviceId);
    }

    private int GetFirstOpenSlotIndex()
    {
        for (var slotIndex = 0; slotIndex < LocalLobbySlotCount; slotIndex++)
        {
            if (!GetLocalPlayer(slotIndex).IsActive)
            {
                return slotIndex;
            }
        }

        return -1;
    }

    private bool HasKeyboardPlayer()
    {
        foreach (var localPlayer in LocalLobbyData.LocalPlayers)
        {
            if (localPlayer.IsActive && localPlayer.InputType == LocalPlayerData.LocalInputType.KeyboardMouse)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasGamepadPlayer(int deviceId)
    {
        foreach (var localPlayer in LocalLobbyData.LocalPlayers)
        {
            if (localPlayer.IsActive
                && localPlayer.InputType == LocalPlayerData.LocalInputType.Gamepad
                && localPlayer.DeviceId == deviceId)
            {
                return true;
            }
        }

        return false;
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
        var nextOpenSlotIndex = GetFirstOpenSlotIndex();

        for (var slotIndex = 0; slotIndex < LocalLobbySlotCount; slotIndex++)
        {
            var localPlayer = GetLocalPlayer(slotIndex);
            var slotPanel = GetNode<PanelContainer>($"MainLayout/LobbyPanel/LobbySlots/Slot{slotIndex + 1}");
            var slotLabel = GetNode<Label>($"MainLayout/LobbyPanel/LobbySlots/Slot{slotIndex + 1}/SlotLabel");
            var isNextOpenSlot = slotIndex == nextOpenSlotIndex;
            slotPanel.Modulate = GetSlotColor(localPlayer, isNextOpenSlot);
            slotLabel.Text = FormatSlotText(localPlayer, HasKeyboardPlayer(), isNextOpenSlot);
        }
    }

    private static Color GetSlotColor(LocalPlayerData localPlayer, bool isNextOpenSlot)
    {
        if (localPlayer.IsActive)
        {
            return new Color(0.55f, 1.0f, 0.6f);
        }

        return isNextOpenSlot ? Colors.White : new Color(0.42f, 0.42f, 0.42f);
    }

    private static string FormatSlotText(LocalPlayerData localPlayer, bool hasKeyboardPlayer, bool isNextOpenSlot)
    {
        if (!localPlayer.IsActive)
        {
            if (!isNextOpenSlot)
            {
                return $"Player {localPlayer.LocalId + 1}\nWaiting\nEmpty";
            }

            var joinPrompt = hasKeyboardPlayer ? "Press A" : "Press A or Enter";
            return $"Player {localPlayer.LocalId + 1}\n{joinPrompt}\nEmpty";
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

    private void OnHostGamePressed()
    {
        GetTree().ChangeSceneToFile(HostServerMenuScenePath);
    }

    private void OnJoinGamePressed()
    {
        GetTree().ChangeSceneToFile(JoinGameMenuScenePath);
    }

    private void ApplyPlaceholderIcons()
    {
        SetPlaceholderIcon(GetNode<Button>("TopRightButtons/SettingsButton"), "Tools", 14);
        SetPlaceholderIcon(GetNode<Button>("TopRightButtons/ExitGameButton"), "Close", 14);
        SetPlaceholderIcon(GetNode<Button>("MainLayout/ActionButtons/HostGameButton"), "Play", 28);
        SetPlaceholderIcon(GetNode<Button>("MainLayout/ActionButtons/JoinGameButton"), "Network", 28);
    }

    private void SetPlaceholderIcon(Button button, string iconName, int maxWidth)
    {
        button.Icon = GetThemeIcon(iconName, "EditorIcons");
        button.Set("icon_max_width", maxWidth);
    }
}
