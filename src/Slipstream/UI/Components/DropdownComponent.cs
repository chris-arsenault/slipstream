using SkiaSharp;

namespace Slipstream.UI.Components;

/// <summary>
/// A dropdown/select component for choosing from multiple options.
/// </summary>
public class DropdownComponent : IUIComponent
{
    public string Id { get; }
    public SKRect Bounds { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;

    public string Label { get; set; }
    public string SettingKey { get; set; }
    public string[] Options { get; set; }
    public int SelectedIndex { get; set; }

    public string SelectedValue => SelectedIndex >= 0 && SelectedIndex < Options.Length
        ? Options[SelectedIndex]
        : "";

    public DropdownComponent(string id, string label, string settingKey, string[] options, int selectedIndex, SKRect bounds)
    {
        Id = id;
        Label = label;
        SettingKey = settingKey;
        Options = options;
        SelectedIndex = selectedIndex;
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

        // Draw dropdown box (right side)
        var dropdownWidth = 120f;
        var dropdownRect = new SKRect(
            Bounds.Right - dropdownWidth,
            Bounds.Top + 2,
            Bounds.Right,
            Bounds.Bottom - 2);

        using var bgPaint = new SKPaint
        {
            Color = theme.Surface,
            IsAntialias = true
        };
        canvas.DrawRoundRect(dropdownRect, 4, 4, bgPaint);

        using var borderPaint = new SKPaint
        {
            Color = theme.Border,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        canvas.DrawRoundRect(dropdownRect, 4, 4, borderPaint);

        // Draw selected value
        using var valuePaint = new SKPaint
        {
            Color = IsEnabled ? theme.Text : theme.TextDisabled,
            IsAntialias = true,
            Typeface = theme.Font,
            TextSize = theme.SmallFontSize
        };

        var valueText = SelectedValue.Length > 15
            ? SelectedValue[..12] + "..."
            : SelectedValue;
        var valueY = dropdownRect.MidY + valuePaint.TextSize / 3;
        canvas.DrawText(valueText, dropdownRect.Left + 8, valueY, valuePaint);

        // Draw dropdown arrow
        var arrowX = dropdownRect.Right - 16;
        var arrowY = dropdownRect.MidY;
        using var arrowPaint = new SKPaint
        {
            Color = theme.TextSecondary,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        using var path = new SKPath();
        path.MoveTo(arrowX - 4, arrowY - 2);
        path.LineTo(arrowX + 4, arrowY - 2);
        path.LineTo(arrowX, arrowY + 3);
        path.Close();
        canvas.DrawPath(path, arrowPaint);
    }

    public bool HitTest(SKPoint point)
    {
        return IsVisible && IsEnabled && Bounds.Contains(point);
    }

    public UIAction? HandleClick(SKPoint point)
    {
        if (HitTest(point))
        {
            // Cycle to next option
            var nextIndex = (SelectedIndex + 1) % Options.Length;
            return new SelectOptionAction(SettingKey, Options[nextIndex]);
        }
        return null;
    }
}
