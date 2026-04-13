using Godot;

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

    public override void _Ready()
    {
        if (FishTabBtn != null) FishTabBtn.Pressed += () => ShowTab("fish");
        if (FoodTabBtn != null) FoodTabBtn.Pressed += () => ShowTab("food");
        if (OtherTabBtn != null) OtherTabBtn.Pressed += () => ShowTab("other");

        GameManager.Instance?.ConfigureAquarium(Aquarium, AvailableFish, SpawnAreaMin, SpawnAreaMax);

        ShowTab("fish");
    }

    private void ShowTab(string tab)
    {
        ClearItemList();

        if (tab == "fish")
        {
            if (AvailableFish == null || ShopItemScene == null || ItemList == null)
                return;

            foreach (var fishData in AvailableFish)
                CreateFishItem(fishData);
            return;
        }

        var placeholder = new Label
        {
            Text = tab == "food"
                ? "Food is not implemented in this prototype"
                : "Other items are not implemented yet"
        };

        ItemList?.AddChild(placeholder);
    }

    private void CreateFishItem(FishData data)
    {
        if (data == null || ShopItemScene == null || ItemList == null)
            return;

        var item = ShopItemScene.Instantiate<ShopItem>();
        ItemList.AddChild(item);
        item.Setup(data, OnBuyFish);
    }

    private void OnBuyFish(FishData data)
    {
        if (GameManager.Instance == null)
            return;

        var purchased = GameManager.Instance.TryBuyFish(data);
        if (!purchased)
        {
            GD.Print("Not enough coins or invalid fish data");
            return;
        }

        GD.Print($"Purchased fish: {data.FishName}");
    }

    private void ClearItemList()
    {
        if (ItemList == null)
            return;

        foreach (var child in ItemList.GetChildren())
            child.QueueFree();
    }
}
