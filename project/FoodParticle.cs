using Godot;

public partial class FoodParticle : Area2D
{
    [Export] public float FallSpeed = 30f;
    [Export] public float NutritionValue = 5f;
    [Export] public float LifetimeSec = 30f;

    private float _age = 0f;
    private float _floorY;
    private bool _landed = false;
    private Sprite2D _sprite;

    public void Setup(FoodData data, float floorY)
    {
        FallSpeed = data.FallSpeed;
        NutritionValue = data.NutritionValue;
        _floorY = floorY;

        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (_sprite != null && data.ParticleTexture != null)
            _sprite.Texture = data.ParticleTexture;

        Modulate = data.ParticleColor;

        var randomScale = (float)GD.RandRange(0.8, 1.2);
        Scale = new Vector2(randomScale, randomScale);
    }

    public override void _Ready()
    {
        CollisionLayer = 4;
        CollisionMask = 0;
        
        AddToGroup("food");

        Position += new Vector2(
            (float)GD.RandRange(-10, 10),
            (float)GD.RandRange(-5, 5)
        );
    }

    public override void _Process(double delta)
    {
        var d = (float)delta;

        _age += d;
        if (_age >= LifetimeSec)
        {
            QueueFree();
            return;
        }

        if (!_landed)
        {
            Position += new Vector2(0, FallSpeed * d);

            if (Position.Y >= _floorY)
            {
                Position = new Vector2(Position.X, _floorY);
                _landed = true;
            }
        }

        if (_age >= LifetimeSec - 5f)
        {
            var alpha = (Mathf.Sin(_age * 8f) + 1f) / 2f;
            Modulate = new Color(Modulate.R, Modulate.G, Modulate.B, Mathf.Lerp(0.3f, 1f, alpha));
        }
    }

    public void Consume() =>
        QueueFree();
}