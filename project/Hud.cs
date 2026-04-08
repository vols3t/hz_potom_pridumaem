using Godot;
using System;

public partial class Hud : Control
{
    [Export] public Label MoneyLabel;
    [Export] public Label IncomeLabel;
    [Export] public Label FishCountLabel;

    [Export] public Button ShopBtn;
    [Export] public Button CurrentFishBtn;
    [Export] public Button BestiaryBtn;
    [Export] public PanelContainer ShopPanel;

    public override void _Ready()
    {
        if (ShopBtn != null) ShopBtn.Pressed += OnShopPressed;
        if (CurrentFishBtn != null) CurrentFishBtn.Pressed += OnCurrentFishPressed;
        if (BestiaryBtn != null) BestiaryBtn.Pressed += OnBestiaryPressed;
    }

    public override void _Process(double delta)
    {
        if (GameManager.Instance != null && MoneyLabel != null)
        {
            MoneyLabel.Text = $"Деньги: ${GameManager.Instance.Money.ToString("F0")}";
            IncomeLabel.Text = $"+${GameManager.Instance.IncomePerSecond.ToString("F1")}/сек";
            FishCountLabel.Text = $"Рыбок: {GameManager.Instance.FishCount}";
        }
    }
    
    private void OnCurrentFishPressed() =>
        GD.Print("Мои рыбки");

    private void OnBestiaryPressed() =>
        GD.Print("Бестиарий");

    private void OnShopPressed()
    {
        if (ShopPanel != null) ShopPanel.Visible = !ShopPanel.Visible;
        GD.Print("Открываем магазин");
    }
}