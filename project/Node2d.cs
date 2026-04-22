using Godot;
using System;

public partial class Node2d : CharacterBody2D
{
    [ExportCategory("Fish Info")]
    [Export] public FishData InitialFishData;
    [Export] public string FishName = "Fish";
    [Export(PropertyHint.MultilineText)] public string Description = "Just swimming";

    [ExportCategory("Movement & Animation")]
    [Export] public float Speed = 100f;
    [Export] public float DirectionChangeTime = 2.5f;
    [Export] public float HitAnimationTime = 3f;
    [Export] public float CollisionCooldown = 1.5f;
    [Export] public bool SpriteFacesRight = false;

    public FishData Data { get; private set; }
    public FishGrowthStage CurrentStage { get; private set; } = FishGrowthStage.Fry;
    public float AgeSec { get; private set; } = 0f;
    public bool IsAdult => CurrentStage == FishGrowthStage.Adult;

    public event Action<Node2d> StageChanged;

    private Vector2 _direction;
    private float _directionTimer;
    private float _hitTimer;
    private float _cooldownTimer;
    private bool _isHit;
    private AnimationPlayer _anim;
    private Sprite2D _sprite;

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
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");

        // Fish move through each other, but still collide with aquarium walls.
        CollisionLayer = 2;
        CollisionMask = 1;

        if (Data == null && InitialFishData != null)
            SetupFromData(InitialFishData, true);

        PlaySwimAnimation();
        PickRandomDirection();
    }

    public override void _Process(double delta)
    {
        AdvanceGrowth((float)delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        var d = (float)delta;
        Velocity = Vector2.Zero;

        if (_cooldownTimer > 0) _cooldownTimer -= d;

        if (_isHit)
        {
            _hitTimer -= d;
            if (_hitTimer <= 0) EndHit();
            return;
        }

        _directionTimer -= d;
        if (_directionTimer <= 0) PickRandomDirection();

        Velocity = _direction * Speed;
        MoveAndSlide();
        CheckCollisions();
    }

    public void SetupFromData(FishData data, bool startAsFry, string customFishName = null)
    {
        Data = data;
        FishName = string.IsNullOrWhiteSpace(customFishName) ? ToRussianFishName(data.FishName) : customFishName;
        Description = data.Description;

        if (startAsFry)
        {
            ResetToInitialStage();
            return;
        }

        AgeSec = data.FryDurationSec + data.TeenDurationSec;
        SetStage(FishGrowthStage.Adult);
    }

    public static string ToRussianFishName(string fishName)
    {
        if (string.IsNullOrWhiteSpace(fishName))
            return "Рыбка";

        return fishName.Trim().ToLowerInvariant() switch
        {
            "goldfish" => "Золотая рыбка",
            "neon" => "Неон",
            "neon tetra" => "Неон",
            "clownfish" => "Клоун",
            "guppy" => "Гуппи",
            "angelfish" => "Скалярия",
            "discus" => "Дискус",
            "swordtail" => "Меченосец",
            "tetra" => "Тетра",
            "cardinal" => "Кардинал",
            "barb" => "Барбус",
            "molly" => "Моллинезия",
            "betta" => "Петушок",
            "phoenix koi" => "Феникс кои",
            _ => fishName
        };
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
        StageChanged?.Invoke(this);
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
        if (_cooldownTimer > 0) return;

        for (var i = 0; i < GetSlideCollisionCount(); i++)
        {
            var collision = GetSlideCollision(i);
            var collider = collision.GetCollider();

            if (collider is StaticBody2D)
            {
                ReactToWallCollision(collision.GetNormal());
                break;
            }

            if (collider is Node2d)
                continue;
        }
    }

    private void EndHit()
    {
        _isHit = false;
        PlaySwimAnimation();
        PickRandomDirection();
    }

    private void ResetToInitialStage()
    {
        AgeSec = 0f;
        CurrentStage = FishGrowthStage.Fry;
    }

    private void ReactToWallCollision(Vector2 wallNormal)
    {
        // 75%: instantly redirect. 25%: short pause animation, then random move.
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

    private void ApplyVisualDirection(Vector2 dir)
    {
        if (_sprite == null || dir == Vector2.Zero)
            return;

        var angle = dir.Angle();
        if (!SpriteFacesRight)
            angle += Mathf.Pi;

        _sprite.Rotation = angle;
    }
}
