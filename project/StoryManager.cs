using Godot;
using System.Threading.Tasks;

// Orchestrates Chapter 1 story events.
// Autoloaded as singleton; hooks into GameManager events after aquarium loads.
public partial class StoryManager : Node
{
    public static StoryManager Instance { get; private set; }

    private bool _chapter1Started;
    private bool _chapter1FishBought;
    private int _piranhaKillCount;
    private Node2d _piranha;

    public override void _EnterTree() => Instance = this;

    // Called by GameManager.ConfigureAquarium (via CallDeferred) after scene is ready
    public void StartChapter1()
    {
        if (_chapter1Started) return;
        _chapter1Started = true;
        QuestManager.Instance?.StartChapter1();
        PlayChapter1Intro();
    }

    // ── Public hook called by GameManager when a fish is purchased ───────────
    public void OnFishPurchased()
    {
        if (_chapter1FishBought) return;
        _chapter1FishBought = true;
        RunPiranhaEvent();
    }

    // ── Chapter 1 opening monologue ──────────────────────────────────────────
    private async void PlayChapter1Intro()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var ds = DialogueSystem.Instance;
        if (ds == null) return;

        var lines = new[]
        {
            new DialogueLine("Герой", Characters.Hero.Stable, "Летом совсем нечем заняться, друзья все уехали, компа нет..."),
            new DialogueLine("Герой", Characters.Hero.Stable, "Осталось только на этих глупых рыбок смотреть"),
            new DialogueLine("Герой", Characters.Hero.Stable, "Точно, папа просил купить в /m магазине \\m ещё одну, чтобы наша рыбка не скучала"),
        };
        ds.StartDialogue(lines);
    }

    // ── Piranha event: shake → spawn → eat 2 fish → Aqua dialogue ───────────
    private async void RunPiranhaEvent()
    {
        // Wait 7 seconds after purchase
        await ToSignal(GetTree().CreateTimer(7f), SceneTreeTimer.SignalName.Timeout);

        var sm = SceneManager.Instance;
        var gm = GameManager.Instance;
        if (sm == null || gm == null) return;

        sm.ShakeScreen(0.8f, 24f);
        GameManager.PlaySfx("res://assets/audio/splash.mp3", 2f);
        await sm.BriefDarken(1.5f);

        _piranha = SpawnPiranha(gm);
        if (_piranha == null)
        {
            // fallback: skip to dialogue if spawn failed
            await ToSignal(GetTree().CreateTimer(1f), SceneTreeTimer.SignalName.Timeout);
            PlayAquaDialogue();
            return;
        }

        // Wait until piranha eats 2 fish (polled every 0.5s, timeout 30s)
        _piranhaKillCount = 0;
        var fishCountBefore = gm.FishCount - 1; // -1 for piranha itself
        var waited = 0f;
        while (_piranhaKillCount < 2 && waited < 30f)
        {
            await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
            waited += 0.5f;
            var killed = fishCountBefore - (gm.FishCount - 1);
            _piranhaKillCount = Mathf.Max(0, killed);
        }

        // Remove piranha before dialogue
        if (IsInstanceValid(_piranha))
        {
            gm.UnregisterFish(_piranha);
            _piranha.QueueFree();
        }

        PlayAquaDialogue();
    }

    // ── Aqua's dialogue after piranha event ─────────────────────────────────
    private void PlayAquaDialogue()
    {
        var ds = DialogueSystem.Instance;
        if (ds == null) return;

        var lines = new[]
        {
            new DialogueLine("Аква",  Characters.Aqua.Happy,  "БУ!!!"),
            new DialogueLine("Герой", Characters.Hero.Stable,  "Ты кто?!"),
            new DialogueLine("Герой", Characters.Hero.Stable,  "Что ты делаешь в моей квартире?!"),
            new DialogueLine("Герой", Characters.Hero.Stable,  "И что ты сделала с моими рыбками..."),
            new DialogueLine("Аква",  Characters.Aqua.Stable,  "Я — Аква, самая крутая и богатая коллекционерша рыб в этом городе"),
            new DialogueLine("Аква",  Characters.Aqua.Stable,  "Мне стало слишком скучно и я решила создать себе конкуренцию"),
            new DialogueLine("Аква",  Characters.Aqua.Sad,     "Мирные способы мне не помогли, никто не может достичь моей вершины..."),
            new DialogueLine("Аква",  Characters.Aqua.Sad,     "Поэтому я решила, что лишь утрата заставит кого-то опередить меня"),
            new DialogueLine("Герой", Characters.Hero.Stable,  "Я обращаюсь в полицию, ты ненормальная"),
            new DialogueLine("Аква",  Characters.Aqua.Stable,  "Пожалуйста, не нужно (("),
            new DialogueLine("Герой", Characters.Hero.Stable,  "Ну ладно"),
            new DialogueLine("Аква",  Characters.Aqua.Stable,  "Чтобы всё было честно, я научу тебя основам моего дела"),
            new DialogueLine("Аква",  Characters.Aqua.Stable,  "Всех своих рыбок нужно /m своевременно кормить \\m, иначе могут быть последствия. Уровень голода указан /m над самой рыбкой \\m"),
            new DialogueLine("Аква",  Characters.Aqua.Stable,  "После покупки корма в /m магазине \\m, еда хранится на /m складе \\m, загляни туда"),
            new DialogueLine("Аква",  Characters.Aqua.Stable,  "Если рыб долго не кормить - они одичают и начнут есть сородичей, будь внимателен"),
            new DialogueLine("Аква",  Characters.Aqua.Stable,  "Так же, рыбки, как и люди, хотят /m жить счастливо \\m, за уровнем счастья ты можешь следить /m нажав на рыбу \\m."),
            new DialogueLine("Аква",  Characters.Aqua.Stable,  "Пока с тебя хватит, я поехала в свой замок, удачи!"),
        };
        ds.StartDialogue(lines, OnAquaDialogueFinished);
    }

    private async void OnAquaDialogueFinished()
    {
        // Show castle cinematic (placeholder path — replace when image is ready)
        var castlePath = "res://assets/scenario/castle_panorama.png";
        if (Godot.FileAccess.FileExists(castlePath))
            await SceneManager.Instance.PlayCinematicPan(castlePath, 4f);
        else
            await ToSignal(GetTree().CreateTimer(2f), SceneTreeTimer.SignalName.Timeout);

        var ds = DialogueSystem.Instance;
        if (ds == null) return;
        var lines = new[]
        {
            new DialogueLine("Герой", Characters.Hero.Stable, "...."),
            new DialogueLine("Герой", Characters.Hero.Stable, "Что это вообще было..."),
            new DialogueLine("Герой", Characters.Hero.Stable, "Ладно, делать всё равно нечего, о чём там она говорила?"),
        };
        ds.StartDialogue(lines, () => QuestManager.Instance?.OnAquaIntroDone());
    }

    // ── Диалог Героя после Q4 (нажал на рыбу) ────────────────────────────────
    public async void PlayHeroPost4Dialogue()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var ds = DialogueSystem.Instance;
        if (ds == null) return;

        var lines = new[]
        {
            new DialogueLine("Герой", Characters.Hero.Stable, "Вроде сытые, а взгляд грустный, о чём Аква и говорила"),
            new DialogueLine("Герой", Characters.Hero.Stable, "Кажется, папа говорил, что красивый аквариум поднимает им настроение..."),
        };
        ds.StartDialogue(lines, () => QuestManager.Instance?.OnHeroPost4DialogueDone());
    }

    // ── Диалог Героя после Q6 (прошло 40 секунд) ─────────────────────────────
    public async void PlayHeroPost6Dialogue()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var ds = DialogueSystem.Instance;
        if (ds == null) return;

        var lines = new[]
        {
            new DialogueLine("Герой", Characters.Hero.Stable, "Каждый раз кормить вручную слишком неудобно, как только Аква с этим справляется"),
            new DialogueLine("Герой", Characters.Hero.Stable, "Точно, кажется в магазине была /m автоматическая кормушка \\m"),
        };
        ds.StartDialogue(lines, () => QuestManager.Instance?.OnHeroPost6DialogueDone());
    }

    // ── Диалог Аквы после Q7 (купил автокормушку), задержка 7 сек ───────────
    public async void PlayAquaPost7Dialogue()
    {
        await ToSignal(GetTree().CreateTimer(7f), SceneTreeTimer.SignalName.Timeout);
        var ds = DialogueSystem.Instance;
        if (ds == null) return;

        var lines = new[]
        {
            new DialogueLine("Аква", Characters.Aqua.Happy,  "Ого!"),
            new DialogueLine("Аква", Characters.Aqua.Happy,  "Ты развиваешься даже слишком быстро..."),
            new DialogueLine("Аква", Characters.Aqua.Happy,  "Так даже интереснее."),
            new DialogueLine("Аква", Characters.Aqua.Stable, "Ты можешь наблюдать, насколько далеко тебе до меня во вкладке /m рейтинг \\m"),
            new DialogueLine("Аква", Characters.Aqua.Stable, "Очки даются за каждую рыбу - чем она реже и круче, тем больше"),
            new DialogueLine("Аква", Characters.Aqua.Stable, "К слову, этот аквариум кажется слишком тесным, задумайся о покупке чего-то большего..."),
        };
        ds.StartDialogue(lines, () => QuestManager.Instance?.OnAquaPost7DialogueDone());
    }

    // ── Экран «Глава 2 / Coming Soon» ─────────────────────────────────────────
    public async void PlayChapter2Screen()
    {
        var sm = SceneManager.Instance;
        if (sm != null) await sm.BriefDarken(0.5f);

        GameManager.Instance?.StopMusic();

        var canvas = new CanvasLayer { Layer = 200 };
        GetTree().Root.AddChild(canvas);

        var bg = new ColorRect { Color = Colors.Black };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        canvas.AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        canvas.AddChild(center);

        var vbox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        vbox.AddThemeConstantOverride("separation", 20);
        center.AddChild(vbox);

        var chLabel = new Label
        {
            Text = "Глава 2",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        chLabel.AddThemeFontSizeOverride("font_size", 80);
        chLabel.AddThemeColorOverride("font_color", Colors.White);
        vbox.AddChild(chLabel);

        var soonLabel = new Label
        {
            Text = "Coming Soon...",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        soonLabel.AddThemeFontSizeOverride("font_size", 40);
        soonLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        vbox.AddChild(soonLabel);

        var exitBtn = new Button
        {
            Text = "Выйти",
            CustomMinimumSize = new Vector2(200, 56),
        };
        exitBtn.AddThemeStyleboxOverride("normal",  UiTheme.BuildButtonStyle(new Color(0.15f, 0.15f, 0.15f), new Color(0.5f, 0.5f, 0.5f), 2, 10));
        exitBtn.AddThemeStyleboxOverride("hover",   UiTheme.BuildButtonStyle(new Color(0.25f, 0.25f, 0.25f), new Color(0.8f, 0.8f, 0.8f), 2, 10));
        exitBtn.AddThemeStyleboxOverride("pressed", UiTheme.BuildButtonStyle(new Color(0.1f,  0.1f,  0.1f),  new Color(0.8f, 0.8f, 0.8f), 2, 10));
        exitBtn.AddThemeColorOverride("font_color", Colors.White);
        exitBtn.AddThemeFontSizeOverride("font_size", 26);
        exitBtn.Pressed += () => GetTree().Quit();
        vbox.AddChild(exitBtn);
    }

    // ── Piranha spawn helper ─────────────────────────────────────────────────
    private static Node2d SpawnPiranha(GameManager gm)
    {
        var data = new FishData
        {
            FishName = "Пиранья",
            Price = 0,
            IncomePerSec = 0f,
            Rarity = FishRarity.Common,
            FryDurationSec = 0f,
            TeenDurationSec = 0f,
            FishScene = GD.Load<PackedScene>("res://node_2d.tscn"),
            BodyTexture = GD.Load<Texture2D>("res://assets/fishes/piranha.png"),
            AdultScale = new Vector2(0.4f, 0.4f),
        };

        // SpawnFish is private — use public TryBuyFish workaround via reflection? No.
        // Instead we call the public SpawnPiranhaStory helper we'll add to GameManager.
        return gm.SpawnStoryFish(data);
    }
}
