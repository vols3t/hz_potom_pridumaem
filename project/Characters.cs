using Godot;

// Convenient sprite access for dialogue authoring.
// Usage: Characters.Aqua.Stable, Characters.Aqua.Happy, etc.
public static class Characters
{
    public static class Aqua
    {
        private static Texture2D _stable, _happy, _sad;
        public static Texture2D Stable => _stable ??= GD.Load<Texture2D>("res://assets/character/aqua/aqua_stable.png");
        public static Texture2D Happy  => _happy  ??= GD.Load<Texture2D>("res://assets/character/aqua/aqua_happy.png");
        public static Texture2D Sad    => _sad    ??= GD.Load<Texture2D>("res://assets/character/aqua/aqua_sad.png");
    }
    public static class Hero
    {
        private static Texture2D _stable;
        public static Texture2D Stable => _stable ??= GD.Load<Texture2D>("res://assets/character/mainhero/mainhero_stable.png");
    }
}
