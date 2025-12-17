using SkiaSharp;

namespace Slipstream.UI.Components;

/// <summary>
/// A clickable button component.
/// </summary>
public class ButtonComponent : IUIComponent
{
    public string Id { get; }
    public SKRect Bounds { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;

    public string Text { get; set; }
    public UIAction? ClickAction { get; set; }
    public ButtonStyle Style { get; set; } = ButtonStyle.Default;

    public ButtonComponent(string id, string text, SKRect bounds, UIAction? clickAction = null)
    {
        Id = id;
        Text = text;
        Bounds = bounds;
        ClickAction = clickAction;
    }

    public void Render(SKCanvas canvas, UITheme theme)
    {
        if (!IsVisible) return;

        var bgColor = Style switch
        {
            ButtonStyle.Primary => theme.Primary,
            ButtonStyle.Success => theme.Success,
            ButtonStyle.Warning => theme.Warning,
            ButtonStyle.Danger => theme.Error,
            _ => theme.Surface
        };

        if (!IsEnabled)
        {
            bgColor = bgColor.WithAlpha(128);
        }

        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = bgColor,
            IsAntialias = true
        };
        canvas.DrawRoundRect(Bounds, 4, 4, bgPaint);

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = theme.Border,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        canvas.DrawRoundRect(Bounds, 4, 4, borderPaint);

        // Draw text
        using var textPaint = new SKPaint
        {
            Color = IsEnabled ? theme.Text : theme.TextDisabled,
            IsAntialias = true,
            Typeface = theme.Font,
            TextSize = theme.FontSize,
            TextAlign = SKTextAlign.Center
        };

        var textY = Bounds.MidY + textPaint.TextSize / 3;
        canvas.DrawText(Text, Bounds.MidX, textY, textPaint);
    }

    public bool HitTest(SKPoint point)
    {
        return IsVisible && IsEnabled && Bounds.Contains(point);
    }

    public UIAction? HandleClick(SKPoint point)
    {
        if (HitTest(point))
        {
            return ClickAction ?? new ButtonClickAction(Id);
        }
        return null;
    }
}

public enum ButtonStyle
{
    Default,
    Primary,
    Success,
    Warning,
    Danger
}
