using Godot;
using System;

public partial class MyFishPanel : PanelContainer
{
    [Signal]
    public delegate void PanelClosedEventHandler();

    private Texture2D _fallbackIcon;
    private Label _summaryLabel;
    private GridContainer _grid;
    private bool _uiBuilt;
    private float _refreshTimerSec;

    public override void _Ready()
    {
        ConfigureFullscreenLayout();
        _fallbackIcon = GD.Load<Texture2D>("res://assets/fishes/medium/clown-fish-medium.png");
        BuildUi();
        RefreshAll(force: true);
        VisibilityChanged += OnVisibilityChanged;
    }

    public override void _Process(double delta)
    {
        if (!_uiBuilt || !Visible)
            return;

        _refreshTimerSec += (float)delta;
        if (_refreshTimerSec >= 0.5f)
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
        RefreshAll(force: true);
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
            RefreshAll(force: true);
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

        var rootVBox = new VBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        rootVBox.AddThemeConstantOverride("separation", 10);
        rootMargin.AddChild(rootVBox);

        BuildHeader(rootVBox);
        BuildCardsArea(rootVBox);
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

        var title = new Label
        {
            Text = "мои рыбки",
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
            TooltipText = "Закрыть раздел (Esc)",
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

        _summaryLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _summaryLabel.AddThemeColorOverride("font_color", new Color("8fb4d8"));
        _summaryLabel.AddThemeFontSizeOverride("font_size", 18);
        parent.AddChild(_summaryLabel);
    }

    private void BuildCardsArea(VBoxContainer parent)
    {
        var contentPanel = new PanelContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        contentPanel.AddThemeStyleboxOverride("panel", BuildPanelStyle(new Color("15324e"), new Color("3a5f83"), 2, 12));
        parent.AddChild(contentPanel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        contentPanel.AddChild(margin);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        margin.AddChild(scroll);

        _grid = new GridContainer
        {
            Columns = 3,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _grid.AddThemeConstantOverride("h_separation", 8);
        _grid.AddThemeConstantOverride("v_separation", 8);
        scroll.AddChild(_grid);
    }

    private void RefreshAll(bool force = false)
    {
        if (!_uiBuilt)
            return;

        var gm = GameManager.Instance;
        var fishes = gm?.GetFishSnapshot();
        RefreshCards(fishes, gm?.MaxFishCount ?? 0);
    }

    private void RefreshCards(System.Collections.Generic.IReadOnlyList<Node2d> fishes, int maxFishCount)
    {
        foreach (var child in _grid.GetChildren())
            child.QueueFree();

        var fishCount = fishes?.Count ?? 0;
        _summaryLabel.Text = $"В аквариуме: {fishCount} / {maxFishCount}";

        if (fishCount == 0)
        {
            var empty = new Label
            {
                Text = "Пока нет рыбок. Купите первую в магазине.",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                CustomMinimumSize = new Vector2(0, 260),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            empty.AddThemeColorOverride("font_color", new Color("d7e8fb"));
            empty.AddThemeFontSizeOverride("font_size", 26);
            _grid.AddChild(empty);
            return;
        }

        for (var i = 0; i < fishCount; i++)
            _grid.AddChild(CreateFishCard(fishes[i]));
    }

    private Control CreateFishCard(Node2d fish)
    {
        var data = fish?.Data;
        var displayName = Node2d.ToRussianFishName(fish?.FishName);
        var teenReward = data?.GetStageReward(FishGrowthStage.Teen) ?? 0;
        var adultReward = data?.GetStageReward(FishGrowthStage.Adult) ?? 0;

        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(220, 220),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        card.AddThemeStyleboxOverride("panel", BuildPanelStyle(new Color("173550"), new Color("3f658a"), 2, 8));

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
            Text = string.IsNullOrWhiteSpace(displayName) ? "Рыба" : displayName,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        name.AddThemeColorOverride("font_color", new Color("eef6ff"));
        name.AddThemeFontSizeOverride("font_size", 18);
        content.AddChild(name);

        var icon = new TextureRect
        {
            Texture = data?.Icon ?? _fallbackIcon,
            CustomMinimumSize = new Vector2(90, 58),
            ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        content.AddChild(icon);

        var properties = new Label
        {
            Text = BuildPropertiesText(fish, teenReward, adultReward),
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        properties.AddThemeColorOverride("font_color", new Color("9ec3e8"));
        properties.AddThemeFontSizeOverride("font_size", 12);
        content.AddChild(properties);

        var description = new Label
        {
            Text = fish?.Description ?? "Описание отсутствует.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        description.AddThemeColorOverride("font_color", new Color("7fa4c8"));
        description.AddThemeFontSizeOverride("font_size", 11);
        content.AddChild(description);

        return card;
    }

    private static string BuildPropertiesText(Node2d fish, int teenReward, int adultReward)
    {
        if (fish == null)
            return "Нет данных";

        var data = fish.Data;
        var rarityText = ToRarityText(data?.Rarity ?? FishRarity.Common);
        var stageText = ToStageText(fish.CurrentStage);
        var ageText = $"{Mathf.RoundToInt(fish.AgeSec)} сек";
        var nextStageText = BuildNextStageText(fish);
        return $"Редкость: {rarityText}\nСтадия: {stageText}\nВозраст: {ageText}\n{nextStageText}\nНаграды: +{teenReward} / +{adultReward}";
    }

    private static string BuildNextStageText(Node2d fish)
    {
        if (fish?.Data == null)
            return "Рост: нет данных";

        var data = fish.Data;
        if (fish.CurrentStage == FishGrowthStage.Fry)
        {
            var left = Mathf.Max(0f, data.FryDurationSec - fish.AgeSec);
            return $"До подростка: {Mathf.CeilToInt(left)} сек";
        }

        if (fish.CurrentStage == FishGrowthStage.Teen)
        {
            var adultAge = data.FryDurationSec + data.TeenDurationSec;
            var left = Mathf.Max(0f, adultAge - fish.AgeSec);
            return $"До взрослой: {Mathf.CeilToInt(left)} сек";
        }

        return "Рыба уже взрослая";
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

    private static string ToStageText(FishGrowthStage stage)
    {
        return stage switch
        {
            FishGrowthStage.Fry => "малек",
            FishGrowthStage.Teen => "подросток",
            FishGrowthStage.Adult => "взрослая",
            _ => "малек"
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
