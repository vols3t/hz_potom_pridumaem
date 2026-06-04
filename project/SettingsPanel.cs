using Godot;

public partial class SettingsPanel : PanelContainer
{
    [Signal]
    public delegate void PanelClosedEventHandler();

    private static float _savedBrightness = 100f;
    private static float _savedSound = 80f;

    public static float SavedBrightness
    {
        get => _savedBrightness;
        set => _savedBrightness = Mathf.Clamp(value, 0f, 100f);
    }

    public static float SavedSound
    {
        get => _savedSound;
        set => _savedSound = Mathf.Clamp(value, 0f, 100f);
    }

    private HSlider _brightnessSlider;
    private Label _brightnessValue;
    private HSlider _soundSlider;
    private Label _soundValue;
    private Button _fullscreenBtn;
    private Button _windowedBtn;
    private bool _isUpdatingUi;
    private bool _uiBuilt;

    public override void _Ready()
    {
        ConfigureLayout();
        BuildUi();
        ApplyAllSavedSettings();
        VisibilityChanged += OnVisibilityChanged;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;
        if (@event.IsActionPressed("ui_cancel"))
        {
            ClosePanel();
            GetViewport().SetInputAsHandled();
        }
    }

    public void OpenPanel()
    {
        if (!IsInsideTree()) return;
        Visible = true;
        MoveToFront();
        ApplyAllSavedSettings();
    }

    public void ClosePanel()
    {
        if (!Visible) return;
        Visible = false;
        EmitSignal(SignalName.PanelClosed);
    }

    private void OnVisibilityChanged()
    {
        if (Visible) ApplyAllSavedSettings();
    }

    private void ConfigureLayout()
    {
        LayoutMode = 1;
        AnchorsPreset = (int)Control.LayoutPreset.FullRect;
        AnchorRight = 1f;
        AnchorBottom = 0.84f;
        OffsetLeft = 0f;
        OffsetTop = 0f;
        OffsetRight = 0f;
        OffsetBottom = -8f;
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.Both;
    }

    private void BuildUi()
    {
        AddThemeStyleboxOverride("panel", UiTheme.BuildPanelStyle(new Color("112b45"), new Color("2f4f73"), 3, 16));

        var rootMargin = new MarginContainer();
        rootMargin.AddThemeConstantOverride("margin_left", 14);
        rootMargin.AddThemeConstantOverride("margin_top", 14);
        rootMargin.AddThemeConstantOverride("margin_right", 14);
        rootMargin.AddThemeConstantOverride("margin_bottom", 14);
        AddChild(rootMargin);

        var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 10);
        rootMargin.AddChild(root);

