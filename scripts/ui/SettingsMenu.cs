using Godot;

public partial class SettingsMenu : Control {
	private const string MainMenuScenePath = "res://scenes/ui/menus/main_menu.tscn";

	private CheckBox _networkDebugOverlayCheckBox;
	private TabContainer _tabContainer;
	private Button _applyButton;
	private Button _backButton;

	public override void _Ready() {
		UiInputActions.EnsureConfigured();
		BuildSettingsUi();
		LoadSettingsValues();
		CallDeferred(MethodName.FocusDefaultButton);
	}

	public override void _UnhandledInput(InputEvent inputEvent) {
		if (!inputEvent.IsActionPressed("ui_cancel"))
			return;

		GetViewport().SetInputAsHandled();
		OnBackPressed();
	}

	private void BuildSettingsUi() {
		var mainLayout = new VBoxContainer {
			Name = "MainLayout",
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -460.0f,
			OffsetTop = -300.0f,
			OffsetRight = 460.0f,
			OffsetBottom = 300.0f,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		mainLayout.AddThemeConstantOverride("separation", 20);
		AddChild(mainLayout);

		var titleLabel = new Label {
			Text = "Settings",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		titleLabel.AddThemeFontSizeOverride("font_size", 42);
		mainLayout.AddChild(titleLabel);

		_tabContainer = new TabContainer {
			Name = "TabContainer",
			CustomMinimumSize = new Vector2(820.0f, 390.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			FocusMode = Control.FocusModeEnum.All,
		};
		mainLayout.AddChild(_tabContainer);

		_tabContainer.AddChild(CreatePlaceholderTab("Video", "Video settings will include resolution, display mode, scaling, and visual quality."));
		_tabContainer.AddChild(CreatePlaceholderTab("Sound", "Sound settings will include master, music, effects, and voice levels."));
		_tabContainer.AddChild(CreateOnlineTab());
		_tabContainer.AddChild(CreatePlaceholderTab("Controls", "Controls settings will include keyboard, mouse, and gamepad bindings."));
		_tabContainer.AddChild(CreatePlaceholderTab("Gameplay", "Gameplay settings will include accessibility and match preference options."));

		var actionsLayout = new HBoxContainer {
			Name = "ActionsLayout",
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		actionsLayout.AddThemeConstantOverride("separation", 12);
		mainLayout.AddChild(actionsLayout);

		_applyButton = new Button {
			Name = "ApplyButton",
			Text = "Apply",
			CustomMinimumSize = new Vector2(180.0f, 42.0f),
			Disabled = true,
		};
		_applyButton.Pressed += OnApplyPressed;
		actionsLayout.AddChild(_applyButton);

		_backButton = new Button {
			Name = "BackButton",
			Text = "Back",
			CustomMinimumSize = new Vector2(180.0f, 42.0f),
		};
		_backButton.Pressed += OnBackPressed;
		actionsLayout.AddChild(_backButton);
	}

	private Control CreatePlaceholderTab(string tabName, string description) {
		var marginContainer = CreateTabRoot(tabName);
		var label = new Label {
			Text = description,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		marginContainer.AddChild(label);
		return marginContainer;
	}

	private Control CreateOnlineTab() {
		var marginContainer = CreateTabRoot("Online");
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 12);
		marginContainer.AddChild(layout);

		var descriptionLabel = new Label {
			Text = "Online and networking debug settings.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		layout.AddChild(descriptionLabel);

		_networkDebugOverlayCheckBox = new CheckBox {
			Text = "Show network debug icon and peer count",
		};
		_networkDebugOverlayCheckBox.Toggled += OnNetworkDebugOverlayToggled;
		layout.AddChild(_networkDebugOverlayCheckBox);

		return marginContainer;
	}

	private static MarginContainer CreateTabRoot(string tabName) {
		var marginContainer = new MarginContainer {
			Name = tabName,
		};
		marginContainer.AddThemeConstantOverride("margin_left", 24);
		marginContainer.AddThemeConstantOverride("margin_top", 24);
		marginContainer.AddThemeConstantOverride("margin_right", 24);
		marginContainer.AddThemeConstantOverride("margin_bottom", 24);
		return marginContainer;
	}

	private void LoadSettingsValues() {
		_networkDebugOverlayCheckBox.ButtonPressed = GetNetworking().SettingsConfig.ShowNetworkDebugOverlay;
		UpdateApplyButtonState();
	}

	private void OnNetworkDebugOverlayToggled(bool enabled) {
		UpdateApplyButtonState();
	}

	private void OnApplyPressed() {
		var networking = GetNetworking();
		networking.SetShowNetworkDebugOverlay(_networkDebugOverlayCheckBox.ButtonPressed);
		networking.SaveSettingsConfig();
		UpdateApplyButtonState();
	}

	private void UpdateApplyButtonState() {
		if (_applyButton == null || _networkDebugOverlayCheckBox == null)
			return;

		_applyButton.Disabled = _networkDebugOverlayCheckBox.ButtonPressed == GetNetworking().SettingsConfig.ShowNetworkDebugOverlay;
	}

	private void FocusDefaultButton() {
		UiFocusHelper.EnsureFocusWithin(this, new NodePath("MainLayout/TabContainer"), new NodePath("MainLayout/ActionsLayout/BackButton"));
	}

	private void OnBackPressed() {
		GetTree().ChangeSceneToFile(MainMenuScenePath);
	}

	private Networking GetNetworking() {
		return GetNode<Networking>("/root/Networking");
	}
}
