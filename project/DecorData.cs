using Godot;

[GlobalClass]
public partial class DecorData : Resource
{
    [Export] public string DecorId = "";
    [Export] public string DecorName = "";
    [Export] public string Description = "";
    [Export] public Texture2D Icon;
    [Export] public Texture2D DecorTexture;
    [Export] public int Price = 200;
    [Export] public float HappinessBonus = 5f;
    [Export] public Vector2 DisplayScale = new(0.45f, 0.45f);
    [Export] public int PlacementZIndex = -50;
}
