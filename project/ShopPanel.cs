using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

public partial class ShopPanel : PanelContainer
{
    [Signal]
    public delegate void ShopClosedEventHandler();

    [ExportCategory("Item Setup")]
    [Export] public PackedScene ShopItemScene;
    [Export] public FishData[] AvailableFish;

    // Legacy exports are kept for scene compatibility.
    [ExportCategory("UI References")]
    [Export] public VBoxContainer ItemList;
    [Export] public Button FishTabBtn;
    [Export] public Button FoodTabBtn;
    [Export] public Button OtherTabBtn;

    [ExportCategory("Spawning")]
    [Export] public Node2D Aquarium;
    [Export] public Vector2 SpawnAreaMin = new(100, 100);
    [Export] public Vector2 SpawnAreaMax = new(500, 400);

    private enum ShopCategory
    {
        Fish,
        Food,
        Decor
    }

    private sealed class ShopEntry
    {
        public readonly ShopCategory Category;
        public readonly string Id;
        public readonly string Name;
        public readonly string Description;
        public readonly int Price;
        public readonly Texture2D Icon;
        public readonly string Details;
        public readonly FishData FishTemplate;

        public ShopEntry(
            ShopCategory category,
            string id,
            string name,
            string description,
            int price,
            Texture2D icon,
            string details,
            FishData fishTemplate = null)
        {
            Category = category;
            Id = id;
            Name = name;
            Description = description;
            Price = price;
            Icon = icon;
            Details = details;
            FishTemplate = fishTemplate;
        }
    }

    private sealed class CategoryView
    {
        public PanelContainer Root;
        public GridContainer Grid;
        public Label PageLabel;
        public Button PrevButton;
        public Button NextButton;
        public Label SummaryLabel;
    }

    private const int ItemsPerPage = 6;
    private readonly Dictionary<ShopCategory, List<ShopEntry>> _entriesByCategory = new();
    private readonly Dictionary<ShopCategory, CategoryView> _viewsByCategory = new();
    private readonly Dictionary<ShopCategory, int> _pageByCategory = new();
    private readonly Dictionary<ShopCategory, Button> _topTabs = new();

    private Texture2D _fallbackIcon;
    private Label _coinsValueLabel;
    private Label _statusLabel;
    private TabContainer _categoryTabs;
    private ShopCategory _activeCategory = ShopCategory.Fish;
    private bool _uiBuilt;
    private int _lastDisplayedCoins = int.MinValue;

    public override void _Ready()
    {
        ConfigureFullscreenLayout();
        _fallbackIcon = LoadIcon("res://assets/fishes/medium/clown-fish-medium.png");

        BuildCatalog();
        BuildUi();
        RefreshAll();

        GameManager.Instance?.ConfigureAquarium(Aquarium, AvailableFish, SpawnAreaMin, SpawnAreaMax);
        VisibilityChanged += OnVisibilityChanged;
    }

    public override void _Process(double delta)
    {
        if (!_uiBuilt || !Visible)
            return;

        RefreshRuntimeState();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || @event == null)
            return;

