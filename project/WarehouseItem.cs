using Godot;
using System;

public partial class WarehouseItem : PanelContainer
{
    [Export] public TextureRect IconRect;
    [Export] public Label NameLabel;
    [Export] public Label CountLabel;
    [Export] public Button UseButton;

    public void SetupForFood(FoodData food, int count, Action onUse)
    {
        if (IconRect != null) IconRect.Texture = food.Icon;
        if (NameLabel != null) NameLabel.Text = food.FoodName;
        if (CountLabel != null) CountLabel.Text = $"×{count}";

        if (UseButton != null)
        {
            UseButton.Visible = true;
            UseButton.Text = "использовать";
            UseButton.Disabled = count <= 0;
            UseButton.Pressed += onUse;
        }
    }

    public void SetupForDecor(string itemName, int count)
    {
        if (NameLabel != null) NameLabel.Text = itemName;
        if (CountLabel != null) CountLabel.Text = $"×{count}";
        if (UseButton != null) UseButton.Visible = false;
    }

    public void SetupForDecorInventory(DecorData decor, int count, Action onPlace)
    {
        if (IconRect != null) IconRect.Texture = decor?.Icon;
        if (NameLabel != null) NameLabel.Text = decor?.DecorName ?? "";
        if (CountLabel != null) CountLabel.Text = $"×{count}";
        if (UseButton != null)
        {
            UseButton.Visible = true;
            UseButton.Text = "поставить";
            UseButton.Disabled = count <= 0;
            if (onPlace != null) UseButton.Pressed += onPlace;
        }
    }

    public void SetupForDecorPlaced(DecorData decor, int slotIndex, Action<int> onRemove)
    {
        if (IconRect != null) IconRect.Texture = decor?.Icon;
        if (NameLabel != null) NameLabel.Text = $"{decor?.DecorName ?? ""}\n(размещено)";
        if (CountLabel != null) CountLabel.Text = $"#{slotIndex + 1}";
        if (UseButton != null)
        {
            UseButton.Visible = true;
            UseButton.Text = "убрать";
            UseButton.Disabled = false;
            if (onRemove != null) UseButton.Pressed += () => onRemove(slotIndex);
        }
    }
}
