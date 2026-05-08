using Godot;

public partial class MainMenu : Control
{
    private const int LocalLobbySlotCount = 4;
    private const string HostServerMenuScenePath = "res://Scenes/UI/HostServerMenu.tscn";
    private const string JoinGameMenuScenePath = "res://Scenes/UI/JoinGameMenu.tscn";
    private const string SettingsMenuScenePath = "res://Scenes/UI/SettingsMenu.tscn";
    private const string ConfirmationOverlayScenePath = "res://Scenes/UI/ConfirmationOverlay.tscn";

    private PackedScene _confirmationOverlayScene;

    private LocalLobbyData LocalLobbyData => GetNetworking().LocalLobbyData;

    public override void _Ready()
    {
        UiInputActions.EnsureConfigured();
        _confirmationOverlayScene = GD.Load<PackedScene>(ConfirmationOverlayScenePath);
        GetNode<Button>("TopRightButtons/SettingsButton").Pressed += OnSettingsPressed;
        GetNode<Button>("TopRightButtons/ExitGameButton").Pressed += OnExitGamePressed;
        GetNode<Button>("MainLayout/ActionButtons/HostGameButton").Pressed += OnHostGamePressed;
        GetNode<Button>("MainLayout/ActionButtons/JoinGameButton").Pressed += OnJoinGamePressed;
        EnsureDefaultLocalLobby();
        RefreshLocalLobbySlots();
        RefreshActionButtonsVisibility();
        CallDeferred(MethodName.RefreshDefaultFocus);
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (inputEvent is InputEventKey { Pressed: true, Echo: false } keyEvent
            && (keyEvent.Keycode == Key.Enter || keyEvent.Keycode == Key.KpEnter))
        {
            if (TryJoinKeyboardPlayer())
            {
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (inputEvent is InputEventKey { Pressed: true, Echo: false } leaveKeyEvent
            && (leaveKeyEvent.Keycode == Key.Escape || leaveKeyEvent.Keycode == Key.Backspace))
        {
            if (TryLeaveKeyboardPlayer())
            {
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (inputEvent is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.X } joypadButtonEvent)
        {
            if (TryJoinGamepadPlayer(joypadButtonEvent.Device))
            {
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (inputEvent is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.Y } leaveJoypadButtonEvent)
        {
            if (TryLeaveGamepadPlayer(leaveJoypadButtonEvent.Device))
            {
                GetViewport().SetInputAsHandled();
            }

            return;
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
        RefreshActionButtonsVisibility();
    }

    private void ConfigureLocalPlayer(int slotIndex, LocalPlayerData.LocalInputType inputType, int deviceId)
    {
        var localPlayer = GetLocalPlayer(slotIndex);
        localPlayer.IsActive = true;
        localPlayer.InputType = inputType;
        localPlayer.DeviceId = deviceId;
        localPlayer.DisplayName = $"Player {slotIndex + 1}";
        RefreshLocalLobbySlots();
        RefreshActionButtonsVisibility();
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

    private bool TryJoinKeyboardPlayer()
    {
        if (HasKeyboardPlayer())
        {
            return false;
        }

        var slotIndex = GetFirstOpenSlotIndex();
        if (slotIndex == -1)
        {
            return false;
        }

        ConfigureKeyboardPlayer(slotIndex);
        return true;
    }

    private bool TryLeaveKeyboardPlayer()
    {
        var slotIndex = GetKeyboardPlayerSlotIndex();
        if (slotIndex == -1)
        {
            return false;
        }

        ClearLocalPlayerSlot(slotIndex);
        return true;
    }

    private bool TryJoinGamepadPlayer(int deviceId)
    {
        if (HasGamepadPlayer(deviceId))
        {
            return false;
        }

        var slotIndex = GetFirstOpenSlotIndex();
        if (slotIndex == -1)
        {
            return false;
        }

        ConfigureGamepadPlayer(slotIndex, deviceId);
        return true;
    }

    private bool TryLeaveGamepadPlayer(int deviceId)
    {
        var slotIndex = GetGamepadPlayerSlotIndex(deviceId);
        if (slotIndex == -1)
        {
            return false;
        }

        ClearLocalPlayerSlot(slotIndex);
        return true;
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
        return GetKeyboardPlayerSlotIndex() != -1;
    }

    private int GetKeyboardPlayerSlotIndex()
    {
        foreach (var localPlayer in LocalLobbyData.LocalPlayers)
        {
            if (localPlayer.IsActive && localPlayer.InputType == LocalPlayerData.LocalInputType.KeyboardMouse)
            {
                return localPlayer.LocalId;
            }
        }

        return -1;
    }

    private bool HasGamepadPlayer(int deviceId)
    {
        return GetGamepadPlayerSlotIndex(deviceId) != -1;
    }

    private int GetGamepadPlayerSlotIndex(int deviceId)
    {
        foreach (var localPlayer in LocalLobbyData.LocalPlayers)
        {
            if (localPlayer.IsActive
                && localPlayer.InputType == LocalPlayerData.LocalInputType.Gamepad
                && localPlayer.DeviceId == deviceId)
            {
                return localPlayer.LocalId;
            }
        }

        return -1;
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
        var hasKeyboardPlayer = HasKeyboardPlayer();
        var nextOpenSlotIndex = GetFirstOpenSlotIndex();
        GetNode<Label>("MainLayout/LobbyPanel/LobbyHelpLabel").Text = FormatLobbyHelpText(hasKeyboardPlayer);

        for (var slotIndex = 0; slotIndex < LocalLobbySlotCount; slotIndex++)
        {
            var localPlayer = GetLocalPlayer(slotIndex);
            var slotPanel = GetNode<PanelContainer>($"MainLayout/LobbyPanel/LobbySlots/Slot{slotIndex + 1}");
            var slotLabel = GetNode<Label>($"MainLayout/LobbyPanel/LobbySlots/Slot{slotIndex + 1}/SlotLabel");
            var isNextOpenSlot = slotIndex == nextOpenSlotIndex;
            slotPanel.Modulate = GetSlotColor(localPlayer, isNextOpenSlot);
            slotLabel.Text = FormatSlotText(localPlayer, hasKeyboardPlayer, isNextOpenSlot);
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

            var joinPrompt = FormatJoinPrompt(hasKeyboardPlayer);
            return $"Player {localPlayer.LocalId + 1}\n{joinPrompt}\nEmpty";
        }

        var inputLabel = localPlayer.InputType switch
        {
            LocalPlayerData.LocalInputType.KeyboardMouse => "Keyboard + Mouse\nEsc or Backspace leaves",
            LocalPlayerData.LocalInputType.Gamepad => $"Gamepad {localPlayer.DeviceId}\nY leaves",
            _ => "No Input",
        };

        return $"{localPlayer.DisplayName}\n{inputLabel}\nIn Lobby";
    }

    private static string FormatJoinPrompt(bool hasKeyboardPlayer)
    {
        return hasKeyboardPlayer ? "Press X" : "Press X or Enter";
    }

    private static string FormatLobbyHelpText(bool hasKeyboardPlayer)
    {
        return $"{FormatJoinPrompt(hasKeyboardPlayer)} to join. Use stick or d-pad, A to select, B to cancel, and Y or Esc to leave";
    }

    private void OnExitGamePressed()
    {
        ShowConfirmationOverlay(
            "Exit Game?",
            "Are you sure you want to quit Multiplayer Arena?",
            "Exit",
            "Stay",
            () => GetTree().Quit());
    }

    private void OnSettingsPressed()
    {
        GetTree().ChangeSceneToFile(SettingsMenuScenePath);
    }

    private void OnHostGamePressed()
    {
        if (!HasActiveLocalPlayer())
        {
            return;
        }

        GetTree().ChangeSceneToFile(HostServerMenuScenePath);
    }

    private void OnJoinGamePressed()
    {
        if (!HasActiveLocalPlayer())
        {
            return;
        }

        GetTree().ChangeSceneToFile(JoinGameMenuScenePath);
    }

    private void RefreshActionButtonsVisibility()
    {
        var hasActiveLocalPlayer = HasActiveLocalPlayer();
        GetNode<Button>("MainLayout/ActionButtons/HostGameButton").Visible = hasActiveLocalPlayer;

        var joinGameButton = GetNode<Button>("MainLayout/ActionButtons/JoinGameButton");
        joinGameButton.Visible = hasActiveLocalPlayer;
        RefreshDefaultFocus();
    }

    private void RefreshDefaultFocus()
    {
        var focusOwner = GetViewport().GuiGetFocusOwner();
        if (focusOwner != null && focusOwner.IsVisibleInTree())
        {
            return;
        }

        if (HasActiveLocalPlayer())
        {
            GetNode<Button>("MainLayout/ActionButtons/HostGameButton").GrabFocus();
            return;
        }

        GetNode<Button>("TopRightButtons/ExitGameButton").GrabFocus();
    }

    private bool HasActiveLocalPlayer()
    {
        foreach (var localPlayer in LocalLobbyData.LocalPlayers)
        {
            if (localPlayer.IsActive)
            {
                return true;
            }
        }

        return false;
    }

    private Networking GetNetworking()
    {
        return GetNode<Networking>("/root/Networking");
    }

    private void ShowConfirmationOverlay(string title, string message, string confirmText, string cancelText, System.Action onConfirmed)
    {
        if (_confirmationOverlayScene == null)
        {
            GD.PushError($"Failed to load confirmation overlay scene at '{ConfirmationOverlayScenePath}'.");
            return;
        }

        var sceneOverlay = SceneOverlay.GetOrCreate(this);
        if (sceneOverlay == null)
        {
            return;
        }

        var confirmationOverlay = _confirmationOverlayScene.Instantiate<ConfirmationOverlay>();
        confirmationOverlay.Configure(title, message, confirmText, cancelText, onConfirmed);
        sceneOverlay.AddOverlay(confirmationOverlay, true);
    }

}
