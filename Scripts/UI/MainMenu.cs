using Godot;

public partial class MainMenu : Control {
    private const int LocalLobbySlotCount = 4;
    private const string HostServerMenuScenePath = "res://Scenes/UI/HostServerMenu.tscn";
    private const string JoinGameMenuScenePath = "res://Scenes/UI/JoinGameMenu.tscn";
    private const string SettingsMenuScenePath = "res://Scenes/UI/SettingsMenu.tscn";
    private const string ConfirmationOverlayScenePath = "res://Scenes/UI/ConfirmationOverlay.tscn";
    private const string XboxButtonXIconPath = "res://Assets/InputIcons/Xbox/button_x.svg";
    private const string XboxButtonYIconPath = "res://Assets/InputIcons/Xbox/button_y.svg";
    private const string KeyboardEnterIconPath = "res://Assets/InputIcons/Keyboard/enter.svg";
    private const string KeyboardEscIconPath = "res://Assets/InputIcons/Keyboard/esc.svg";
    private const string KeyboardBackspaceIconPath = "res://Assets/InputIcons/Keyboard/backspace.svg";
    private const string NetworkIconLanPath = "res://Assets/Network/NetworkModes/network_lan.svg";
    private const string NetworkIconClientPath = "res://Assets/Network/NetworkModes/network_client.svg";

    private PackedScene _confirmationOverlayScene;

    private LocalLobbyData LocalLobbyData => GetNetworking().LocalLobbyData;

    public override void _Ready() {
        UiInputActions.EnsureConfigured();
        _confirmationOverlayScene = GD.Load<PackedScene>(ConfirmationOverlayScenePath);
        GetNode<Button>("TopRightButtons/SettingsButton").Pressed += OnSettingsPressed;
        GetNode<Button>("TopRightButtons/ExitGameButton").Pressed += OnExitGamePressed;
        GetNode<Button>("MainLayout/ActionButtons/HostGameButton").Pressed += OnHostGamePressed;
        GetNode<Button>("MainLayout/ActionButtons/JoinGameButton").Pressed += OnJoinGamePressed;
        ApplyButtonIcons();
        EnsureDefaultLocalLobby();
        RefreshLocalLobbySlots();
        RefreshActionButtonsVisibility();
        CallDeferred(MethodName.RefreshDefaultFocus);
    }

    public override void _Input(InputEvent inputEvent) {
        if (inputEvent is InputEventKey { Pressed: true, Echo: false } keyEvent
            && (keyEvent.Keycode == Key.Enter || keyEvent.Keycode == Key.KpEnter)) {
            if (TryJoinKeyboardPlayer())
                GetViewport().SetInputAsHandled();

            return;
        }

        if (inputEvent is InputEventKey { Pressed: true, Echo: false } leaveKeyEvent
            && (leaveKeyEvent.Keycode == Key.Escape || leaveKeyEvent.Keycode == Key.Backspace)) {
            if (TryLeaveKeyboardPlayer())
                GetViewport().SetInputAsHandled();

            return;
        }

        if (inputEvent is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.X } joypadButtonEvent) {
            if (TryJoinGamepadPlayer(joypadButtonEvent.Device))
                GetViewport().SetInputAsHandled();

            return;
        }

