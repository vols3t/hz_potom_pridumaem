using Godot;
using System.Collections.Generic;

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }

    public float Money { get; private set; } = 300f;
    public int FishCount => _fishList.Count;
    public string LastEventText { get; private set; } = "";

    [Export] public int MaxFishCount = 15;
    [Export] public int CommonBirthCoins = 30;
    [Export] public int RareBirthCoins = 80;
    [Export] public int UniqueBirthCoins = 250;
    [Export] public float MaxIncomePerFishPerSec = 50.0f;

    [ExportCategory("Breeding")] [Export] public float BreedChanceOnContact = 0.7f;
    [Export] public float ParentBreedCooldownSec = 25f;
    [Export] public float MeetingCheckIntervalSec = 0.5f;
    [Export] public float MeetingDistance = 100f;
    [Export] public float HybridChance = 0.33f;

    [ExportCategory("Mutations")] [Export] public float MutationCheckInterval = 2.0f;

    private readonly List<Node2d> _fishList = new();
    private readonly Dictionary<Node2d, float> _nextBreedAtSec = new();
    private readonly Dictionary<string, int> _ownedShopItems = new();
    private readonly HashSet<FishData> _discoveredFish = new();
    private readonly Dictionary<string, FishData> _discoveredFishByName = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly List<FishMutation> _mutations = new();
    private readonly Dictionary<string, int> _foodInventory = new();
    private readonly Dictionary<string, int> _decorInventory = new();
    private readonly List<PlacedDecoration> _placedDecorations = new();
    private int _extraFishCapacity = 0;

    private sealed class PlacedDecoration
    {
        public DecorData Data;
        public Vector2 Position;
        public Sprite2D Sprite;
    }

    public int EffectiveMaxFishCount => MaxFishCount + _extraFishCapacity;
    public int ExtraFishCapacity => _extraFishCapacity;

    public void AddFishCapacity(int amount)
    {
        _extraFishCapacity = Mathf.Max(0, _extraFishCapacity + amount);
    }

    public int GetFishScore(Node2d fish)
    {
        if (fish?.Data == null) return 0;
        var rarityScore = fish.Data.Rarity switch
        {
            FishRarity.Common => 1f,
            FishRarity.Rare => 2f,
            FishRarity.Unique => 5f,
            _ => 1f
        };
        var stageScore = fish.CurrentStage switch
        {
            FishGrowthStage.Fry => 0.3f,
            FishGrowthStage.Teen => 0.6f,
            FishGrowthStage.Adult => 1.0f,
            _ => 1.0f
        };
        return Mathf.RoundToInt(fish.Data.Price * rarityScore * stageScore);
    }

    public int GetAquariumScore()
    {
        var score = 0;
        foreach (var fish in _fishList)
            score += GetFishScore(fish);
        return score;
    }

    private Node2D _aquarium;
    private FishData[] _catalog = System.Array.Empty<FishData>();
    private Vector2 _spawnAreaMin = new(100, 100);
    private Vector2 _spawnAreaMax = new(500, 400);
    private float _elapsedSec = 0f;
    private float _meetingTimerSec = 0f;
    private float _mutationTimerSec = 0f;
    private float _autosaveTimerSec = 0f;
    private Godot.Collections.Array _pendingFishLoad;

    private const string SaveFilePath = "user://save.cfg";
    private const float AutosaveIntervalSec = 30f;

    public override void _EnterTree() => Instance = this;

    public override void _Ready()
    {
        AddChild(new DecorPlacer());
        LoadMutations();
        // Load(); // saves disabled
    }

    public override void _Notification(int what)
    {
        // if (what == NotificationWMCloseRequest) Save(); // saves disabled
    }

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

        // autosave disabled
        // _autosaveTimerSec += d;
        // if (_autosaveTimerSec >= AutosaveIntervalSec) { _autosaveTimerSec = 0f; Save(); }
    }

    public void ConfigureAquarium(Node2D aquarium, FishData[] catalog, Vector2 spawnMin, Vector2 spawnMax)
    {
        _aquarium = aquarium;
        _catalog = catalog ?? System.Array.Empty<FishData>();
        _spawnAreaMin = spawnMin;
        _spawnAreaMax = spawnMax;

        TryRestoreDecorSprites();
        TryRestoreFishFromSave();
        if (_hasAutoFeeder) PlaceAutoFeeder();
        CallDeferred(nameof(SetupBubbleParticles));
        StartBackgroundMusic();
    }

    // Spawns a story-only fish bypassing the fish cap and purchase flow
    public Node2d SpawnStoryFish(FishData data)
    {
        if (_aquarium == null || data?.FishScene == null) return null;
        var fishNode = data.FishScene.Instantiate<Node2D>();
        if (fishNode is not Node2d fishScript) return null;
        fishScript.SetupFromData(data, false); // false = start as adult
        fishScript.SpriteFacesRight = true;
        fishScript.IsClickable = false;
        fishNode.Position = ClampToSpawnArea(GetRandomSpawnPosition());
        _aquarium.AddChild(fishNode);

        var predatorMutation = new FishMutation
        {
            MutationName = "Хищник",
            MakesPredator = true,
            SpeedMultiplier = 2.8f,
            RequiresAdult = false
        };
        fishScript.AddMutation(predatorMutation);
        fishScript.ForceHungry();
        RegisterFish(fishScript);
        return fishScript;
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
        if (FishCount >= EffectiveMaxFishCount)
        {
            LastEventText = $"Fish limit reached ({EffectiveMaxFishCount})";
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
        StoryManager.Instance?.OnFishPurchased();
        QuestManager.Instance?.NotifyFishPurchased();
        return true;
    }

    public bool TryBuyFishOffer(FishData data, int offerPrice, string offerName)
    {
        if (data == null || data.FishScene == null || offerPrice < 0)
            return false;
        if (FishCount >= EffectiveMaxFishCount)
        {
            LastEventText = $"Fish limit reached ({EffectiveMaxFishCount})";
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
        StoryManager.Instance?.OnFishPurchased();
        QuestManager.Instance?.NotifyFishPurchased();
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
            if (pair.Key.StartsWith(normalized))
                total += pair.Value;

        return total;
    }

    public bool TryBuyFood(FoodData data)
    {
        if (data == null || data.IsUnlimited)
            return false;

        if (!SpendMoney(data.Price))
            return false;

        var key = data.ResourcePath;
        _foodInventory[key] = GetFoodCount(data) + data.BatchSize;
        LastEventText = $"Куплено {data.BatchSize} порций: {data.FoodName}";
        return true;
    }

    public int GetFoodCount(FoodData data)
    {
        if (data == null || data.IsUnlimited) return -1;
        return _foodInventory.TryGetValue(data.ResourcePath, out var v) ? v : 0;
    }

    public bool CanUseFood(FoodData data)
    {
        if (data == null) return false;
        if (data.IsUnlimited) return true;
        return GetFoodCount(data) > 0;
    }

    public System.Collections.Generic.IReadOnlyList<(FoodData Food, int Count)> GetFoodInventorySnapshot(FoodData[] catalog)
    {
        var result = new System.Collections.Generic.List<(FoodData, int)>();
        if (catalog == null) return result;
        foreach (var food in catalog)
        {
            if (food == null || food.IsUnlimited) continue;
            var count = GetFoodCount(food);
            if (count > 0) result.Add((food, count));
        }
        return result;
    }

    public void ConsumeFood(FoodData data)
    {
        if (data == null || data.IsUnlimited) return;
        var key = data.ResourcePath;
        var current = GetFoodCount(data);
        if (current > 0)
        {
            _foodInventory[key] = current - 1;
            QuestManager.Instance?.NotifyFishFed();
        }
    }

    public bool CanAfford(float amount) => amount <= Money;

    public int GetFishCountByRarity(FishRarity rarity)
    {
        var count = 0;
        foreach (var fish in _fishList)
            if (fish.Data != null && fish.Data.Rarity == rarity)
                count++;

        return count;
    }

    public (int Common, int Rare, int Unique) GetRarityCounts()
    {
        var common = 0;
        var rare = 0;
        var unique = 0;
        foreach (var fish in _fishList)
        {
            if (fish?.Data == null)
                continue;

            switch (fish.Data.Rarity)
            {
                case FishRarity.Common: common++; break;
                case FishRarity.Rare: rare++; break;
                case FishRarity.Unique: unique++; break;
            }
        }

        return (common, rare, unique);
    }

    public bool HasAnyPredator()
    {
        foreach (var fish in _fishList)
            if (fish != null && fish.IsPredator)
                return true;
        return false;
    }

    // ── Автокормушка ──────────────────────────────────────────────────────────

    private bool _hasAutoFeeder;
    private AutoFeeder _autoFeeder;

    public bool HasAutoFeeder => _hasAutoFeeder;

    public bool TryBuyAutoFeeder()
    {
        if (_hasAutoFeeder) return false;
        if (!SpendMoney(1000)) return false;
        _hasAutoFeeder = true;
        LastEventText = "Куплено: Автокормушка";
        PlaceAutoFeeder();
        QuestManager.Instance?.NotifyAutoFeederBought();
        return true;
    }

    // ── Новый аквариум (Квест 8) ──────────────────────────────────────────────

    private bool _hasNewAquarium;
    public bool HasNewAquarium => _hasNewAquarium;

    public bool TryBuyAquarium(int price = 12000)
    {
        if (_hasNewAquarium) return false;
        if (!SpendMoney(price)) return false;
        _hasNewAquarium = true;
        LastEventText = "Куплено: Новый аквариум";
        QuestManager.Instance?.NotifyAquariumBought();
        return true;
    }

    private void PlaceAutoFeeder()
    {
        if (_aquarium == null) return;
        if (_autoFeeder != null && IsInstanceValid(_autoFeeder)) return;

        var fd = FoodDropper.Instance;
        var right = fd?.AquariumRight ?? 1395f;
        var x = right - 48f;
        var y = (fd?.AquariumTop ?? 95f) + 130f;

        _autoFeeder = new AutoFeeder { Position = new Vector2(x, y), ZIndex = 2 };
        _aquarium.AddChild(_autoFeeder);
    }

    public bool TryBuyDecor(DecorData data)
    {
        if (data == null) return false;
        if (!SpendMoney(data.Price)) return false;
        _decorInventory[data.ResourcePath] = GetDecorCount(data) + 1;
        LastEventText = $"Куплено: {data.DecorName}";
        return true;
    }

    public int GetDecorCount(DecorData data)
    {
        if (data == null || string.IsNullOrEmpty(data.ResourcePath)) return 0;
        return _decorInventory.TryGetValue(data.ResourcePath, out var v) ? v : 0;
    }

    public bool PlaceDecor(DecorData data, Vector2 position)
    {
        if (data == null || GetDecorCount(data) <= 0) return false;
        _decorInventory[data.ResourcePath] = GetDecorCount(data) - 1;
        var sprite = CreateDecorSprite(data, position);
        _placedDecorations.Add(new PlacedDecoration { Data = data, Position = position, Sprite = sprite });
        QuestManager.Instance?.NotifyDecorationPlaced();
        return true;
    }

    public bool RemovePlacedDecor(int index)
    {
        if (index < 0 || index >= _placedDecorations.Count) return false;
        var pd = _placedDecorations[index];
        pd.Sprite?.QueueFree();
        _placedDecorations.RemoveAt(index);
        _decorInventory[pd.Data.ResourcePath] = GetDecorCount(pd.Data) + 1;
        return true;
    }

    public float GetDecorHappinessBonus()
    {
        var total = 0f;
        foreach (var pd in _placedDecorations)
            if (pd.Data != null) total += pd.Data.HappinessBonus;
        return Mathf.Min(total, 30f);
    }

    public System.Collections.Generic.IReadOnlyList<(DecorData Decor, int Count)> GetDecorInventorySnapshot(DecorData[] catalog)
    {
        var result = new System.Collections.Generic.List<(DecorData, int)>();
        if (catalog == null) return result;
        foreach (var d in catalog)
        {
            if (d == null) continue;
            var count = GetDecorCount(d);
            if (count > 0) result.Add((d, count));
        }
        return result;
    }

    public System.Collections.Generic.IReadOnlyList<(DecorData Data, Vector2 Position, int Index)> GetPlacedDecors()
    {
        var result = new System.Collections.Generic.List<(DecorData, Vector2, int)>(_placedDecorations.Count);
        for (var i = 0; i < _placedDecorations.Count; i++)
            result.Add((_placedDecorations[i].Data, _placedDecorations[i].Position, i));
        return result;
    }

    private Sprite2D CreateDecorSprite(DecorData data, Vector2 position)
    {
        if (_aquarium == null) return null;
        var tex = data.DecorTexture ?? CreatePlaceholderTexture(new Color(0.35f, 0.6f, 0.9f, 0.85f));
        var sprite = new Sprite2D { Texture = tex, Position = position, Scale = data.DisplayScale, ZIndex = data.PlacementZIndex,
            Modulate = new Color(0.72f, 0.78f, 0.88f, 1f) };
        _aquarium.AddChild(sprite);
        return sprite;
    }

    private static Texture2D CreatePlaceholderTexture(Color color, int size = 64)
    {
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        img.Fill(color);
        return ImageTexture.CreateFromImage(img);
    }

    private AudioStreamPlayer _musicPlayer;

    private const float MusicNormalDb  = -12f;
    private const float MusicDuckedDb  = -30f;
    private const float MusicFadeTime  = 0.4f;

    private void StartBackgroundMusic()
    {
        if (_musicPlayer != null && IsInstanceValid(_musicPlayer)) return;
        var stream = GD.Load<AudioStreamMP3>("res://assets/audio/background.mp3");
        if (stream == null) { GD.PrintErr("[Audio] background.mp3 not found"); return; }
        stream.Loop = true;
        _musicPlayer = new AudioStreamPlayer { Stream = stream, VolumeDb = MusicNormalDb };
        AddChild(_musicPlayer);
        _musicPlayer.Play();
    }

    public void DuckMusic()   => FadeMusicTo(MusicDuckedDb);
    public void UnduckMusic() => FadeMusicTo(MusicNormalDb);

    public void StopMusic()
    {
        if (_musicPlayer == null || !IsInstanceValid(_musicPlayer)) return;
        var tween = CreateTween();
        tween.TweenProperty(_musicPlayer, "volume_db", -80f, 0.6f).SetTrans(Tween.TransitionType.Sine);
        tween.TweenCallback(Callable.From(() => _musicPlayer.Stop()));
    }

    private void FadeMusicTo(float targetDb)
    {
        if (_musicPlayer == null || !IsInstanceValid(_musicPlayer)) return;
        var tween = CreateTween();
        tween.TweenProperty(_musicPlayer, "volume_db", targetDb, MusicFadeTime)
             .SetTrans(Tween.TransitionType.Sine);
    }

    public static void PlaySfx(string path, float volumeDb = 0f)
    {
        var stream = GD.Load<AudioStream>(path);
        if (stream == null) { GD.PrintErr($"[Audio] SFX not found: {path}"); return; }
        var player = new AudioStreamPlayer { Stream = stream, VolumeDb = volumeDb };
        Instance?.AddChild(player);
        player.Play();
        player.Finished += player.QueueFree;
    }

    private void SetupBubbleParticles()
    {
        if (_aquarium == null || _aquarium.FindChild("BubbleParticles") != null) return;

        var fd = FoodDropper.Instance;
        var left   = fd?.AquariumLeft   ?? 42f;
        var right  = fd?.AquariumRight  ?? 1395f;
        var top    = fd?.AquariumTop    ?? 95f;
        var bottom = fd?.AquariumBottom ?? 640f;

        var width   = right - left;
        var height  = bottom - top;
        var centerX = (left + right) * 0.5f;
        var lifetime = height / 55f; // ~9.9 сек от дна до верха при средней скорости

        var mat = new ParticleProcessMaterial();
        mat.Direction         = new Vector3(0f, -1f, 0f);
        mat.Spread            = 10f;
        mat.InitialVelocityMin = 40f;
        mat.InitialVelocityMax = 90f;
        mat.Gravity           = Vector3.Zero;
        mat.ScaleMin          = 0.7f;
        mat.ScaleMax          = 2.0f;
        mat.Color             = new Color(0.75f, 0.9f, 1f, 0.55f);
        mat.EmissionShape     = ParticleProcessMaterial.EmissionShapeEnum.Box;
        mat.EmissionBoxExtents = new Vector3(width * 0.5f, 2f, 0f);

        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.8f, 0.93f, 1f, 0.6f));
        gradient.SetColor(1, new Color(0.8f, 0.93f, 1f, 0f));
        mat.ColorRamp = new GradientTexture1D { Gradient = gradient };

        var particles = new GpuParticles2D
        {
            Name        = "BubbleParticles",
            Position    = new Vector2(centerX, bottom - 5f),
            Amount      = 55,
            Lifetime    = lifetime,
            Preprocess  = lifetime * 0.8f,
            LocalCoords = false,
            ProcessMaterial = mat,
            Texture     = CreateBubbleTexture(16),
            ZIndex      = 1
        };

        _aquarium.AddChild(particles);
    }

    private static Texture2D CreateBubbleTexture(int size)
    {
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var c = size / 2f;
        var r = c - 0.5f;
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var dx = x - c + 0.5f;
            var dy = y - c + 0.5f;
            var t  = Mathf.Clamp(1f - Mathf.Sqrt(dx * dx + dy * dy) / r, 0f, 1f);
            img.SetPixel(x, y, new Color(1f, 1f, 1f, t * t));
        }
        return ImageTexture.CreateFromImage(img);
    }

    private void TryRestoreDecorSprites()
    {
        foreach (var pd in _placedDecorations)
        {
            if (pd.Sprite != null && IsInstanceValid(pd.Sprite)) continue;
            pd.Sprite = CreateDecorSprite(pd.Data, pd.Position);
        }
    }

    public IReadOnlyList<Node2d> GetFishSnapshot()
    {
        var snapshot = new List<Node2d>(_fishList.Count);
        foreach (var fish in _fishList)
            if (fish != null && IsInstanceValid(fish))
                snapshot.Add(fish);

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

    public bool HasDiscoveredFish(FishData fishData) => fishData != null && _discoveredFish.Contains(fishData);

    public bool HasDiscoveredFishName(string fishName)
    {
        var normalizedName = NormalizeFishName(fishName);
        return !string.IsNullOrWhiteSpace(normalizedName) && _discoveredFishByName.ContainsKey(normalizedName);
    }

    public IReadOnlyList<FishData> GetDiscoveredFishCatalog()
    {
        var result = new List<FishData>(_discoveredFish.Count);
        foreach (var fish in _discoveredFish)
            if (fish != null && fish.FishScene != null)
                result.Add(fish);

        return result;
    }

    public IReadOnlyDictionary<string, FishData> GetDiscoveredFishByName()
    {
        var result = new Dictionary<string, FishData>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _discoveredFishByName)
            if (pair.Value != null && pair.Value.FishScene != null)
                result[pair.Key] = pair.Value;

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

    public float GetFishIncomePerSecond(Node2d fish) =>
        GetFishIncome(fish);

    private float GetFishIncome(Node2d fish)
    {
        if (fish?.Data == null)
            return 0f;

        var baseIncome = fish.HybridIncome > 0
            ? fish.HybridIncome
            : fish.Data.IncomePerSec;

        var income = baseIncome;

        income *= fish.Data.GetRarityMultiplier();

        income *= fish.CurrentStage switch
        {
            FishGrowthStage.Fry => 0.25f,
            FishGrowthStage.Teen => 0.6f,
            FishGrowthStage.Adult => 1.0f,
            _ => 1.0f
        };

        income *= fish.GetIncomeMultiplier();
        income *= fish.GetHappinessMultiplier();

        return Mathf.Clamp(income, 0f, Mathf.Max(0f, MaxIncomePerFishPerSec));
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
                    MutationTrigger.Overfed => fish.OverfedAmount >= mutation.TriggerThreshold,
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
        if (_fishList.Count < 2 || FishCount >= EffectiveMaxFishCount)
            return;

        var meetingDistanceSq = MeetingDistance * MeetingDistance;

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

                if (fishA.GlobalPosition.DistanceSquaredTo(fishB.GlobalPosition) > meetingDistanceSq)
                    continue;

                var meetPos = (fishA.GlobalPosition + fishB.GlobalPosition) * 0.5f;
                if (TryBreedByMeeting(fishA, fishB, meetPos))
                    return;
            }
        }
    }

    private bool TryBreedByMeeting(Node2d parentA, Node2d parentB, Vector2 contactPos)
    {
        if (_aquarium == null || FishCount >= EffectiveMaxFishCount)
            return false;

        if (!CanBreed(parentA) || !CanBreed(parentB))
            return false;

        if (!AreCompatible(parentA.Data, parentB.Data))
            return false;

        var breedChance = BreedChanceOnContact
            + parentA.BreedChanceBonus
            + parentB.BreedChanceBonus;
        if (GD.Randf() > breedChance)
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

        parentA.OnBreedSuccess();
        parentB.OnBreedSuccess();

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


        if (fish.IsHybrid)
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
        if (_aquarium == null || data == null || data.FishScene == null || FishCount >= EffectiveMaxFishCount)
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
        if (_aquarium == null || FishCount >= EffectiveMaxFishCount)
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

    private static string NormalizeFishName(string fishName) =>
        string.IsNullOrWhiteSpace(fishName) ? string.Empty : fishName.Trim();

    private void OnFishClicked(Node2d fish)
    {
        var hud = GetTree().CurrentScene.GetNodeOrNull<Hud>("UI/HUD");
        hud?.OnFishClicked(fish);
        QuestManager.Instance?.NotifyFishClicked();
    }

    public void Save()
    {
        if (_pendingFishLoad != null)
            return;

        var config = new ConfigFile();
        config.SetValue("meta", "version", 1);
        config.SetValue("economy", "money", Money);
        config.SetValue("economy", "extra_fish_capacity", _extraFishCapacity);

        var fishArray = new Godot.Collections.Array();
        foreach (var fish in _fishList)
        {
            if (fish == null || !IsInstanceValid(fish) || fish.Data == null || string.IsNullOrEmpty(fish.Data.ResourcePath))
                continue;

            var dict = new Godot.Collections.Dictionary
            {
                ["data_path"] = fish.Data.ResourcePath,
                ["name"] = fish.FishName ?? "",
                ["age"] = fish.AgeSec,
                ["is_hybrid"] = fish.IsHybrid,
                ["parent_a_path"] = fish.ParentA?.ResourcePath ?? "",
                ["parent_b_path"] = fish.ParentB?.ResourcePath ?? "",
            };

            var muts = new Godot.Collections.Array();
            foreach (var m in fish.Mutations)
            {
                if (m != null && !string.IsNullOrEmpty(m.ResourcePath))
                    muts.Add(m.ResourcePath);
            }
            dict["mutations"] = muts;

            fishArray.Add(dict);
        }
        config.SetValue("fish", "items", fishArray);

        var discoveredArray = new Godot.Collections.Array();
        foreach (var pair in _discoveredFishByName)
        {
            if (pair.Value == null || string.IsNullOrEmpty(pair.Value.ResourcePath))
                continue;

            var dict = new Godot.Collections.Dictionary
            {
                ["name"] = pair.Key,
                ["data_path"] = pair.Value.ResourcePath,
            };
            discoveredArray.Add(dict);
        }
        config.SetValue("discovered", "items", discoveredArray);

        var discoveredPaths = new Godot.Collections.Array();
        foreach (var fish in _discoveredFish)
            if (fish != null && !string.IsNullOrEmpty(fish.ResourcePath))
                discoveredPaths.Add(fish.ResourcePath);
        config.SetValue("discovered", "paths", discoveredPaths);

        var ownedDict = new Godot.Collections.Dictionary();
        foreach (var pair in _ownedShopItems)
            ownedDict[pair.Key] = pair.Value;
        config.SetValue("owned", "items", ownedDict);

        var foodDict = new Godot.Collections.Dictionary();
        foreach (var pair in _foodInventory)
            if (pair.Value > 0)
                foodDict[pair.Key] = pair.Value;
        config.SetValue("food", "inventory", foodDict);

        var decorInvDict = new Godot.Collections.Dictionary();
        foreach (var pair in _decorInventory)
            if (pair.Value > 0) decorInvDict[pair.Key] = pair.Value;
        config.SetValue("decor", "inventory", decorInvDict);

        var placedDecorArray = new Godot.Collections.Array();
        foreach (var pd in _placedDecorations)
        {
            if (pd.Data == null || string.IsNullOrEmpty(pd.Data.ResourcePath)) continue;
            placedDecorArray.Add(new Godot.Collections.Dictionary
            {
                ["path"] = pd.Data.ResourcePath,
                ["x"] = pd.Position.X,
                ["y"] = pd.Position.Y
            });
        }
        config.SetValue("decor", "placed", placedDecorArray);

        config.SetValue("settings", "brightness", SettingsPanel.SavedBrightness);
        config.SetValue("settings", "sound", SettingsPanel.SavedSound);

        var error = config.Save(SaveFilePath);
        if (error != Error.Ok)
            GD.PrintErr($"[Save] Failed to save: {error}");
    }

    private void Load()
    {
        var config = new ConfigFile();
        var error = config.Load(SaveFilePath);
        if (error != Error.Ok)
        {
            GD.Print($"[Save] No save file ({SaveFilePath}), starting fresh");
            return;
        }

        Money = Mathf.Max(300f, (float)(double)config.GetValue("economy", "money", 300.0));
        if (config.HasSectionKey("economy", "extra_fish_capacity"))
            _extraFishCapacity = config.GetValue("economy", "extra_fish_capacity").AsInt32();

        if (config.HasSectionKey("food", "inventory"))
        {
            var foodVar = config.GetValue("food", "inventory").AsGodotDictionary();
            foreach (var key in foodVar.Keys)
                _foodInventory[key.AsString()] = foodVar[key].AsInt32();
        }

        if (config.HasSectionKey("decor", "inventory"))
        {
            var decorInvVar = config.GetValue("decor", "inventory").AsGodotDictionary();
            foreach (var key in decorInvVar.Keys)
                _decorInventory[key.AsString()] = decorInvVar[key].AsInt32();
        }

        if (config.HasSectionKey("decor", "placed"))
        {
            foreach (var item in config.GetValue("decor", "placed").AsGodotArray())
            {
                var dict = item.AsGodotDictionary();
                var data = GD.Load<DecorData>(dict["path"].AsString());
                if (data == null) continue;
                _placedDecorations.Add(new PlacedDecoration
                {
                    Data = data,
                    Position = new Vector2((float)dict["x"].AsDouble(), (float)dict["y"].AsDouble())
                });
            }
        }

        SettingsPanel.SavedBrightness = (float)(double)config.GetValue("settings", "brightness", 50.0);
        SettingsPanel.SavedSound = (float)(double)config.GetValue("settings", "sound", 80.0);

        if (config.HasSectionKey("owned", "items"))
        {
            var ownedVar = config.GetValue("owned", "items");
            var ownedDict = ownedVar.AsGodotDictionary();
            foreach (var key in ownedDict.Keys)
                _ownedShopItems[key.AsString()] = ownedDict[key].AsInt32();
        }

        if (config.HasSectionKey("discovered", "items"))
        {
            var discoveredArray = config.GetValue("discovered", "items").AsGodotArray();
            foreach (var item in discoveredArray)
            {
                var dict = item.AsGodotDictionary();
                var path = dict["data_path"].AsString();
                var name = dict["name"].AsString();
                var data = GD.Load<FishData>(path);
                if (data == null)
                    continue;

                _discoveredFish.Add(data);
                if (!string.IsNullOrWhiteSpace(name))
                    _discoveredFishByName[name] = data;
            }
        }

        if (config.HasSectionKey("discovered", "paths"))
        {
            var pathsArray = config.GetValue("discovered", "paths").AsGodotArray();
            foreach (var item in pathsArray)
            {
                var data = GD.Load<FishData>(item.AsString());
                if (data != null)
                    _discoveredFish.Add(data);
            }
        }

        if (config.HasSectionKey("fish", "items"))
            _pendingFishLoad = config.GetValue("fish", "items").AsGodotArray();
    }

    private void TryRestoreFishFromSave()
    {
        if (_pendingFishLoad == null || _aquarium == null)
            return;

        var pending = _pendingFishLoad;
        _pendingFishLoad = null;

        var existing = new List<Node2d>(_fishList);
        foreach (var fish in existing)
        {
            if (fish != null && IsInstanceValid(fish))
                fish.QueueFree();
        }
        _fishList.Clear();
        _nextBreedAtSec.Clear();

        foreach (var item in pending)
        {
            var dict = item.AsGodotDictionary();
            var dataPath = dict["data_path"].AsString();
            var data = GD.Load<FishData>(dataPath);
            if (data == null)
                continue;

            var name = dict.ContainsKey("name") ? dict["name"].AsString() : null;
            var age = dict.ContainsKey("age") ? (float)dict["age"].AsDouble() : 0f;
            var isHybrid = dict.ContainsKey("is_hybrid") && dict["is_hybrid"].AsBool();

            Node2d spawned;
            if (isHybrid)
            {
                var parentA = GD.Load<FishData>(dict["parent_a_path"].AsString());
                var parentB = GD.Load<FishData>(dict["parent_b_path"].AsString());
                if (parentA == null || parentB == null)
                    continue;

                spawned = SpawnHybridFish(parentA, parentB, GetRandomSpawnPosition());
            }
            else
            {
                spawned = SpawnFish(data, true, GetRandomSpawnPosition(), name);
            }

            if (spawned == null)
                continue;

            spawned.RestoreAge(age);

            if (dict.ContainsKey("mutations"))
            {
                foreach (var mp in dict["mutations"].AsGodotArray())
                {
                    var mutation = GD.Load<FishMutation>(mp.AsString());
                    if (mutation != null)
                        spawned.AddMutation(mutation);
                }
            }
        }
    }
}
