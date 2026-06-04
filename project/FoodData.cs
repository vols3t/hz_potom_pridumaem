using Godot;

[GlobalClass]
public partial class FoodData : Resource
{
    [Export] public string FoodName = "Basic Food";
    [Export(PropertyHint.MultilineText)] public string Description = "Simple fish food";
    [Export] public int Price = 10;
    [Export] public int BatchSize = 5;
    [Export] public float NutritionValue = 5f;
    [Export] public int ParticleCount = 5;
    [Export] public float FallSpeed = 30f;
    [Export] public Texture2D Icon;
    [Export] public Texture2D ParticleTexture;
    [Export] public Color ParticleColor = new Color(0.8f, 0.6f, 0.2f, 1f);

    [ExportCategory("Bonuses")]
    [Export] public float BreedChanceBonus = 0f;
    [Export] public float GrowthMultiplier = 1f;
    [Export] public float BoostDurationSec = 30f;
    [Export] public bool IsUnlimited = false;
}