        if (inputEvent is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.Y } leaveJoypadButtonEvent) {
            if (TryLeaveGamepadPlayer(leaveJoypadButtonEvent.Device))
                GetViewport().SetInputAsHandled();

            return;
        }
    }

    public void ConfigureKeyboardPlayer(int slotIndex) {
        ConfigureLocalPlayer(slotIndex, LocalPlayerData.LocalInputType.KeyboardMouse, -1);
    }

    public void ConfigureGamepadPlayer(int slotIndex, int deviceId) {
        ConfigureLocalPlayer(slotIndex, LocalPlayerData.LocalInputType.Gamepad, deviceId);
    }

    public void ClearLocalPlayerSlot(int slotIndex) {
        var localPlayer = GetLocalPlayer(slotIndex);
        localPlayer.IsActive = false;
        localPlayer.InputType = LocalPlayerData.LocalInputType.None;
        localPlayer.DeviceId = -1;
        RefreshLocalLobbySlots();
        RefreshActionButtonsVisibility();
    }

    private void ConfigureLocalPlayer(int slotIndex, LocalPlayerData.LocalInputType inputType, int deviceId) {
        var localPlayer = GetLocalPlayer(slotIndex);
        localPlayer.IsActive = true;
        localPlayer.InputType = inputType;
        localPlayer.DeviceId = deviceId;
        localPlayer.DisplayName = $"Player {slotIndex + 1}";
        RefreshLocalLobbySlots();
        RefreshActionButtonsVisibility();
    }

    private void EnsureDefaultLocalLobby() {
        for (var slotIndex = LocalLobbyData.LocalPlayers.Count; slotIndex < LocalLobbySlotCount; slotIndex++) {
            LocalLobbyData.LocalPlayers.Add(new LocalPlayerData {
                LocalId = slotIndex,
                DisplayName = $"Player {slotIndex + 1}",
            });
        }
    }

    private bool TryJoinKeyboardPlayer() {
        if (HasKeyboardPlayer())
            return false;

        var slotIndex = GetFirstOpenSlotIndex();
        if (slotIndex == -1)
            return false;

        ConfigureKeyboardPlayer(slotIndex);
        return true;
    }

    private bool TryLeaveKeyboardPlayer() {
        var slotIndex = GetKeyboardPlayerSlotIndex();
        if (slotIndex == -1)
            return false;

        ClearLocalPlayerSlot(slotIndex);
        return true;
    }

    private bool TryJoinGamepadPlayer(int deviceId) {
        if (HasGamepadPlayer(deviceId))
            return false;

        var slotIndex = GetFirstOpenSlotIndex();
        if (slotIndex == -1)
            return false;

        ConfigureGamepadPlayer(slotIndex, deviceId);
        return true;
    }

    private bool TryLeaveGamepadPlayer(int deviceId) {
        var slotIndex = GetGamepadPlayerSlotIndex(deviceId);
        if (slotIndex == -1)
            return false;

        ClearLocalPlayerSlot(slotIndex);
        return true;
    }

    private int GetFirstOpenSlotIndex() {
        for (var slotIndex = 0; slotIndex < LocalLobbySlotCount; slotIndex++) {
            if (!GetLocalPlayer(slotIndex).IsActive)
                return slotIndex;
        }

        return -1;
    }

    private bool HasKeyboardPlayer() {
        return GetKeyboardPlayerSlotIndex() != -1;
    }

    private int GetKeyboardPlayerSlotIndex() {
        foreach (var localPlayer in LocalLobbyData.LocalPlayers) {
            if (localPlayer.IsActive && localPlayer.InputType == LocalPlayerData.LocalInputType.KeyboardMouse)
                return localPlayer.LocalId;
        }

        return -1;
    }

    private bool HasGamepadPlayer(int deviceId) {
        return GetGamepadPlayerSlotIndex(deviceId) != -1;
    }

    private int GetGamepadPlayerSlotIndex(int deviceId) {
        foreach (var localPlayer in LocalLobbyData.LocalPlayers) {
            if (localPlayer.IsActive
                && localPlayer.InputType == LocalPlayerData.LocalInputType.Gamepad
                && localPlayer.DeviceId == deviceId) {
                return localPlayer.LocalId;
            }
        }

        return -1;
    }

    private LocalPlayerData GetLocalPlayer(int slotIndex) {
        while (LocalLobbyData.LocalPlayers.Count <= slotIndex) {
            LocalLobbyData.LocalPlayers.Add(new LocalPlayerData {
                LocalId = LocalLobbyData.LocalPlayers.Count,
                DisplayName = $"Player {LocalLobbyData.LocalPlayers.Count + 1}",
            });
        }

        return LocalLobbyData.LocalPlayers[slotIndex];
    }

    private void RefreshLocalLobbySlots() {
        var hasKeyboardPlayer = HasKeyboardPlayer();
        var nextOpenSlotIndex = GetFirstOpenSlotIndex();
        GetNode<Label>("MainLayout/LobbyPanel/LobbyHelpLabel").Text = "Use stick or d-pad to navigate. Select and cancel with the shown controller buttons.";

        for (var slotIndex = 0; slotIndex < LocalLobbySlotCount; slotIndex++) {
            var localPlayer = GetLocalPlayer(slotIndex);
            var slotPanel = GetNode<PanelContainer>($"MainLayout/LobbyPanel/LobbySlots/Slot{slotIndex + 1}");
            var isNextOpenSlot = slotIndex == nextOpenSlotIndex;
            slotPanel.Modulate = GetSlotColor(localPlayer, isNextOpenSlot);
            ReplaceSlotContent(slotPanel, CreateSlotContent(localPlayer, hasKeyboardPlayer, isNextOpenSlot));
        }
    }

    private Control CreateSlotContent(LocalPlayerData localPlayer, bool hasKeyboardPlayer, bool isNextOpenSlot) {
        var content = new VBoxContainer {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", 5);

        content.AddChild(CreateCenteredLabel(localPlayer.IsActive ? localPlayer.DisplayName : $"Player {localPlayer.LocalId + 1}", 18));

        if (!localPlayer.IsActive) {
            if (!isNextOpenSlot) {
                content.AddChild(CreateCenteredLabel("Waiting", 16));
                content.AddChild(CreateCenteredLabel("Empty", 16));
                return content;
            }

            content.AddChild(CreatePromptBlock("join", hasKeyboardPlayer ? new[] { XboxButtonXIconPath } : new[] { XboxButtonXIconPath, KeyboardEnterIconPath }));
            content.AddChild(CreateCenteredLabel("Empty", 16));
            return content;
        }

        content.AddChild(CreateCenteredLabel(GetInputName(localPlayer), 16));
        content.AddChild(CreatePromptBlock("leave", GetLeaveIconPaths(localPlayer)));
        content.AddChild(CreateCenteredLabel("In Lobby", 16));
        return content;
    }

    private static string GetInputName(LocalPlayerData localPlayer) {
        return localPlayer.InputType switch {
            LocalPlayerData.LocalInputType.KeyboardMouse => "Keyboard + Mouse",
            LocalPlayerData.LocalInputType.Gamepad => $"Gamepad {localPlayer.DeviceId}",
            _ => "No Input",
        };
    }

    private static string[] GetLeaveIconPaths(LocalPlayerData localPlayer) {
        return localPlayer.InputType switch {
            LocalPlayerData.LocalInputType.KeyboardMouse => new[] { KeyboardEscIconPath },
            LocalPlayerData.LocalInputType.Gamepad => new[] { XboxButtonYIconPath },
            _ => System.Array.Empty<string>(),
        };
    }

    private static Label CreateCenteredLabel(string text, int fontSize) {
        var label = new Label {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }

    private static VBoxContainer CreatePromptBlock(string action, string[] iconPaths) {
        var prompt = new VBoxContainer {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        prompt.AddThemeConstantOverride("separation", 2);
        prompt.AddChild(CreateCenteredLabel("Press", 15));

        var icons = new HBoxContainer {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        icons.AddThemeConstantOverride("separation", 6);
        foreach (var iconPath in iconPaths)
            icons.AddChild(CreatePromptIcon(iconPath));

        prompt.AddChild(icons);
        prompt.AddChild(CreateCenteredLabel($"to {action}", 15));
        return prompt;
    }

    private static TextureRect CreatePromptIcon(string iconPath) {
        return new TextureRect {
            Texture = GD.Load<Texture2D>(iconPath),
            CustomMinimumSize = new Vector2(34.0f, 34.0f),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }

    private static void ReplaceSlotContent(PanelContainer slotPanel, Control content) {
        foreach (var child in slotPanel.GetChildren()) {
            slotPanel.RemoveChild(child);
            child.QueueFree();
        }

        slotPanel.AddChild(content);
    }

    private static Color GetSlotColor(LocalPlayerData localPlayer, bool isNextOpenSlot) {
        if (localPlayer.IsActive)
            return new Color(0.55f, 1.0f, 0.6f);

        return isNextOpenSlot ? Colors.White : new Color(0.42f, 0.42f, 0.42f);
    }

    private void OnExitGamePressed() {
        ShowConfirmationOverlay(
            "Exit Game?",
            "Are you sure you want to quit Multiplayer Arena?",
            "Exit",
            "Stay",
            () => GetTree().Quit());
    }

    private void OnSettingsPressed() {
        GetTree().ChangeSceneToFile(SettingsMenuScenePath);
    }

    private void OnHostGamePressed() {
        if (!HasActiveLocalPlayer())
            return;

        GetTree().ChangeSceneToFile(HostServerMenuScenePath);
    }

    private void OnJoinGamePressed() {
        if (!HasActiveLocalPlayer())
            return;

        GetTree().ChangeSceneToFile(JoinGameMenuScenePath);
    }

    private void RefreshActionButtonsVisibility() {
        var hasActiveLocalPlayer = HasActiveLocalPlayer();
        GetNode<Button>("MainLayout/ActionButtons/HostGameButton").Visible = hasActiveLocalPlayer;

        var joinGameButton = GetNode<Button>("MainLayout/ActionButtons/JoinGameButton");
        joinGameButton.Visible = hasActiveLocalPlayer;
        RefreshDefaultFocus();
    }

    private void ApplyButtonIcons() {
        GetNode<Button>("MainLayout/ActionButtons/HostGameButton").Icon = GD.Load<Texture2D>(NetworkIconLanPath);
        GetNode<Button>("MainLayout/ActionButtons/JoinGameButton").Icon = GD.Load<Texture2D>(NetworkIconClientPath);
    }

    private void RefreshDefaultFocus() {
        var focusOwner = GetViewport().GuiGetFocusOwner();
        if (focusOwner != null && focusOwner.IsVisibleInTree())
            return;

        if (HasActiveLocalPlayer()) {
            GetNode<Button>("MainLayout/ActionButtons/HostGameButton").GrabFocus();
            return;
        }

        GetNode<Button>("TopRightButtons/ExitGameButton").GrabFocus();
    }

    private bool HasActiveLocalPlayer() {
        foreach (var localPlayer in LocalLobbyData.LocalPlayers) {
            if (localPlayer.IsActive)
                return true;
        }

        return false;
    }

    private Networking GetNetworking() {
        return GetNode<Networking>("/root/Networking");
    }

    private void ShowConfirmationOverlay(string title, string message, string confirmText, string cancelText, System.Action onConfirmed) {
        if (_confirmationOverlayScene == null) {
            GD.PushError($"Failed to load confirmation overlay scene at '{ConfirmationOverlayScenePath}'.");
            return;
        }

        var sceneOverlay = SceneOverlay.GetOrCreate(this);
        if (sceneOverlay == null)
            return;

        var confirmationOverlay = _confirmationOverlayScene.Instantiate<ConfirmationOverlay>();
        confirmationOverlay.Configure(title, message, confirmText, cancelText, onConfirmed);
        sceneOverlay.AddOverlay(confirmationOverlay, true);
    }

}
