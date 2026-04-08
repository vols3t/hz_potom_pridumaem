using Godot;
using System;

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }
    public float Money { get; private set; } = 0f;
    public float IncomePerSecond { get; private set; } = 0f;
    public int FishCount { get; private set; } = 0;

    public override void _EnterTree()
    {
        if (Instance == null)
            Instance = this;
        else
            QueueFree();
    }

    public override void _Process(double delta)
    {
        if (IncomePerSecond > 0) Money += IncomePerSecond * (float)delta;
    }
    
    public void RegisterFish(float fishIncome)
    {
        FishCount++;
        IncomePerSecond += fishIncome;
    }
    
    public void UnregisterFish(float fishIncome)
    {
        FishCount--;
        IncomePerSecond -= fishIncome;

        if (FishCount < 0) FishCount = 0;
        if (IncomePerSecond < 0) IncomePerSecond = 0;
    }
    
    public bool SpendMoney(float amount)
    {
        if (Money >= amount)
        {
            Money -= amount;
            return true;
        }
        return false;
    }
}