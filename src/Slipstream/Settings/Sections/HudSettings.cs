namespace Slipstream.Settings.Sections;

using Slipstream.Models;

/// <summary>
/// HUD (Heads-Up Display) appearance and behavior settings.
/// </summary>
public class HudSettings : ISettingsSection
{
    public string SectionName => "HUD";

    /// <summary>
    /// Position of the HUD window on screen.
    /// </summary>
    public HudPosition Position { get; set; } = HudPosition.BottomRight;

    /// <summary>
    /// Opacity of the HUD window (0.0 to 1.0).
    /// </summary>
    public float Opacity { get; set; } = 0.9f;

    /// <summary>
    /// Whether clicks pass through the HUD window.
    /// </summary>
    public bool ClickThrough { get; set; } = false;

    /// <summary>
    /// Color palette/theme for the application.
    /// </summary>
    public ColorPalette ColorPalette { get; set; } = ColorPalette.Dark;

    /// <summary>
    /// Saved HUD window X position (null = use default).
    /// </summary>
    public double? WindowX { get; set; } = null;

    /// <summary>
    /// Saved HUD window Y position (null = use default).
    /// </summary>
    public double? WindowY { get; set; } = null;

    /// <summary>
    /// Saved Settings window X position (null = use default).
    /// </summary>
    public double? SettingsWindowX { get; set; } = null;

    /// <summary>
    /// Saved Settings window Y position (null = use default).
    /// </summary>
    public double? SettingsWindowY { get; set; } = null;

    public void ApplyDefaults()
    {
        Position = HudPosition.BottomRight;
        Opacity = 0.9f;
        ClickThrough = false;
        ColorPalette = ColorPalette.Dark;
        WindowX = null;
        WindowY = null;
        SettingsWindowX = null;
        SettingsWindowY = null;
    }
}
