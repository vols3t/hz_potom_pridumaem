using Godot;

public partial class DecorPlacer : Node
{
    public static DecorPlacer Instance { get; private set; }

    private DecorData _pendingDecor;
    private bool _isPlaceMode;
    private CanvasLayer _previewCanvas;
    private Sprite2D _previewSprite;
    private ColorRect _previewRect;
    private ColorRect _zoneHighlight;
    private Vector2 _halfSize;

    public override void _EnterTree() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    // Высота полосы у дна, куда можно ставить декорации
    private const float FloorZoneHeight = 60f;

    private static (float left, float right, float zoneTop, float bottom) GetPlacementZone()
    {
        float left, right, bottom;
        if (FoodDropper.Instance != null)
        {
            left   = FoodDropper.Instance.AquariumLeft;
            right  = FoodDropper.Instance.AquariumRight;
            bottom = FoodDropper.Instance.AquariumBottom;
        }
        else
        {
            left = 42f; right = 1395f; bottom = 640f;
        }
        return (left, right, bottom - FloorZoneHeight, bottom);
    }

    // Курсор = низ спрайта. Проверяем X по краям спрайта, Y — просто курсор внутри полосы.
    private bool IsValidPlacement(Vector2 cursorPos)
    {
        var (left, right, zoneTop, bottom) = GetPlacementZone();
        return cursorPos.X - _halfSize.X >= left
            && cursorPos.X + _halfSize.X <= right
            && cursorPos.Y >= zoneTop
            && cursorPos.Y <= bottom;
    }

    // Центр спрайта — halfSize.Y выше курсора (привязка снизу)
    private Vector2 SpriteCenter(Vector2 cursorPos) => cursorPos - new Vector2(0f, _halfSize.Y);

    public override void _Input(InputEvent @event)
    {
        if (!_isPlaceMode || _pendingDecor == null) return;

        if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed)
        {
            if (mouseBtn.ButtonIndex == MouseButton.Left)
            {
                var pos = GetViewport().GetMousePosition();
                if (IsValidPlacement(pos))
                {
                    GameManager.Instance?.PlaceDecor(_pendingDecor, SpriteCenter(pos));
                    EndPlaceMode();
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (mouseBtn.ButtonIndex == MouseButton.Right)
            {
                EndPlaceMode();
                GetViewport().SetInputAsHandled();
            }
        }

        if (@event is InputEventKey key && key.Keycode == Key.Escape && key.Pressed)
        {
            EndPlaceMode();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (!_isPlaceMode) return;
        var pos = GetViewport().GetMousePosition();
        var valid = IsValidPlacement(pos);
        var tint = valid
            ? new Color(1f, 1f, 1f, 0.75f)
            : new Color(1f, 0.35f, 0.35f, 0.75f);

        if (_previewSprite != null)
        {
            _previewSprite.Position = SpriteCenter(pos);
            _previewSprite.Modulate = tint;
        }
        if (_previewRect != null)
        {
            _previewRect.Position = SpriteCenter(pos) - _halfSize;
            _previewRect.Color = tint;
        }
    }

    public void StartPlaceMode(DecorData decor)
    {
        if (decor == null) return;

        _pendingDecor = decor;
        _isPlaceMode = true;

        _previewCanvas = new CanvasLayer { Layer = 200 };
        GetTree().Root.AddChild(_previewCanvas);

        var (left, right, zoneTop, bottom) = GetPlacementZone();
        _zoneHighlight = new ColorRect
        {
            Position = new Vector2(left, zoneTop),
            Size = new Vector2(right - left, bottom - zoneTop),
            Color = new Color(0.3f, 1f, 0.3f, 0.08f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _previewCanvas.AddChild(_zoneHighlight);

        var scale = decor.DisplayScale;
        if (decor.DecorTexture != null)
        {
            _previewSprite = new Sprite2D
            {
                Texture = decor.DecorTexture,
                Scale = scale,
                Modulate = new Color(1f, 1f, 1f, 0.75f)
            };
            _previewCanvas.AddChild(_previewSprite);
            _halfSize = decor.DecorTexture.GetSize() * scale * 0.5f;
        }
        else
        {
            var fallbackSize = new Vector2(64f, 64f);
            _previewRect = new ColorRect
            {
                Color = new Color(0.3f, 0.6f, 1f, 0.75f),
                Size = fallbackSize,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _previewCanvas.AddChild(_previewRect);
            _halfSize = fallbackSize * 0.5f;
        }

        if (decor.Icon != null)
        {
            const int CursorSize = 32;
            var img = decor.Icon.GetImage();
            img.Resize(CursorSize, CursorSize, Image.Interpolation.Nearest);
            var cursorTex = ImageTexture.CreateFromImage(img);
            Input.SetCustomMouseCursor(cursorTex, Input.CursorShape.Arrow, new Vector2(CursorSize * 0.5f, CursorSize * 0.5f));
        }
    }

    public void EndPlaceMode()
    {
        _isPlaceMode = false;
        _pendingDecor = null;
        _previewCanvas?.QueueFree();
        _previewCanvas = null;
        _previewSprite = null;
        _previewRect = null;
        _zoneHighlight = null;
        _halfSize = Vector2.Zero;
        Input.SetCustomMouseCursor(null);
    }

    public bool IsPlaceModeActive() => _isPlaceMode;
}
