using Godot;

public partial class QuestPanel : Control
{
    private Label _textLabel;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.TopLeft);
        GrowHorizontal = GrowDirection.End;
        GrowVertical = GrowDirection.End;
        Position = new Vector2(12f, 200f);
        CustomMinimumSize = new Vector2(220f, 0f);

        var panel = new PanelContainer();
        panel.SizeFlagsHorizontal = SizeFlags.Fill;
        panel.AddThemeStyleboxOverride("panel",
            UiTheme.BuildPanelStyle(
                new Color(0.08f, 0.18f, 0.3f, 0.92f),
                new Color(0.29f, 0.48f, 0.63f, 1f),
                2, 8));
        AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 5);
        margin.AddChild(vbox);

        var titleLabel = new Label
        {
            Text = "ЗАДАНИЕ",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        titleLabel.AddThemeColorOverride("font_color", new Color(0.67f, 0.78f, 1f));
        titleLabel.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(titleLabel);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", new Color(0.29f, 0.48f, 0.63f, 0.6f));
        vbox.AddChild(sep);

        _textLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(200f, 0f),
        };
        _textLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.95f, 1f));
        _textLabel.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(_textLabel);

        var qm = QuestManager.Instance;
        if (qm != null)
        {
            qm.QuestChanged += OnQuestChanged;
            OnQuestChanged(qm.CurrentQuestText);
        }
    }

    private void OnQuestChanged(string text)
    {
        if (_textLabel == null) return;
        Visible = !string.IsNullOrEmpty(text);
        if (Visible) _textLabel.Text = text;
    }

    public override void _ExitTree()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.QuestChanged -= OnQuestChanged;
    }
}