        if (@event.IsActionPressed("ui_cancel"))
        {
            CloseShop();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnVisibilityChanged()
    {
        if (Visible)
            RefreshAll();
    }

    private void ConfigureFullscreenLayout()
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

    private void BuildCatalog()
    {
        _entriesByCategory.Clear();
        _pageByCategory.Clear();

        _entriesByCategory[ShopCategory.Fish] = BuildFishCatalog();
        _entriesByCategory[ShopCategory.Food] = BuildFoodCatalog();
        _entriesByCategory[ShopCategory.Decor] = BuildDecorCatalog();

        _pageByCategory[ShopCategory.Fish] = 0;
        _pageByCategory[ShopCategory.Food] = 0;
        _pageByCategory[ShopCategory.Decor] = 0;
    }

    private List<ShopEntry> BuildFishCatalog()
    {
        var validFish = GetValidFishTemplates();

        var names = new[]
        {
            "Клоун", "Неон", "Гуппи", "Скалярия", "Золотая рыбка", "Дискус",
            "Меченосец", "Тетра", "Кардинал", "Барбус", "Моллинезия", "Петушок"
        };

        var prices = new[] { 150, 200, 180, 300, 250, 400, 170, 210, 260, 190, 220, 280 };
        var entries = new List<ShopEntry>(names.Length);

        for (var i = 0; i < names.Length; i++)
        {
            var template = validFish.Count > 0 ? validFish[i % validFish.Count] : null;
            var icon = template?.Icon ?? _fallbackIcon;
            var rarityText = template != null ? ToRarityText(template.Rarity) : "обычная";
            var teenReward = template?.GetStageReward(FishGrowthStage.Teen) ?? 0;
            var adultReward = template?.GetStageReward(FishGrowthStage.Adult) ?? 0;

            entries.Add(new ShopEntry(
                ShopCategory.Fish,
                $"fish_{i}",
                names[i],
                $"Редкость: {rarityText}.",
                prices[i],
                icon,
                $"Рост: подросток +{teenReward}, взрослый +{adultReward}",
                template));
        }

        return entries;
    }

    private List<ShopEntry> BuildFoodCatalog()
    {
        var jarIcon = LoadIcon("res://assets/meal/meal-button.png", "res://assets/coins/coins.png");

        var names = new[]
        {
            "Хлопья", "Гранулы", "Мотыль", "Артемия", "Дафния", "Спирулина",
            "Микс-Про", "Витаминный корм", "Креветка", "Микропланктон", "Фито-паста", "Премиум-рацион"
        };

        var prices = new[] { 50, 75, 60, 80, 60, 70, 95, 110, 120, 85, 90, 140 };
        var details = new[]
        {
            "Базовый корм на каждый день",
            "Сбалансированный рацион для роста",
            "Живой белковый корм",
            "Ускоряет набор массы у мальков",
            "Повышает активность и подвижность",
            "Растительная поддержка иммунитета",
            "Комбо-корм для смешанной популяции",
            "Витамины для ускорения восстановления",
            "Высокий процент белка",
            "Подходит для мелкой рыбы",
            "Добавка для яркости окраса",
            "Максимальная питательность"
        };

        var entries = new List<ShopEntry>(names.Length);
        for (var i = 0; i < names.Length; i++)
        {
            entries.Add(new ShopEntry(
                ShopCategory.Food,
                $"food_{i}",
                names[i],
                "Корм для обитателей аквариума.",
                prices[i],
                jarIcon ?? _fallbackIcon,
                details[i]));
        }

        return entries;
    }

    private List<ShopEntry> BuildDecorCatalog()
    {
        var decorIcons = new[]
        {
            LoadIcon("res://assets/settings/settings-button.png"),
            LoadIcon("res://assets/store/store-button.png"),
            LoadIcon("res://assets/bestiary/bestiary-button.png"),
            LoadIcon("res://assets/my-fishes-button.png")
        };

        var names = new[]
        {
            "Замок", "Коряга", "Амфора", "Растения (выс.)", "Растения (сред.)", "Камни",
            "Коралловый риф", "Грот", "Мини-руины", "Мостик", "Жемчужная раковина", "Каменная арка"
        };

        var prices = new[] { 500, 300, 250, 150, 100, 120, 420, 280, 240, 260, 340, 390 };
        var details = new[]
        {
            "Эпичный акцент для центра аквариума",
            "Натуральное укрытие для рыб",
            "Классический античный декор",
            "Высокие растения для фона",
            "Средние кусты для середины сцены",
            "Каменная группа для дна",
            "Яркая рифовая композиция",
            "Укрытие в форме пещеры",
            "Старинный стиль для дизайна",
            "Добавляет глубину композиции",
            "Редкая декоративная деталь",
            "Большой архитектурный элемент"
        };

        var entries = new List<ShopEntry>(names.Length);
        for (var i = 0; i < names.Length; i++)
        {
            entries.Add(new ShopEntry(
                ShopCategory.Decor,
                $"decor_{i}",
                names[i],
                "Декорация для оформления аквариума.",
                prices[i],
                decorIcons[i % decorIcons.Length] ?? _fallbackIcon,
                details[i]));
        }

        return entries;
    }

    private List<FishData> GetValidFishTemplates()
    {
        var result = new List<FishData>();
        if (AvailableFish == null)
            return result;

        foreach (var fish in AvailableFish)
        {
            if (fish != null && fish.FishScene != null)
                result.Add(fish);
        }

        return result;
    }

    private static string ToRarityText(FishRarity rarity)
    {
        return rarity switch
        {
            FishRarity.Common => "обычная",
            FishRarity.Rare => "редкая",
            FishRarity.Unique => "уникальная",
            _ => "обычная"
        };
    }

    private void BuildUi()
    {
        var legacyChildren = GetChildren();
        foreach (var child in legacyChildren)
        {
            if (child is Control control)
            {
                control.Visible = false;
                control.MouseFilter = MouseFilterEnum.Ignore;
            }
        }

        AddThemeStyleboxOverride("panel", BuildPanelStyle(new Color("112b45"), new Color("2f4f73"), 3, 16));

        var rootMargin = new MarginContainer();
        rootMargin.AddThemeConstantOverride("margin_left", 14);
        rootMargin.AddThemeConstantOverride("margin_top", 14);
        rootMargin.AddThemeConstantOverride("margin_right", 14);
        rootMargin.AddThemeConstantOverride("margin_bottom", 14);
        AddChild(rootMargin);

        var rootVBox = new VBoxContainer();
        rootVBox.AddThemeConstantOverride("separation", 10);
        rootMargin.AddChild(rootVBox);

        BuildHeader(rootVBox);
        BuildCategoryArea(rootVBox);

        _uiBuilt = true;
    }

    private void BuildHeader(VBoxContainer parent)
    {
        var headerPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 92)
        };
        headerPanel.AddThemeStyleboxOverride("panel", BuildPanelStyle(new Color("16314d"), new Color("35597e"), 2, 12));
        parent.AddChild(headerPanel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        headerPanel.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        margin.AddChild(row);

        var coinsPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(340, 64)
        };
        coinsPanel.AddThemeStyleboxOverride("panel", BuildPanelStyle(new Color("1a3a58"), new Color("4e7092"), 2, 10));
        row.AddChild(coinsPanel);

