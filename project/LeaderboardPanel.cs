using Godot;
using System.Collections.Generic;

public partial class LeaderboardPanel : PanelContainer
{
    private static readonly (string Name, int Score)[] FakePlayers =
    {
        ("ТопАкварист",   85000),
        ("АквА-Чемп",     54000),
        ("RybkaFan99",    38000),
        ("МорскойЧёрт",   24000),
        ("GuppyKing",     15000),
        ("АкваМастер",     9500),
        ("ФишБосс",        5200),
        ("Гуппифил",       2100),
        ("Новичок",         800),
        ("Плеколюб",        200),
    };

    private VBoxContainer _listContainer;
    private Label _playerRankLabel;
    private bool _uiBuilt;

    public override void _Ready()
    {
        BuildUi();
    }

    public void OpenPanel()
    {
        if (!IsInsideTree())
            return;

        Visible = true;
        MoveToFront();
        RefreshList();
    }

    public void ClosePanel()
    {
        Visible = false;
    }

    private void BuildUi()
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

        AddThemeStyleboxOverride("panel", UiTheme.BuildPanelStyle(new Color("112b45"), new Color("2f4f73"), 3, 16));

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

        _playerRankLabel = new Label
        {
            Text = "Ваше место: —",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _playerRankLabel.AddThemeColorOverride("font_color", new Color("8fbde0"));
        _playerRankLabel.AddThemeFontSizeOverride("font_size", 22);
        rootVBox.AddChild(_playerRankLabel);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        rootVBox.AddChild(scroll);

        _listContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _listContainer.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_listContainer);

        _uiBuilt = true;
    }

    private void BuildHeader(VBoxContainer parent)
    {
        var headerPanel = new PanelContainer { CustomMinimumSize = new Vector2(0, 92) };
        headerPanel.AddThemeStyleboxOverride("panel", UiTheme.BuildPanelStyle(new Color("16314d"), new Color("35597e"), 2, 12));
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
            Text = "ТАБЛИЦА ЛИДЕРОВ",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        title.AddThemeColorOverride("font_color", new Color("f5d76e"));
        title.AddThemeFontSizeOverride("font_size", 46);
        row.AddChild(title);

        var closeBtn = new Button
        {
            Text = "X",
            CustomMinimumSize = new Vector2(44, 40),
            FocusMode = FocusModeEnum.None
        };
        closeBtn.AddThemeStyleboxOverride("normal", UiTheme.BuildButtonStyle(new Color("274563"), new Color("7da6d1"), 2, 8));
        closeBtn.AddThemeStyleboxOverride("hover", UiTheme.BuildButtonStyle(new Color("315679"), new Color("b1d7ff"), 2, 8));
        closeBtn.AddThemeColorOverride("font_color", new Color("eaf4ff"));
        closeBtn.AddThemeFontSizeOverride("font_size", 24);
        closeBtn.Pressed += ClosePanel;
        row.AddChild(closeBtn);
    }

    private void RefreshList()
    {
        if (!_uiBuilt || _listContainer == null)
            return;

        var playerScore = GameManager.Instance?.GetAquariumScore() ?? 0;

        var entries = new List<(string Name, int Score, bool IsPlayer)>();
        foreach (var (name, score) in FakePlayers)
            entries.Add((name, score, false));
        entries.Add(("ВЫ", playerScore, true));
        entries.Sort((a, b) => b.Score.CompareTo(a.Score));

        var playerRank = 1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].IsPlayer) { playerRank = i + 1; break; }
        }

        _playerRankLabel.Text = $"Ваше место: #{playerRank} из {entries.Count}";

        foreach (var child in _listContainer.GetChildren())
            child.QueueFree();

        for (var i = 0; i < entries.Count; i++)
            _listContainer.AddChild(BuildRow(i + 1, entries[i].Name, entries[i].Score, entries[i].IsPlayer));
    }

    private static Control BuildRow(int rank, string name, int score, bool isPlayer)
    {
        var bg = isPlayer
            ? new Color("1e4f20")
            : rank <= 3 ? new Color("2a3416") : new Color("152e48");
        var border = isPlayer
            ? new Color("5aaa5e")
            : rank <= 3 ? new Color("c0a030") : new Color("2f5278");

        var row = new PanelContainer();
        row.AddThemeStyleboxOverride("panel", UiTheme.BuildPanelStyle(bg, border, isPlayer ? 3 : 2, 8));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        row.AddChild(margin);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);
        margin.AddChild(hbox);

        var rankStr = rank switch { 1 => "#1 *", 2 => "#2", 3 => "#3", _ => $"#{rank}" };
        var rankLabel = new Label
        {
            Text = rankStr,
            CustomMinimumSize = new Vector2(64, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        rankLabel.AddThemeColorOverride("font_color", rank <= 3 ? new Color("ffd700") : new Color("aac8e8"));
        rankLabel.AddThemeFontSizeOverride("font_size", rank <= 3 ? 24 : 20);
        hbox.AddChild(rankLabel);

        var nameLabel = new Label
        {
            Text = name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        nameLabel.AddThemeColorOverride("font_color", isPlayer ? new Color("a0f4a0") : new Color("daeeff"));
        nameLabel.AddThemeFontSizeOverride("font_size", isPlayer ? 24 : 21);
        hbox.AddChild(nameLabel);

        var scoreLabel = new Label
        {
            Text = FormatScore(score),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        scoreLabel.AddThemeColorOverride("font_color", new Color("ffd66b"));
        scoreLabel.AddThemeFontSizeOverride("font_size", 21);
        hbox.AddChild(scoreLabel);

        return row;
    }

    private static string FormatScore(int score)
    {
        if (score >= 1_000_000) return $"{score / 1_000_000}M";
        if (score >= 1_000) return $"{score / 1_000}K";
        return score.ToString();
    }
}
