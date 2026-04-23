using Godot;
using System.Collections.Generic;

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }

    public float Money { get; private set; } = 300f;
    public int FishCount => _fishList.Count;
    public string LastEventText { get; private set; } = "";

    [Export] public int MaxFishCount = 15;
    [Export] public int CommonBirthCoins = 12;
    [Export] public int RareBirthCoins = 28;
    [Export] public int UniqueBirthCoins = 55;

    [ExportCategory("Breeding")] [Export] public float BreedChanceOnContact = 0.45f;
    [Export] public float ParentBreedCooldownSec = 25f;
    [Export] public float MeetingCheckIntervalSec = 0.5f;
    [Export] public float MeetingDistance = 100f;
    [Export] public float HybridChance = 0.3f;

    [ExportCategory("Mutations")] [Export] public float MutationCheckInterval = 2.0f;

    private readonly List<Node2d> _fishList = new();
    private readonly Dictionary<Node2d, float> _nextBreedAtSec = new();
    private readonly Dictionary<string, int> _ownedShopItems = new();
    private readonly HashSet<FishData> _discoveredFish = new();
    private readonly Dictionary<string, FishData> _discoveredFishByName = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly List<FishMutation> _mutations = new();

    private Node2D _aquarium;
    private FishData[] _catalog = System.Array.Empty<FishData>();
    private Vector2 _spawnAreaMin = new(100, 100);
    private Vector2 _spawnAreaMax = new(500, 400);
    private float _elapsedSec = 0f;
    private float _meetingTimerSec = 0f;
    private float _mutationTimerSec = 0f;

    public override void _EnterTree()
    {
        if (Instance == null)
        {
            Instance = this;
            return;
        }

        QueueFree();
    }

    public override void _Ready() =>
        LoadMutations();


    public override void _Process(double delta)
    {
        var d = (float)delta;

        _elapsedSec += d;

        Money += GetIncomePerSecond() * d;

        _meetingTimerSec += d;
        if (_meetingTimerSec >= MeetingCheckIntervalSec)
        {
            _meetingTimerSec = 0f;
            EvaluateMeetings();
        }

        _mutationTimerSec += d;
        if (_mutationTimerSec >= MutationCheckInterval)
        {
            _mutationTimerSec = 0f;
            EvaluateMutations();
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

        fish.Clicked += OnFishClicked;
    }

    public void UnregisterFish(Node2d fish)
    {
        if (fish == null)
            return;

        fish.StageChanged -= OnFishStageChanged;
        fish.Clicked -= OnFishClicked;

        _fishList.Remove(fish);
        _nextBreedAtSec.Remove(fish);
    }

    public bool TryBuyFish(FishData data)
    {
        if (data == null || data.FishScene == null)
            return false;
        if (FishCount >= MaxFishCount)
        {
            LastEventText = $"Fish limit reached ({MaxFishCount})";
            return false;
        }

        if (!SpendMoney(data.Price))
            return false;

        var spawned = SpawnFish(data, true, GetRandomSpawnPosition());
        if (spawned == null)
        {
            Money += data.Price;
            LastEventText = "Unable to spawn fish right now";
            return false;
        }

        LastEventText = $"Purchased {spawned.FishName} for {data.Price} coins";
        return true;
    }

    public bool TryBuyFishOffer(FishData data, int offerPrice, string offerName)
    {
        if (data == null || data.FishScene == null || offerPrice < 0)
            return false;
        if (FishCount >= MaxFishCount)
        {
            LastEventText = $"Fish limit reached ({MaxFishCount})";
            return false;
        }

        if (!SpendMoney(offerPrice))
            return false;

        var spawned = SpawnFish(data, true, GetRandomSpawnPosition(), offerName);
        if (spawned == null)
        {
            Money += offerPrice;
            LastEventText = "Unable to spawn fish right now";
            return false;
        }

        var purchasedName = string.IsNullOrWhiteSpace(offerName) ? spawned.FishName : offerName;
        LastEventText = $"Purchased {purchasedName} for {offerPrice} coins";
        return true;
    }

    public bool TryBuyShopItem(string itemName, int price, string category)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return false;

        if (!SpendMoney(price))
            return false;

        var key = MakeInventoryKey(category, itemName);
        _ownedShopItems[key] = GetOwnedShopItemCount(category, itemName) + 1;
        LastEventText = $"Purchased {category}: {itemName} for {price} coins";
        return true;
    }

    public int GetOwnedShopItemCount(string category, string itemName)
    {
        var key = MakeInventoryKey(category, itemName);
        return _ownedShopItems.TryGetValue(key, out var value) ? value : 0;
    }

    public int GetOwnedCategoryCount(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return 0;

        var normalized = $"{category.Trim().ToLowerInvariant()}::";
        var total = 0;
        foreach (var pair in _ownedShopItems)
        {
            if (pair.Key.StartsWith(normalized))
                total += pair.Value;
        }

        return total;
    }

    public bool CanAfford(float amount)
    {
        return amount <= Money;
    }

    public int GetFishCountByRarity(FishRarity rarity)
    {
        var count = 0;
        foreach (var fish in _fishList)
            if (fish.Data != null && fish.Data.Rarity == rarity)
                count++;

        return count;
    }

    public IReadOnlyList<Node2d> GetFishSnapshot()
    {
        var snapshot = new List<Node2d>(_fishList.Count);
        foreach (var fish in _fishList)
        {
            if (fish != null && IsInstanceValid(fish))
                snapshot.Add(fish);
        }

        return snapshot;
    }

    public void NotifyFishDiscovered(FishData fishData, string fishName = null)
    {
        if (fishData == null || fishData.FishScene == null)
            return;

        _discoveredFish.Add(fishData);

        var normalizedName = NormalizeFishName(fishName ?? fishData.FishName);
        if (string.IsNullOrWhiteSpace(normalizedName))
            return;

        if (_discoveredFishByName.TryGetValue(normalizedName, out var existing) && existing != null)
            return;

        _discoveredFishByName[normalizedName] = fishData;
    }

    public bool HasDiscoveredFish(FishData fishData)
    {
        return fishData != null && _discoveredFish.Contains(fishData);
    }

    public bool HasDiscoveredFishName(string fishName)
    {
        var normalizedName = NormalizeFishName(fishName);
        return !string.IsNullOrWhiteSpace(normalizedName) && _discoveredFishByName.ContainsKey(normalizedName);
    }

    public IReadOnlyList<FishData> GetDiscoveredFishCatalog()
    {
        var result = new List<FishData>(_discoveredFish.Count);
        foreach (var fish in _discoveredFish)
        {
            if (fish != null && fish.FishScene != null)
                result.Add(fish);
        }

        return result;
    }

    public IReadOnlyDictionary<string, FishData> GetDiscoveredFishByName()
    {
        var result = new Dictionary<string, FishData>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _discoveredFishByName)
        {
            if (pair.Value != null && pair.Value.FishScene != null)
                result[pair.Key] = pair.Value;
        }

        return result;
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

    public float GetIncomePerSecond()
    {
        var total = 0f;
        foreach (var fish in _fishList)
            total += GetFishIncome(fish);

        return total;
    }

    private float GetFishIncome(Node2d fish)
    {
        if (fish?.Data == null)
            return 0f;

        var income = fish.Data.IncomePerSec;

        income *= fish.Data.GetRarityMultiplier();

        income *= fish.CurrentStage switch
        {
            FishGrowthStage.Fry => 0.25f,
            FishGrowthStage.Teen => 0.6f,
            FishGrowthStage.Adult => 1.0f,
            _ => 1.0f
        };

        income *= fish.GetIncomeMultiplier();

        return income;
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

    private void LoadMutations()
    {
        _mutations.Clear();

        const string mutationsDirPath = "res://assets/mutations";

        if (!DirAccess.DirExistsAbsolute(mutationsDirPath))
        {
            GD.Print($"[Mutations] Directory not found: {mutationsDirPath}");
            return;
        }

        using var dir = DirAccess.Open(mutationsDirPath);
        if (dir == null)
        {
            GD.PrintErr($"[Mutations] Failed to open directory: {mutationsDirPath}");
            return;
        }

        dir.ListDirBegin();
        while (true)
        {
            var fileName = dir.GetNext();
            if (string.IsNullOrEmpty(fileName))
                break;

            if (dir.CurrentIsDir())
                continue;

            if (!fileName.EndsWith(".tres") && !fileName.EndsWith(".res"))
                continue;

            var fullPath = $"{mutationsDirPath}/{fileName}";
            var mutation = GD.Load<FishMutation>(fullPath);
            if (mutation == null)
            {
                GD.PrintErr($"[Mutations] Failed to load: {fullPath}");
                continue;
            }

            _mutations.Add(mutation);
            GD.Print($"[Mutations] Loaded: {mutation.MutationName}");
        }

        dir.ListDirEnd();
    }

    private void EvaluateMutations()
    {
        if (_mutations.Count == 0)
            return;

        foreach (var fish in _fishList)
        {
            if (fish?.Data == null)
                continue;

            foreach (var mutation in _mutations)
            {
                if (mutation.RequiresAdult && !fish.IsAdult)
                    continue;

                var alreadyHas = false;
                foreach (var m in fish.Mutations)
                    if (m.MutationName == mutation.MutationName)
                    {
                        alreadyHas = true;
                        break;
                    }

                if (alreadyHas)
                    continue;

                var triggered = mutation.Trigger switch
                {
                    MutationTrigger.FoodEaten => fish.FoodEaten >= mutation.TriggerThreshold,
                    MutationTrigger.Starving => fish.TimeSinceLastFed >= mutation.TriggerThreshold,
                    MutationTrigger.AgeReached => fish.AgeSec >= mutation.TriggerThreshold,
                    _ => false
                };

                if (triggered)
                    fish.AddMutation(mutation);
            }
        }
    }

    public IReadOnlyList<FishMutation> GetAvailableMutations() => _mutations;

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

        if (!AreCompatible(parentA.Data, parentB.Data))
            return false;

        if (GD.Randf() > BreedChanceOnContact)
            return false;

        var spawnPos = ClampToSpawnArea(contactPos + new Vector2(
            (float)GD.RandRange(-30.0, 30.0),
            (float)GD.RandRange(-20.0, 20.0)
        ));

        Node2d spawned;

        var differentSpecies = !string.IsNullOrEmpty(parentA.Data.SpeciesId)
                               && !string.IsNullOrEmpty(parentB.Data.SpeciesId)
                               && parentA.Data.SpeciesId != parentB.Data.SpeciesId;

        if (differentSpecies && GD.Randf() < HybridChance)
            spawned = SpawnHybridFish(parentA.Data, parentB.Data, spawnPos);
        else
        {
            var offspringData = RollOffspring(parentA.Data, parentB.Data);
            if (offspringData == null)
                return false;

            var offspringName = ResolveOffspringName(parentA, parentB, offspringData);
            spawned = SpawnFish(offspringData, true, spawnPos, offspringName);
        }
        if (spawned == null)
            return false;

        var nextTime = _elapsedSec + ParentBreedCooldownSec;
        _nextBreedAtSec[parentA] = nextTime;
        _nextBreedAtSec[parentB] = nextTime;
        _nextBreedAtSec[spawned] = _elapsedSec + ParentBreedCooldownSec * 0.8f;

        var rarity = spawned.Data?.Rarity ?? FishRarity.Common;
        var birthReward = GetBirthReward(rarity);
        AddCoins(birthReward, $"New {rarity} fish born: +{birthReward}");
        return true;
    }

    private bool CanBreed(Node2d fish)
    {
        if (fish?.Data == null || !fish.IsAdult)
            return false;

        var nextTime = _nextBreedAtSec.TryGetValue(fish, out var v) ? v : 0f;
        return _elapsedSec >= nextTime;
    }

    private bool AreCompatible(FishData a, FishData b)
    {
        var aId = NormalizeSpeciesId(a.SpeciesId);
        var bId = NormalizeSpeciesId(b.SpeciesId);


        if (!string.IsNullOrEmpty(aId) && aId == bId) return true;

        if (a.CompatibleSpeciesIds != null)
        {
            foreach (var id in a.CompatibleSpeciesIds)
            {
                var normalized = NormalizeSpeciesId(id);
                if (normalized == bId) return true;
            }
        }

        if (b.CompatibleSpeciesIds != null)
        {
            foreach (var id in b.CompatibleSpeciesIds)
            {
                var normalized = NormalizeSpeciesId(id);
                if (normalized == aId) return true;
            }
        }

        return false;
    }

    private string NormalizeSpeciesId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim().ToLowerInvariant();
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
            if (fishData != null)
                totalWeight += Mathf.Max(0.01f, fishData.BreedWeight);

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

    private Node2d SpawnFish(FishData data, bool startAsFry, Vector2 spawnPos, string customFishName = null)
    {
        if (_aquarium == null || data == null || data.FishScene == null || FishCount >= MaxFishCount)
            return null;

        var fishNode = data.FishScene.Instantiate<Node2D>();
        if (fishNode is not Node2d fishScript)
            return null;

        fishScript.SetupFromData(data, startAsFry, customFishName);
        fishNode.Position = ClampToSpawnArea(spawnPos);

        _aquarium.AddChild(fishNode);
        return fishScript;
    }

    private static string ResolveOffspringName(Node2d parentA, Node2d parentB, FishData offspringData)
    {
        if (offspringData == null)
            return null;

        if (parentA?.Data == offspringData && !string.IsNullOrWhiteSpace(parentA.FishName))
            return parentA.FishName;

        if (parentB?.Data == offspringData && !string.IsNullOrWhiteSpace(parentB.FishName))
            return parentB.FishName;

        return null;
    }

    private Node2d SpawnHybridFish(FishData momData, FishData dadData, Vector2 spawnPos)
    {
        if (_aquarium == null || FishCount >= MaxFishCount)
            return null;

        if (momData?.FishScene == null)
            return null;

        var fishNode = momData.FishScene.Instantiate<Node2D>();
        if (fishNode is not Node2d fishScript)
            return null;

        fishScript.SetupAsHybrid(momData, dadData, true);
        fishNode.Position = ClampToSpawnArea(spawnPos);

        _aquarium.AddChild(fishNode);

        GD.Print($"[Hybrid] Born: {fishScript.FishName}");
        return fishScript;
    }

    private Vector2 GetRandomSpawnPosition()
    {
        var x = (float)GD.RandRange(_spawnAreaMin.X, _spawnAreaMax.X);
        var y = (float)GD.RandRange(_spawnAreaMin.Y, _spawnAreaMax.Y);
        return new Vector2(x, y);
    }

    private Vector2 ClampToSpawnArea(Vector2 pos) =>
        new(
            Mathf.Clamp(pos.X, _spawnAreaMin.X, _spawnAreaMax.X),
            Mathf.Clamp(pos.Y, _spawnAreaMin.Y, _spawnAreaMax.Y)
        );

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

    private static string MakeInventoryKey(string category, string itemName)
    {
        var normalizedCategory = string.IsNullOrWhiteSpace(category) ? "misc" : category.Trim().ToLowerInvariant();
        var normalizedItem = string.IsNullOrWhiteSpace(itemName) ? "item" : itemName.Trim().ToLowerInvariant();
        return $"{normalizedCategory}::{normalizedItem}";
    }

    private static string NormalizeFishName(string fishName)
    {
        return string.IsNullOrWhiteSpace(fishName) ? string.Empty : fishName.Trim();
    }

    private void OnFishClicked(Node2d fish)
    {
        var hud = GetTree().CurrentScene.GetNodeOrNull<Hud>("UI/HUD");

        hud?.OnFishClicked(fish);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseBtn
            && mouseBtn.ButtonIndex == MouseButton.Left
            && mouseBtn.Pressed)
        {
            var mousePos = GetViewport().GetMousePosition();
            var clickedFish = FindClosestFishAt(mousePos, 50f);

            if (clickedFish != null) OnFishClicked(clickedFish);
        }
    }

    private Node2d FindClosestFishAt(Vector2 pos, float maxDistance)
    {
        Node2d closest = null;
        var closestDist = maxDistance;

        foreach (var fish in _fishList)
        {
            if (fish == null)
                continue;

            var dist = fish.GlobalPosition.DistanceTo(pos);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = fish;
            }
        }

        return closest;
    }
}
