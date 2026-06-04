using Godot;

public partial class WarehousePanel : PanelContainer
{
    [Export] public GridContainer ItemGrid;
    [Export] public PackedScene WarehouseItemScene;
    [Export] public Button CloseBtn;
    [Export] public Button FoodFilterBtn;
    [Export] public Button DecorFilterBtn;
    [Export] public Label EmptyLabel;
    [Export] public FoodData[] AvailableFoods;

    private DecorData[] _decorCatalog;

    private enum Filter { Food, Decor }
    private Filter _activeFilter = Filter.Food;

    public override void _Ready()
    {
        Visible = false;
        _decorCatalog = LoadDecorCatalog();
        if (CloseBtn != null) CloseBtn.Pressed += ClosePanel;
        if (FoodFilterBtn != null) FoodFilterBtn.Pressed += () => SetFilter(Filter.Food);
        if (DecorFilterBtn != null) DecorFilterBtn.Pressed += () => SetFilter(Filter.Decor);
    }

    private static DecorData[] LoadDecorCatalog()
    {
        const string decorDir = "res://assets/decor";
        var result = new Godot.Collections.Array<DecorData>();

        if (!DirAccess.DirExistsAbsolute(decorDir))
            return System.Array.Empty<DecorData>();

        using var dir = DirAccess.Open(decorDir);
        if (dir == null) return System.Array.Empty<DecorData>();

        dir.ListDirBegin();
        while (true)
        {
            var fileName = dir.GetNext();
            if (string.IsNullOrEmpty(fileName)) break;
            if (dir.CurrentIsDir()) continue;
            if (!fileName.EndsWith(".tres") && !fileName.EndsWith(".res")) continue;
            var decor = GD.Load<DecorData>($"{decorDir}/{fileName}");
            if (decor != null) result.Add(decor);
        }
        dir.ListDirEnd();

        var arr = new DecorData[result.Count];
        for (var i = 0; i < result.Count; i++) arr[i] = result[i];
        return arr;
    }

    public void OpenPanel()
    {
        if (!IsInsideTree()) return;
        Visible = true;
        MoveToFront();
        CenterOnScreen();
        Refresh();
    }

    private void CenterOnScreen()
    {
        var viewportSize = GetViewport().GetVisibleRect().Size;
        var s = Size == Vector2.Zero ? CustomMinimumSize : Size;
        Position = ((viewportSize - s) * 0.5f).Floor();
    }

    public void ClosePanel() => Visible = false;

    private void SetFilter(Filter filter)
    {
        _activeFilter = filter;
        UpdateFilterButtons();
        Refresh();
    }

    private void UpdateFilterButtons()
    {
        if (FoodFilterBtn != null) FoodFilterBtn.ButtonPressed = _activeFilter == Filter.Food;
        if (DecorFilterBtn != null) DecorFilterBtn.ButtonPressed = _activeFilter == Filter.Decor;
    }

    private void Refresh()
    {
        if (ItemGrid == null || WarehouseItemScene == null) return;

        foreach (var child in ItemGrid.GetChildren())
            child.QueueFree();

        if (_activeFilter == Filter.Food)
            RefreshFood();
        else
            RefreshDecor();
    }

    private void RefreshFood()
    {
        var gm = GameManager.Instance;
        if (gm == null || AvailableFoods == null)
        {
            ShowEmpty(true);
            return;
        }

        var snapshot = gm.GetFoodInventorySnapshot(AvailableFoods);
        ShowEmpty(snapshot.Count == 0);

        foreach (var (food, count) in snapshot)
        {
            var item = WarehouseItemScene.Instantiate<WarehouseItem>();
            var f = food;
            item.SetupForFood(food, count, () =>
            {
                ClosePanel();
                FoodDropper.Instance?.StartDropMode(f);
            });
            ItemGrid.AddChild(item);
        }
    }

    private void RefreshDecor()
    {
        var gm = GameManager.Instance;
        if (gm == null || WarehouseItemScene == null)
        {
            ShowEmpty(true);
            return;
        }

        var hasAny = false;

        var inventory = gm.GetDecorInventorySnapshot(_decorCatalog);
        foreach (var (decor, count) in inventory)
        {
            var item = WarehouseItemScene.Instantiate<WarehouseItem>();
            var d = decor;
            item.SetupForDecorInventory(decor, count, () =>
            {
                ClosePanel();
                DecorPlacer.Instance?.StartPlaceMode(d);
            });
            ItemGrid.AddChild(item);
            hasAny = true;
        }

        var placed = gm.GetPlacedDecors();
        foreach (var (data, _, index) in placed)
        {
            var item = WarehouseItemScene.Instantiate<WarehouseItem>();
            var i = index;
            item.SetupForDecorPlaced(data, index, idx =>
            {
                gm.RemovePlacedDecor(idx);
                Refresh();
            });
            ItemGrid.AddChild(item);
            hasAny = true;
        }

        ShowEmpty(!hasAny);
    }

    private void ShowEmpty(bool empty)
    {
        if (EmptyLabel != null) EmptyLabel.Visible = empty;
    }
}
