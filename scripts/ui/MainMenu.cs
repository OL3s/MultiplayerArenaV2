using Godot;

public partial class MainMenu : Control {
    private const int LocalLobbySlotCount = 4;
    private const string HostServerMenuScenePath = "res://scenes/ui/menus/host_server_menu.tscn";
    private const string JoinGameMenuScenePath = "res://scenes/ui/menus/join_game_menu.tscn";
    private const string SettingsMenuScenePath = "res://scenes/ui/menus/settings_menu.tscn";
    private const string ConfirmationOverlayScenePath = "res://scenes/ui/overlays/confirmation_overlay.tscn";
    private const string TestScenesOverlayScenePath = "res://scenes/ui/overlays/test_scenes_overlay.tscn";
    private const string XboxButtonXIconPath = "res://assets/inputicons/xbox/button_x.svg";
    private const string KeyboardJoinIconPath = "res://assets/inputicons/keyboard/key_c.svg";
    private const string DeviceKeyboardMouseIconPath = "res://assets/inputicons/device_keyboard_mouse.svg";
    private const string DeviceGamepadIconPath = "res://assets/inputicons/device_gamepad.svg";
    private const string DeviceTouchIconPath = "res://assets/inputicons/device_touch.svg";
    private const string NetworkIconLanPath = "res://assets/network/networkmodes/network_lan.svg";
    private const string NetworkIconClientPath = "res://assets/network/networkmodes/network_client.svg";
    private const string TestScenesIconPath = "res://assets/ui/test_scenes.svg";
    private const string SettingsIconPath = "res://assets/ui/settings_cog.svg";
    private const string ExitIconPath = "res://assets/ui/exit_power.svg";
    private const string ResetIconPath = "res://assets/ui/reset_revert.svg";

    private PackedScene _confirmationOverlayScene;
    private PackedScene _testScenesOverlayScene;
    private double _joinPromptIconElapsed;
    private int _joinPromptIconIndex;

    private LocalLobbyData LocalLobbyData => GetNetworking().LocalLobbyData;

    public override void _Ready() {
        UiInputActions.EnsureConfigured();
        _confirmationOverlayScene = GD.Load<PackedScene>(ConfirmationOverlayScenePath);
        _testScenesOverlayScene = GD.Load<PackedScene>(TestScenesOverlayScenePath);
        GetNode<Button>("TopRightButtons/TestScenesButton").Pressed += OnTestScenesPressed;
        GetNode<Button>("TopRightButtons/SettingsButton").Pressed += OnSettingsPressed;
        GetNode<Button>("TopRightButtons/ExitGameButton").Pressed += OnExitGamePressed;
        GetNode<Button>("MainLayout/ActionButtons/HostGameButton").Pressed += OnHostGamePressed;
        GetNode<Button>("MainLayout/ActionButtons/JoinGameButton").Pressed += OnJoinGamePressed;
        GetNode<Button>("MainLayout/ActionButtons/ResetPlayersButton").Pressed += OnResetPlayersPressed;
        ConnectPlayerCardInput();
        ApplyButtonIcons();
        EnsureDefaultLocalLobby();
        RefreshLocalLobbySlots();
        RefreshActionButtonsVisibility();
        CallDeferred(MethodName.RefreshDefaultFocus);
    }

