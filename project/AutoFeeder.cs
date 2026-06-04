using Godot;
using System.Linq;

public partial class AutoFeeder : Node2D
{
    public static AutoFeeder Instance { get; private set; }

    private const float FeedIntervalSec = 18f;

    private const float IconDisplaySize = 256f;
    private const float BarHeight       = 7f;
    private const float BarMargin       = 4f;

    // Половина ширины бара (чуть уже иконки)
    private static float HalfBarW => IconDisplaySize * 0.5f;

    private float _timer = FeedIntervalSec * 0.5f;
    private FoodData[] _catalog;
    private FoodData _selectedFood;
    private CanvasLayer _panelLayer;
    private Sprite2D _iconSprite;

    // Имена файлов иконок для каждого типа корма
    private const string IconBasic    = "res://assets/decor/feeder-basic.png";
    private const string IconGrowth   = "res://assets/decor/feeder-growth.png";
    private const string IconBreeding = "res://assets/decor/feeder-breeding.png";

    public override void _EnterTree() => Instance = this;
    public override void _ExitTree() { if (Instance == this) Instance = null; }

    public override void _Ready()
    {
        _catalog = ScanFoodCatalog();
        _selectedFood = _catalog.Length > 0 ? _catalog[0] : null;

        // иконка центрирована, бар прямо под ней
        _iconSprite = new Sprite2D { Position = new Vector2(0f, -( BarMargin + BarHeight) * 0.5f) };
        AddChild(_iconSprite);
        UpdateIcon();

        QueueRedraw();
    }

    private static string GetIconPath(FoodData food)
    {
        if (food == null) return IconBasic;
        if (food.BreedChanceBonus > 0f) return IconBreeding;
        if (food.GrowthMultiplier > 1f) return IconGrowth;
        return IconBasic;
    }

    private void UpdateIcon()
    {
        if (_iconSprite == null) return;
        var path = GetIconPath(_selectedFood);
        if (!ResourceLoader.Exists(path)) { _iconSprite.Texture = null; return; }
        var tex = GD.Load<Texture2D>(path);
        if (tex == null) { _iconSprite.Texture = null; return; }
        _iconSprite.Texture = tex;
        var scale = IconDisplaySize / Mathf.Max(tex.GetSize().X, tex.GetSize().Y);
        _iconSprite.Scale = new Vector2(scale, scale);
    }

    public override void _Draw()
    {
        // Только прогресс-бар под иконкой, никакого фона
        var barY  = IconDisplaySize * 0.5f + BarMargin - (BarMargin + BarHeight) * 0.5f;
        var barBg = new Rect2(-HalfBarW, barY, HalfBarW * 2f, BarHeight);
        DrawRect(barBg, new Color(0f, 0f, 0f, 0.45f), true);
        var progress = Mathf.Clamp(_timer / FeedIntervalSec, 0f, 1f);
        DrawRect(new Rect2(barBg.Position, new Vector2(barBg.Size.X * progress, BarHeight)),
            new Color(0.3f, 0.9f, 0.5f, 0.9f), true);
    }

