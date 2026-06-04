using Godot;

public partial class MainMenu : Control
{
    private static readonly float[] Strengths = { 18f, 36f };

    private TextureRect[] _layers;
    private Vector2[] _basePositions;
    private Vector2 _smoothMouse;
    private bool _mouseInit;

    private SettingsPanel _settingsPanel;

    public override void _Ready()
    {
        LayoutMode = 1;
        AnchorsPreset = (int)LayoutPreset.FullRect;
        AnchorRight = 1f;
        AnchorBottom = 1f;
        MouseFilter = MouseFilterEnum.Pass;

        BuildBackground();
        BuildParallaxLayers();
        BuildUi();
        BuildSettingsPanel();
    }

    public override void _Process(double delta)
    {
        if (_layers == null) return;

        var viewport = GetViewportRect();
        var center = viewport.Size / 2f;
        var mouse = GetViewport().GetMousePosition();

        if (!_mouseInit)
        {
            _smoothMouse = mouse;
            _mouseInit = true;
        }

        _smoothMouse = _smoothMouse.Lerp(mouse, (float)delta * 4f);

        // Normalised offset: -1..+1 per axis
        var offset = (_smoothMouse - center) / center;
        offset = offset.Clamp(new Vector2(-1f, -1f), new Vector2(1f, 1f));

        for (var i = 0; i < 2; i++)
        {
            if (_layers[i] == null) continue;
            var s = Strengths[i];
            _layers[i].Position = _basePositions[i] + new Vector2(offset.X * s, offset.Y * s * 0.65f);
        }
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private void BuildBackground()
    {
        var bg = new ColorRect { Color = new Color("060e1a") };
        bg.LayoutMode = 1;
        bg.AnchorRight = 1f;
        bg.AnchorBottom = 1f;
        AddChild(bg);
    }

    private void BuildParallaxLayers()
    {
        var vp = GetViewportRect().Size;
        var textures = new[]
        {
            GD.Load<Texture2D>("res://assets/menu/midbgmmenu.png"),
            GD.Load<Texture2D>("res://assets/menu/frontbdmenu.png")
        };
        _layers = new TextureRect[2];
        _basePositions = new Vector2[2];

        for (var i = 0; i < 2; i++)
        {
            var s = Strengths[i];
            // Layer is 2*s bigger on each axis so it never shows empty border
            var layerSize = vp + new Vector2(s * 2f, s * 2f);
            var basePos = new Vector2(-s, -s);

            var rect = new TextureRect
            {
                Texture = textures[i],
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                Size = layerSize,
                Position = basePos,
                LayoutMode = 3   // free / pixel placement
            };
            AddChild(rect);
            _layers[i] = rect;
            _basePositions[i] = basePos;
        }
    }

    private void BuildUi()
    {
        var center = new CenterContainer();
        center.LayoutMode = 1;
        center.AnchorRight = 1f;
        center.AnchorBottom = 1f;
        AddChild(center);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 18);
        center.AddChild(vbox);

        var title = new Label
        {
            Text = "little aquarium",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeColorOverride("font_color", new Color("c8e8ff"));
        title.AddThemeFontSizeOverride("font_size", 72);
        vbox.AddChild(title);

        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 36) });

        var newGameBtn = MakeButton("Новая игра");
        newGameBtn.Pressed += OnNewGame;
        vbox.AddChild(newGameBtn);

        var settingsBtn = MakeButton("Настройки");
        settingsBtn.Pressed += OnSettings;
        vbox.AddChild(settingsBtn);
    }

    private void BuildSettingsPanel()
    {
        _settingsPanel = new SettingsPanel { Visible = false };
        AddChild(_settingsPanel);
    }

    private static Button MakeButton(string text)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(300, 72),
            FocusMode = FocusModeEnum.None
        };
        btn.AddThemeStyleboxOverride("normal", UiTheme.BuildButtonStyle(new Color("1a3e5e"), new Color("4a7aa0"), 2, 12));
        btn.AddThemeStyleboxOverride("hover", UiTheme.BuildButtonStyle(new Color("235070"), new Color("7ab3d8"), 2, 12));
        btn.AddThemeStyleboxOverride("pressed", UiTheme.BuildButtonStyle(new Color("152e46"), new Color("7ab3d8"), 2, 12));
        btn.AddThemeColorOverride("font_color", new Color("e0f0ff"));
        btn.AddThemeFontSizeOverride("font_size", 28);
        return btn;
    }

    private void OnNewGame() => SceneManager.Instance.GoToAquarium();

    private void OnSettings() => _settingsPanel.OpenPanel();
}
