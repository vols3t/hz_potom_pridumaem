using Godot;
using System;
using System.Collections.Generic;

public partial class Node2d : CharacterBody2D
{
    [ExportCategory("Fish Info")] [Export] public FishData InitialFishData;
    [Export] public string FishName = "Fish";
    [Export(PropertyHint.MultilineText)] public string Description = "Just swimming";

    [ExportCategory("Movement & Animation")] [Export]
    public float Speed = 100f;

    [Export] public float DirectionChangeTime = 2.5f;
    [Export] public float HitAnimationTime = 3f;
    [Export] public float CollisionCooldown = 1.5f;
    [Export] public bool SpriteFacesRight = false;

    public FishData Data { get; private set; }
    public FishGrowthStage CurrentStage { get; private set; } = FishGrowthStage.Fry;
    public float AgeSec { get; private set; } = 0f;
    public bool IsAdult => CurrentStage == FishGrowthStage.Adult;

    private readonly List<FishMutation> _mutations = new();
    public IReadOnlyList<FishMutation> Mutations => _mutations;
    public bool IsPredator { get; private set; } = false;
    public float FoodEaten { get; private set; } = 0f;
    public float TimeSinceLastFed { get; private set; } = 0f;
    public FishData ParentA { get; private set; }

    public FishData ParentB { get; private set; }


    [ExportCategory("Hunger")] [Export] public float HungerThreshold = 10f;

    [Export] public float FoodDetectionRange = 400f; // Радиус поиска еды

    private FoodParticle _targetFood = null;
    private bool _isSeekingFood = false;
    public event Action<Node2d> StageChanged;

    private Node2D _visualRoot;
    private Sprite2D _bodySprite;
    private Sprite2D _finsSprite;
    private Sprite2D _tailSprite;
    private Sprite2D _eyesSprite;
    private Sprite2D _mutationOverlay;

    private float _baseSpeed;
    private Vector2 _visualBaseScale = Vector2.One;
    private Vector2 _direction;
    private float _directionTimer;
    private float _hitTimer;
    private float _cooldownTimer;
    private bool _isHit;
    private AnimationPlayer _anim;

    public override void _EnterTree()
    {
        GameManager.Instance?.RegisterFish(this);
    }

    public override void _ExitTree()
    {
        GameManager.Instance?.UnregisterFish(this);
    }

    public override void _Ready()
    {
        _anim = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

        _visualRoot = GetNodeOrNull<Node2D>("VisualRoot");
        _bodySprite = GetNodeOrNull<Sprite2D>("VisualRoot/BodySprite");
        _finsSprite = GetNodeOrNull<Sprite2D>("VisualRoot/FinsSprite");
        _tailSprite = GetNodeOrNull<Sprite2D>("VisualRoot/TailSprite");
        _eyesSprite = GetNodeOrNull<Sprite2D>("VisualRoot/EyesSprite");
        _mutationOverlay = GetNodeOrNull<Sprite2D>("VisualRoot/MutationOverlay");

        if (_visualRoot != null)
            _visualBaseScale = _visualRoot.Scale;

        if (_mutationOverlay != null)
            _mutationOverlay.Visible = false;

        _baseSpeed = Speed;

        CollisionLayer = 2;
        CollisionMask = 1;

        if (Data != null)
        {
            ApplyBodyParts(Data);
            UpdateStageScale();
        }
        else if (InitialFishData != null) SetupFromData(InitialFishData, true);

        PlaySwimAnimation();
        PickRandomDirection();
    }

    public override void _Process(double delta)
    {
        var d = (float)delta;
        AdvanceGrowth(d);
        ProcessHunger(d);
        SeekFood();
    }

    public override void _PhysicsProcess(double delta)
    {
        var d = (float)delta;
        Velocity = Vector2.Zero;

        if (_cooldownTimer > 0)
            _cooldownTimer -= d;

        if (_isHit)
        {
            _hitTimer -= d;
            if (_hitTimer <= 0)
                EndHit();

            return;
        }
        
        var movingToFood = MoveTowardsFood();

        if (!movingToFood)
        {
            _directionTimer -= d;
            if (_directionTimer <= 0)
                PickRandomDirection();
        }

        Velocity = _direction * Speed;
        MoveAndSlide();

        CheckCollisions();
    }


    public void SetupFromData(FishData data, bool startAsFry)
    {
        if (data == null)
            return;

        Data = data;
        FishName = data.FishName;
        Description = data.Description;

        ApplyBodyParts(data);

        if (startAsFry)
        {
            ResetToInitialStage();
            return;
        }

        AgeSec = data.FryDurationSec + data.TeenDurationSec;
        SetStage(FishGrowthStage.Adult);
    }

    public void SetupAsHybrid(FishData momData, FishData dadData, bool startAsFry)
    {
        Data = momData;
        FishName = $"{momData.FishName}-{dadData.FishName}";
        Description = $"Hybrid of {momData.FishName} and {dadData.FishName}";

        ParentA = momData;
        ParentB = dadData;

        ApplyHybridBodyParts(momData, dadData);

        if (startAsFry)
        {
            ResetToInitialStage();
            return;
        }

        AgeSec = momData.FryDurationSec + momData.TeenDurationSec;
        SetStage(FishGrowthStage.Adult);
    }

