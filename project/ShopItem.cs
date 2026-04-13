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
            NameLabel.Text = $"{data.FishName} ({data.Rarity})";

        if (PriceLabel != null)
        {
            var rarityMult = data.GetRarityMultiplier();
            PriceLabel.Text =
                $"Price: {data.Price} | Stage rewards: Teen {data.GetStageReward(FishGrowthStage.Teen)} / " +
                $"Adult {data.GetStageReward(FishGrowthStage.Adult)} | Rarity x{rarityMult:F1}";
        }

        Pressed -= OnPressed;
        Pressed += OnPressed;
    }

    private void OnPressed()
    {
        _onBuyCallback?.Invoke(_fishData);
    }
}
