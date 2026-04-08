using Godot;
using System;

public partial class Node2d : CharacterBody2D
{
    [ExportCategory("Fish Info")] [Export] public string FishName = "Обычная рыбка";
    [Export(PropertyHint.MultilineText)] public string Description = "Просто плавает";
    [Export] public float IncomePerSec = 1f; 

    [ExportCategory("Movement & Animation")] [Export]
    public float Speed = 100f;

    [Export] public float DirectionChangeTime = 2.5f;
    [Export] public float HitAnimationTime = 3f;
    [Export] public float CollisionCooldown = 1.5f;

    private Vector2 direction;
    private float directionTimer;
    private float hitTimer;
    private float cooldownTimer;
    private bool isHit;
    private AnimationPlayer anim;
    
    public override void _EnterTree()
    {
        if (GameManager.Instance != null) GameManager.Instance.RegisterFish(IncomePerSec);
    }
    
    public override void _ExitTree()
    {
        if (GameManager.Instance != null) GameManager.Instance.UnregisterFish(IncomePerSec);
    }

    public override void _Ready()
    {
        anim = GetNode<AnimationPlayer>("AnimationPlayer");
        anim.Play("swim");
        PickRandomDirection();
    }

    public override void _PhysicsProcess(double delta)
    {
        var d = (float)delta;
        Velocity = Vector2.Zero;

        if (cooldownTimer > 0) cooldownTimer -= d;

        if (isHit)
        {
            hitTimer -= d;
            if (hitTimer <= 0) EndHit();
            return;
        }

        directionTimer -= d;
        if (directionTimer <= 0) PickRandomDirection();

        Velocity = direction * Speed;
        MoveAndSlide();
        CheckCollisions();
    }

    private void PickRandomDirection()
    {
        direction = new Vector2(
            (float)GD.RandRange(-1.0, 1.0),
            (float)GD.RandRange(-1.0, 1.0)
        ).Normalized();
        directionTimer = DirectionChangeTime;
    }

    private void CheckCollisions()
    {
        if (cooldownTimer > 0) return;

        for (var i = 0; i < GetSlideCollisionCount(); i++)
        {
            var collider = GetSlideCollision(i).GetCollider();
            if (collider is StaticBody2D || collider is Node2d)
            {
                StartHit();
                break;
            }
        }
    }

    private void StartHit()
    {
        isHit = true;
        hitTimer = HitAnimationTime;
        cooldownTimer = CollisionCooldown;
        Velocity = Vector2.Zero;
        anim.Play("hit");
    }

    private void EndHit()
    {
        isHit = false;
        anim.Play("swim");
        PickRandomDirection();
    }
}