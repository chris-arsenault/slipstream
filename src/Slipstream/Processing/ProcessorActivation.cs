namespace Slipstream.Processing;

/// <summary>
/// Manages which processors are active for paste operations.
/// Currently only supports toggle-based activation (chord support disabled for debugging).
/// </summary>
public class ProcessorActivation
{
    private readonly ProcessorToggleState _toggleState;

    public ProcessorActivation(ProcessorToggleState toggleState)
    {
        _toggleState = toggleState;
    }

    /// <summary>
    /// Gets the toggle state for HUD display.
    /// </summary>
    public ProcessorToggleState ToggleState => _toggleState;

    /// <summary>
    /// Gets the active set of processors (toggle-based only for now).
    /// </summary>
    public IReadOnlySet<string> GetActiveSet()
    {
        return _toggleState.ToggledProcessors;
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
    public bool HasActiveProcessors => _toggleState.ToggledProcessors.Count > 0;
}
