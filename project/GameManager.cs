using Godot;
using System.Collections.Generic;

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }

    public float Money { get; private set; } = 300f;
    public int FishCount => _fishList.Count;
    public string LastEventText { get; private set; } = "";

    [Export] public int MaxFishCount = 60;
    [Export] public int CommonBirthCoins = 12;
    [Export] public int RareBirthCoins = 28;
    [Export] public int UniqueBirthCoins = 55;

    [ExportCategory("Breeding")]
    [Export] public float BreedChanceOnContact = 0.45f;
    [Export] public float ParentBreedCooldownSec = 12f;
    [Export] public float MeetingCheckIntervalSec = 0.5f;
    [Export] public float MeetingDistance = 85f;

    private readonly List<Node2d> _fishList = new();
    private readonly Dictionary<Node2d, float> _nextBreedAtSec = new();

    private Node2D _aquarium;
    private FishData[] _catalog = System.Array.Empty<FishData>();
    private Vector2 _spawnAreaMin = new(100, 100);
    private Vector2 _spawnAreaMax = new(500, 400);
    private float _elapsedSec = 0f;
    private float _meetingTimerSec = 0f;

    public override void _EnterTree()
    {
        if (Instance == null)
        {
            Instance = this;
            return;
        }

        QueueFree();
    }

    public override void _Process(double delta)
    {
        _elapsedSec += (float)delta;
        _meetingTimerSec += (float)delta;

        if (_meetingTimerSec >= MeetingCheckIntervalSec)
        {
            _meetingTimerSec = 0f;
            EvaluateMeetings();
        }
    }

    public void ConfigureAquarium(Node2D aquarium, FishData[] catalog, Vector2 spawnMin, Vector2 spawnMax)
    {
        _aquarium = aquarium;
        _catalog = catalog ?? System.Array.Empty<FishData>();
        _spawnAreaMin = spawnMin;
        _spawnAreaMax = spawnMax;
    }

    public void RegisterFish(Node2d fish)
    {
        if (fish == null || _fishList.Contains(fish))
            return;

        _fishList.Add(fish);
        fish.StageChanged += OnFishStageChanged;
        _nextBreedAtSec[fish] = 0f;
    }

    public void UnregisterFish(Node2d fish)
    {
        if (fish == null)
            return;

        fish.StageChanged -= OnFishStageChanged;

        _fishList.Remove(fish);
        _nextBreedAtSec.Remove(fish);
    }

    public bool TryBuyFish(FishData data)
    {
        if (data == null || data.FishScene == null)
            return false;

        if (!SpendMoney(data.Price))
            return false;

        SpawnFish(data, true, GetRandomSpawnPosition());
        LastEventText = $"Purchased {data.FishName} for {data.Price} coins";
        return true;
    }

    public int GetFishCountByRarity(FishRarity rarity)
    {
        var count = 0;
        foreach (var fish in _fishList)
        {
            if (fish.Data != null && fish.Data.Rarity == rarity)
                count++;
        }

        return count;
    }

    public bool SpendMoney(float amount)
    {
        if (amount <= 0f)
            return true;

        if (Money < amount)
            return false;

        Money -= amount;
        return true;
    }

    private void OnFishStageChanged(Node2d fish)
    {
        if (fish?.Data == null)
            return;

        var reward = fish.Data.GetStageReward(fish.CurrentStage);
        if (reward <= 0)
            return;

        AddCoins(reward, $"{fish.FishName} reached {fish.CurrentStage}: +{reward}");
    }

    private void EvaluateMeetings()
    {
        if (_fishList.Count < 2 || FishCount >= MaxFishCount)
            return;

        for (var i = 0; i < _fishList.Count - 1; i++)
        {
            var fishA = _fishList[i];
            if (fishA == null)
                continue;

            for (var j = i + 1; j < _fishList.Count; j++)
            {
                var fishB = _fishList[j];
                if (fishB == null)
                    continue;

                if (fishA.GlobalPosition.DistanceTo(fishB.GlobalPosition) > MeetingDistance)
                    continue;

                var meetPos = (fishA.GlobalPosition + fishB.GlobalPosition) * 0.5f;
                if (TryBreedByMeeting(fishA, fishB, meetPos))
                    return;
            }
        }
    }

    private bool TryBreedByMeeting(Node2d parentA, Node2d parentB, Vector2 contactPos)
    {
        if (_aquarium == null || FishCount >= MaxFishCount)
            return false;

        if (!CanBreed(parentA) || !CanBreed(parentB))
            return false;

        if (GD.Randf() > BreedChanceOnContact)
            return false;

        var offspringData = RollOffspring(parentA.Data, parentB.Data);
        if (offspringData == null)
            return false;

        var spawnPos = ClampToSpawnArea(contactPos + new Vector2(
            (float)GD.RandRange(-30.0, 30.0),
            (float)GD.RandRange(-20.0, 20.0)
        ));

        var spawned = SpawnFish(offspringData, true, spawnPos);
        if (spawned == null)
            return false;

        var nextTime = _elapsedSec + ParentBreedCooldownSec;
        _nextBreedAtSec[parentA] = nextTime;
        _nextBreedAtSec[parentB] = nextTime;
        _nextBreedAtSec[spawned] = _elapsedSec + ParentBreedCooldownSec * 0.8f;

        var birthReward = GetBirthReward(offspringData.Rarity);
        AddCoins(birthReward, $"New {offspringData.Rarity} fish born: +{birthReward}");
        return true;
    }

    private bool CanBreed(Node2d fish)
    {
        if (fish?.Data == null || !fish.IsAdult)
            return false;

        var nextTime = _nextBreedAtSec.TryGetValue(fish, out var v) ? v : 0f;
        return _elapsedSec >= nextTime;
    }

    private FishData RollOffspring(FishData parentA, FishData parentB)
    {
        if (parentA == null || parentB == null)
            return null;

        var roll = GD.Randf();
        if (roll < 0.15f)
            return PickWeightedFromCatalog();

        if (roll < 0.55f)
            return parentA;

        if (roll < 0.95f)
            return parentB;

        return parentA.Rarity >= parentB.Rarity ? parentA : parentB;
    }

    private FishData PickWeightedFromCatalog()
    {
        if (_catalog == null || _catalog.Length == 0)
            return null;

        var totalWeight = 0f;
        foreach (var fishData in _catalog)
        {
            if (fishData != null)
                totalWeight += Mathf.Max(0.01f, fishData.BreedWeight);
        }

        if (totalWeight <= 0f)
            return _catalog[0];

        var roll = GD.Randf() * totalWeight;

        foreach (var fishData in _catalog)
        {
            if (fishData == null)
                continue;

            roll -= Mathf.Max(0.01f, fishData.BreedWeight);
            if (roll <= 0f)
                return fishData;
        }

        return _catalog[_catalog.Length - 1];
    }

    private Node2d SpawnFish(FishData data, bool startAsFry, Vector2 spawnPos)
    {
        if (_aquarium == null || data == null || data.FishScene == null || FishCount >= MaxFishCount)
            return null;

        var fishNode = data.FishScene.Instantiate<Node2D>();
        if (fishNode is not Node2d fishScript)
            return null;

        fishScript.SetupFromData(data, startAsFry);
        fishNode.Position = ClampToSpawnArea(spawnPos);

        _aquarium.AddChild(fishNode);
        return fishScript;
    }

    private Vector2 GetRandomSpawnPosition()
    {
        var x = (float)GD.RandRange(_spawnAreaMin.X, _spawnAreaMax.X);
        var y = (float)GD.RandRange(_spawnAreaMin.Y, _spawnAreaMax.Y);
        return new Vector2(x, y);
    }

    private Vector2 ClampToSpawnArea(Vector2 pos)
    {
        return new Vector2(
            Mathf.Clamp(pos.X, _spawnAreaMin.X, _spawnAreaMax.X),
            Mathf.Clamp(pos.Y, _spawnAreaMin.Y, _spawnAreaMax.Y)
        );
    }

    private int GetBirthReward(FishRarity rarity)
    {
        return rarity switch
        {
            FishRarity.Common => CommonBirthCoins,
            FishRarity.Rare => RareBirthCoins,
            FishRarity.Unique => UniqueBirthCoins,
            _ => CommonBirthCoins
        };
    }

    private void AddCoins(int amount, string eventText)
    {
        if (amount <= 0)
            return;

        Money += amount;
        LastEventText = eventText;
    }
}
