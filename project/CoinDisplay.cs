using Godot;
using System.Collections.Generic;

public partial class CoinDisplay : Control
{
    [Export] public TextureRect BackgroundRect;
    [Export] public HBoxContainer DigitsContainer;
    [Export] public Texture2D BackgroundTexture;

    [Export] public int DigitHeight = 30;
    [Export] public int DigitSpacing = -1;
    [Export] public bool UseGrouping = false;

    private readonly Dictionary<char, Texture2D> _digitTextures = new();
    private int _lastAmount = int.MinValue;

    public override void _Ready()
    {
        ClipContents = true;
        BackgroundRect ??= GetNodeOrNull<TextureRect>("BackgroundRect");
        DigitsContainer ??= GetNodeOrNull<HBoxContainer>("DigitsContainer");

        if (BackgroundTexture == null)
            BackgroundTexture = GD.Load<Texture2D>("res://assets/coins/coins.png");

        if (BackgroundRect != null && BackgroundTexture != null)
            BackgroundRect.Texture = BackgroundTexture;

        if (DigitsContainer != null)
            DigitsContainer.AddThemeConstantOverride("separation", DigitSpacing);

        LoadDigitTextures();
    }

    public void SetAmount(int amount)
    {
        if (amount < 0)
            amount = 0;

        if (_lastAmount == amount || DigitsContainer == null)
            return;

        _lastAmount = amount;

        foreach (Node child in DigitsContainer.GetChildren())
            child.QueueFree();

        var text = UseGrouping ? amount.ToString("N0") : amount.ToString();
        foreach (var ch in text)
        {
            if (char.IsDigit(ch))
                AddDigit(ch);
        }
    }

    private void AddDigit(char digit)
    {
        if (!_digitTextures.TryGetValue(digit, out var texture) || texture == null || DigitsContainer == null)
            return;

        var rect = new TextureRect
        {
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        rect.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        rect.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;

        var size = texture.GetSize();
        var width = size.Y > 0 ? (size.X / size.Y) * DigitHeight : DigitHeight;
        rect.CustomMinimumSize = new Vector2(width, DigitHeight);
        DigitsContainer.AddChild(rect);
    }

    private void LoadDigitTextures()
    {
        _digitTextures.Clear();
        AddDigitTexture('0', "zero");
        AddDigitTexture('1', "one");
        AddDigitTexture('2', "two");
        AddDigitTexture('3', "three");
        AddDigitTexture('4', "four");
        AddDigitTexture('5', "five");
        AddDigitTexture('6', "six");
        AddDigitTexture('7', "seven");
        AddDigitTexture('8', "eight");
        AddDigitTexture('9', "nine");
    }

    private void AddDigitTexture(char key, string name)
    {
        _digitTextures[key] = GD.Load<Texture2D>($"res://assets/digits/{name}.png");
    }
}