    public void SetParents(FishData parentA, FishData parentB)
    {
        ParentA = parentA;
        ParentB = parentB;
    }

    private void ApplyBodyParts(FishData data)
    {
        if (data == null)
            return;

        if (_bodySprite != null && data.BodyTexture != null)
            _bodySprite.Texture = data.BodyTexture;

        if (_finsSprite != null)
        {
            _finsSprite.Texture = data.FinsTexture;
            _finsSprite.Visible = data.FinsTexture != null;
        }

        if (_tailSprite != null)
        {
            _tailSprite.Texture = data.TailTexture;
            _tailSprite.Visible = data.TailTexture != null;
        }

        if (_eyesSprite != null)
        {
            _eyesSprite.Texture = data.EyesTexture;
            _eyesSprite.Visible = data.EyesTexture != null;
        }

        if (_visualRoot != null)
            _visualRoot.Modulate = data.BaseColor;
    }

    private void ApplyHybridBodyParts(FishData mom, FishData dad)
    {
        if (_bodySprite != null)
            _bodySprite.Texture = mom.BodyTexture ?? dad.BodyTexture;

        if (_eyesSprite != null)
        {
            _eyesSprite.Texture = mom.EyesTexture ?? dad.EyesTexture;
            _eyesSprite.Visible = _eyesSprite.Texture != null;
        }

        if (_finsSprite != null)
        {
            _finsSprite.Texture = dad.FinsTexture ?? mom.FinsTexture;
            _finsSprite.Visible = _finsSprite.Texture != null;
        }

        if (_tailSprite != null)
        {
            _tailSprite.Texture = dad.TailTexture ?? mom.TailTexture;
            _tailSprite.Visible = _tailSprite.Texture != null;
        }

        if (_visualRoot != null)
            _visualRoot.Modulate = mom.BaseColor.Lerp(dad.BaseColor, 0.5f);
    }

    public bool AddMutation(FishMutation mutation)
    {
        if (mutation == null)
            return false;

        if (mutation.RequiresAdult && !IsAdult)
            return false;

        foreach (var m in _mutations)
            if (m.MutationName == mutation.MutationName)
                return false;

        _mutations.Add(mutation);
        ApplyAllMutationEffects();

        GD.Print($"[Mutation] {FishName} got mutation: {mutation.MutationName}");
        return true;
    }

    public void RemoveMutation(FishMutation mutation)
    {
        if (_mutations.Remove(mutation))
            ApplyAllMutationEffects();
    }

    private void ApplyAllMutationEffects()
    {
        Speed = _baseSpeed;
        IsPredator = false;
        CollisionMask = 1;

        var baseColor = Data?.BaseColor ?? new Color(1, 1, 1, 1);
        var finalColor = baseColor;
        var mutationScale = Vector2.One;

        if (_mutationOverlay != null)
        {
            _mutationOverlay.Visible = false;
            _mutationOverlay.Texture = null;
        }

        if (Data != null && _bodySprite != null && Data.BodyTexture != null)
            _bodySprite.Texture = Data.BodyTexture;

        foreach (var m in _mutations)
        {
            Speed *= m.SpeedMultiplier;

            if (m.MakesPredator)
            {
                IsPredator = true;
                CollisionMask = 1 | 2;
            }

            finalColor *= m.ColorTint;

            mutationScale *= m.ScaleModifier;

            if (m.OverlayTexture != null && _mutationOverlay != null)
            {
                _mutationOverlay.Texture = m.OverlayTexture;
                _mutationOverlay.Visible = true;
            }

            if (m.BodyOverride != null && _bodySprite != null) _bodySprite.Texture = m.BodyOverride;
        }

        if (_visualRoot != null)
        {
            _visualRoot.Modulate = finalColor;

            var stageScale = Data?.GetStageScale(CurrentStage) ?? Vector2.One;
            _visualRoot.Scale = _visualBaseScale * stageScale * mutationScale;
        }
    }

    public float GetIncomeMultiplier()
    {
        var multiplier = 1.0f;
        foreach (var m in _mutations)
            multiplier *= m.IncomeMultiplier;

        return multiplier;
    }

    public void Feed(float amount)
    {
        FoodEaten += amount;
        TimeSinceLastFed = 0f;
    }

    private void ProcessHunger(float delta)
    {
        TimeSinceLastFed += delta;
    }

    private void EatFish(Node2d prey)
    {
        GD.Print($"[Predator] {FishName} ate {prey.FishName}!");

        FoodEaten += 5f;
        TimeSinceLastFed = 0f;
        _cooldownTimer = CollisionCooldown;

        prey.QueueFree();
    }


    private void AdvanceGrowth(float delta)
    {
        if (Data == null)
            return;

        AgeSec += delta;

        if (CurrentStage == FishGrowthStage.Fry && AgeSec >= Data.FryDurationSec)
            SetStage(FishGrowthStage.Teen);

        if (CurrentStage == FishGrowthStage.Teen && AgeSec >= Data.FryDurationSec + Data.TeenDurationSec)
            SetStage(FishGrowthStage.Adult);
    }

