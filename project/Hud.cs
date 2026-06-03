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
        _myFishPanelScript = MyFishPanel as global::MyFishPanel;
        _bestiaryPanelScript = BestiaryPanel as global::BestiaryPanel;
        _settingsPanelScript = SettingsPanel as global::SettingsPanel;

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
            IncomeLabel.Text = $"inc: {FormatSigned(_smoothedNetFlowPerSec)}";

        if (FishCountDisplay != null)
            FishCountDisplay.SetAmount(gm.FishCount);
        else if (FishCountLabel != null)
            FishCountLabel.Text = $"Fish: {gm.FishCount}";

        if (CommonCountLabel != null || RareCountLabel != null || UniqueCountLabel != null)
        {
            var (common, rare, unique) = gm.GetRarityCounts();
            if (CommonCountLabel != null) CommonCountLabel.Text = $"Common: {common}";
            if (RareCountLabel != null) RareCountLabel.Text = $"Rare: {rare}";
            if (UniqueCountLabel != null) UniqueCountLabel.Text = $"Unique: {unique}";
        }
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

        CloseFishInfoPanel();
    }

    public void OnFishClicked(Node2d fish)
    {
        FishInfoPanel?.ShowForFish(fish);
    }

    private void OnShopPressed() => TogglePanel(_shopPanelScript);
    private void OnCurrentFishPressed() => TogglePanel(_myFishPanelScript);
    private void OnBestiaryPressed() => TogglePanel(_bestiaryPanelScript);
    private void OnSettingsPressed() => TogglePanel(_settingsPanelScript);

    private void TogglePanel(Control panel)
    {
        if (panel == null)
            return;

        if (panel.Visible)
        {
            CallClose(panel);
            return;
        }

        if (panel != _shopPanelScript) CallClose(_shopPanelScript);
        if (panel != _myFishPanelScript) CallClose(_myFishPanelScript);
        if (panel != _bestiaryPanelScript) CallClose(_bestiaryPanelScript);
        if (panel != _settingsPanelScript) CallClose(_settingsPanelScript);
        CloseFishInfoPanel();

        CallOpen(panel);
    }

    private static void CallOpen(Control panel)
    {
        switch (panel)
        {
            case global::ShopPanel s: s.OpenPanel(); break;
            case global::MyFishPanel m: m.OpenPanel(); break;
            case global::BestiaryPanel b: b.OpenPanel(); break;
            case global::SettingsPanel st: st.OpenPanel(); break;
        }
    }

    private static void CallClose(Control panel)
    {
        if (panel == null) return;

        switch (panel)
        {
            case global::ShopPanel s: s.ClosePanel(); break;
            case global::MyFishPanel m: m.ClosePanel(); break;
            case global::BestiaryPanel b: b.ClosePanel(); break;
            case global::SettingsPanel st: st.ClosePanel(); break;
        }
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

    private void CloseFishInfoPanel()
    {
        if (FishInfoPanel != null && FishInfoPanel.Visible)
            FishInfoPanel.Close();
    }

    public override void _ExitTree()
    {
        if (ShopBtn != null) ShopBtn.Pressed -= OnShopPressed;
        if (CurrentFishBtn != null) CurrentFishBtn.Pressed -= OnCurrentFishPressed;
        if (BestiaryBtn != null) BestiaryBtn.Pressed -= OnBestiaryPressed;
        if (SettingBtn != null) SettingBtn.Pressed -= OnSettingsPressed;
        if (FeedBtn != null) FeedBtn.Pressed -= OnFeedPressed;
    }
}