        var coinsRow = new HBoxContainer();
        coinsRow.AddThemeConstantOverride("separation", 8);
        coinsPanel.AddChild(coinsRow);

        var coinsTitle = new Label
        {
            Text = "Coins:",
            VerticalAlignment = VerticalAlignment.Center
        };
        coinsTitle.AddThemeColorOverride("font_color", new Color("f5f0da"));
        coinsTitle.AddThemeFontSizeOverride("font_size", 36);
        coinsRow.AddChild(coinsTitle);

        _coinsValueLabel = new Label
        {
            Text = "0",
            HorizontalAlignment = HorizontalAlignment.Right,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        _coinsValueLabel.AddThemeColorOverride("font_color", new Color("ffd66b"));
        _coinsValueLabel.AddThemeFontSizeOverride("font_size", 38);
        coinsRow.AddChild(_coinsValueLabel);

        var title = new Label
        {
            Text = "магазин",
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
            TooltipText = "Закрыть магазин (Esc)",
            CustomMinimumSize = new Vector2(44, 40),
            FocusMode = FocusModeEnum.None
        };
        closeButton.AddThemeStyleboxOverride("normal", BuildButtonStyle(new Color("274563"), new Color("7da6d1"), 2, 8));
        closeButton.AddThemeStyleboxOverride("hover", BuildButtonStyle(new Color("315679"), new Color("b1d7ff"), 2, 8));
        closeButton.AddThemeStyleboxOverride("pressed", BuildButtonStyle(new Color("1f3851"), new Color("b1d7ff"), 2, 8));
        closeButton.AddThemeColorOverride("font_color", new Color("eaf4ff"));
        closeButton.AddThemeFontSizeOverride("font_size", 24);
        closeButton.Pressed += CloseShop;
        row.AddChild(closeButton);

        _statusLabel = new Label
        {
            Text = "Откройте нужную категорию и выберите товар",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _statusLabel.AddThemeColorOverride("font_color", new Color("8fb4d8"));
        _statusLabel.AddThemeFontSizeOverride("font_size", 18);
        parent.AddChild(_statusLabel);
    }

    private void BuildCategoryArea(VBoxContainer parent)
    {
        var panel = new PanelContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride("panel", BuildPanelStyle(new Color("15324e"), new Color("3a5f83"), 2, 12));
        parent.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        panel.AddChild(margin);

        var content = new VBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 8);
        margin.AddChild(content);

        var topTabsRow = new HBoxContainer();
        topTabsRow.AddThemeConstantOverride("separation", 8);
        content.AddChild(topTabsRow);

        CreateTopTab(topTabsRow, ShopCategory.Fish, "рыбки");
        CreateTopTab(topTabsRow, ShopCategory.Food, "еда");
        CreateTopTab(topTabsRow, ShopCategory.Decor, "декорации");

        _categoryTabs = new TabContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            TabsVisible = false
        };
        content.AddChild(_categoryTabs);

