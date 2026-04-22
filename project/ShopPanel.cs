using Godot;
using System;

public partial class ShopPanel : PanelContainer
{
    [ExportCategory("Item Setup")]
    [Export] public PackedScene ShopItemScene;
    [Export] public FishData[] AvailableFish;

    [ExportCategory("UI References")]
    [Export] public VBoxContainer ItemList;
    [Export] public Button FishTabBtn;
    [Export] public Button FoodTabBtn;
    [Export] public Button OtherTabBtn;

    [ExportCategory("Spawning")]
    [Export] public Node2D Aquarium;
    [Export] public Vector2 SpawnAreaMin = new(100, 100);
    [Export] public Vector2 SpawnAreaMax = new(500, 400);

    private const string FishTab = "fish";
    private const string FoodTab = "food";
    private const string DecorTab = "decor";

    private Texture2D _fallbackIcon;
    private ShopCatalogItem[] _decorationItems = Array.Empty<ShopCatalogItem>();
    private ShopCatalogItem[] _foodItems = Array.Empty<ShopCatalogItem>();

    private sealed class ShopCatalogItem
    {
        public readonly string Name;
        public readonly string Description;
        public readonly int Price;
        public readonly string Details;
        public readonly Texture2D Icon;

        public ShopCatalogItem(string name, string description, int price, string details, Texture2D icon)
        {
            Name = name;
            Description = description;
            Price = price;
            Details = details;
            Icon = icon;
        }
    }

    public override void _Ready()
    {
        _fallbackIcon = GD.Load<Texture2D>("res://assets/fishes/medium/clown-fish-medium.png");
        BuildStaticCatalog();

        if (FishTabBtn != null)
        {
            FishTabBtn.Text = "Fish";
            FishTabBtn.Pressed += () => ShowTab(FishTab);
        }

        if (FoodTabBtn != null)
        {
            FoodTabBtn.Text = "Food";
            FoodTabBtn.Pressed += () => ShowTab(FoodTab);
        }

        if (OtherTabBtn != null)
        {
            OtherTabBtn.Text = "Decor";
            OtherTabBtn.Pressed += () => ShowTab(DecorTab);
        }

        GameManager.Instance?.ConfigureAquarium(Aquarium, AvailableFish, SpawnAreaMin, SpawnAreaMax);
        ShowTab(FishTab);
    }

    private void BuildStaticCatalog()
    {
        var decorIcon = GD.Load<Texture2D>("res://assets/settings/settings-button.png") ?? _fallbackIcon;
        var foodIcon = GD.Load<Texture2D>("res://assets/meal/meal-button.png") ?? _fallbackIcon;

        _decorationItems = new[]
        {
            new ShopCatalogItem(
                "Coral Arch",
                "Natural shelter that makes the aquarium look richer.",
                120,
                "Type: Decoration | Effect: visual style",
                decorIcon),
            new ShopCatalogItem(
                "Ancient Amphora",
                "Classic clay amphora for bottom-scene composition.",
                160,
                "Type: Decoration | Effect: visual style",
                decorIcon),
            new ShopCatalogItem(
                "Pearl Shell",
                "Rare decorative shell with a soft shiny accent.",
                220,
                "Type: Decoration | Effect: visual style",
                decorIcon)
        };

        _foodItems = new[]
        {
            new ShopCatalogItem(
                "Basic Flakes",
                "Affordable everyday food for small fish.",
                20,
                "Type: Food | Portion: small",
                foodIcon),
            new ShopCatalogItem(
                "Protein Granules",
                "Balanced food for active and growing fish.",
                45,
                "Type: Food | Portion: medium",
                foodIcon),
            new ShopCatalogItem(
                "Premium Mix",
                "High-quality feed with vitamins and minerals.",
                80,
                "Type: Food | Portion: large",
                foodIcon)
        };
    }

    private void ShowTab(string tab)
    {
        ClearItemList();

        switch (tab)
        {
            case FishTab:
                if (AvailableFish == null)
                    return;

                foreach (var fishData in AvailableFish)
                    CreateFishItem(fishData);
                break;

            case FoodTab:
                foreach (var item in _foodItems)
                    CreateCatalogItem(item, "food");
                break;

            case DecorTab:
                foreach (var item in _decorationItems)
                    CreateCatalogItem(item, "decoration");
                break;
        }
    }

    private void CreateFishItem(FishData data)
    {
        if (data == null || ShopItemScene == null || ItemList == null)
            return;

        var item = ShopItemScene.Instantiate<ShopItem>();
        ItemList.AddChild(item);

        var fryReward = data.GetStageReward(FishGrowthStage.Fry);
        var teenReward = data.GetStageReward(FishGrowthStage.Teen);
        var adultReward = data.GetStageReward(FishGrowthStage.Adult);
        var details =
            $"Rarity: {data.Rarity} | Stage coins: Fry +{fryReward}, Teen +{teenReward}, Adult +{adultReward} | " +
            $"x{data.GetRarityMultiplier():F1}";

        item.Setup(
            data.FishName,
            data.Description,
            data.Price,
            data.Icon ?? _fallbackIcon,
            details,
            "Buy fish",
            () => OnBuyFish(data));
    }

    private void CreateCatalogItem(ShopCatalogItem itemData, string category)
    {
        if (itemData == null || ShopItemScene == null || ItemList == null)
            return;

        var item = ShopItemScene.Instantiate<ShopItem>();
        ItemList.AddChild(item);

        item.Setup(
            itemData.Name,
            itemData.Description,
            itemData.Price,
            itemData.Icon ?? _fallbackIcon,
            itemData.Details,
            $"Buy {category}",
            () => OnBuyCategoryItem(itemData, category));
    }

    private void OnBuyFish(FishData data)
    {
        if (GameManager.Instance == null)
            return;

        if (!GameManager.Instance.TryBuyFish(data))
        {
            GD.Print("Not enough coins or invalid fish data");
            return;
        }

        GD.Print($"Purchased fish: {data.FishName}");
    }

    private void OnBuyCategoryItem(ShopCatalogItem itemData, string category)
    {
        if (GameManager.Instance == null || itemData == null)
            return;

        if (!GameManager.Instance.TryBuyShopItem(itemData.Name, itemData.Price, category))
        {
            GD.Print($"Not enough coins to buy {category}: {itemData.Name}");
            return;
        }

        GD.Print($"Purchased {category}: {itemData.Name}");
    }

    private void ClearItemList()
    {
        if (ItemList == null)
            return;

        foreach (var child in ItemList.GetChildren())
            child.QueueFree();
    }
}