    public override void _Input(InputEvent inputEvent) {
        if (inputEvent is InputEventKey { Pressed: true, Echo: false } keyEvent
            && keyEvent.Keycode == Key.C) {
            if (TryJoinKeyboardPlayer())
                GetViewport().SetInputAsHandled();

            return;
        }

        if (inputEvent is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.X } joypadButtonEvent) {
            if (TryJoinGamepadPlayer(joypadButtonEvent.Device))
                GetViewport().SetInputAsHandled();

            return;
        }
    }

    public override void _Process(double delta) {
        var optionCount = GetJoinPromptOptionCount(HasKeyboardPlayer(), HasTouchPlayer());
        if (optionCount <= 1)
            return;

        _joinPromptIconElapsed += delta;
        if (_joinPromptIconElapsed < 2.0)
            return;

        _joinPromptIconElapsed = 0.0;
        _joinPromptIconIndex = (_joinPromptIconIndex + 1) % optionCount;
        RefreshLocalLobbySlots();
    }

    public bool ConfigureKeyboardPlayer(int slotIndex) {
        return ConfigureLocalPlayer(slotIndex, LocalPlayerData.LocalInputType.KeyboardMouse, -1);
    }

    public bool ConfigureGamepadPlayer(int slotIndex, int deviceId) {
        return ConfigureLocalPlayer(slotIndex, LocalPlayerData.LocalInputType.Gamepad, deviceId);
    }

    public bool ConfigureTouchPlayer(int slotIndex) {
        return ConfigureLocalPlayer(slotIndex, LocalPlayerData.LocalInputType.Touch, -1);
    }

    private bool ConfigureLocalPlayer(int slotIndex, LocalPlayerData.LocalInputType inputType, int deviceId) {
        if (slotIndex < 0 || slotIndex >= LocalLobbySlotCount) {
            GameLog.Print(GameLogScope.UI, GameLogType.Validation, "LocalPlayerJoinRejected", $"slot={slotIndex} input={inputType} device={deviceId} reason=invalidSlot");
            return false;
        }

        if (inputType == LocalPlayerData.LocalInputType.KeyboardMouse && HasKeyboardPlayer()) {
            GameLog.Print(GameLogScope.UI, GameLogType.Validation, "LocalPlayerJoinRejected", $"slot={slotIndex} input={inputType} device={deviceId} reason=keyboardAlreadyJoined");
            return false;
        }

        if (inputType == LocalPlayerData.LocalInputType.Touch && HasTouchPlayer()) {
            GameLog.Print(GameLogScope.UI, GameLogType.Validation, "LocalPlayerJoinRejected", $"slot={slotIndex} input={inputType} device={deviceId} reason=touchAlreadyJoined");
            return false;
        }

        if (inputType == LocalPlayerData.LocalInputType.Gamepad && HasGamepadPlayer(deviceId)) {
            GameLog.Print(GameLogScope.UI, GameLogType.Validation, "LocalPlayerJoinRejected", $"slot={slotIndex} input={inputType} device={deviceId} reason=gamepadAlreadyJoined");
            return false;
        }

        var localPlayer = GetLocalPlayer(slotIndex);
        if (localPlayer.IsActive) {
            GameLog.Print(GameLogScope.UI, GameLogType.Validation, "LocalPlayerJoinRejected", $"slot={slotIndex} input={inputType} device={deviceId} reason=slotActive");
            return false;
        }

        localPlayer.IsActive = true;
        localPlayer.InputType = inputType;
        localPlayer.DeviceId = deviceId;
        localPlayer.DisplayName = $"Player {slotIndex + 1}";
        GameLog.Print(GameLogScope.UI, GameLogType.StateChange, "LocalPlayerJoined", $"slot={slotIndex} localId={localPlayer.LocalId} input={inputType} device={deviceId} activePlayers={GetActiveLocalPlayerCount()}");
        RefreshLocalLobbySlots();
        RefreshActionButtonsVisibility();
        return true;
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
        if (HasKeyboardPlayer()) {
            GameLog.Print(GameLogScope.UI, GameLogType.Validation, "KeyboardJoinRejected", "reason=keyboardAlreadyJoined");
            return false;
        }

        var slotIndex = GetFirstOpenSlotIndex();
        if (slotIndex == -1) {
            GameLog.Print(GameLogScope.UI, GameLogType.Validation, "KeyboardJoinRejected", "reason=noOpenSlot");
            return false;
        }

        return ConfigureKeyboardPlayer(slotIndex);
    }

    private bool TryJoinGamepadPlayer(int deviceId) {
        if (HasGamepadPlayer(deviceId)) {
            GameLog.Print(GameLogScope.UI, GameLogType.Validation, "GamepadJoinRejected", $"device={deviceId} reason=gamepadAlreadyJoined");
            return false;
        }

        var slotIndex = GetFirstOpenSlotIndex();
        if (slotIndex == -1) {
            GameLog.Print(GameLogScope.UI, GameLogType.Validation, "GamepadJoinRejected", $"device={deviceId} reason=noOpenSlot");
            return false;
        }

        return ConfigureGamepadPlayer(slotIndex, deviceId);
    }

    private bool TryJoinTouchPlayer(int slotIndex) {
        if (HasTouchPlayer()) {
            GameLog.Print(GameLogScope.UI, GameLogType.Validation, "TouchJoinRejected", $"slot={slotIndex} reason=touchAlreadyJoined");
            return false;
        }

        if (slotIndex < 0 || slotIndex >= LocalLobbySlotCount) {
            GameLog.Print(GameLogScope.UI, GameLogType.Validation, "TouchJoinRejected", $"slot={slotIndex} reason=invalidSlot");
            return false;
        }

        var localPlayer = GetLocalPlayer(slotIndex);
        if (localPlayer.IsActive) {
            GameLog.Print(GameLogScope.UI, GameLogType.Validation, "TouchJoinRejected", $"slot={slotIndex} reason=slotActive");
            return false;
        }

        return ConfigureTouchPlayer(slotIndex);
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

    private bool HasTouchPlayer() {
        foreach (var localPlayer in LocalLobbyData.LocalPlayers) {
            if (localPlayer.IsActive && localPlayer.InputType == LocalPlayerData.LocalInputType.Touch)
                return true;
        }

        return false;
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
        var hasTouchPlayer = HasTouchPlayer();
        var nextOpenSlotIndex = GetFirstOpenSlotIndex();

        for (var slotIndex = 0; slotIndex < LocalLobbySlotCount; slotIndex++) {
            var localPlayer = GetLocalPlayer(slotIndex);
            var slot = GetNode<Control>($"MainLayout/PlayerCardsCenter/LobbySlotsPanel/Slot{slotIndex + 1}");
            var slotPanel = slot.GetNode<PanelContainer>("CardPanel");
            var isNextOpenSlot = slotIndex == nextOpenSlotIndex;
            slot.Visible = localPlayer.IsActive || isNextOpenSlot;

            ReplaceSlotContent(slot, slotPanel, CreateSlotContent(localPlayer, hasKeyboardPlayer, hasTouchPlayer, isNextOpenSlot), GetSlotBadgeText(localPlayer, isNextOpenSlot));
        }
    }

    private Control CreateSlotContent(LocalPlayerData localPlayer, bool hasKeyboardPlayer, bool hasTouchPlayer, bool isNextOpenSlot) {
        var content = new VBoxContainer {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", 5);

        if (!localPlayer.IsActive) {
            if (!isNextOpenSlot) {
                content.AddChild(CreateCenteredLabel("Waiting", 16));
                content.AddChild(CreateCenteredLabel("Empty", 16));
                return content;
            }

            content.AddChild(CreateJoinPrompt(GetJoinPromptIconPath(hasKeyboardPlayer, hasTouchPlayer), GetJoinPromptActionText(hasKeyboardPlayer, hasTouchPlayer)));
            content.AddChild(CreateCenteredLabel("Empty", 16));
            return content;
        }

        content.AddChild(CreateCenteredLabel(GetInputName(localPlayer), 16));
        content.AddChild(CreateDeviceIcon(GetDeviceIconPath(localPlayer)));
        content.AddChild(CreateCenteredLabel("In Lobby", 16));
        return content;
    }

    private static string GetInputName(LocalPlayerData localPlayer) {
        return localPlayer.InputType switch {
            LocalPlayerData.LocalInputType.KeyboardMouse => "Keyboard + Mouse",
            LocalPlayerData.LocalInputType.Gamepad => $"Gamepad {localPlayer.DeviceId}",
            LocalPlayerData.LocalInputType.Touch => "Touch Screen",
            _ => "No Input",
        };
    }

    private static string GetDeviceIconPath(LocalPlayerData localPlayer) {
        return localPlayer.InputType switch {
            LocalPlayerData.LocalInputType.KeyboardMouse => DeviceKeyboardMouseIconPath,
            LocalPlayerData.LocalInputType.Gamepad => DeviceGamepadIconPath,
            LocalPlayerData.LocalInputType.Touch => DeviceTouchIconPath,
            _ => string.Empty,
        };
    }

    private string GetJoinPromptIconPath(bool hasKeyboardPlayer, bool hasTouchPlayer) {
        var optionIndex = Mathf.PosMod(_joinPromptIconIndex, GetJoinPromptOptionCount(hasKeyboardPlayer, hasTouchPlayer));
        if (!hasKeyboardPlayer) {
            if (optionIndex == 0)
                return KeyboardJoinIconPath;

            optionIndex--;
        }

        if (optionIndex == 0)
            return XboxButtonXIconPath;

        return DeviceTouchIconPath;
    }

    private string GetJoinPromptActionText(bool hasKeyboardPlayer, bool hasTouchPlayer) {
        return GetJoinPromptIconPath(hasKeyboardPlayer, hasTouchPlayer) == DeviceTouchIconPath ? "Tap" : "Press";
    }

    private static int GetJoinPromptOptionCount(bool hasKeyboardPlayer, bool hasTouchPlayer) {
        var count = 1;
        if (!hasKeyboardPlayer)
            count++;
        if (!hasTouchPlayer)
            count++;
        return count;
    }

    private static string GetSlotBadgeText(LocalPlayerData localPlayer, bool isNextOpenSlot) {
        if (localPlayer.IsActive)
            return $"P{localPlayer.LocalId + 1}";

        return isNextOpenSlot ? "Unassigned" : string.Empty;
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

    private static PanelContainer CreateSlotBadge(string text) {
        var badge = new PanelContainer {
            CustomMinimumSize = text.Length <= 2 ? new Vector2(42.0f, 28.0f) : new Vector2(104.0f, 28.0f),
        };
        var style = new StyleBoxFlat {
            BgColor = new Color(0.16f, 0.19f, 0.24f),
            BorderColor = new Color(0.73f, 0.79f, 0.86f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomRight = 14,
            CornerRadiusBottomLeft = 14,
        };
        badge.AddThemeStyleboxOverride("panel", style);

        var label = new Label {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", text.Length <= 2 ? 15 : 13);
        badge.AddChild(label);
        return badge;
    }

    private static VBoxContainer CreateJoinPrompt(string iconPath, string actionText) {
        var prompt = new VBoxContainer {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        prompt.AddThemeConstantOverride("separation", 3);
        prompt.AddChild(CreateCenteredLabel(actionText, 15));
        prompt.AddChild(CreatePromptIcon(iconPath, 18.0f));
        return prompt;
    }

    private static TextureRect CreatePromptIcon(string iconPath, float size = 30.0f) {
        return new TextureRect {
            Texture = UiResourceLoader.LoadIconTexture(iconPath),
            CustomMinimumSize = new Vector2(size, size),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }

    private static TextureRect CreateDeviceIcon(string iconPath) {
        return new TextureRect {
            Texture = UiResourceLoader.LoadIconTexture(iconPath),
            CustomMinimumSize = new Vector2(54.0f, 38.0f),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }

    private static void ReplaceSlotContent(Control slot, PanelContainer slotPanel, Control content, string badgeText) {
        var badgeSlot = slot.GetNode<CenterContainer>("PlayerIdBadgeSlot");
        badgeSlot.Visible = false;
        foreach (var badgeChild in badgeSlot.GetChildren()) {
            badgeSlot.RemoveChild(badgeChild);
            badgeChild.QueueFree();
        }

        foreach (var child in slotPanel.GetChildren()) {
            slotPanel.RemoveChild(child);
            child.QueueFree();
        }

        slotPanel.AddChild(content);

        if (string.IsNullOrWhiteSpace(badgeText))
            return;

        badgeSlot.Visible = true;
        badgeSlot.AddChild(CreateSlotBadge(badgeText));
    }

    private void OnExitGamePressed() {
        ShowConfirmationOverlay(
            "Exit Game?",
            "Are you sure you want to quit Multiplayer Arena?",
            "Exit",
            "Stay",
            () => GetTree().Quit());
    }

    private void OnTestScenesPressed() {
        if (_testScenesOverlayScene == null) {
            GD.PushError($"Failed to load test scenes overlay scene at '{TestScenesOverlayScenePath}'.");
            return;
        }

        var sceneOverlay = SceneOverlay.GetOrCreate(this);
        if (sceneOverlay == null)
            return;

        sceneOverlay.AddOverlay(_testScenesOverlayScene.Instantiate<Control>(), true);
    }

    private void OnSettingsPressed() {
        GetTree().ChangeSceneToFile(SettingsMenuScenePath);
    }

    private void OnHostGamePressed() {
        GetTree().ChangeSceneToFile(HostServerMenuScenePath);
    }

    private void OnJoinGamePressed() {
        if (!HasActiveLocalPlayer())
            return;

        GetTree().ChangeSceneToFile(JoinGameMenuScenePath);
    }

    private void OnResetPlayersPressed() {
        var activePlayersBeforeReset = GetActiveLocalPlayerCount();
        foreach (var localPlayer in LocalLobbyData.LocalPlayers) {
            localPlayer.IsActive = false;
            localPlayer.InputType = LocalPlayerData.LocalInputType.None;
            localPlayer.DeviceId = -1;
        }

        GameLog.Print(GameLogScope.UI, GameLogType.StateChange, "LocalPlayersReset", $"clearedPlayers={activePlayersBeforeReset}");
        RefreshLocalLobbySlots();
        RefreshActionButtonsVisibility();
    }

    private void RefreshActionButtonsVisibility() {
        var hasActiveLocalPlayer = HasActiveLocalPlayer();
        GetNode<Button>("MainLayout/ActionButtons/HostGameButton").Visible = true;

        var joinGameButton = GetNode<Button>("MainLayout/ActionButtons/JoinGameButton");
        joinGameButton.Visible = hasActiveLocalPlayer;

        GetNode<Button>("MainLayout/ActionButtons/ResetPlayersButton").Visible = hasActiveLocalPlayer;
        RefreshDefaultFocus();
    }

    private void ApplyButtonIcons() {
        GetNode<Button>("TopRightButtons/TestScenesButton").Icon = UiResourceLoader.LoadIconTexture(TestScenesIconPath);
        GetNode<Button>("TopRightButtons/SettingsButton").Icon = UiResourceLoader.LoadIconTexture(SettingsIconPath);
        GetNode<Button>("TopRightButtons/ExitGameButton").Icon = UiResourceLoader.LoadIconTexture(ExitIconPath);
        GetNode<Button>("MainLayout/ActionButtons/HostGameButton").Icon = UiResourceLoader.LoadIconTexture(NetworkIconLanPath);
        GetNode<Button>("MainLayout/ActionButtons/JoinGameButton").Icon = UiResourceLoader.LoadIconTexture(NetworkIconClientPath);
        GetNode<Button>("MainLayout/ActionButtons/ResetPlayersButton").Icon = UiResourceLoader.LoadIconTexture(ResetIconPath);
    }

    private void ConnectPlayerCardInput() {
        for (var slotIndex = 0; slotIndex < LocalLobbySlotCount; slotIndex++) {
            var capturedSlotIndex = slotIndex;
            var slot = GetNode<Control>($"MainLayout/PlayerCardsCenter/LobbySlotsPanel/Slot{slotIndex + 1}");
            var slotPanel = slot.GetNode<PanelContainer>("CardPanel");
            slot.MouseFilter = MouseFilterEnum.Stop;
            slotPanel.MouseFilter = MouseFilterEnum.Stop;
            slot.GuiInput += inputEvent => OnPlayerCardInput(capturedSlotIndex, inputEvent);
            slotPanel.GuiInput += inputEvent => OnPlayerCardInput(capturedSlotIndex, inputEvent);
        }
    }

    private void OnPlayerCardInput(int slotIndex, InputEvent inputEvent) {
        var shouldJoin = inputEvent is InputEventScreenTouch { Pressed: true }
            || inputEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left };
        if (!shouldJoin)
            return;

        if (TryJoinTouchPlayer(slotIndex))
            GetViewport().SetInputAsHandled();
    }

    private void RefreshDefaultFocus() {
        var focusOwner = GetViewport().GuiGetFocusOwner();
        if (focusOwner != null && focusOwner.IsVisibleInTree())
            return;

        GetNode<Button>("MainLayout/ActionButtons/HostGameButton").GrabFocus();
    }

    private bool HasActiveLocalPlayer() {
        return GetActiveLocalPlayerCount() > 0;
    }

    private int GetActiveLocalPlayerCount() {
        var count = 0;
        foreach (var localPlayer in LocalLobbyData.LocalPlayers) {
            if (localPlayer.IsActive)
                count++;
        }

        return count;
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
