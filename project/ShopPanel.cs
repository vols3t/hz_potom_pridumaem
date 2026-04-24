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
        public HBoxContainer NavigationRow;
        public PanelContainer ComingSoonPanel;
        public Label PageLabel;
        public Button PrevButton;
        public Button NextButton;
        public Label SummaryLabel;
    }

    private const int ItemsPerPage = 6;
    private static readonly string[] RequiredShopFishPaths =
    {
        "res://guppyfish.tres",
        "res://plekofish.tres",
        "res://scalaryafish.tres",
        "res://tetrafish.tres"
    };
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

        EnsureAquariumConfigured();
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
        var templates = GetShopFishTemplates();
        var entries = new List<ShopEntry>(templates.Count);

        for (var i = 0; i < templates.Count; i++)
        {
            var template = templates[i];
            var teenReward = template.GetStageReward(FishGrowthStage.Teen);
            var adultReward = template.GetStageReward(FishGrowthStage.Adult);
            var displayName = string.IsNullOrWhiteSpace(template.FishName) ? $"\u0420\u044b\u0431\u043a\u0430 {i + 1}" : template.FishName.Trim();
            var description = string.IsNullOrWhiteSpace(template.Description)
                ? $"\u0420\u0435\u0434\u043a\u043e\u0441\u0442\u044c: {ToRarityText(template.Rarity)}."
                : template.Description.Trim();
            var price = template.Price > 0 ? template.Price : 100;

            entries.Add(new ShopEntry(
                ShopCategory.Fish,
                $"fish_{i}",
                displayName,
                description,
                price,
                template.Icon ?? _fallbackIcon,
                $"\u0420\u043e\u0441\u0442: \u043f\u043e\u0434\u0440\u043e\u0441\u0442\u043e\u043a +{teenReward}, \u0432\u0437\u0440\u043e\u0441\u043b\u044b\u0439 +{adultReward}",
                template));
        }

        return entries;
    }

    private List<ShopEntry> BuildFoodCatalog()
    {
        return new List<ShopEntry>();
    }

    private List<ShopEntry> BuildDecorCatalog()
    {
        return new List<ShopEntry>();
    }

    private List<FishData> GetShopFishTemplates()
    {
        var result = GetValidFishTemplates();

        foreach (var path in RequiredShopFishPaths)
        {
            var fish = GD.Load<FishData>(path);
            if (fish == null || fish.FishScene == null)
                continue;

            if (!ContainsFishTemplate(result, fish))
                result.Add(fish);
        }

        if (result.Count == 0)
            result = GetFallbackFishTemplates();

        result.Sort(CompareFishTemplates);
        return result;
    }

    private static bool ContainsFishTemplate(List<FishData> collection, FishData candidate)
    {
        if (collection == null || candidate == null)
            return false;

        var candidateSpecies = NormalizeFishKey(candidate.SpeciesId);
        var candidateName = NormalizeFishKey(candidate.FishName);
        var candidatePath = NormalizeFishKey(candidate.ResourcePath);

        foreach (var fish in collection)
        {
            if (fish == null)
                continue;

            if (!string.IsNullOrWhiteSpace(candidatePath) && NormalizeFishKey(fish.ResourcePath) == candidatePath)
                return true;
            if (!string.IsNullOrWhiteSpace(candidateSpecies) && NormalizeFishKey(fish.SpeciesId) == candidateSpecies)
                return true;
            if (!string.IsNullOrWhiteSpace(candidateName) && NormalizeFishKey(fish.FishName) == candidateName)
                return true;
        }

        return false;
    }

    private static int CompareFishTemplates(FishData left, FishData right)
    {
        var leftPriority = GetFishPriority(left);
        var rightPriority = GetFishPriority(right);
        if (leftPriority != rightPriority)
            return leftPriority.CompareTo(rightPriority);

        var leftName = left?.FishName ?? string.Empty;
        var rightName = right?.FishName ?? string.Empty;
        return string.Compare(leftName, rightName, StringComparison.CurrentCultureIgnoreCase);
    }

    private static int GetFishPriority(FishData fish)
    {
        var species = NormalizeFishKey(fish?.SpeciesId);
        return species switch
        {
            "guppy" => 0,
            "pleko" => 1,
            "scalarya" => 2,
            "tetra" => 3,
            _ => 10
        };
    }

    private static string NormalizeFishKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
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
            FishRarity.Common => "\u043e\u0431\u044b\u0447\u043d\u0430\u044f",
            FishRarity.Rare => "\u0440\u0435\u0434\u043a\u0430\u044f",
            FishRarity.Unique => "\u0443\u043d\u0438\u043a\u0430\u043b\u044c\u043d\u0430\u044f",
            _ => "\u043e\u0431\u044b\u0447\u043d\u0430\u044f"
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
            Text = "\u041c\u043e\u043d\u0435\u0442\u044b:",
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
            Text = "\u043c\u0430\u0433\u0430\u0437\u0438\u043d",
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
            TooltipText = "\u0417\u0430\u043a\u0440\u044b\u0442\u044c \u043c\u0430\u0433\u0430\u0437\u0438\u043d (Esc)",
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
            Text = "\u041e\u0442\u043a\u0440\u043e\u0439\u0442\u0435 \u043d\u0443\u0436\u043d\u0443\u044e \u043a\u0430\u0442\u0435\u0433\u043e\u0440\u0438\u044e \u0438 \u0432\u044b\u0431\u0435\u0440\u0438\u0442\u0435 \u0442\u043e\u0432\u0430\u0440",
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

        CreateTopTab(topTabsRow, ShopCategory.Fish, "\u0440\u044b\u0431\u043a\u0438");
        CreateTopTab(topTabsRow, ShopCategory.Food, "\u0435\u0434\u0430");
        CreateTopTab(topTabsRow, ShopCategory.Decor, "\u0434\u0435\u043a\u043e\u0440\u0430\u0446\u0438\u0438");

        _categoryTabs = new TabContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            TabsVisible = false
        };
        content.AddChild(_categoryTabs);

        BuildCategoryView(_categoryTabs, ShopCategory.Fish, "\u0440\u044b\u0431\u043a\u0438");
        BuildCategoryView(_categoryTabs, ShopCategory.Food, "\u0435\u0434\u0430");
        BuildCategoryView(_categoryTabs, ShopCategory.Decor, "\u0434\u0435\u043a\u043e\u0440\u0430\u0446\u0438\u0438");
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

        var comingSoonPanel = BuildComingSoonPanel(category);
        content.AddChild(comingSoonPanel);

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
            NavigationRow = navRow,
            ComingSoonPanel = comingSoonPanel,
            PageLabel = page,
            PrevButton = prev,
            NextButton = next,
            SummaryLabel = summary
        };
    }

    private PanelContainer BuildComingSoonPanel(ShopCategory category)
    {
        var panel = new PanelContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore
        };
        panel.AddThemeStyleboxOverride("panel", BuildPanelStyle(new Color("1a3856"), new Color("4b79a8"), 2, 10));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        panel.AddChild(margin);

        var center = new CenterContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        margin.AddChild(center);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);
        center.AddChild(content);

        var comingSoon = new Label
        {
            Text = "COMING SOON",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        comingSoon.AddThemeColorOverride("font_color", new Color("f8d976"));
        comingSoon.AddThemeFontSizeOverride("font_size", 48);
        content.AddChild(comingSoon);

        var subtitle = new Label
        {
            Text = category == ShopCategory.Food
                ? "\u0420\u0430\u0437\u0434\u0435\u043b \u00ab\u0435\u0434\u0430\u00bb \u0432 \u0440\u0430\u0437\u0440\u0430\u0431\u043e\u0442\u043a\u0435"
                : "\u0420\u0430\u0437\u0434\u0435\u043b \u00ab\u0434\u0435\u043a\u043e\u0440\u0430\u0446\u0438\u0438\u00bb \u0432 \u0440\u0430\u0437\u0440\u0430\u0431\u043e\u0442\u043a\u0435",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        subtitle.AddThemeColorOverride("font_color", new Color("d7e7ff"));
        subtitle.AddThemeFontSizeOverride("font_size", 24);
        content.AddChild(subtitle);

        var details = new Label
        {
            Text = "\u0421\u043a\u043e\u0440\u043e \u0437\u0434\u0435\u0441\u044c \u043f\u043e\u044f\u0432\u044f\u0442\u0441\u044f \u043d\u043e\u0432\u044b\u0435 \u0442\u043e\u0432\u0430\u0440\u044b \u0438 \u0443\u043b\u0443\u0447\u0448\u0435\u043d\u0438\u044f.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        details.AddThemeColorOverride("font_color", new Color("96b9df"));
        details.AddThemeFontSizeOverride("font_size", 18);
        content.AddChild(details);

        return panel;
    }

    private static bool IsComingSoonCategory(ShopCategory category)
    {
        return category == ShopCategory.Food || category == ShopCategory.Decor;
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

    public void OpenShop()
    {
        if (!IsInsideTree())
            return;

        EnsureAquariumConfigured();
        Visible = true;
        MoveToFront();
        RefreshAll();
    }

    private bool EnsureAquariumConfigured()
    {
        var gm = GameManager.Instance;
        if (gm == null)
            return false;

        var aquariumNode = Aquarium;
        if (aquariumNode == null)
            aquariumNode = GetTree()?.CurrentScene as Node2D;
        if (aquariumNode == null)
            aquariumNode = GetNodeOrNull<Node2D>("../../..");
        if (aquariumNode == null)
            return false;

        Aquarium = aquariumNode;

        var catalog = AvailableFish;
        if (catalog == null || catalog.Length == 0)
            catalog = GetShopFishTemplates().ToArray();

        gm.ConfigureAquarium(aquariumNode, catalog, SpawnAreaMin, SpawnAreaMax);
        return true;
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

        if (IsComingSoonCategory(category))
        {
            foreach (var child in view.Grid.GetChildren())
                child.QueueFree();

            view.Grid.Visible = false;
            if (view.NavigationRow != null)
                view.NavigationRow.Visible = false;
            if (view.ComingSoonPanel != null)
                view.ComingSoonPanel.Visible = true;

            view.PageLabel.Text = string.Empty;
            view.PrevButton.Disabled = true;
            view.NextButton.Disabled = true;
            view.SummaryLabel.Text = category == ShopCategory.Food
                ? "\u0420\u0430\u0437\u0434\u0435\u043b \u00ab\u0435\u0434\u0430\u00bb \u043e\u0442\u043a\u0440\u043e\u0435\u0442\u0441\u044f \u0432 \u0431\u043b\u0438\u0436\u0430\u0439\u0448\u0435\u043c \u043e\u0431\u043d\u043e\u0432\u043b\u0435\u043d\u0438\u0438"
                : "\u0420\u0430\u0437\u0434\u0435\u043b \u00ab\u0434\u0435\u043a\u043e\u0440\u0430\u0446\u0438\u0438\u00bb \u043e\u0442\u043a\u0440\u043e\u0435\u0442\u0441\u044f \u0432 \u0431\u043b\u0438\u0436\u0430\u0439\u0448\u0435\u043c \u043e\u0431\u043d\u043e\u0432\u043b\u0435\u043d\u0438\u0438";
            return;
        }

        view.Grid.Visible = true;
        if (view.NavigationRow != null)
            view.NavigationRow.Visible = true;
        if (view.ComingSoonPanel != null)
            view.ComingSoonPanel.Visible = false;

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

        view.SummaryLabel.Text = "\u041f\u043e\u043a\u0443\u043f\u043a\u0430: \u0440\u044b\u0431\u0430 \u0441\u0440\u0430\u0437\u0443 \u043f\u043e\u044f\u0432\u043b\u044f\u0435\u0442\u0441\u044f \u0432 \u0430\u043a\u0432\u0430\u0440\u0438\u0443\u043c\u0435";
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
            Text = "\u043a\u0443\u043f\u0438\u0442\u044c",
            CustomMinimumSize = new Vector2(0, 28)
        };
        buyButton.AddThemeStyleboxOverride("normal", BuildButtonStyle(new Color("4d833f"), new Color("8eb16a"), 2, 8));
        buyButton.AddThemeStyleboxOverride("hover", BuildButtonStyle(new Color("5a9649"), new Color("b7d48f"), 2, 8));
        buyButton.AddThemeStyleboxOverride("pressed", BuildButtonStyle(new Color("406f35"), new Color("b7d48f"), 2, 8));
        buyButton.AddThemeStyleboxOverride("disabled", BuildButtonStyle(new Color("3a4f38"), new Color("576852"), 2, 8));
        buyButton.AddThemeColorOverride("font_color", new Color("eaf4db"));
        buyButton.AddThemeFontSizeOverride("font_size", 17);

        var gm = GameManager.Instance;
        var canAfford = gm?.CanAfford(entry.Price) ?? false;
        var validFishOffer = entry.Category != ShopCategory.Fish || ResolveFishTemplate(entry) != null;
        var hasFishSlots = entry.Category != ShopCategory.Fish || (gm != null && gm.FishCount < gm.MaxFishCount);
        buyButton.Disabled = !canAfford || !validFishOffer || !hasFishSlots;
        if (!hasFishSlots)
            buyButton.Text = "\u043b\u0438\u043c\u0438\u0442";
        else if (!canAfford)
            buyButton.Text = "\u0434\u043e\u0440\u043e\u0433\u043e";

        buyButton.Pressed += () => TryBuyEntry(entry);
        content.AddChild(buyButton);

        if (entry.Category != ShopCategory.Fish)
        {
            var ownedCount = GameManager.Instance?.GetOwnedShopItemCount(ToStorageCategory(entry.Category), entry.Name) ?? 0;
            var ownedLabel = new Label
            {
                Text = $"\u0432 \u043d\u0430\u043b\u0438\u0447\u0438\u0438: {ownedCount}",
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
            if (!EnsureAquariumConfigured())
            {
                _statusLabel.Text = "\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u043f\u043e\u0434\u0433\u043e\u0442\u043e\u0432\u0438\u0442\u044c \u0430\u043a\u0432\u0430\u0440\u0438\u0443\u043c \u0434\u043b\u044f \u043f\u043e\u043a\u0443\u043f\u043a\u0438";
                return;
            }

            var fishTemplate = ResolveFishTemplate(entry);
            if (fishTemplate == null)
            {
                _statusLabel.Text = "\u041d\u0435\u043b\u044c\u0437\u044f \u043a\u0443\u043f\u0438\u0442\u044c \u044d\u0442\u0443 \u0440\u044b\u0431\u0443: \u043e\u0442\u0441\u0443\u0442\u0441\u0442\u0432\u0443\u0435\u0442 \u0448\u0430\u0431\u043b\u043e\u043d \u0441\u043f\u0430\u0432\u043d\u0430";
                return;
            }

            purchased = gm.TryBuyFishOffer(fishTemplate, entry.Price, entry.Name);
        }
        else
        {
            purchased = gm.TryBuyShopItem(entry.Name, entry.Price, ToStorageCategory(entry.Category));
        }

        if (!purchased)
        {
            _statusLabel.Text = !string.IsNullOrWhiteSpace(gm.LastEventText)
                ? gm.LastEventText
                : $"\u041d\u0435\u0434\u043e\u0441\u0442\u0430\u0442\u043e\u0447\u043d\u043e \u043c\u043e\u043d\u0435\u0442 \u0434\u043b\u044f \u043f\u043e\u043a\u0443\u043f\u043a\u0438: {entry.Name}";
            return;
        }

        RefreshAll();
    }
    private FishData ResolveFishTemplate(ShopEntry entry)
    {
        if (entry?.Category != ShopCategory.Fish)
            return null;

        if (entry.FishTemplate != null && entry.FishTemplate.FishScene != null)
            return entry.FishTemplate;

        var validFish = GetValidFishTemplates();
        if (validFish.Count == 0)
            validFish = GetFallbackFishTemplates();
        if (validFish.Count == 0)
            return null;

        var index = ExtractEntryIndex(entry.Id);
        if (index < 0)
            return validFish[0];

        return validFish[index % validFish.Count];
    }

    private static List<FishData> GetFallbackFishTemplates()
    {
        var result = new List<FishData>();
        var fallbackPaths = new[]
        {
            "res://guppyfish.tres",
            "res://plekofish.tres",
            "res://scalaryafish.tres",
            "res://tetrafish.tres",
            "res://goldfish.tres",
            "res://neonfish.tres",
            "res://uniquefish.tres"
        };

        foreach (var path in fallbackPaths)
        {
            var data = GD.Load<FishData>(path);
            if (data != null && data.FishScene != null)
                result.Add(data);
        }

        return result;
    }

    private static int ExtractEntryIndex(string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
            return -1;

        var underscoreIndex = entryId.LastIndexOf('_');
        if (underscoreIndex < 0 || underscoreIndex >= entryId.Length - 1)
            return -1;

        return int.TryParse(entryId.Substring(underscoreIndex + 1), out var index) ? index : -1;
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