    private void SetStage(FishGrowthStage newStage)
    {
        if (CurrentStage == newStage)
            return;

        CurrentStage = newStage;
        UpdateStageScale();
        StageChanged?.Invoke(this);
    }

    private void UpdateStageScale()
    {
        if (_visualRoot == null || Data == null)
            return;

        var stageScale = Data.GetStageScale(CurrentStage);

        var mutationScale = Vector2.One;
        foreach (var m in _mutations)
            mutationScale *= m.ScaleModifier;

        _visualRoot.Scale = _visualBaseScale * stageScale * mutationScale;
    }

    private void ResetToInitialStage()
    {
        AgeSec = 0f;
        CurrentStage = FishGrowthStage.Fry;
        UpdateStageScale();
    }

    private void PickRandomDirection()
    {
        _direction = new Vector2(
            (float)GD.RandRange(-1.0, 1.0),
            (float)GD.RandRange(-1.0, 1.0)
        ).Normalized();

        ApplyVisualDirection(_direction);
        _directionTimer = DirectionChangeTime;
    }

    private void CheckCollisions()
    {
        if (_cooldownTimer > 0)
            return;

        for (var i = 0; i < GetSlideCollisionCount(); i++)
        {
            var collision = GetSlideCollision(i);
            var collider = collision.GetCollider();

            if (collider is StaticBody2D)
            {
                ReactToWallCollision(collision.GetNormal());
                break;
            }

            if (collider is Node2d otherFish)
            {
                if (IsPredator && !otherFish.IsPredator)
                {
                    EatFish(otherFish);
                    break;
                }

                continue;
            }
        }
    }

    private void ReactToWallCollision(Vector2 wallNormal)
    {
        if (GD.Randf() < 0.75f)
        {
            var reflected = _direction.Bounce(wallNormal).Normalized();
            var randomDir = new Vector2(
                (float)GD.RandRange(-1.0, 1.0),
                (float)GD.RandRange(-1.0, 1.0)
            ).Normalized();

            _direction = reflected.Lerp(randomDir, 0.35f).Normalized();

            if (_direction == Vector2.Zero)
                PickRandomDirection();
            else
                ApplyVisualDirection(_direction);

            _directionTimer = (float)GD.RandRange(0.15, DirectionChangeTime);
            _cooldownTimer = Mathf.Max(_cooldownTimer, 0.1f);
            return;
        }

        StartHit(0.35f);
    }


    private void PlaySwimAnimation()
    {
        if (_anim == null)
            return;

        if (_anim.HasAnimation("swim"))
            _anim.Play("swim");
        else if (_anim.HasAnimation("swm"))
            _anim.Play("swm");
    }

    private void StartHit(float? customDuration = null)
    {
        _isHit = true;
        _hitTimer = customDuration ?? HitAnimationTime;
        _cooldownTimer = CollisionCooldown;
        Velocity = Vector2.Zero;

        if (_anim != null && _anim.HasAnimation("hit"))
            _anim.Play("hit");
    }

    private void EndHit()
    {
        _isHit = false;
        PlaySwimAnimation();
        PickRandomDirection();
    }

    private void ApplyVisualDirection(Vector2 dir)
    {
        if (_visualRoot == null || dir == Vector2.Zero)
            return;

        var angle = dir.Angle();
        if (!SpriteFacesRight)
            angle += Mathf.Pi;

        _visualRoot.Rotation = angle;
    }

    private void SeekFood()
    {
        if (TimeSinceLastFed < HungerThreshold)
        {
            _isSeekingFood = false;
            _targetFood = null;
            return;
        }

        _isSeekingFood = true;

        if (IsInstanceValid(_targetFood)
            && GlobalPosition.DistanceTo(_targetFood.GlobalPosition) <= FoodDetectionRange)
            return;

        _targetFood = null;
        var closestDist = FoodDetectionRange;

        foreach (var node in GetTree().GetNodesInGroup("food"))
        {
            if (node is not FoodParticle food)
                continue;

            var dist = GlobalPosition.DistanceTo(food.GlobalPosition);
            if (dist < closestDist)
            {
                closestDist = dist;
                _targetFood = food;
            }
        }
    }

    private bool MoveTowardsFood()
    {
        if (!_isSeekingFood || !IsInstanceValid(_targetFood))
        {
            _isSeekingFood = false;
            _targetFood = null;
            return false;
        }

        var dirToFood = GlobalPosition.DirectionTo(_targetFood.GlobalPosition);
        _direction = dirToFood;
        ApplyVisualDirection(_direction);

        if (GlobalPosition.DistanceTo(_targetFood.GlobalPosition) < 15f)
        {
            EatFood(_targetFood);
            return false;
        }

        return true;
    }

    private void EatFood(FoodParticle food)
    {
        if (!IsInstanceValid(food))
            return;

        Feed(food.NutritionValue);
        food.Consume();

        _targetFood = null;
        _isSeekingFood = false;

        GD.Print($"[Feed] {FishName} ate food! Total: {FoodEaten:F0}, hunger reset");
    }
}