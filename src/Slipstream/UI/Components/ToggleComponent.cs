using SkiaSharp;

namespace Slipstream.UI.Components;

/// <summary>
/// A toggle switch component for boolean settings.
/// </summary>
public class ToggleComponent : IUIComponent
{
    public string Id { get; }
    public SKRect Bounds { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;

    public string Label { get; set; }
    public bool IsChecked { get; set; }
    public string SettingKey { get; set; }

    private const float ToggleWidth = 36;
    private const float ToggleHeight = 18;
    private const float KnobRadius = 7;

    public ToggleComponent(string id, string label, string settingKey, bool isChecked, SKRect bounds)
    {
        Id = id;
        Label = label;
        SettingKey = settingKey;
        IsChecked = isChecked;
        Bounds = bounds;
    }

    public void Render(SKCanvas canvas, UITheme theme)
    {
        if (!IsVisible) return;

        // Draw label
        using var labelPaint = new SKPaint
        {
            Color = IsEnabled ? theme.Text : theme.TextDisabled,
            IsAntialias = true,
            Typeface = theme.Font,
            TextSize = theme.FontSize
        };

        var labelY = Bounds.MidY + labelPaint.TextSize / 3;
        canvas.DrawText(Label, Bounds.Left, labelY, labelPaint);

        // Draw toggle switch (right-aligned)
        var toggleLeft = Bounds.Right - ToggleWidth;
        var toggleTop = Bounds.MidY - ToggleHeight / 2;
        var toggleRect = new SKRect(toggleLeft, toggleTop, toggleLeft + ToggleWidth, toggleTop + ToggleHeight);

        // Track background
        var trackColor = IsChecked ? theme.Primary : theme.Surface;
        if (!IsEnabled)
        {
            trackColor = trackColor.WithAlpha(128);
        }

        using var trackPaint = new SKPaint
        {
            Color = trackColor,
            IsAntialias = true
        };
        canvas.DrawRoundRect(toggleRect, ToggleHeight / 2, ToggleHeight / 2, trackPaint);

        // Track border
        using var borderPaint = new SKPaint
        {
            Color = theme.Border,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        canvas.DrawRoundRect(toggleRect, ToggleHeight / 2, ToggleHeight / 2, borderPaint);

        // Knob
        var knobX = IsChecked
            ? toggleLeft + ToggleWidth - KnobRadius - 2
            : toggleLeft + KnobRadius + 2;
        var knobY = toggleTop + ToggleHeight / 2;

        using var knobPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };
        canvas.DrawCircle(knobX, knobY, KnobRadius, knobPaint);
    }

    public bool HitTest(SKPoint point)
    {
        return IsVisible && IsEnabled && Bounds.Contains(point);
    }

    public UIAction? HandleClick(SKPoint point)
    {
        if (HitTest(point))
        {
            return new ToggleSettingAction(SettingKey, !IsChecked);
        }
        return null;
    }
}
