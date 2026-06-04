using Godot;

public partial class DialogueSystem : Node
{
    public static DialogueSystem Instance { get; private set; }

    // Portrait size & positioning constants
    private const int PortraitW  = 240;
    private const int PortraitH  = 340;
    private const int PanelH     = 250;
    private const int PortraitMarginRight = 30;

    private CanvasLayer _layer;
    private TextureRect _portrait;  // added first → renders behind the panel
    private DialogueBox _box;

    private DialogueLine[] _lines;
    private int _currentLine;
    private System.Action _onFinished;

    public bool IsActive => _box != null && _box.Visible;

    public override void _EnterTree() => Instance = this;

    public override void _Ready()
    {
        _layer = new CanvasLayer { Layer = 90 };
        AddChild(_layer);

        // Portrait — added first so it draws behind the panel
        _portrait = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(PortraitW, PortraitH),
            Size = new Vector2(PortraitW, PortraitH),
            Visible = false
        };
        _layer.AddChild(_portrait);

        // Panel — added second so it draws in front of portrait
        _box = new DialogueBox();
        _box.SetOnComplete(OnLineComplete);
        _layer.AddChild(_box);
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsActive) return;
        if (@event is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left) return;
        GetViewport().SetInputAsHandled();
        _box.AdvanceOrSkip();
    }

    public override void _Process(double delta)
    {
        // Keep portrait anchored to bottom-right relative to current viewport
        if (_portrait.Visible)
            PositionPortrait();
    }

    public void StartDialogue(DialogueLine[] lines, System.Action onFinished = null)
    {
        if (lines == null || lines.Length == 0)
        {
            onFinished?.Invoke();
            return;
        }
        _lines = lines;
        _currentLine = 0;
        _onFinished = onFinished;
        GameManager.Instance?.DuckMusic();
        ShowLine(_lines[0]);
    }

    public void Hide()
    {
        _box.Visible = false;
        _portrait.Visible = false;
    }

    // ── Private ─────────────────────────────────────────────────────────────

    private void ShowLine(DialogueLine line)
    {
        _box.ShowLine(line);

        if (line.CharacterSprite != null)
        {
            _portrait.Texture = line.CharacterSprite;
            _portrait.Visible = true;
            PositionPortrait();
        }
        else
        {
            _portrait.Visible = false;
        }
    }

    private void PositionPortrait()
    {
        var vp = GetViewport().GetVisibleRect();
        // Horizontally: right edge minus margin
        var x = vp.Size.X - PortraitW - PortraitMarginRight;
        // Vertically: portrait center sits at the top edge of the panel,
        // so upper half is visible, lower half is behind the panel
        var y = vp.Size.Y - PanelH - PortraitH / 2f;
        _portrait.Position = new Vector2(x, y);
    }

    private void OnLineComplete()
    {
        _currentLine++;
        if (_currentLine >= _lines.Length)
        {
            Hide();
            GameManager.Instance?.UnduckMusic();
            _onFinished?.Invoke();
            return;
        }
        ShowLine(_lines[_currentLine]);
    }
}
