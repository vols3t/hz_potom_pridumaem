using Godot;

[GlobalClass]
public partial class FishMutation : Resource
{
    [Export] public string MutationName = "Mutation";
    [Export(PropertyHint.MultilineText)] public string Description = "Mutation description";
    [Export] public MutationType Type = MutationType.Food;

    [ExportCategory("Visuals")] [Export] public Color ColorTint = new(1, 1, 1, 1);
    [Export] public Vector2 ScaleModifier = Vector2.One;
    [Export] public Texture2D OverlayTexture;

    [ExportCategory("Body Override")] [Export]
    public Texture2D BodyOverride; 

    [ExportCategory("Behavior")] [Export] public float SpeedMultiplier = 1.0f;
    [Export] public float IncomeMultiplier = 1.0f;
    [Export] public bool MakesPredator = false;
    [Export] public bool RequiresAdult = true; 

    [ExportCategory("Trigger")] [Export] public MutationTrigger Trigger = MutationTrigger.None;
    [Export] public float TriggerThreshold = 0f;
}

public enum MutationType
{
    Genetic,
    Food,
    Environment
}

public enum MutationTrigger
{
    None,
    FoodEaten,
    Starving,
    AgeReached,
    Overfed      
}