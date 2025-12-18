namespace Slipstream.Processing;

/// <summary>
/// Manages the toggle state of processors. Toggled processors apply to all paste operations
/// until toggled off. State is persisted across sessions.
/// </summary>
public class ProcessorToggleState
{
    private readonly HashSet<string> _toggledOn = new(StringComparer.OrdinalIgnoreCase);
    private Action<HashSet<string>>? _persistCallback;

    /// <summary>
    /// Event fired when toggle state changes.
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// Gets the set of currently toggled-on processor names.
    /// </summary>
    public IReadOnlySet<string> ToggledProcessors => _toggledOn;

    /// <summary>
    /// Checks if a processor is currently toggled on.
    /// </summary>
    public bool IsToggled(string processorName) =>
        _toggledOn.Contains(processorName);

    /// <summary>
    /// Toggles a processor on/off.
    /// </summary>
    public void Toggle(string processorName)
    {
        if (!_toggledOn.Remove(processorName))
            _toggledOn.Add(processorName);

        Console.WriteLine($"[ProcessorToggleState] Toggled '{processorName}': {IsToggled(processorName)}");
        OnStateChanged();
    }

    /// <summary>
    /// Sets a processor's toggle state explicitly.
    /// </summary>
    public void SetToggle(string processorName, bool on)
    {
        bool changed;
        if (on)
            changed = _toggledOn.Add(processorName);
        else
            changed = _toggledOn.Remove(processorName);

        if (changed)
        {
            Console.WriteLine($"[ProcessorToggleState] Set '{processorName}': {on}");
            OnStateChanged();
        }
    }

    /// <summary>
    /// Clears all toggle states.
    /// </summary>
    public void ClearAll()
    {
        if (_toggledOn.Count == 0) return;

        _toggledOn.Clear();
        Console.WriteLine("[ProcessorToggleState] Cleared all toggles");
        OnStateChanged();
    }

    /// <summary>
    /// Loads toggle state from persisted data.
    /// </summary>
    public void LoadFromPersisted(IEnumerable<string>? processorNames)
    {
        _toggledOn.Clear();
        if (processorNames != null)
        {
            foreach (var name in processorNames)
            {
                // Only load valid processor names
                if (ProcessorDefinitions.GetByName(name) != null)
                    _toggledOn.Add(name);
            }
        }

        if (_toggledOn.Count > 0)
            Console.WriteLine($"[ProcessorToggleState] Loaded {_toggledOn.Count} toggled processors: {string.Join(", ", _toggledOn)}");
    }

    /// <summary>
    /// Sets a callback to persist toggle state when it changes.
    /// </summary>
    public void SetPersistCallback(Action<HashSet<string>> callback)
    {
        _persistCallback = callback;
    }

    /// <summary>
    /// Gets the definitions of currently toggled processors, sorted by priority.
    /// </summary>
    public IEnumerable<ProcessorDefinition> GetToggledDefinitions() =>
        ProcessorDefinitions.All.Where(d => _toggledOn.Contains(d.Name));

    private void OnStateChanged()
    {
        // Persist the state
        _persistCallback?.Invoke(new HashSet<string>(_toggledOn));

        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
