using Godot;

[GlobalClass]
public partial class FishData : Resource
{
    [Export] public string FishName = "Fish";
    [Export(PropertyHint.MultilineText)] public string Description = "Fish description";
    [Export] public int Price = 100;
    [Export] public float IncomePerSec = 1.0f;
    [Export] public FishRarity Rarity = FishRarity.Common;

    [ExportCategory("Growth")]
    [Export] public float FryDurationSec = 20.0f;
    [Export] public float TeenDurationSec = 30.0f;

    [ExportCategory("Stage Rewards")]
    [Export] public int TeenStageCoins = 8;
    [Export] public int AdultStageCoins = 16;

    [ExportCategory("Breeding")]
    [Export] public float BreedWeight = 1.0f;
    [Export] public string SpeciesId = "";
    [Export] public string[] CompatibleSpeciesIds = System.Array.Empty<string>();

    [ExportCategory("Body Parts")]
    [Export] public Texture2D BodyTexture;
    [Export] public Texture2D FinsTexture;
    [Export] public Texture2D TailTexture;
    [Export] public Texture2D EyesTexture;
    [Export] public Color BaseColor = new Color(1, 1, 1, 1);
    [ExportCategory("Hybrid")]
    [Export] public Texture2D HybridTexture;
    
    [ExportCategory("Stage Scales")]
    [Export] public Vector2 FryScale = new Vector2(0.4f, 0.4f);
    [Export] public Vector2 TeenScale = new Vector2(0.7f, 0.7f);
    [Export] public Vector2 AdultScale = new Vector2(1.0f, 1.0f);

    [Export] public Texture2D Icon;
    [Export] public PackedScene FishScene;

    public float GetRarityMultiplier()
    {
        return Rarity switch
        {
            FishRarity.Common => 1.0f,
            FishRarity.Rare => 1.6f,
            FishRarity.Unique => 2.6f,
            _ => 1.0f
        };
    }

    public int GetStageReward(FishGrowthStage stage)
    {
        var baseReward = stage switch
        {
            FishGrowthStage.Teen => TeenStageCoins,
            FishGrowthStage.Adult => AdultStageCoins,
            _ => 0
        };
        return Mathf.RoundToInt(baseReward * GetRarityMultiplier());
    }

    public Vector2 GetStageScale(FishGrowthStage stage)
    {
        return stage switch
        {
            FishGrowthStage.Fry => FryScale,
            FishGrowthStage.Teen => TeenScale,
            FishGrowthStage.Adult => AdultScale,
            _ => AdultScale
        };
    }
}