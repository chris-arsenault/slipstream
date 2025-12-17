namespace Slipstream.Settings;

/// <summary>
/// Interface for settings sections. Each section manages a logical group of related settings.
/// </summary>
public interface ISettingsSection
{
    /// <summary>
    /// Unique name for this settings section (e.g., "General", "HUD", "Hotkeys").
    /// </summary>
    string SectionName { get; }

    /// <summary>
    /// Applies default values to this section.
    /// </summary>
    void ApplyDefaults();
}
