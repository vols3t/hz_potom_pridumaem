using Godot;
using System.Threading.Tasks;

public partial class SceneManager : Node
{
    public static SceneManager Instance { get; private set; }

    private CanvasLayer _layer;
    private ColorRect _brightnessOverlay;
    private TextureRect _backgroundRect;
    private Control _chapterOverlay;
    private Label _chapterLabel;
    private ColorRect _fadeRect;

    private bool _chapterSkipRequested;

    public override void _EnterTree() => Instance = this;

    public override void _Ready()
    {
        _layer = new CanvasLayer { Layer = 100 };
        AddChild(_layer);

        // 1. Brightness — bottommost, never blocks input
        _brightnessOverlay = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        ApplyFullRect(_brightnessOverlay);
        _layer.AddChild(_brightnessOverlay);

        // 2. Story background image
        _backgroundRect = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        ApplyFullRect(_backgroundRect);
        _layer.AddChild(_backgroundRect);

        // 3. Chapter title — black bg + centered label
        _chapterOverlay = new Control
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        ApplyFullRect(_chapterOverlay);
        _layer.AddChild(_chapterOverlay);

        var chapterBg = new ColorRect { Color = new Color(0f, 0f, 0f, 1f) };
        ApplyFullRect(chapterBg);
        _chapterOverlay.AddChild(chapterBg);

        _chapterLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _chapterLabel.AddThemeColorOverride("font_color", new Color("e0f0ff"));
        _chapterLabel.AddThemeFontSizeOverride("font_size", 72);
        ApplyFullRect(_chapterLabel);
        _chapterOverlay.AddChild(_chapterLabel);

        // 4. Fade rect — topmost
        _fadeRect = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        ApplyFullRect(_fadeRect);
        _layer.AddChild(_fadeRect);
    }

