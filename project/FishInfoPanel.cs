using Godot;

public partial class FishInfoPanel : PanelContainer
{
    [Export] public LineEdit NameEdit;
    [Export] public Button RenameBtn;
    [Export] public Label SpeciesLabel;
    [Export] public Label StageLabel;
    [Export] public Label AgeLabel;
    [Export] public Label IncomeLabel;
    [Export] public Label HungerLabel;
    [Export] public Label MutationsTitle;
    [Export] public VBoxContainer MutationsList;
    [Export] public Button CloseBtn;

    private Node2d _selectedFish;

    public override void _Ready()
    {
        Visible = false;

        if (RenameBtn != null)
            RenameBtn.Pressed += OnRenamePressed;

        if (CloseBtn != null)
            CloseBtn.Pressed += Close;

        if (NameEdit != null)
            NameEdit.TextSubmitted += OnNameSubmitted;
    }

    public override void _Process(double delta)
    {
        if (!Visible || _selectedFish == null)
            return;

        if (!IsInstanceValid(_selectedFish))
        {
            Close();
            return;
        }

        UpdateInfo();
        FollowFish();
    }

    public void ShowForFish(Node2d fish)
    {
        if (fish == null)
            return;

        _selectedFish = fish;
        Visible = true;

        if (NameEdit != null)
            NameEdit.Text = fish.FishName;

        UpdateInfo();
        FollowFish();
    }

    public void Close()
    {
        _selectedFish = null;
        Visible = false;
    }

    public Node2d GetSelectedFish() => _selectedFish;

    private void UpdateInfo()
    {
        var fish = _selectedFish;
        if (fish == null)
            return;

        if (SpeciesLabel != null)
        {
            var species = fish.Data != null ? fish.Data.FishName : "Unknown";
            SpeciesLabel.Text = $"{Localization.T("Species")}: {species}";
        }

        if (StageLabel != null)
        {
            var stage = Localization.T(fish.CurrentStage.ToString());
            StageLabel.Text = $"{Localization.T("Stage")}: {stage}";
        }

        if (AgeLabel != null)
            AgeLabel.Text = $"{Localization.T("Age")}: {fish.AgeSec:F0} \u0441";

        if (IncomeLabel != null)
        {
            var gm = GameManager.Instance;
            var income = gm != null ? gm.GetFishIncomePerSecond(fish) : 0f;

            if (gm == null && fish.Data != null)
            {
                income = fish.Data.IncomePerSec
                         * fish.Data.GetRarityMultiplier()
                         * (fish.CurrentStage switch
                         {
                             FishGrowthStage.Fry => 0.25f,
                             FishGrowthStage.Teen => 0.6f,
                             FishGrowthStage.Adult => 1.0f,
                             _ => 1.0f
                         })
                         * fish.GetIncomeMultiplier();
                income = Mathf.Clamp(income, 0f, 0.5f);
            }

            IncomeLabel.Text = $"{Localization.T("Income")}: +{income:F1}/\u0441\u0435\u043a";
        }

        if (HungerLabel != null)
        {
            string hungerKey;
            string hungerEmoji;

            if (fish.TimeSinceLastFed < 15f)
            {
                hungerKey = "Fed";
                hungerEmoji = "\U0001F7E2";
            }
            else if (fish.TimeSinceLastFed < 60f)
            {
                hungerKey = "Hungry";
                hungerEmoji = "\U0001F7E1";
            }
            else
            {
                hungerKey = "Starving";
                hungerEmoji = "\U0001F534";
            }

            HungerLabel.Text = $"{Localization.T("Hunger")}: {hungerEmoji} {Localization.T(hungerKey)}";
        }

        UpdateMutationsList();
    }

    private void UpdateMutationsList()
    {
        if (MutationsList == null || _selectedFish == null)
            return;

        foreach (var child in MutationsList.GetChildren())
            child.QueueFree();

        if (MutationsTitle != null)
            MutationsTitle.Text = $"{Localization.T("Mutations")}:";

        if (_selectedFish.Mutations.Count == 0)
        {
            var none = new Label();
            none.Text = $"  {Localization.T("None")}";
            none.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            MutationsList.AddChild(none);
            return;
        }

        foreach (var mutation in _selectedFish.Mutations)
        {
            var label = new Label();
            label.Text = $"  \u2022 {Localization.T(mutation.MutationName)}";
            MutationsList.AddChild(label);
        }
    }

    private void FollowFish()
    {
        if (_selectedFish == null)
            return;

        var fishPos = _selectedFish.GlobalPosition;
        var viewportSize = GetViewportRect().Size;
        var panelSize = Size;

        var offsetX = 40f;
        var offsetY = -80f;

        var targetX = fishPos.X + offsetX;
        var targetY = fishPos.Y + offsetY;

        if (targetX + panelSize.X > viewportSize.X)
            targetX = fishPos.X - offsetX - panelSize.X;

        if (targetX < 0)
            targetX = 0;

        if (targetY + panelSize.Y > viewportSize.Y)
            targetY = fishPos.Y - offsetY - panelSize.Y;

        if (targetY < 0)
            targetY = 0;

        GlobalPosition = new Vector2(targetX, targetY);
    }

    private void OnRenamePressed()
    {
        ApplyNewName();
    }

    private void OnNameSubmitted(string newText)
    {
        ApplyNewName();
    }

    private void ApplyNewName()
    {
        if (_selectedFish == null || NameEdit == null)
            return;

        var newName = NameEdit.Text.Trim();
        if (string.IsNullOrEmpty(newName))
            return;

        _selectedFish.FishName = newName;
        GD.Print($"[Rename] Fish renamed to: {newName}");
    }
}

