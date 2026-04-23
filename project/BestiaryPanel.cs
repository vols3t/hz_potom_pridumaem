using Godot;
using System;
using System.Collections.Generic;

public partial class BestiaryPanel : PanelContainer
{
    [Signal]
    public delegate void PanelClosedEventHandler();

    [Export] public FishData[] CatalogFish;

    private enum BestiaryFilter
    {
        All,
        Common,
        Rare,
        Unique
    }

    private sealed class FilterView
    {
        public PanelContainer Root;
        public GridContainer Grid;
        public Label SummaryLabel;
    }

    private sealed class BestiaryEntry
    {
        public FishData Data;
        public string DisplayName;
    }

    private Texture2D _fallbackIcon;
    private TabContainer _tabs;
    private readonly Dictionary<BestiaryFilter, Button> _filterButtons = new();
    private readonly Dictionary<BestiaryFilter, FilterView> _views = new();
    private BestiaryFilter _activeFilter = BestiaryFilter.All;
    private bool _uiBuilt;
    private float _refreshTimerSec;

    public override void _Ready()
    {
        ConfigureFullscreenLayout();
        _fallbackIcon = GD.Load<Texture2D>("res://assets/fishes/medium/clown-fish-medium.png");
        BuildUi();
        RefreshAll();
        VisibilityChanged += OnVisibilityChanged;
    }

    public override void _Process(double delta)
    {
        if (!_uiBuilt || !Visible)
            return;

        _refreshTimerSec += (float)delta;
        if (_refreshTimerSec >= 0.6f)
        {
            _refreshTimerSec = 0f;
            RefreshAll();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || @event == null)
            return;

        if (@event.IsActionPressed("ui_cancel"))
        {
            ClosePanel();
            GetViewport().SetInputAsHandled();
        }
    }

    public void OpenPanel()
    {
        if (!IsInsideTree())
            return;

        Visible = true;
        MoveToFront();
        RefreshAll();
    }

    public void ClosePanel()
    {
        if (!Visible)
            return;

        Visible = false;
        EmitSignal(SignalName.PanelClosed);
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

    private void BuildUi()
    {
        AddThemeStyleboxOverride("panel", BuildPanelStyle(new Color("112b45"), new Color("2f4f73"), 3, 16));

        var rootMargin = new MarginContainer();
        rootMargin.AddThemeConstantOverride("margin_left", 14);
        rootMargin.AddThemeConstantOverride("margin_top", 14);
        rootMargin.AddThemeConstantOverride("margin_right", 14);
        rootMargin.AddThemeConstantOverride("margin_bottom", 14);
        AddChild(rootMargin);

        var root = new VBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 10);
        rootMargin.AddChild(root);

        BuildHeader(root);
        BuildContent(root);
        _uiBuilt = true;
    }

