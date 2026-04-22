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
    [Export] public PanelContainer MyFishPanel;

    private global::ShopPanel _shopPanelScript;
    private global::MyFishPanel _myFishPanelScript;

    public override void _Ready()
    {
        if (ShopBtn != null) ShopBtn.Pressed += OnShopPressed;
        if (CurrentFishBtn != null) CurrentFishBtn.Pressed += OnCurrentFishPressed;
        if (BestiaryBtn != null) BestiaryBtn.Pressed += OnBestiaryPressed;

        _shopPanelScript = ShopPanel as global::ShopPanel;
        if (_shopPanelScript != null)
            _shopPanelScript.ShopClosed += OnShopClosed;

        _myFishPanelScript = MyFishPanel as global::MyFishPanel;
        if (_myFishPanelScript != null)
            _myFishPanelScript.PanelClosed += OnMyFishClosed;
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
            IncomeLabel.Text = gm.LastEventText;

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
        if (_myFishPanelScript != null)
        {
            if (_myFishPanelScript.Visible)
                _myFishPanelScript.ClosePanel();
            else
            {
                _shopPanelScript?.CloseShop();
                _myFishPanelScript.OpenPanel();
            }

            return;
        }

        if (MyFishPanel != null)
            MyFishPanel.Visible = !MyFishPanel.Visible;
    }

    private void OnBestiaryPressed()
    {
        GD.Print("Bestiary UI can be added here");
    }

    private void OnShopPressed()
    {
        if (_shopPanelScript != null)
        {
            if (_shopPanelScript.Visible)
                _shopPanelScript.CloseShop();
            else
            {
                _myFishPanelScript?.ClosePanel();
                _shopPanelScript.OpenShop();
            }
            return;
        }

        if (ShopPanel != null)
            ShopPanel.Visible = !ShopPanel.Visible;
    }

    private void OnShopClosed()
    {
        CloseShop();
    }

    private void OnMyFishClosed()
    {
        CloseMyFishPanel();
    }

    private void CloseShop()
    {
        if (ShopPanel != null)
            ShopPanel.Visible = false;
    }

    private void CloseMyFishPanel()
    {
        if (MyFishPanel != null)
            MyFishPanel.Visible = false;
    }

    public override void _ExitTree()
    {
        if (_shopPanelScript != null)
            _shopPanelScript.ShopClosed -= OnShopClosed;

        if (_myFishPanelScript != null)
            _myFishPanelScript.PanelClosed -= OnMyFishClosed;
    }
}
