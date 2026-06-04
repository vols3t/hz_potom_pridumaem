using Godot;

// The text panel only — portrait is rendered as a separate node behind this panel.
public partial class DialogueBox : PanelContainer
{
    private Label _nameLabel;
    private RichTextLabel _textLabel;

    private string _fullText = "";       // plain text (no markup)
    private string _bbcodeText = "";     // bbcode version for display
    private float _charTimer;
    private bool _isTyping;
    private System.Action _onComplete;

    private const float CharsPerSec = 40f;
    private const int RightPad = 270;

    // Converts /m word \m → [color=#6ab4ff]word[/color]
    private static string ToBbcode(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var result = System.Text.RegularExpressions.Regex.Replace(
            raw,
            @"/m\s*(.*?)\s*\\m",
            "[color=#6ab4ff]$1[/color]");
        return result;
    }

    // Strips /m \m markers to get plain char count for typewriter
    private static string StripMarkers(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        return System.Text.RegularExpressions.Regex.Replace(raw, @"/m\s*|\s*\\m", "");
    }

    public override void _Ready()
    {
        BuildLayout();
        Visible = false;
    }

    private void BuildLayout()
    {
        // Anchor to bottom, full width
        LayoutMode = 1;
        AnchorLeft   = 0f;
        AnchorTop    = 1f;
        AnchorRight  = 1f;
        AnchorBottom = 1f;
        OffsetTop    = -250f;
        OffsetBottom = 0f;
        OffsetLeft   = 0f;
        OffsetRight  = 0f;
        MouseFilter  = MouseFilterEnum.Stop;

        AddThemeStyleboxOverride("panel", UiTheme.BuildPanelStyle(
            new Color(0.05f, 0.10f, 0.18f, 0.93f),
            new Color(0.28f, 0.45f, 0.65f, 1f), 2, 0));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   40);
        margin.AddThemeConstantOverride("margin_top",    18);
        margin.AddThemeConstantOverride("margin_right",  RightPad);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vbox);

        _nameLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _nameLabel.AddThemeColorOverride("font_color", new Color("ffd66b"));
        _nameLabel.AddThemeFontSizeOverride("font_size", 22);
        vbox.AddChild(_nameLabel);

        _textLabel = new RichTextLabel
        {
            BbcodeEnabled = true,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ScrollActive = false,
            VisibleCharacters = 0
        };
        _textLabel.AddThemeColorOverride("default_color", new Color("e8f4ff"));
        _textLabel.AddThemeFontSizeOverride("normal_font_size", 22);
        vbox.AddChild(_textLabel);
    }

    public void SetOnComplete(System.Action callback) => _onComplete = callback;

    public void ShowLine(DialogueLine line)
    {
        _nameLabel.Text = line.CharacterName ?? "";
        var raw = line.Text ?? "";
        _fullText = StripMarkers(raw);          // plain text for char count
        _bbcodeText = ToBbcode(raw);            // bbcode for display
        _charTimer = 0f;
        _isTyping = _fullText.Length > 0;
        _textLabel.Text = _bbcodeText;
        _textLabel.VisibleCharacters = 0;
        Visible = true;

        if (!_isTyping) _textLabel.VisibleCharacters = -1;
    }

    public override void _Process(double delta)
    {
        if (!_isTyping || !Visible) return;

        _charTimer += (float)delta * CharsPerSec;
        var newVisible = Mathf.Min((int)_charTimer, _fullText.Length);
        _textLabel.VisibleCharacters = newVisible;

        if (newVisible >= _fullText.Length)
        {
            _isTyping = false;
            _textLabel.VisibleCharacters = -1;
        }
    }

    public void AdvanceOrSkip()
    {
        if (!Visible) return;
        if (_isTyping)
        {
            _isTyping = false;
            _charTimer = _fullText.Length;
            _textLabel.VisibleCharacters = -1;
        }
        else
        {
            _onComplete?.Invoke();
        }
    }
}