    public override void _Process(double delta)
    {
        _timer += (float)delta;
        QueueRedraw();

        if (_timer >= FeedIntervalSec)
        {
            _timer = 0f;
            TryFeed();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
        {
            var localPos = ToLocal(GetViewport().GetMousePosition());
            var half = IconDisplaySize * 0.5f;
            var bounds = new Rect2(-half, -half, IconDisplaySize, IconDisplaySize + BarMargin + BarHeight);
            if (bounds.HasPoint(localPos))
            {
                TogglePanel();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void TryFeed()
    {
        var gm = GameManager.Instance;
        var fd = FoodDropper.Instance;
        if (gm == null || fd == null || _selectedFood == null) return;

        if (!gm.CanUseFood(_selectedFood))
        {
            if (!gm.TryBuyFood(_selectedFood)) return;
        }

        fd.SpawnFoodAt(_selectedFood, new Vector2(Position.X, Position.Y + IconDisplaySize * 0.5f + BarMargin + BarHeight + 8f));
        gm.ConsumeFood(_selectedFood);
    }

    // ── Панель выбора корма ─────────────────────────────────────────────────

    private void TogglePanel()
    {
        if (_panelLayer != null && IsInstanceValid(_panelLayer))
        {
            _panelLayer.QueueFree();
            _panelLayer = null;
            return;
        }
        OpenPanel();
    }

    private void OpenPanel()
    {
        _panelLayer = new CanvasLayer { Layer = 150 };
        GetTree().Root.AddChild(_panelLayer);

        var panel = new PanelContainer();
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.1f, 0.18f, 0.28f, 0.97f);
        style.BorderColor = new Color(0.45f, 0.75f, 1f, 0.8f);
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(8);
        style.SetContentMarginAll(12f);
        panel.AddThemeStyleboxOverride("panel", style);
        panel.Size = new Vector2(260f, 0f);
        panel.Position = new Vector2(Position.X - 10f, Position.Y - 200f).Clamp(
            Vector2.Zero, GetViewport().GetVisibleRect().Size - new Vector2(280f, 280f));
        _panelLayer.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        panel.AddChild(vbox);

        // Заголовок
        var title = new Label { Text = "Автокормушка" };
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", new Color(0.7f, 0.9f, 1f));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        var sep = new HSeparator();
        vbox.AddChild(sep);

        var hint = new Label { Text = "Выберите тип корма:", AutowrapMode = TextServer.AutowrapMode.Word };
        hint.AddThemeFontSizeOverride("font_size", 14);
        hint.AddThemeColorOverride("font_color", new Color(0.75f, 0.85f, 0.95f));
        vbox.AddChild(hint);

        // Кнопки еды
        foreach (var food in _catalog)
        {
            if (food == null) continue;
            var f = food;
            var btn = new Button();
            btn.Text = food.FoodName;
            btn.ToggleMode = true;
            btn.ButtonPressed = (_selectedFood == food);
            btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            btn.AddThemeFontSizeOverride("font_size", 15);
            btn.Pressed += () =>
            {
                _selectedFood = f;
                UpdateIcon();
                RefreshPanelButtons(_panelLayer);
            };
            vbox.AddChild(btn);
        }

        vbox.AddChild(new HSeparator());

        var closeBtn = new Button { Text = "Закрыть" };
        closeBtn.AddThemeFontSizeOverride("font_size", 15);
        closeBtn.Pressed += () => { _panelLayer?.QueueFree(); _panelLayer = null; };
        vbox.AddChild(closeBtn);
    }

    private void RefreshPanelButtons(CanvasLayer layer)
    {
        if (layer == null || !IsInstanceValid(layer)) return;
        var panel = layer.GetChildOrNull<PanelContainer>(0);
        var vbox = panel?.GetChildOrNull<VBoxContainer>(0);
        if (vbox == null) return;

        foreach (var child in vbox.GetChildren())
            if (child is Button btn && btn.ToggleMode)
                btn.ButtonPressed = (btn.Text == _selectedFood?.FoodName);
    }

    // ── Сканирование каталога еды ───────────────────────────────────────────

    private static FoodData[] ScanFoodCatalog()
    {
        const string dir = "res://assets/meal";
        var result = new Godot.Collections.Array<FoodData>();

        if (!DirAccess.DirExistsAbsolute(dir)) return System.Array.Empty<FoodData>();
        using var d = DirAccess.Open(dir);
        if (d == null) return System.Array.Empty<FoodData>();

        d.ListDirBegin();
        while (true)
        {
            var name = d.GetNext();
            if (string.IsNullOrEmpty(name)) break;
            if (d.CurrentIsDir()) continue;
            if (!name.EndsWith(".tres") && !name.EndsWith(".res")) continue;
            var food = GD.Load<FoodData>($"{dir}/{name}");
            if (food != null && !food.IsUnlimited) result.Add(food);
        }
        d.ListDirEnd();

        var arr = new FoodData[result.Count];
        for (var i = 0; i < result.Count; i++) arr[i] = result[i];
        return arr;
    }
}
