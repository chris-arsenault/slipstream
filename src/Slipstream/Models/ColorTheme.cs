using SkiaSharp;

namespace Slipstream.Models;

/// <summary>
/// Defines a complete color theme for the application UI.
/// </summary>
public class ColorTheme
{
    public SKColor Background { get; init; }
    public SKColor BackgroundSecondary { get; init; }
    public SKColor TitleBar { get; init; }
    public SKColor Text { get; init; }
    public SKColor TextSecondary { get; init; }
    public SKColor Accent { get; init; }
    public SKColor Button { get; init; }
    public SKColor ButtonHover { get; init; }
    public SKColor Border { get; init; }
    public SKColor SlotBackground { get; init; }
    public SKColor SlotActive { get; init; }
    public SKColor SlotLocked { get; init; }
    public SKColor Danger { get; init; }
    public SKColor DangerHover { get; init; }

    /// <summary>
    /// Dark theme - the original theme with blue accent.
    /// </summary>
    public static ColorTheme Dark { get; } = new()
    {
        Background = new SKColor(35, 35, 35, 255),
        BackgroundSecondary = new SKColor(45, 45, 45, 255),
        TitleBar = new SKColor(25, 25, 25, 255),
        Text = new SKColor(220, 220, 220, 255),
        TextSecondary = new SKColor(150, 150, 150, 255),
        Accent = new SKColor(70, 130, 180, 255),
        Button = new SKColor(55, 55, 55, 255),
        ButtonHover = new SKColor(70, 70, 70, 255),
        Border = new SKColor(60, 60, 60, 255),
        SlotBackground = new SKColor(45, 45, 45, 255),
        SlotActive = new SKColor(70, 130, 180, 60),
        SlotLocked = new SKColor(180, 130, 70, 255),
        Danger = new SKColor(140, 50, 50, 255),
        DangerHover = new SKColor(180, 60, 60, 255)
    };

    /// <summary>
    /// Light theme - clean light mode.
    /// </summary>
    public static ColorTheme Light { get; } = new()
    {
        Background = new SKColor(245, 245, 245, 255),
        BackgroundSecondary = new SKColor(235, 235, 235, 255),
        TitleBar = new SKColor(225, 225, 225, 255),
        Text = new SKColor(30, 30, 30, 255),
        TextSecondary = new SKColor(100, 100, 100, 255),
        Accent = new SKColor(50, 110, 160, 255),
        Button = new SKColor(215, 215, 215, 255),
        ButtonHover = new SKColor(200, 200, 200, 255),
        Border = new SKColor(180, 180, 180, 255),
        SlotBackground = new SKColor(255, 255, 255, 255),
        SlotActive = new SKColor(50, 110, 160, 60),
        SlotLocked = new SKColor(200, 150, 50, 255),
        Danger = new SKColor(180, 60, 60, 255),
        DangerHover = new SKColor(200, 70, 70, 255)
    };

    /// <summary>
    /// Terminal theme - dark with green text like classic terminals.
    /// </summary>
    public static ColorTheme Terminal { get; } = new()
    {
        Background = new SKColor(15, 15, 15, 255),
        BackgroundSecondary = new SKColor(25, 25, 25, 255),
        TitleBar = new SKColor(10, 10, 10, 255),
        Text = new SKColor(0, 255, 65, 255),
        TextSecondary = new SKColor(0, 180, 45, 255),
        Accent = new SKColor(0, 255, 65, 255),
        Button = new SKColor(30, 30, 30, 255),
        ButtonHover = new SKColor(45, 45, 45, 255),
        Border = new SKColor(0, 180, 45, 255),
        SlotBackground = new SKColor(20, 20, 20, 255),
        SlotActive = new SKColor(0, 255, 65, 60),
        SlotLocked = new SKColor(255, 180, 0, 255),
        Danger = new SKColor(180, 50, 50, 255),
        DangerHover = new SKColor(220, 60, 60, 255)
    };

    /// <summary>
    /// Gets the theme for a given palette enum value.
    /// </summary>
    public static ColorTheme GetTheme(ColorPalette palette) => palette switch
    {
        ColorPalette.Dark => Dark,
        ColorPalette.Light => Light,
        ColorPalette.Terminal => Terminal,
        _ => Dark
    };
}
