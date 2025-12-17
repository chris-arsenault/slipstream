using SkiaSharp;

namespace Slipstream.UI.Components;

/// <summary>
/// A simple text label component.
/// </summary>
public class LabelComponent : IUIComponent
{
    public string Id { get; }
    public SKRect Bounds { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;

    public string Text { get; set; }
    public LabelStyle Style { get; set; } = LabelStyle.Normal;
    public SKTextAlign Alignment { get; set; } = SKTextAlign.Left;

    public LabelComponent(string id, string text, SKRect bounds, LabelStyle style = LabelStyle.Normal)
    {
        Id = id;
        Text = text;
        Bounds = bounds;
        Style = style;
    }

    public void Render(SKCanvas canvas, UITheme theme)
    {
        if (!IsVisible) return;

        var (color, size) = Style switch
        {
            LabelStyle.Header => (theme.Text, theme.LargeFontSize),
            LabelStyle.Secondary => (theme.TextSecondary, theme.FontSize),
            LabelStyle.Small => (theme.TextSecondary, theme.SmallFontSize),
            _ => (theme.Text, theme.FontSize)
        };

        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            Typeface = theme.Font,
            TextSize = size,
            TextAlign = Alignment
        };

        var x = Alignment switch
        {
            SKTextAlign.Center => Bounds.MidX,
            SKTextAlign.Right => Bounds.Right,
            _ => Bounds.Left
        };

        var y = Bounds.MidY + paint.TextSize / 3;
        canvas.DrawText(Text, x, y, paint);
    }

    public bool HitTest(SKPoint point) => false; // Labels are not clickable

    public UIAction? HandleClick(SKPoint point) => null;
}

public enum LabelStyle
{
    Normal,
    Header,
    Secondary,
    Small
}
