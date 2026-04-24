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
    [Export] public BaseButton SettingBtn;
    [Export] public PanelContainer ShopPanel;
    [Export] public PanelContainer MyFishPanel;
    [Export] public PanelContainer BestiaryPanel;
    [Export] public PanelContainer SettingsPanel;

    [Export] public BaseButton FeedBtn;
    [Export] public FishInfoPanel FishInfoPanel;

    private global::ShopPanel _shopPanelScript;
    private global::MyFishPanel _myFishPanelScript;
    private global::BestiaryPanel _bestiaryPanelScript;
    private global::SettingsPanel _settingsPanelScript;

    private Node2d _lastClickedFish;
    private float _previousMoney;
    private bool _hasMoneySnapshot;
    private float _smoothedNetFlowPerSec;

    public override void _Ready()
    {
        if (ShopBtn != null) ShopBtn.Pressed += OnShopPressed;
        if (CurrentFishBtn != null) CurrentFishBtn.Pressed += OnCurrentFishPressed;
        if (BestiaryBtn != null) BestiaryBtn.Pressed += OnBestiaryPressed;
        if (SettingBtn != null) SettingBtn.Pressed += OnSettingsPressed;
        if (FeedBtn != null) FeedBtn.Pressed += OnFeedPressed;

        _shopPanelScript = ShopPanel as global::ShopPanel;
        if (_shopPanelScript != null)
            _shopPanelScript.ShopClosed += OnShopClosed;

        _myFishPanelScript = MyFishPanel as global::MyFishPanel;
        if (_myFishPanelScript != null)
            _myFishPanelScript.PanelClosed += OnMyFishClosed;

        _bestiaryPanelScript = BestiaryPanel as global::BestiaryPanel;
        if (_bestiaryPanelScript != null)
            _bestiaryPanelScript.PanelClosed += OnBestiaryClosed;

        _settingsPanelScript = SettingsPanel as global::SettingsPanel;
        if (_settingsPanelScript != null)
            _settingsPanelScript.PanelClosed += OnSettingsClosed;

        if (IncomeLabel != null)
        {
            IncomeLabel.CustomMinimumSize = new Vector2(340f, IncomeLabel.CustomMinimumSize.Y);
            IncomeLabel.HorizontalAlignment = HorizontalAlignment.Left;
            IncomeLabel.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        }

        var gm = GameManager.Instance;
        if (gm != null)
        {
            _previousMoney = gm.Money;
            _hasMoneySnapshot = true;
            _smoothedNetFlowPerSec = 0f;
        }
    }

    public override void _Process(double delta)
    {
        var gm = GameManager.Instance;
        if (gm == null)
            return;

        var dt = Mathf.Max((float)delta, 0.0001f);
        if (!_hasMoneySnapshot)
        {
            _previousMoney = gm.Money;
            _hasMoneySnapshot = true;
        }

        var netFlowPerSec = (gm.Money - _previousMoney) / dt;
        _smoothedNetFlowPerSec = Mathf.Lerp(_smoothedNetFlowPerSec, netFlowPerSec, 0.2f);
        _previousMoney = gm.Money;

        if (CoinsDisplay != null)
            CoinsDisplay.SetAmount(Mathf.RoundToInt(gm.Money));
        else if (MoneyLabel != null)
            MoneyLabel.Text = $"Coins: {gm.Money:F0}";

        if (IncomeLabel != null)
        {
            IncomeLabel.Text = $"balance: {FormatSigned(_smoothedNetFlowPerSec)}";
        }

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

    private static string FormatSigned(float value)
    {
        var clamped = Mathf.Clamp(value, -999.9f, 999.9f);
        var sign = clamped >= 0f ? "+" : "-";
        return $"{sign}{Mathf.Abs(clamped):0.0}/sec";
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseBtn
            || mouseBtn.ButtonIndex != MouseButton.Left
            || !mouseBtn.Pressed)
        {
            return;
        }

        if (FishInfoPanel != null && FishInfoPanel.Visible)
        {
            GetTree().CreateTimer(0.05f).Timeout += () =>
            {
                if (FishInfoPanel.GetSelectedFish() == _lastClickedFish)
                    return;

                FishInfoPanel.Close();
            };
        }
    }

    public void OnFishClicked(Node2d fish)
    {
        _lastClickedFish = fish;
        FishInfoPanel?.ShowForFish(fish);
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
                _bestiaryPanelScript?.ClosePanel();
                _settingsPanelScript?.ClosePanel();
                CloseFishInfoPanel();
                _myFishPanelScript.OpenPanel();
            }

            return;
        }

        if (MyFishPanel != null)
            MyFishPanel.Visible = !MyFishPanel.Visible;
    }

    private void OnBestiaryPressed()
    {
        if (_bestiaryPanelScript != null)
        {
            if (_bestiaryPanelScript.Visible)
                _bestiaryPanelScript.ClosePanel();
            else
            {
                _shopPanelScript?.CloseShop();
                _myFishPanelScript?.ClosePanel();
                _settingsPanelScript?.ClosePanel();
                CloseFishInfoPanel();
                _bestiaryPanelScript.OpenPanel();
            }

            return;
        }

        if (BestiaryPanel != null)
            BestiaryPanel.Visible = !BestiaryPanel.Visible;
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
                _bestiaryPanelScript?.ClosePanel();
                _settingsPanelScript?.ClosePanel();
                CloseFishInfoPanel();
                _shopPanelScript.OpenShop();
            }

            return;
        }

        if (ShopPanel != null)
            ShopPanel.Visible = !ShopPanel.Visible;
    }

    private void OnSettingsPressed()
    {
        if (_settingsPanelScript != null)
        {
            if (_settingsPanelScript.Visible)
                _settingsPanelScript.ClosePanel();
            else
            {
                _shopPanelScript?.CloseShop();
                _myFishPanelScript?.ClosePanel();
                _bestiaryPanelScript?.ClosePanel();
                CloseFishInfoPanel();
                _settingsPanelScript.OpenPanel();
            }

            return;
        }

        if (SettingsPanel != null)
            SettingsPanel.Visible = !SettingsPanel.Visible;
    }

    private void OnFeedPressed()
    {
        var defaultFood = GD.Load<FoodData>("res://assets/meal/basic_food.tres");
        if (defaultFood == null)
        {
            GD.PrintErr("Food resource not found!");
            return;
        }

        FoodDropper.Instance?.StartDropMode(defaultFood);
    }

    private void OnShopClosed()
    {
        CloseShop();
    }

    private void OnMyFishClosed()
    {
        CloseMyFishPanel();
    }

    private void OnBestiaryClosed()
    {
        CloseBestiaryPanel();
    }

    private void OnSettingsClosed()
    {
        CloseSettingsPanel();
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

    private void CloseBestiaryPanel()
    {
        if (BestiaryPanel != null)
            BestiaryPanel.Visible = false;
    }

    private void CloseSettingsPanel()
    {
        if (SettingsPanel != null)
            SettingsPanel.Visible = false;
    }

    private void CloseFishInfoPanel()
    {
        if (FishInfoPanel != null && FishInfoPanel.Visible)
            FishInfoPanel.Close();
    }

    public override void _ExitTree()
    {
        if (_shopPanelScript != null)
            _shopPanelScript.ShopClosed -= OnShopClosed;

        if (_myFishPanelScript != null)
            _myFishPanelScript.PanelClosed -= OnMyFishClosed;

        if (_bestiaryPanelScript != null)
            _bestiaryPanelScript.PanelClosed -= OnBestiaryClosed;

        if (_settingsPanelScript != null)
            _settingsPanelScript.PanelClosed -= OnSettingsClosed;

        if (ShopBtn != null) ShopBtn.Pressed -= OnShopPressed;
        if (CurrentFishBtn != null) CurrentFishBtn.Pressed -= OnCurrentFishPressed;
        if (BestiaryBtn != null) BestiaryBtn.Pressed -= OnBestiaryPressed;
        if (SettingBtn != null) SettingBtn.Pressed -= OnSettingsPressed;
        if (FeedBtn != null) FeedBtn.Pressed -= OnFeedPressed;
    }
}
