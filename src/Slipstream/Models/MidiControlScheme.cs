namespace Slipstream.Models;

/// <summary>
/// Represents a pre-built MIDI control scheme for a device type
/// </summary>
public class MidiControlScheme
{
    /// <summary>
    /// Unique identifier for this scheme
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Human-readable description
    /// </summary>
    public string Description { get; init; } = "";

    /// <summary>
    /// Device type hint for auto-detection (e.g., "Launchkey", "Launchpad", "nanoKONTROL")
    /// </summary>
    public string DeviceHint { get; init; } = "";

    /// <summary>
    /// All MIDI mappings for this scheme
    /// Key: ActionName (e.g., "CopyToSlot1", "PasteFromSlot1", "ToggleHud")
    /// </summary>
    public Dictionary<string, MidiTrigger> Mappings { get; init; } = new();

    /// <summary>
    /// Optional copy modifier for this scheme (hold to switch paste to copy)
    /// </summary>
    public MidiTrigger? CopyModifier { get; init; }
}
