namespace Slipstream.Models;

/// <summary>
/// Settings for MIDI input handling
/// </summary>
public class MidiSettings
{
    /// <summary>
    /// Whether MIDI input is enabled (auto-enabled when device detected)
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Selected MIDI device name (null = auto-select first available)
    /// </summary>
    public string? DeviceName { get; set; } = null;

    /// <summary>
    /// Active control scheme preset name
    /// </summary>
    public string ActivePreset { get; set; } = "Launchkey25Mini";

    /// <summary>
    /// Custom mappings that override preset defaults
    /// Key: ActionName (e.g., "CopyToSlot1", "PasteFromSlot3")
    /// </summary>
    public Dictionary<string, MidiTrigger> CustomMappings { get; set; } = new();

    /// <summary>
    /// Optional modifier note - when held, notes trigger Copy instead of Paste
    /// </summary>
    public MidiTrigger? CopyModifier { get; set; } = null;

    /// <summary>
    /// Minimum velocity to register a note (0-127, default 1 = any touch)
    /// </summary>
    public int VelocityThreshold { get; set; } = 1;
}

/// <summary>
/// Defines a single MIDI trigger (note or CC)
/// </summary>
public class MidiTrigger
{
    /// <summary>
    /// Type of MIDI message that triggers this action
    /// </summary>
    public MidiTriggerType Type { get; set; }

    /// <summary>
    /// MIDI channel (0-15), or null for any channel
    /// </summary>
    public int? Channel { get; set; } = null;

    /// <summary>
    /// Note number (0-127) for Note triggers, or CC number for CC triggers
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// For CC: threshold value (0-127) above which trigger fires
    /// For Note: minimum velocity (default 1)
    /// </summary>
    public int Threshold { get; set; } = 64;

    /// <summary>
    /// For CC triggers: fire on rising edge (crossing threshold upward)
    /// </summary>
    public bool TriggerOnRise { get; set; } = true;

    public MidiTrigger() { }

    public MidiTrigger(MidiTriggerType type, int number, int? channel = null, int threshold = 1)
    {
        Type = type;
        Number = number;
        Channel = channel;
        Threshold = threshold;
    }

    /// <summary>
    /// Create a Note On trigger
    /// </summary>
    public static MidiTrigger NoteOn(int noteNumber, int? channel = null, int velocityThreshold = 1)
        => new(MidiTriggerType.NoteOn, noteNumber, channel, velocityThreshold);

    /// <summary>
    /// Create a Control Change trigger
    /// </summary>
    public static MidiTrigger CC(int ccNumber, int threshold = 64, int? channel = null, bool triggerOnRise = true)
        => new(MidiTriggerType.ControlChange, ccNumber, channel, threshold) { TriggerOnRise = triggerOnRise };
}

/// <summary>
/// Type of MIDI message
/// </summary>
public enum MidiTriggerType
{
    /// <summary>
    /// Note On message (key/pad pressed)
    /// </summary>
    NoteOn,

    /// <summary>
    /// Note Off message (key/pad released)
    /// </summary>
    NoteOff,

    /// <summary>
    /// Control Change message (knob/fader/button)
    /// </summary>
    ControlChange
}