        BuildCategoryView(_categoryTabs, ShopCategory.Fish, "рыбки");
        BuildCategoryView(_categoryTabs, ShopCategory.Food, "еда");
        BuildCategoryView(_categoryTabs, ShopCategory.Decor, "декорации");
    }

    private void CreateTopTab(HBoxContainer parent, ShopCategory category, string text)
    {
        var tab = new Button
        {
            Text = text,
            ToggleMode = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 56)
        };

        tab.AddThemeFontSizeOverride("font_size", 28);
        tab.Pressed += () => SetActiveCategory(category);
        parent.AddChild(tab);
        _topTabs[category] = tab;
    }

    private void BuildCategoryView(Container container, ShopCategory category, string title)
    {
        var root = new PanelContainer
        {
            Name = category.ToString(),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true
        };
        root.AddThemeStyleboxOverride("panel", BuildPanelStyle(new Color("17324d"), new Color("3c5d80"), 2, 12));
        container.AddChild(root);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        root.AddChild(margin);

        var content = new VBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 8);
        margin.AddChild(content);

        var titleLabel = new Label
        {
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(0, 30)
        };
        titleLabel.AddThemeColorOverride("font_color", new Color("edf4ff"));
        titleLabel.AddThemeFontSizeOverride("font_size", 24);
        content.AddChild(titleLabel);

        var grid = new GridContainer
        {
            Columns = 3,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        content.AddChild(grid);

        var navRow = new HBoxContainer();
        navRow.AddThemeConstantOverride("separation", 10);
        content.AddChild(navRow);

        var prev = new Button
        {
            Text = "<",
            CustomMinimumSize = new Vector2(54, 44)
        };
        var next = new Button
        {
            Text = ">",
            CustomMinimumSize = new Vector2(54, 44)
        };

        prev.AddThemeStyleboxOverride("normal", BuildButtonStyle(new Color("1d3d5c"), new Color("476c90"), 2, 8));
        prev.AddThemeStyleboxOverride("hover", BuildButtonStyle(new Color("24496d"), new Color("6d8fb1"), 2, 8));
        next.AddThemeStyleboxOverride("normal", BuildButtonStyle(new Color("1d3d5c"), new Color("476c90"), 2, 8));
        next.AddThemeStyleboxOverride("hover", BuildButtonStyle(new Color("24496d"), new Color("6d8fb1"), 2, 8));
        prev.AddThemeFontSizeOverride("font_size", 30);
        next.AddThemeFontSizeOverride("font_size", 30);
        prev.AddThemeColorOverride("font_color", new Color("dbe9ff"));
        next.AddThemeColorOverride("font_color", new Color("dbe9ff"));

        var page = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        page.AddThemeColorOverride("font_color", new Color("d3e4f7"));
        page.AddThemeFontSizeOverride("font_size", 26);

        prev.Pressed += () => ChangePage(category, -1);
        next.Pressed += () => ChangePage(category, 1);

        navRow.AddChild(prev);
        navRow.AddChild(page);
        navRow.AddChild(next);

        var summary = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        summary.AddThemeColorOverride("font_color", new Color("8fb4d8"));
        summary.AddThemeFontSizeOverride("font_size", 16);
        content.AddChild(summary);

        _viewsByCategory[category] = new CategoryView
        {
            Root = root,
            Grid = grid,
            PageLabel = page,
            PrevButton = prev,
            NextButton = next,
            SummaryLabel = summary
        };
    }

    private void SetActiveCategory(ShopCategory category)
    {
        _activeCategory = category;

        foreach (var pair in _viewsByCategory)
        {
            var isActive = pair.Key == _activeCategory;
            var panel = pair.Value.Root;
            var bg = isActive ? new Color("1e3f62") : new Color("17324d");
            var border = isActive ? new Color("79b4e8") : new Color("3c5d80");
            panel.AddThemeStyleboxOverride("panel", BuildPanelStyle(bg, border, isActive ? 3 : 2, 12));
            panel.SelfModulate = isActive ? Colors.White : new Color(0.9f, 0.94f, 1f, 0.95f);
        }

        foreach (var pair in _topTabs)
            ApplyCategoryTabStyle(pair.Value, pair.Key == _activeCategory);

        if (_categoryTabs != null)
            _categoryTabs.CurrentTab = (int)_activeCategory;
    }

    private static void ApplyCategoryTabStyle(Button button, bool isActive)
    {
        if (button == null)
            return;

        button.SetPressedNoSignal(isActive);
        button.AddThemeStyleboxOverride(
            "normal",
            BuildButtonStyle(
                isActive ? new Color("2b5b87") : new Color("1d3d5c"),
                isActive ? new Color("7ebef4") : new Color("476c90"),
                2,
                10));
        button.AddThemeStyleboxOverride(
            "hover",
            BuildButtonStyle(
                isActive ? new Color("356d9f") : new Color("24496d"),
                isActive ? new Color("a7dbff") : new Color("6d8fb1"),
                2,
                10));
        button.AddThemeStyleboxOverride(
            "pressed",
            BuildButtonStyle(
                isActive ? new Color("224b71") : new Color("1a3856"),
                isActive ? new Color("a7dbff") : new Color("6d8fb1"),
                2,
                10));
        button.AddThemeColorOverride("font_color", isActive ? new Color("f5fcff") : new Color("d5e6fb"));
    }

    public void CloseShop()
    {
        if (!Visible)
            return;

        Visible = false;
        EmitSignal(SignalName.ShopClosed);
    }

    private void RefreshAll()
    {
        if (!_uiBuilt)
            return;

        RefreshRuntimeState(forceRebuildPages: true);
        SetActiveCategory(_activeCategory);
    }

    private void RefreshRuntimeState(bool forceRebuildPages = false)
    {
        var gm = GameManager.Instance;
        var coins = gm != null ? Mathf.RoundToInt(gm.Money) : 0;

        if (_coinsValueLabel != null)
            _coinsValueLabel.Text = FormatCoins(coins);

        if (_statusLabel != null && gm != null && !string.IsNullOrWhiteSpace(gm.LastEventText))
            _statusLabel.Text = gm.LastEventText;

        if (forceRebuildPages || coins != _lastDisplayedCoins)
        {
            _lastDisplayedCoins = coins;
            RefreshAllCategoryPages();
        }
    }

    private void RefreshAllCategoryPages()
    {
        RefreshCategoryPage(ShopCategory.Fish);
        RefreshCategoryPage(ShopCategory.Food);
        RefreshCategoryPage(ShopCategory.Decor);
    }

    private void ChangePage(ShopCategory category, int delta)
    {
        if (!_entriesByCategory.TryGetValue(category, out var entries))
            return;

        var pageCount = Math.Max(1, Mathf.CeilToInt(entries.Count / (float)ItemsPerPage));
        var current = _pageByCategory.TryGetValue(category, out var value) ? value : 0;
        current = Mathf.Clamp(current + delta, 0, pageCount - 1);
        _pageByCategory[category] = current;

        RefreshCategoryPage(category);
    }

    private void RefreshCategoryPage(ShopCategory category)
    {
        if (!_viewsByCategory.TryGetValue(category, out var view))
            return;
        if (!_entriesByCategory.TryGetValue(category, out var entries))
            return;

        var currentPage = _pageByCategory.TryGetValue(category, out var page) ? page : 0;
        var pageCount = Math.Max(1, Mathf.CeilToInt(entries.Count / (float)ItemsPerPage));
        currentPage = Mathf.Clamp(currentPage, 0, pageCount - 1);
        _pageByCategory[category] = currentPage;

        foreach (var child in view.Grid.GetChildren())
            child.QueueFree();

        var startIndex = currentPage * ItemsPerPage;
        var endExclusive = Math.Min(startIndex + ItemsPerPage, entries.Count);
        for (var i = startIndex; i < endExclusive; i++)
            view.Grid.AddChild(CreateCard(entries[i]));

        for (var i = endExclusive; i < startIndex + ItemsPerPage; i++)
        {
            var spacer = new Control
            {
                CustomMinimumSize = new Vector2(126, 168),
                MouseFilter = MouseFilterEnum.Ignore
            };
            view.Grid.AddChild(spacer);
        }

        view.PageLabel.Text = $"{currentPage + 1} / {pageCount}";
        view.PrevButton.Disabled = currentPage <= 0;
        view.NextButton.Disabled = currentPage >= pageCount - 1;

        view.SummaryLabel.Text = category switch
        {
            ShopCategory.Fish => "Покупка: рыба сразу появляется в аквариуме",
            ShopCategory.Food => $"Куплено корма: {GameManager.Instance?.GetOwnedCategoryCount("food") ?? 0}",
            ShopCategory.Decor => $"Куплено декора: {GameManager.Instance?.GetOwnedCategoryCount("decor") ?? 0}",
            _ => string.Empty
        };
    }

    private Control CreateCard(ShopEntry entry)
    {
        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(126, 168),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        card.AddThemeStyleboxOverride("panel", BuildPanelStyle(new Color("173550"), new Color("3f658a"), 2, 8));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 6);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_right", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        card.AddChild(margin);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 3);
        margin.AddChild(content);

        var name = new Label
        {
            Text = entry.Name,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        name.AddThemeColorOverride("font_color", new Color("eef6ff"));
        name.AddThemeFontSizeOverride("font_size", 15);
        content.AddChild(name);

        var icon = new TextureRect
        {
            Texture = entry.Icon ?? _fallbackIcon,
            CustomMinimumSize = new Vector2(80, 52),
            ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        content.AddChild(icon);

        var description = new Label
        {
            Text = entry.Description,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        description.AddThemeColorOverride("font_color", new Color("9cc1e5"));
        description.AddThemeFontSizeOverride("font_size", 11);
        content.AddChild(description);

        var details = new Label
        {
            Text = entry.Details,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        details.AddThemeColorOverride("font_color", new Color("7fa4c8"));
        details.AddThemeFontSizeOverride("font_size", 10);
        content.AddChild(details);

        var price = new Label
        {
            Text = $" {entry.Price}",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        price.AddThemeColorOverride("font_color", new Color("ffd66b"));
        price.AddThemeFontSizeOverride("font_size", 18);
        content.AddChild(price);

        var buyButton = new Button
        {
            Text = "купить",
            CustomMinimumSize = new Vector2(0, 28)
        };
        buyButton.AddThemeStyleboxOverride("normal", BuildButtonStyle(new Color("4d833f"), new Color("8eb16a"), 2, 8));
        buyButton.AddThemeStyleboxOverride("hover", BuildButtonStyle(new Color("5a9649"), new Color("b7d48f"), 2, 8));
        buyButton.AddThemeStyleboxOverride("pressed", BuildButtonStyle(new Color("406f35"), new Color("b7d48f"), 2, 8));
        buyButton.AddThemeStyleboxOverride("disabled", BuildButtonStyle(new Color("3a4f38"), new Color("576852"), 2, 8));
        buyButton.AddThemeColorOverride("font_color", new Color("eaf4db"));
        buyButton.AddThemeFontSizeOverride("font_size", 17);

        var canAfford = GameManager.Instance?.CanAfford(entry.Price) ?? false;
        var validFishOffer = entry.Category != ShopCategory.Fish || entry.FishTemplate != null;
        buyButton.Disabled = !canAfford || !validFishOffer;
        if (!canAfford)
            buyButton.Text = "дорого";

        buyButton.Pressed += () => TryBuyEntry(entry);
        content.AddChild(buyButton);

        if (entry.Category != ShopCategory.Fish)
        {
            var ownedCount = GameManager.Instance?.GetOwnedShopItemCount(ToStorageCategory(entry.Category), entry.Name) ?? 0;
            var ownedLabel = new Label
            {
                Text = $"в наличии: {ownedCount}",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            ownedLabel.AddThemeColorOverride("font_color", new Color("8bb0d0"));
            ownedLabel.AddThemeFontSizeOverride("font_size", 10);
            content.AddChild(ownedLabel);
        }

        return card;
    }

    private void TryBuyEntry(ShopEntry entry)
    {
        var gm = GameManager.Instance;
        if (gm == null)
            return;

        bool purchased;
        if (entry.Category == ShopCategory.Fish)
        {
            if (entry.FishTemplate == null)
            {
                _statusLabel.Text = "Нельзя купить эту рыбу: отсутствует шаблон спавна";
                return;
            }

            purchased = gm.TryBuyFishOffer(entry.FishTemplate, entry.Price, entry.Name);
        }
        else
        {
            purchased = gm.TryBuyShopItem(entry.Name, entry.Price, ToStorageCategory(entry.Category));
        }

        if (!purchased)
        {
            _statusLabel.Text = $"Недостаточно монет для покупки: {entry.Name}";
            return;
        }

        RefreshAll();
    }

    private static string ToStorageCategory(ShopCategory category)
    {
        return category switch
        {
            ShopCategory.Food => "food",
            ShopCategory.Decor => "decor",
            _ => "fish"
        };
    }

    private static string FormatCoins(int amount)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:N0}", amount).Replace(",", " ");
    }

    private Texture2D LoadIcon(params string[] resourcePaths)
    {
        foreach (var path in resourcePaths)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            var texture = GD.Load<Texture2D>(path);
            if (texture != null)
                return texture;
        }

        return null;
    }

    private static StyleBoxFlat BuildPanelStyle(Color background, Color border, int borderWidth, int radius)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius
        };

        style.SetBorderWidthAll(borderWidth);
        return style;
    }

    private static StyleBoxFlat BuildButtonStyle(Color background, Color border, int borderWidth, int radius)
    {
        var style = BuildPanelStyle(background, border, borderWidth, radius);
        style.ContentMarginTop = 4;
        style.ContentMarginBottom = 4;
        style.ContentMarginLeft = 10;
        style.ContentMarginRight = 10;
        return style;
    }
}