    private void BuildHeader(VBoxContainer parent)
    {
        var header = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 92)
        };
        header.AddThemeStyleboxOverride("panel", BuildPanelStyle(new Color("16314d"), new Color("35597e"), 2, 12));
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
            Text = "бестиарий",
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
            TooltipText = "Закрыть бестиарий (Esc)",
            CustomMinimumSize = new Vector2(44, 40),
            FocusMode = FocusModeEnum.None
        };
        closeButton.AddThemeStyleboxOverride("normal", BuildButtonStyle(new Color("274563"), new Color("7da6d1"), 2, 8));
        closeButton.AddThemeStyleboxOverride("hover", BuildButtonStyle(new Color("315679"), new Color("b1d7ff"), 2, 8));
        closeButton.AddThemeStyleboxOverride("pressed", BuildButtonStyle(new Color("1f3851"), new Color("b1d7ff"), 2, 8));
        closeButton.AddThemeColorOverride("font_color", new Color("eaf4ff"));
        closeButton.AddThemeFontSizeOverride("font_size", 24);
        closeButton.Pressed += ClosePanel;
        row.AddChild(closeButton);
    }

    private void BuildContent(VBoxContainer parent)
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

        var filterRow = new HBoxContainer();
        filterRow.AddThemeConstantOverride("separation", 8);
        content.AddChild(filterRow);

        CreateFilterButton(filterRow, BestiaryFilter.All, "все");
        CreateFilterButton(filterRow, BestiaryFilter.Common, "обычные");
        CreateFilterButton(filterRow, BestiaryFilter.Rare, "редкие");
        CreateFilterButton(filterRow, BestiaryFilter.Unique, "уникальные");

        _tabs = new TabContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            TabsVisible = false,
            ClipContents = true
        };
        content.AddChild(_tabs);

        BuildFilterView(_tabs, BestiaryFilter.All);
        BuildFilterView(_tabs, BestiaryFilter.Common);
        BuildFilterView(_tabs, BestiaryFilter.Rare);
        BuildFilterView(_tabs, BestiaryFilter.Unique);
    }

    private void CreateFilterButton(HBoxContainer parent, BestiaryFilter filter, string text)
    {
        var button = new Button
        {
            Text = text,
            ToggleMode = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 54)
        };
        button.AddThemeFontSizeOverride("font_size", 24);
        button.Pressed += () => SetFilter(filter);
        parent.AddChild(button);
        _filterButtons[filter] = button;
    }

    private void BuildFilterView(Container parent, BestiaryFilter filter)
    {
        var root = new PanelContainer
        {
            Name = filter.ToString(),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true
        };
        root.AddThemeStyleboxOverride("panel", BuildPanelStyle(new Color("17324d"), new Color("3c5d80"), 2, 12));
        parent.AddChild(root);

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

        var summary = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        summary.AddThemeColorOverride("font_color", new Color("8fb4d8"));
        summary.AddThemeFontSizeOverride("font_size", 16);
        content.AddChild(summary);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddChild(scroll);

        var grid = new GridContainer
        {
            Columns = 3,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        scroll.AddChild(grid);

        _views[filter] = new FilterView
        {
            Root = root,
            Grid = grid,
            SummaryLabel = summary
        };
    }

    private void SetFilter(BestiaryFilter filter)
    {
        _activeFilter = filter;
        if (_tabs != null)
            _tabs.CurrentTab = (int)_activeFilter;

        foreach (var pair in _filterButtons)
            ApplyFilterButtonStyle(pair.Value, pair.Key == _activeFilter);
    }

    private static void ApplyFilterButtonStyle(Button button, bool isActive)
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

    private void RefreshAll()
    {
        if (!_uiBuilt)
            return;

        foreach (var filter in Enum.GetValues<BestiaryFilter>())
            RefreshFilterCards(filter);

        SetFilter(_activeFilter);
    }

    private void RefreshFilterCards(BestiaryFilter filter)
    {
        if (!_views.TryGetValue(filter, out var view))
            return;

        foreach (var child in view.Grid.GetChildren())
            child.QueueFree();

        var catalog = GetCatalogEntries();
        var matching = new List<BestiaryEntry>();
        foreach (var entry in catalog)
        {
            if (entry?.Data == null)
                continue;

            if (filter == BestiaryFilter.All ||
                (filter == BestiaryFilter.Common && entry.Data.Rarity == FishRarity.Common) ||
                (filter == BestiaryFilter.Rare && entry.Data.Rarity == FishRarity.Rare) ||
                (filter == BestiaryFilter.Unique && entry.Data.Rarity == FishRarity.Unique))
            {
                matching.Add(entry);
            }
        }

        var discovered = 0;
        var gm = GameManager.Instance;
        foreach (var entry in matching)
        {
            var countInAquarium = GetCountInAquariumByName(entry.DisplayName);
            var known = (gm?.HasDiscoveredFishName(entry.DisplayName) ?? false) ||
                        (gm?.HasDiscoveredFish(entry.Data) ?? false) ||
                        countInAquarium > 0;
            if (known)
                discovered++;

            view.Grid.AddChild(CreateBestiaryCard(entry, countInAquarium, known));
        }

        if (matching.Count == 0)
        {
            var empty = new Label
            {
                Text = "В этой категории пока нет рыб.",
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(0, 260),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            empty.AddThemeColorOverride("font_color", new Color("c9ddf4"));
            empty.AddThemeFontSizeOverride("font_size", 22);
            view.Grid.AddChild(empty);
        }

        view.SummaryLabel.Text = $"Открыто видов: {discovered} / {matching.Count}";
    }

    private Control CreateBestiaryCard(BestiaryEntry entry, int countInAquarium, bool known)
    {
        var fish = entry.Data;
        var teenReward = fish.GetStageReward(FishGrowthStage.Teen);
        var adultReward = fish.GetStageReward(FishGrowthStage.Adult);
        var nameText = Node2d.ToRussianFishName(entry.DisplayName);

        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(220, 250),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        card.AddThemeStyleboxOverride(
            "panel",
            BuildPanelStyle(
                known ? new Color("173550") : new Color("1a3248"),
                known ? new Color("3f658a") : new Color("516880"),
                2,
                8));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        card.AddChild(margin);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 4);
        margin.AddChild(content);

        var name = new Label
        {
            Text = string.IsNullOrWhiteSpace(nameText) ? "Рыба" : nameText,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        name.AddThemeColorOverride("font_color", known ? new Color("eef6ff") : new Color("cbd9e8"));
        name.AddThemeFontSizeOverride("font_size", 18);
        content.AddChild(name);

        var icon = new TextureRect
        {
            Texture = fish.Icon ?? _fallbackIcon,
            CustomMinimumSize = new Vector2(90, 58),
            ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        icon.Modulate = known ? Colors.White : new Color(0.72f, 0.76f, 0.82f, 0.95f);
        content.AddChild(icon);

        var properties = new Label
        {
            Text = $"Редкость: {ToRarityText(fish.Rarity)}\nЦена: {fish.Price}\nНаграды: +{teenReward} / +{adultReward}",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        properties.AddThemeColorOverride("font_color", known ? new Color("9ec3e8") : new Color("8aa0b8"));
        properties.AddThemeFontSizeOverride("font_size", 12);
        content.AddChild(properties);

        var description = new Label
        {
            Text = fish.Description,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        description.AddThemeColorOverride("font_color", known ? new Color("7fa4c8") : new Color("7088a0"));
        description.AddThemeFontSizeOverride("font_size", 11);
        content.AddChild(description);

        var status = new Label
        {
            Text = known
                ? (countInAquarium > 0 ? $"В аквариуме: {countInAquarium}" : "Уже встречалась")
                : "Еще не встречалась",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        status.AddThemeColorOverride("font_color", known ? new Color("8dcf95") : new Color("a7b8ca"));
        status.AddThemeFontSizeOverride("font_size", 12);
        content.AddChild(status);

        return card;
    }

    private List<BestiaryEntry> GetCatalogEntries()
    {
        var result = new List<BestiaryEntry>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIfValid(FishData fish, string displayName = null)
        {
            if (fish == null || fish.FishScene == null)
                return;

            var normalizedName = NormalizeEntryName(displayName ?? fish.FishName);
            if (string.IsNullOrWhiteSpace(normalizedName) || !usedNames.Add(normalizedName))
                return;

            result.Add(new BestiaryEntry
            {
                Data = fish,
                DisplayName = normalizedName
            });
        }

        if (CatalogFish != null)
        {
            foreach (var fish in CatalogFish)
                AddIfValid(fish);
        }

        var discoveredByName = GameManager.Instance?.GetDiscoveredFishByName();
        if (discoveredByName != null)
        {
            foreach (var pair in discoveredByName)
                AddIfValid(pair.Value, pair.Key);
        }

        if (result.Count > 0)
            return result;

        var fallbackPaths = new[] { "res://goldfish.tres", "res://neonfish.tres", "res://uniquefish.tres" };
        foreach (var path in fallbackPaths)
        {
            var fish = GD.Load<FishData>(path);
            AddIfValid(fish);
        }

        return result;
    }

    private static int GetCountInAquariumByName(string fishName)
    {
        var normalizedName = NormalizeEntryName(fishName);
        if (string.IsNullOrWhiteSpace(normalizedName))
            return 0;

        var gm = GameManager.Instance;
        var snapshot = gm?.GetFishSnapshot();
        if (snapshot == null)
            return 0;

        var count = 0;
        foreach (var fish in snapshot)
        {
            var currentFishName = NormalizeEntryName(fish?.FishName);
            if (string.Equals(currentFishName, normalizedName, StringComparison.OrdinalIgnoreCase))
                count++;
        }

        return count;
    }

    private static string NormalizeEntryName(string fishName)
    {
        return Node2d.ToRussianFishName(fishName)?.Trim();
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
