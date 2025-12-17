using SkiaSharp;

namespace Slipstream.UI.Components;

/// <summary>
/// Base interface for UI components that can be rendered and interacted with.
/// </summary>
public interface IUIComponent
{
    /// <summary>
    /// Unique identifier for this component.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Bounding rectangle of this component.
    /// </summary>
    SKRect Bounds { get; }

    /// <summary>
    /// Whether the component is currently enabled.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Whether the component is visible.
    /// </summary>
    bool IsVisible { get; set; }

    /// <summary>
    /// Renders the component to the canvas.
    /// </summary>
    void Render(SKCanvas canvas, UITheme theme);

    /// <summary>
    /// Tests if a point is within this component's bounds.
    /// </summary>
    bool HitTest(SKPoint point);

    /// <summary>
    /// Handles a click at the given point. Returns an action if one should be triggered.
    /// </summary>
    UIAction? HandleClick(SKPoint point);
}

/// <summary>
/// Theme configuration for UI components.
/// </summary>
public class UITheme
{
    public SKColor Background { get; set; } = new(45, 45, 48);
    public SKColor Surface { get; set; } = new(60, 60, 64);
    public SKColor Primary { get; set; } = new(0, 122, 204);
    public SKColor PrimaryHover { get; set; } = new(30, 144, 220);
    public SKColor Text { get; set; } = SKColors.White;
    public SKColor TextSecondary { get; set; } = new(180, 180, 180);
    public SKColor TextDisabled { get; set; } = new(100, 100, 100);
    public SKColor Border { get; set; } = new(80, 80, 84);
    public SKColor Success { get; set; } = new(76, 175, 80);
    public SKColor Warning { get; set; } = new(255, 152, 0);
    public SKColor Error { get; set; } = new(244, 67, 54);

    public SKTypeface Font { get; set; } = SKTypeface.FromFamilyName("Segoe UI");
    public float FontSize { get; set; } = 12f;
    public float SmallFontSize { get; set; } = 10f;
    public float LargeFontSize { get; set; } = 14f;
}
