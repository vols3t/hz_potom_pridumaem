using Godot;
using System;

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

    public event Action<Node2d> StageChanged;

    private Vector2 _direction;
    private float _directionTimer;
    private float _hitTimer;
    private float _cooldownTimer;
    private bool _isHit;

    private AnimationPlayer _anim;

    private Node2D _visualRoot;
    private Sprite2D _bodySprite;
    private Sprite2D _finsSprite;
    private Sprite2D _tailSprite;
    private Sprite2D _eyesSprite;
    private Sprite2D _mutationOverlay;

    private Vector2 _visualBaseScale = Vector2.One;

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
        AdvanceGrowth((float)delta);
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

        _directionTimer -= d;
        if (_directionTimer <= 0)
            PickRandomDirection();

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

        _visualRoot.Scale = _visualBaseScale * Data.GetStageScale(CurrentStage);
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
        UpdateStageScale();
    }

    private void ReactToWallCollision(Vector2 wallNormal)
    {
        // 75%: сразу меняем направление. 25%: короткий hit, потом случайный разворот.
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
        if (_visualRoot == null || dir == Vector2.Zero)
            return;

        var angle = dir.Angle();
        if (!SpriteFacesRight)
            angle += Mathf.Pi;

        _visualRoot.Rotation = angle;
    }
}