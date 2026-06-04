using Godot;
using System.Linq;

public partial class AutoFeeder : Node2D
{
    public static AutoFeeder Instance { get; private set; }

    private const float FeedIntervalSec = 18f;
    private static readonly Vector2 FeederHalfSize = new(32f, 36f);

    private float _timer = FeedIntervalSec * 0.5f; // первый дроп через 22 сек
    private FoodData[] _catalog;
    private FoodData _selectedFood;

    private CanvasLayer _panelLayer;

    public override void _EnterTree() => Instance = this;
    public override void _ExitTree() { if (Instance == this) Instance = null; }

    public override void _Ready()
    {
        _catalog = ScanFoodCatalog();
        _selectedFood = _catalog.Length > 0 ? _catalog[0] : null;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var body = new Rect2(-FeederHalfSize, FeederHalfSize * 2f);
        DrawRect(body, new Color(0.18f, 0.35f, 0.55f, 0.92f), true);
        DrawRect(body, new Color(0.45f, 0.75f, 1f, 0.8f), false, 2f);

        // Горлышко кормушки (треугольник снизу)
        var spout = new Vector2[] {
            new(-10f, FeederHalfSize.Y),
            new(10f, FeederHalfSize.Y),
            new(0f, FeederHalfSize.Y + 12f)
        };
        DrawColoredPolygon(spout, new Color(0.18f, 0.35f, 0.55f, 0.92f));
        DrawPolyline(new Vector2[] { spout[0], spout[2], spout[1] }, new Color(0.45f, 0.75f, 1f, 0.8f), 2f);

        // Прогресс-полоска (насколько скоро следующий корм)
        var progress = Mathf.Clamp(_timer / FeedIntervalSec, 0f, 1f);
        var barBg = new Rect2(-FeederHalfSize.X + 6f, FeederHalfSize.Y - 10f, (FeederHalfSize.X * 2f) - 12f, 6f);
        DrawRect(barBg, new Color(0f, 0f, 0f, 0.4f), true);
        DrawRect(new Rect2(barBg.Position, new Vector2(barBg.Size.X * progress, barBg.Size.Y)),
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
            var bounds = new Rect2(-FeederHalfSize, FeederHalfSize * 2f + new Vector2(0, 12f));
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

        fd.SpawnFoodAt(_selectedFood, new Vector2(Position.X, Position.Y + FeederHalfSize.Y + 12f));
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
