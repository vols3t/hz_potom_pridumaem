using Godot;

[GlobalClass]
public partial class FishData : Resource
{
    [Export] public string FishName = "Рыбка";
    [Export(PropertyHint.MultilineText)] public string Description = "Описание";
    [Export] public int Price = 100;
    [Export] public float IncomePerSec = 1.0f;
    [Export] public Texture2D Icon;
    [Export] public PackedScene FishScene;
}