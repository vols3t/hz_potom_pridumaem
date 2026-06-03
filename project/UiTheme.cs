using Godot;

public static class UiTheme
{
    public static StyleBoxFlat BuildPanelStyle(Color background, Color border, int borderWidth, int radius)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius
        };

        style.SetBorderWidthAll(borderWidth);
        return style;
    }

    public static StyleBoxFlat BuildButtonStyle(Color background, Color border, int borderWidth, int radius)
    {
        var style = BuildPanelStyle(background, border, borderWidth, radius);
        style.ContentMarginTop = 4;
        style.ContentMarginBottom = 4;
        style.ContentMarginLeft = 10;
        style.ContentMarginRight = 10;
        return style;
    }
}
