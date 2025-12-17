using SkiaSharp;
using Slipstream.Models;

namespace Slipstream.UI;

/// <summary>
/// Base class for UI renderers providing shared paint management and common utilities.
/// </summary>
public abstract class BaseRenderer
{
    protected ColorTheme _theme;

    // Common paints shared by all renderers
    protected readonly SKPaint _backgroundPaint;
    protected readonly SKPaint _titleBarPaint;
    protected readonly SKPaint _titlePaint;
    protected readonly SKPaint _textPaint;
    protected readonly SKPaint _secondaryTextPaint;
    protected readonly SKPaint _borderPaint;

    protected BaseRenderer(ColorTheme theme)
    {
        _theme = theme;

        _backgroundPaint = CreateFillPaint(theme.Background);
        _titleBarPaint = CreateFillPaint(theme.TitleBar);
        _titlePaint = CreateTextPaint(theme.Text, 14f, SKFontStyle.Bold);
        _textPaint = CreateTextPaint(theme.Text, 12f);
        _secondaryTextPaint = CreateTextPaint(theme.TextSecondary, 10f);
        _borderPaint = CreateStrokePaint(theme.Border, 1f);
    }

    /// <summary>
    /// Updates all paint colors when theme changes.
    /// Override in derived classes to update additional paints.
    /// </summary>
    protected virtual void UpdatePaintColors()
    {
        _backgroundPaint.Color = _theme.Background;
        _titleBarPaint.Color = _theme.TitleBar;
        _titlePaint.Color = _theme.Text;
        _textPaint.Color = _theme.Text;
        _secondaryTextPaint.Color = _theme.TextSecondary;
        _borderPaint.Color = _theme.Border;
    }

    /// <summary>
    /// Creates a fill paint with antialiasing.
    /// </summary>
    protected static SKPaint CreateFillPaint(SKColor color) => new()
    {
        Color = color,
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    /// <summary>
    /// Creates a text paint with the specified size and style.
    /// </summary>
    protected static SKPaint CreateTextPaint(SKColor color, float size, SKFontStyle? style = null) => new()
    {
        Color = color,
        IsAntialias = true,
        TextSize = size,
        Typeface = SKTypeface.FromFamilyName("Segoe UI", style ?? SKFontStyle.Normal)
    };

    /// <summary>
    /// Creates a stroke paint with the specified width.
    /// </summary>
    protected static SKPaint CreateStrokePaint(SKColor color, float strokeWidth) => new()
    {
        Color = color,
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = strokeWidth
    };
}
