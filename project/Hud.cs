using Godot;

public partial class Hud : Control
{
    [Export] public Label MoneyLabel;
    [Export] public CoinDisplay CoinsDisplay;
    [Export] public Label IncomeLabel;
    [Export] public Label FishCountLabel;
    [Export] public CoinDisplay FishCountDisplay;

    [Export] public Label CommonCountLabel;
    [Export] public Label RareCountLabel;
    [Export] public Label UniqueCountLabel;

    [Export] public BaseButton ShopBtn;
    [Export] public BaseButton CurrentFishBtn;
    [Export] public BaseButton BestiaryBtn;
    [Export] public PanelContainer ShopPanel;
    [Export] public BaseButton FeedBtn;

    public override void _Ready()
    {
        if (ShopBtn != null) ShopBtn.Pressed += OnShopPressed;
        if (CurrentFishBtn != null) CurrentFishBtn.Pressed += OnCurrentFishPressed;
        if (BestiaryBtn != null) BestiaryBtn.Pressed += OnBestiaryPressed;
        if (FeedBtn != null) FeedBtn.Pressed += OnFeedPressed;
    }

    public override void _Process(double delta)
    {
        var gm = GameManager.Instance;
        if (gm == null)
            return;
        
        if (CoinsDisplay != null)
            CoinsDisplay.SetAmount(Mathf.RoundToInt(gm.Money));
        else if (MoneyLabel != null)
            MoneyLabel.Text = $"Coins: {gm.Money:F0}";

        if (IncomeLabel != null)
            IncomeLabel.Text = $"+{gm.GetIncomePerSecond():F1}/sec";

        if (FishCountDisplay != null)
            FishCountDisplay.SetAmount(gm.FishCount);
        else if (FishCountLabel != null)
            FishCountLabel.Text = $"Fish: {gm.FishCount}";

        if (CommonCountLabel != null)
            CommonCountLabel.Text = $"Common: {gm.GetFishCountByRarity(FishRarity.Common)}";

        if (RareCountLabel != null)
            RareCountLabel.Text = $"Rare: {gm.GetFishCountByRarity(FishRarity.Rare)}";

        if (UniqueCountLabel != null)
            UniqueCountLabel.Text = $"Unique: {gm.GetFishCountByRarity(FishRarity.Unique)}";
    }

    private void OnCurrentFishPressed()
    {
        GD.Print("Current fish list UI can be added here");
    }

    private void OnBestiaryPressed()
    {
        GD.Print("Bestiary UI can be added here");
    }

    private void OnShopPressed()
    {
        if (ShopPanel != null)
            ShopPanel.Visible = !ShopPanel.Visible;
    }

    private void OnFeedPressed()
    {
        var defaultFood = GD.Load<FoodData>("res://assets/meal/basic_food.tres");
        if (defaultFood == null)
        {
            GD.PrintErr("Food resource not found!");
            return;
        }

        if (FoodDropper.Instance != null) FoodDropper.Instance.StartDropMode(defaultFood);
    }
}