using Godot;

public partial class QuestManager : Node
{
    public static QuestManager Instance { get; private set; }

    [Signal] public delegate void QuestChangedEventHandler(string text);

    public int CurrentQuestId { get; private set; } = 0;
    public string CurrentQuestText { get; private set; } = "";
    public bool IsAutoFeederUnlocked => CurrentQuestId >= 7;

    private int _decorationsPlaced = 0;

    private static readonly string[] QuestTexts =
    {
        "Купите любую рыбу в магазине",                          // 1
        "В очередной раз, купите любую рыбу",                    // 2
        "Купите корм в магазине и покормите им рыбу",            // 3
        "Посмотрите информацию о состоянии рыбы, нажав на неё", // 4
        "Купите и установите любые 3 декорации",                 // 5
        "Продолжайте кормить рыб и ухаживать за ними...",       // 6
        "Купите в магазине Автокормушку",                        // 7
        "Купите новый аквариум",                                 // 8
    };

    public override void _EnterTree() => Instance = this;

    public override void _Ready()
    {
        var canvas = new CanvasLayer { Layer = 10 };
        AddChild(canvas);
        canvas.AddChild(new QuestPanel());
    }

    public void StartChapter1() => StartQuest(1);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void StartQuest(int id)
    {
        if (id < 1 || id > QuestTexts.Length)
        {
            CurrentQuestId = -1;
            CurrentQuestText = "";
            EmitSignal(SignalName.QuestChanged, "");
            return;
        }
        CurrentQuestId = id;
        CurrentQuestText = QuestTexts[id - 1];
        EmitSignal(SignalName.QuestChanged, CurrentQuestText);
    }

    private void ClearQuest()
    {
        CurrentQuestId = 0;
        CurrentQuestText = "";
        EmitSignal(SignalName.QuestChanged, "");
    }

    // ── Notifications from GameManager ────────────────────────────────────────

    public void NotifyFishPurchased()
    {
        if (CurrentQuestId == 1) ClearQuest();       // Q2 стартует после диалога Аквы
        else if (CurrentQuestId == 2) StartQuest(3);
    }

    public void NotifyFishFed()
    {
        if (CurrentQuestId == 3) StartQuest(4);
    }

    public void NotifyFishClicked()
    {
        if (CurrentQuestId != 4) return;
        ClearQuest();
        StoryManager.Instance?.PlayHeroPost4Dialogue();
    }

    public void NotifyDecorationPlaced()
    {
        if (CurrentQuestId != 5) return;
        _decorationsPlaced++;
        if (_decorationsPlaced >= 3)
        {
            _decorationsPlaced = 0;
            StartQuest(6);
            RunQuest6Timer();
        }
    }

    public void NotifyAutoFeederBought()
    {
        if (CurrentQuestId != 7) return;
        ClearQuest();
        StoryManager.Instance?.PlayAquaPost7Dialogue();
    }

    public void NotifyAquariumBought()
    {
        if (CurrentQuestId != 8) return;
        ClearQuest();
        StoryManager.Instance?.PlayChapter2Screen();
    }

    // ── Callbacks from StoryManager (after dialogues finish) ─────────────────

    public void OnAquaIntroDone()      => StartQuest(2);
    public void OnHeroPost4DialogueDone() => StartQuest(5);
    public void OnHeroPost6DialogueDone() => StartQuest(7);
    public void OnAquaPost7DialogueDone() => StartQuest(8);

    // ── Q6: ждём 40 секунд, потом диалог Героя ────────────────────────────────

    private async void RunQuest6Timer()
    {
        await ToSignal(GetTree().CreateTimer(25f), SceneTreeTimer.SignalName.Timeout);
        if (CurrentQuestId != 6) return;
        ClearQuest();
        StoryManager.Instance?.PlayHeroPost6Dialogue();
    }
}
