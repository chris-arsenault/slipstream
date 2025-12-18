namespace Slipstream.Processing;

/// <summary>
/// Manages which processors are active for paste operations.
/// Combines toggle-based activation with MIDI chord keys.
/// </summary>
public class ProcessorActivation
{
    private readonly ProcessorToggleState _toggleState;
    private Func<IReadOnlySet<string>>? _midiChordProvider;
    private static readonly HashSet<string> EmptySet = new();

    public ProcessorActivation(ProcessorToggleState toggleState)
    {
        _toggleState = toggleState;
    }

    /// <summary>
    /// Sets the MIDI chord provider function.
    /// </summary>
    public void SetMidiChordProvider(Func<IReadOnlySet<string>> provider)
    {
        _midiChordProvider = provider;
    }

    /// <summary>
    /// Gets the toggle state for HUD display.
    /// </summary>
    public ProcessorToggleState ToggleState => _toggleState;

    /// <summary>
    /// Gets the set of MIDI chord processors currently held.
    /// </summary>
    public IReadOnlySet<string> GetMidiChords()
    {
        return _midiChordProvider?.Invoke() ?? EmptySet;
    }

    /// <summary>
    /// Gets the active set of processors (toggles + MIDI chords).
    /// </summary>
    public IReadOnlySet<string> GetActiveSet()
    {
        var toggles = _toggleState.ToggledProcessors;
        var midiChords = GetMidiChords();

        if (midiChords.Count == 0)
            return toggles;

        if (toggles.Count == 0)
            return midiChords;

        // Combine both sets
        var combined = new HashSet<string>(toggles);
        foreach (var chord in midiChords)
            combined.Add(chord);
        return combined;
    }

    /// <summary>
    /// Gets the active processor definitions sorted by priority.
    /// </summary>
    public IEnumerable<ProcessorDefinition> GetActiveDefinitions()
    {
        var activeSet = GetActiveSet();
        return ProcessorDefinitions.All.Where(d => activeSet.Contains(d.Name));
    }

    /// <summary>
    /// Checks if any processors are currently active.
    /// </summary>
    public bool HasActiveProcessors => _toggleState.ToggledProcessors.Count > 0 || (GetMidiChords().Count > 0);
}
