using Godot;
using System;

public partial class Node2d : CharacterBody2D
{
    [Export] public float Speed = 100f;
    [Export] public float DirectionChangeTime = 2.5f;
    [Export] public float HitAnimationTime = 3f;
    [Export] public float CollisionCooldown = 1.5f;

    private Vector2 direction;
    private float directionTimer;
    private float hitTimer;
    private float cooldownTimer;

    private bool isHit;

    private AnimationPlayer anim;

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

        if (cooldownTimer > 0)
            cooldownTimer -= d;

        if (isHit)
        {
            hitTimer -= d;
            if (hitTimer <= 0)
                EndHit();

            return;
        }

        directionTimer -= d;
        if (directionTimer <= 0)
            PickRandomDirection();

        Velocity = direction * Speed;
        MoveAndSlide();

        CheckCollisions();
    }

    private void PickRandomDirection()
    {
        direction = new Vector2(
            (float)GD.RandRange(-1, 1),
            (float)GD.RandRange(-1, 1)
        ).Normalized();

        directionTimer = DirectionChangeTime;
    }

    private void CheckCollisions()
    {
        if (cooldownTimer > 0)
            return;

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