    public override void _Input(InputEvent @event)
    {
        if (!_chapterOverlay.Visible) return;
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            _chapterSkipRequested = true;
            GetViewport().SetInputAsHandled();
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public async void GoToAquarium()
    {
        await FadeToBlack();
        GetTree().ChangeSceneToFile("res://aquarium.tscn");
        // wait one frame for scene to register
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ShowChapterTitleAsync("Глава 1", 5f);
        await FadeFromBlack();
        StoryManager.Instance?.StartChapter1();
    }

    public async void ShakeScreen(float duration = 0.4f, float strength = 18f)
    {
        var scene = GetTree().CurrentScene as Node2D;
        if (scene == null) return;
        var elapsed = 0f;
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        while (elapsed < duration)
        {
            elapsed += (float)GetProcessDeltaTime();
            var t = 1f - elapsed / duration;
            scene.Position = new Vector2(
                rng.RandfRange(-strength, strength) * t,
                rng.RandfRange(-strength * 0.6f, strength * 0.6f) * t);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        scene.Position = Vector2.Zero;
    }

    // Cinematic pan over a wide image (e.g. 4320x868).
    // Zooms out slightly at start while panning left→right.
    public async Task PlayCinematicPan(string texturePath, float duration = 5f)
    {
        var tex = GD.Load<Texture2D>(texturePath);
        if (tex == null) return;

        var vp = GetViewport().GetVisibleRect().Size;
        var imgW = tex.GetWidth();
        var imgH = tex.GetHeight();
        // Scale image to fill viewport height
        var scale = vp.Y / imgH;
        var displayW = imgW * scale;

        _backgroundRect.Texture = tex;
        _backgroundRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _backgroundRect.StretchMode = TextureRect.StretchModeEnum.Scale;
        _backgroundRect.Size = new Vector2(displayW * 1.15f, vp.Y * 1.15f);
        _backgroundRect.Position = new Vector2(-(displayW * 1.15f - vp.X) * 0f, -(vp.Y * 1.15f - vp.Y) * 0.5f);
        _backgroundRect.LayoutMode = 3;
        _backgroundRect.Visible = true;

        // Tween: pan right + zoom out (scale 1.15 → 1.0)
        var tween = CreateTween();
        tween.SetParallel(true);
        // Pan: move position.x from 0 to -(displayW - vp.X)
        var endX = -(displayW - vp.X);
        tween.TweenProperty(_backgroundRect, "position:x", endX, duration).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(_backgroundRect, "size", new Vector2(displayW, vp.Y), duration * 0.8).SetTrans(Tween.TransitionType.Sine);
        await ToSignal(tween, Tween.SignalName.Finished);

        _backgroundRect.Visible = false;
        _backgroundRect.LayoutMode = 1;
        _backgroundRect.AnchorLeft = 0f; _backgroundRect.AnchorTop = 0f;
        _backgroundRect.AnchorRight = 1f; _backgroundRect.AnchorBottom = 1f;
    }

    public void ShowBackground(string texturePath)
    {
        var tex = GD.Load<Texture2D>(texturePath);
        if (tex == null) return;
        _backgroundRect.Texture = tex;
        _backgroundRect.Visible = true;
    }

    public void HideBackground()
    {
        _backgroundRect.Visible = false;
        _backgroundRect.Texture = null;
    }

    // value 0..100: 100 = no overlay, 0 = dark
    public void ApplyBrightness(float value)
    {
        if (_brightnessOverlay == null) return;
        var alpha = (1f - Mathf.Clamp(value, 0f, 100f) / 100f) * 0.85f;
        _brightnessOverlay.Color = new Color(0f, 0f, 0f, alpha);
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private async Task ShowChapterTitleAsync(string text, float duration)
    {
        _chapterSkipRequested = false;
        _chapterLabel.Text = text;
        _chapterOverlay.Visible = true;

        // Fade rect goes transparent to reveal the chapter title
        var tIn = CreateTween();
        tIn.TweenProperty(_fadeRect, "color:a", 0f, 0.4);
        _fadeRect.MouseFilter = Control.MouseFilterEnum.Ignore;
        await ToSignal(tIn, Tween.SignalName.Finished);

        // Wait up to 'duration' seconds, but bail early on click
        var elapsed = 0f;
        while (elapsed < duration && !_chapterSkipRequested)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            elapsed += (float)GetProcessDeltaTime();
        }

        // Fade back to black before hiding overlay
        _fadeRect.MouseFilter = Control.MouseFilterEnum.Stop;
        var tOut = CreateTween();
        tOut.TweenProperty(_fadeRect, "color:a", 1f, 0.4);
        await ToSignal(tOut, Tween.SignalName.Finished);

        _chapterOverlay.Visible = false;
    }

    public async Task BriefDarken(float duration)
    {
        _fadeRect.MouseFilter = Control.MouseFilterEnum.Stop;
        _fadeRect.Color = new Color(0f, 0f, 0f, 0f);
        var t1 = CreateTween();
        t1.TweenProperty(_fadeRect, "color:a", 1f, duration * 0.4f);
        await ToSignal(t1, Tween.SignalName.Finished);
        var t2 = CreateTween();
        t2.TweenProperty(_fadeRect, "color:a", 0f, duration * 0.6f);
        await ToSignal(t2, Tween.SignalName.Finished);
        _fadeRect.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    private async Task FadeToBlack()
    {
        _fadeRect.MouseFilter = Control.MouseFilterEnum.Stop;
        _fadeRect.Color = new Color(0f, 0f, 0f, 0f);
        var tween = CreateTween();
        tween.TweenProperty(_fadeRect, "color:a", 1f, 0.4);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private async Task FadeFromBlack()
    {
        _fadeRect.Color = new Color(0f, 0f, 0f, 1f);
        var tween = CreateTween();
        tween.TweenProperty(_fadeRect, "color:a", 0f, 0.4);
        await ToSignal(tween, Tween.SignalName.Finished);
        _fadeRect.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    private static void ApplyFullRect(Control control)
    {
        control.LayoutMode = 1;
        control.AnchorLeft   = 0f;
        control.AnchorTop    = 0f;
        control.AnchorRight  = 1f;
        control.AnchorBottom = 1f;
        control.OffsetLeft   = 0f;
        control.OffsetTop    = 0f;
        control.OffsetRight  = 0f;
        control.OffsetBottom = 0f;
    }
}
