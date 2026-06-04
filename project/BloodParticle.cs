using Godot;

public partial class BloodParticle : Node2D
{
    private float _vx;
    private float _vy;
    private float _age;
    private float _size;
    private const float Lifetime = 5f;
    private const float Gravity = 80f;

    public static void Spawn(Node parent, Vector2 position, int count = 8)
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        for (var i = 0; i < count; i++)
        {
            var p = new BloodParticle
            {
                _vx    = rng.RandfRange(-50f, 50f),
                _vy    = rng.RandfRange(-80f, -10f),
                _size  = rng.RandfRange(4f, 10f),
                Position = position + new Vector2(
                    rng.RandfRange(-12f, 12f),
                    rng.RandfRange(-12f, 12f))
            };
            parent.AddChild(p);
        }
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(-_size * 0.5f, -_size * 0.5f, _size, _size),
                 new Color(0.85f, 0.05f, 0.05f));
    }

    public override void _Process(double delta)
    {
        var d = (float)delta;
        _age += d;

        if (_age >= Lifetime) { QueueFree(); return; }

        _vy += Gravity * d;
        Position += new Vector2(_vx * d, _vy * d);
        Modulate = new Color(1f, 1f, 1f, 1f - _age / Lifetime);
    }
}
