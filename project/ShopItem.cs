using Godot;
using System;

public partial class ShopItem : Button
{
    [Export] public TextureRect IconRect;
    [Export] public Label NameLabel;
    [Export] public Label PriceLabel;

    private FishData _fishData;
    private Action<FishData> _onBuyCallback;

    public void Setup(FishData data, Action<FishData> onBuyCallback)
    {
        _fishData = data;
        _onBuyCallback = onBuyCallback;

        if (IconRect != null && data.Icon != null)
            IconRect.Texture = data.Icon;

        if (NameLabel != null)
            NameLabel.Text = data.FishName;

        if (PriceLabel != null)
            PriceLabel.Text = $"${data.Price} | +${data.IncomePerSec}/сек";

        Pressed += OnPressed;
    }

    private void OnPressed()
    {
        _onBuyCallback?.Invoke(_fishData);
    }
}