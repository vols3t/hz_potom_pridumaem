using Godot;
using System;

public partial class ShopPanel : PanelContainer
{
    [ExportCategory("Item Setup")] [Export]
    public PackedScene ShopItemScene;

    [Export] public FishData[] AvailableFish;

    [ExportCategory("UI References")] [Export]
    public VBoxContainer ItemList;

    [Export] public Button FishTabBtn;
    [Export] public Button FoodTabBtn;
    [Export] public Button OtherTabBtn;

    [ExportCategory("Spawning")] [Export] public Node2D Aquarium;
    [Export] public Vector2 SpawnAreaMin = new(100, 100);
    [Export] public Vector2 SpawnAreaMax = new(500, 400);

    public override void _Ready()
    {
        if (FishTabBtn != null) FishTabBtn.Pressed += () => ShowTab("fish");
        if (FoodTabBtn != null) FoodTabBtn.Pressed += () => ShowTab("food");
        if (OtherTabBtn != null) OtherTabBtn.Pressed += () => ShowTab("other");

        ShowTab("fish");
    }

    private void ShowTab(string tab)
    {
        ClearItemList();

        if (tab == "fish")
        {
            foreach (var fishData in AvailableFish)
                CreateFishItem(fishData);
        }

        else if (tab == "food")
        {
            var placeholder = new Label { Text = "Корм пока не завезли..." };
            ItemList.AddChild(placeholder);
        }
        else if (tab == "other")
        {
            var placeholder = new Label { Text = "Скоро будет..." };
            ItemList.AddChild(placeholder);
        }
    }

    private void CreateFishItem(FishData data)
    {
        var item = ShopItemScene.Instantiate<ShopItem>();
        ItemList.AddChild(item);
        item.Setup(data, OnBuyFish);
    }

    private void OnBuyFish(FishData data)
    {
        if (GameManager.Instance.Money < data.Price)
        {
            GD.Print("мало денег");
            return;
        }

        GameManager.Instance.SpendMoney(data.Price);

        SpawnFish(data);

        GD.Print($"Куплена рыбка: {data.FishName}!");
    }

    private void SpawnFish(FishData data)
    {
        if (data.FishScene == null || Aquarium == null) return;

        var fish = data.FishScene.Instantiate<Node2D>();

        if (fish is Node2d fishScript)
        {
            fishScript.FishName = data.FishName;
            fishScript.Description = data.Description;
            fishScript.IncomePerSec = data.IncomePerSec;
        }
        
        var x = (float)GD.RandRange(SpawnAreaMin.X, SpawnAreaMax.X);
        var y = (float)GD.RandRange(SpawnAreaMin.Y, SpawnAreaMax.Y);
        fish.Position = new Vector2(x, y);

        Aquarium.AddChild(fish);
    }

    private void ClearItemList()
    {
        foreach (var child in ItemList.GetChildren())
            child.QueueFree();
    }
}