        BuildHeader(root);
        BuildBody(root);
        _uiBuilt = true;
    }

    private void BuildHeader(VBoxContainer parent)
    {
        var header = new PanelContainer { CustomMinimumSize = new Vector2(0, 92) };
        header.AddThemeStyleboxOverride("panel", UiTheme.BuildPanelStyle(new Color("16314d"), new Color("35597e"), 2, 12));
        parent.AddChild(header);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        header.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        margin.AddChild(row);

        var title = new Label
        {
            Text = "Настройки",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        title.AddThemeColorOverride("font_color", new Color("e9f2ff"));
        title.AddThemeFontSizeOverride("font_size", 48);
        row.AddChild(title);

        var closeButton = new Button
        {
            Text = "X",
            CustomMinimumSize = new Vector2(44, 40),
            FocusMode = FocusModeEnum.None
        };
        closeButton.AddThemeStyleboxOverride("normal", UiTheme.BuildButtonStyle(new Color("274563"), new Color("7da6d1"), 2, 8));
        closeButton.AddThemeStyleboxOverride("hover", UiTheme.BuildButtonStyle(new Color("315679"), new Color("b1d7ff"), 2, 8));
        closeButton.AddThemeStyleboxOverride("pressed", UiTheme.BuildButtonStyle(new Color("1f3851"), new Color("b1d7ff"), 2, 8));
        closeButton.AddThemeColorOverride("font_color", new Color("eaf4ff"));
        closeButton.AddThemeFontSizeOverride("font_size", 24);
        closeButton.Pressed += ClosePanel;
        row.AddChild(closeButton);
    }

    private void BuildBody(VBoxContainer parent)
    {
        var panel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", UiTheme.BuildPanelStyle(new Color("15324e"), new Color("3a5f83"), 2, 12));
        parent.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        panel.AddChild(margin);

        var content = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 14);
        margin.AddChild(content);

        BuildSliderCard(content, "Яркость", out _brightnessSlider, out _brightnessValue);
        _brightnessSlider.ValueChanged += OnBrightnessChanged;

        BuildSliderCard(content, "Звук", out _soundSlider, out _soundValue);
        _soundSlider.ValueChanged += OnSoundChanged;

        BuildWindowModeCard(content);
    }

    private static void BuildSliderCard(VBoxContainer parent, string titleText, out HSlider slider, out Label valueLabel)
    {
        var card = new PanelContainer { CustomMinimumSize = new Vector2(0, 100) };
        card.AddThemeStyleboxOverride("panel", UiTheme.BuildPanelStyle(new Color("173550"), new Color("3f658a"), 2, 10));
        parent.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        card.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        margin.AddChild(row);

        var label = new Label
        {
            Text = titleText,
            CustomMinimumSize = new Vector2(180, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeColorOverride("font_color", new Color("edf4ff"));
        label.AddThemeFontSizeOverride("font_size", 30);
        row.AddChild(label);

        slider = new HSlider
        {
            MinValue = 0,
            MaxValue = 100,
            Step = 1,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        row.AddChild(slider);

        valueLabel = new Label
        {
            Text = "0%",
            CustomMinimumSize = new Vector2(86, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        valueLabel.AddThemeColorOverride("font_color", new Color("ffd66b"));
        valueLabel.AddThemeFontSizeOverride("font_size", 28);
        row.AddChild(valueLabel);
    }

    private void BuildWindowModeCard(VBoxContainer parent)
    {
        var card = new PanelContainer { CustomMinimumSize = new Vector2(0, 100) };
        card.AddThemeStyleboxOverride("panel", UiTheme.BuildPanelStyle(new Color("173550"), new Color("3f658a"), 2, 10));
        parent.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        card.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        margin.AddChild(row);

        var label = new Label
        {
            Text = "Экран",
            CustomMinimumSize = new Vector2(180, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeColorOverride("font_color", new Color("edf4ff"));
        label.AddThemeFontSizeOverride("font_size", 30);
        row.AddChild(label);

        _fullscreenBtn = new Button
        {
            Text = "Полный экран",
            ToggleMode = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 54)
        };
        _fullscreenBtn.AddThemeFontSizeOverride("font_size", 22);
        _fullscreenBtn.Pressed += OnFullscreenPressed;
        row.AddChild(_fullscreenBtn);

        _windowedBtn = new Button
        {
            Text = "Оконный",
            ToggleMode = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 54)
        };
        _windowedBtn.AddThemeFontSizeOverride("font_size", 22);
        _windowedBtn.Pressed += OnWindowedPressed;
        row.AddChild(_windowedBtn);

        RefreshWindowModeButtons();
    }

    private void OnFullscreenPressed()
    {
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
        RefreshWindowModeButtons();
    }

    private void OnWindowedPressed()
    {
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Maximized);
        RefreshWindowModeButtons();
    }

    private void RefreshWindowModeButtons()
    {
        if (_fullscreenBtn == null || _windowedBtn == null) return;
        var isFullscreen = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;
        _fullscreenBtn.SetPressedNoSignal(isFullscreen);
        _windowedBtn.SetPressedNoSignal(!isFullscreen);

        var activeStyle  = UiTheme.BuildButtonStyle(new Color("2b5b87"), new Color("7ebef4"), 2, 8);
        var normalStyle  = UiTheme.BuildButtonStyle(new Color("1d3d5c"), new Color("476c90"), 2, 8);
        _fullscreenBtn.AddThemeStyleboxOverride("normal", isFullscreen  ? activeStyle : normalStyle);
        _windowedBtn.AddThemeStyleboxOverride("normal",  !isFullscreen ? activeStyle : normalStyle);
        _fullscreenBtn.AddThemeColorOverride("font_color", isFullscreen  ? new Color("f5fcff") : new Color("d5e6fb"));
        _windowedBtn.AddThemeColorOverride("font_color",  !isFullscreen ? new Color("f5fcff") : new Color("d5e6fb"));
    }

    private void ApplyAllSavedSettings()
    {
        if (!_uiBuilt) return;
        _isUpdatingUi = true;
        _brightnessSlider.SetValueNoSignal(_savedBrightness);
        _soundSlider.SetValueNoSignal(_savedSound);
        _isUpdatingUi = false;
        ApplyBrightness(_savedBrightness);
        ApplySound(_savedSound);
        _brightnessValue.Text = $"{Mathf.RoundToInt(_savedBrightness)}%";
        _soundValue.Text = $"{Mathf.RoundToInt(_savedSound)}%";
        RefreshWindowModeButtons();
    }

    private void OnBrightnessChanged(double value)
    {
        _savedBrightness = Mathf.Clamp((float)value, 0f, 100f);
        _brightnessValue.Text = $"{Mathf.RoundToInt(_savedBrightness)}%";
        if (!_isUpdatingUi) ApplyBrightness(_savedBrightness);
    }

    private void OnSoundChanged(double value)
    {
        _savedSound = Mathf.Clamp((float)value, 0f, 100f);
        _soundValue.Text = $"{Mathf.RoundToInt(_savedSound)}%";
        if (!_isUpdatingUi) ApplySound(_savedSound);
    }

    private static void ApplyBrightness(float value)
    {
        SceneManager.Instance?.ApplyBrightness(value);
    }

    private static void ApplySound(float value)
    {
        var busIndex = AudioServer.GetBusIndex("Master");
        if (busIndex < 0) busIndex = 0;

        var linear = Mathf.Clamp(value / 100f, 0f, 1f);
        if (linear <= 0.0001f)
        {
            AudioServer.SetBusMute(busIndex, true);
            AudioServer.SetBusVolumeDb(busIndex, -80f);
        }
        else
        {
            AudioServer.SetBusMute(busIndex, false);
            AudioServer.SetBusVolumeDb(busIndex, Mathf.LinearToDb(linear));
        }
    }
}
