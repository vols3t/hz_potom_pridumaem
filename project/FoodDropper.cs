using Godot;

public partial class FoodDropper : Node
{
    public static FoodDropper Instance { get; private set; }

    [Export] public PackedScene FoodParticleScene;

    [Export] public float AquariumLeft = 200f;
    [Export] public float AquariumRight = 930f;
    [Export] public float AquariumTop = 160f;
    [Export] public float AquariumBottom = 530f;
    [Export] public float FloorOffset = 15f;
    private FoodData _selectedFood;
    private bool _isDropMode = false;
    private Node2D _aquarium;

    public override void _EnterTree()
    {
        if (Instance == null)
            Instance = this;
        else
            QueueFree();
    }

    public override void _Ready()
    {
        _aquarium = GetTree().CurrentScene as Node2D;
        if (_aquarium == null)
            _aquarium = GetTree().CurrentScene.GetNodeOrNull<Node2D>("Aquarium");
    }

    public override void _Input(InputEvent @event)
    {
        if (!_isDropMode || _selectedFood == null)
            return;

        if (@event is InputEventMouseButton mouseBtn
            && mouseBtn.ButtonIndex == MouseButton.Left
            && mouseBtn.Pressed)
        {
            var mousePos = GetViewport().GetMousePosition();

            if (IsInsideAquarium(mousePos))
            {
                DropFood(mousePos);
                EndDropMode();
            }
        }

        if (@event is InputEventMouseButton cancelBtn
            && cancelBtn.ButtonIndex == MouseButton.Right
            && cancelBtn.Pressed)
            EndDropMode();

        if (@event is InputEventKey key
            && key.Keycode == Key.Escape
            && key.Pressed)
            EndDropMode();
    }

    public void StartDropMode(FoodData food)
    {
        if (food == null)
            return;

        _selectedFood = food;
        _isDropMode = true;

        GD.Print($"[Food] Drop mode ON: {food.FoodName}. Click inside aquarium!");
    }

    public void EndDropMode()
    {
        _isDropMode = false;
        _selectedFood = null;
        GD.Print("[Food] Drop mode OFF");
    }

    public bool IsDropModeActive() => _isDropMode;

    private bool IsInsideAquarium(Vector2 pos) =>
        pos.X >= AquariumLeft
        && pos.X <= AquariumRight
        && pos.Y >= AquariumTop
        && pos.Y <= AquariumBottom;

    private void DropFood(Vector2 dropPosition)
    {
        if (FoodParticleScene == null || _aquarium == null || _selectedFood == null)
            return;

        for (var i = 0; i < _selectedFood.ParticleCount; i++)
        {
            var particle = FoodParticleScene.Instantiate<FoodParticle>();

            var offset = new Vector2(
                (float)GD.RandRange(-25, 25),
                (float)GD.RandRange(-10, 5)
            );

            particle.Position = dropPosition + offset;
            particle.Setup(_selectedFood, AquariumBottom - FloorOffset);

            _aquarium.AddChild(particle);
        }

        GD.Print($"[Food] Dropped {_selectedFood.ParticleCount} particles of {_selectedFood.FoodName}");
    }
}