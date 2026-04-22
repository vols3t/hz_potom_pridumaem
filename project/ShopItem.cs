using Godot;
using System;

public partial class ShopItem : PanelContainer
{
    [Export] public TextureRect IconRect;
    [Export] public Label NameLabel;
    [Export] public Label DescriptionLabel;
    [Export] public Label PriceLabel;
    [Export] public Label DetailsLabel;
    [Export] public Button BuyButton;

    private Action _onBuyCallback;

    public void Setup(
        string itemName,
        string description,
        int price,
        Texture2D icon,
        string details,
        string buyButtonText,
        Action onBuyCallback)
    {
        _onBuyCallback = onBuyCallback;

        if (IconRect != null)
            IconRect.Texture = icon;

        if (NameLabel != null)
            NameLabel.Text = itemName;

        if (DescriptionLabel != null)
            DescriptionLabel.Text = description;

        if (PriceLabel != null)
            PriceLabel.Text = $"Price: {price}";

        if (DetailsLabel != null)
            DetailsLabel.Text = details;

        if (BuyButton != null)
        {
            BuyButton.Text = string.IsNullOrWhiteSpace(buyButtonText) ? "Buy" : buyButtonText;
            BuyButton.Pressed -= OnPressed;
            BuyButton.Pressed += OnPressed;
        }
    }

    private void OnPressed()
    {
        _onBuyCallback?.Invoke();
    }
}
