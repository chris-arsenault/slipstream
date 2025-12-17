namespace Slipstream.Settings.Sections;

using Slipstream.Models;

/// <summary>
/// General application settings including startup behavior and slot configuration.
/// </summary>
public class GeneralSettings : ISettingsSection
{
    public string SectionName => "General";

    /// <summary>
    /// Number of clipboard slots available (1-10).
    /// </summary>
    public int SlotCount { get; set; } = 10;

    /// <summary>
    /// Start Slipstream automatically with Windows.
    /// </summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>
    /// Start minimized to system tray.
    /// </summary>
    public bool StartMinimized { get; set; } = true;

    /// <summary>
    /// Show HUD window when application starts.
    /// </summary>
    public bool ShowHudOnStart { get; set; } = false;

    /// <summary>
    /// How slots are filled when clipboard content is captured.
    /// </summary>
    public SlotFillMode FillMode { get; set; } = SlotFillMode.RoundRobin;

    /// <summary>
    /// Auto-promote temp slot to numbered slot on capture.
    /// </summary>
    public bool AutoPromote { get; set; } = false;

    /// <summary>
    /// How promote picks the target slot.
    /// </summary>
    public SlotBehavior SlotBehavior { get; set; } = SlotBehavior.RoundRobin;

    /// <summary>
    /// List of process names that use sticky slot behavior.
    /// </summary>
    public List<string> StickyApps { get; set; } = new()
    {
        "ScreenClippingHost",
        "SnippingTool",
    };

    public void ApplyDefaults()
    {
        SlotCount = 10;
        StartWithWindows = false;
        StartMinimized = true;
        ShowHudOnStart = false;
        FillMode = SlotFillMode.RoundRobin;
        AutoPromote = false;
        SlotBehavior = SlotBehavior.RoundRobin;
        StickyApps = new List<string> { "ScreenClippingHost", "SnippingTool" };
    }
}
