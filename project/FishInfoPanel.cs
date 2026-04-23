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
            SpeciesLabel.Text = $"Species: {species}";
        }

        if (StageLabel != null)
            StageLabel.Text = $"Stage: {fish.CurrentStage}";

        if (AgeLabel != null)
            AgeLabel.Text = $"Age: {fish.AgeSec:F0}s";

        if (IncomeLabel != null)
        {
            var income = 0f;
            if (fish.Data != null)
            {
                income = fish.Data.IncomePerSec
                         * fish.Data.GetRarityMultiplier()
                         * fish.CurrentStage switch
                         {
                             FishGrowthStage.Fry => 0.25f,
                             FishGrowthStage.Teen => 0.6f,
                             FishGrowthStage.Adult => 1.0f,
                             _ => 1.0f
                         }
                         * fish.GetIncomeMultiplier();
            }

            IncomeLabel.Text = $"Income: +{income:F1}/sec";
        }

        if (HungerLabel != null)
        {
            string hungerStatus;
            if (fish.TimeSinceLastFed < 15f)
                hungerStatus = "🟢 Сытая";
            else if (fish.TimeSinceLastFed < 60f)
                hungerStatus = "🟡 Проголодалась";
            else
                hungerStatus = "🔴 Очень голодная!";

            HungerLabel.Text = $"Hunger: {hungerStatus} ({fish.TimeSinceLastFed:F0}s)";
        }

        UpdateMutationsList();
    }

    private void UpdateMutationsList()
    {
        if (MutationsList == null || _selectedFish == null)
            return;
        
        foreach (var child in MutationsList.GetChildren())
            child.QueueFree();

        if (_selectedFish.Mutations.Count == 0)
        {
            var none = new Label();
            none.Text = "  None";
            none.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            MutationsList.AddChild(none);
            return;
        }

        foreach (var mutation in _selectedFish.Mutations)
        {
            var label = new Label();
            label.Text = $"  • {mutation.MutationName}";
            MutationsList.AddChild(label);
        }
    }

    private void FollowFish()
    {
        if (_selectedFish == null)
            return;

        var screenPos = _selectedFish.GlobalPosition;
        var offset = new Vector2(40, -80);
        var targetPos = screenPos + offset;
        
        var viewportSize = GetViewportRect().Size;
        targetPos.X = Mathf.Clamp(targetPos.X, 0, viewportSize.X - Size.X);
        targetPos.Y = Mathf.Clamp(targetPos.Y, 0, viewportSize.Y - Size.Y);

        GlobalPosition = targetPos;
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