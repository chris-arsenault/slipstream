namespace Slipstream.Processing;

/// <summary>
/// Manages the state of the processor picker overlay.
/// </summary>
public class ProcessorPickerState
{
    private bool _isOpen;
    private int _targetSlotIndex = -1;
    private ProcessorOutputMode _outputMode = ProcessorOutputMode.Replace;
    private bool _isBuildingPipeline;

    /// <summary>
    /// The pipeline being built in the picker.
    /// </summary>
    public ProcessorPipeline Pipeline { get; } = new();

    /// <summary>
    /// Whether the picker is currently open.
    /// </summary>
    public bool IsOpen => _isOpen;

    /// <summary>
    /// The slot index the picker is operating on (-1 for temp slot).
    /// </summary>
    public int TargetSlotIndex => _targetSlotIndex;

    /// <summary>
    /// Where the processor output will go.
    /// </summary>
    public ProcessorOutputMode OutputMode
    {
        get => _outputMode;
        set
        {
            if (_outputMode != value)
            {
                _outputMode = value;
                OnStateChanged(PickerChangeType.OutputModeChanged);
            }
        }
    }

    /// <summary>
    /// Whether the user is currently building a pipeline (Shift held).
    /// </summary>
    public bool IsBuildingPipeline
    {
        get => _isBuildingPipeline;
        set
        {
            if (_isBuildingPipeline != value)
            {
                _isBuildingPipeline = value;
                OnStateChanged(PickerChangeType.PipelineModeChanged);
            }
        }
    }

    /// <summary>
    /// Fires when the picker state changes.
    /// </summary>
    public event EventHandler<PickerStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Opens the picker for the specified slot.
    /// </summary>
    /// <param name="slotIndex">The slot index to operate on (-1 for temp slot).</param>
    /// <param name="outputMode">Initial output mode (defaults to Replace).</param>
    public void Open(int slotIndex, ProcessorOutputMode outputMode = ProcessorOutputMode.Replace)
    {
        if (_isOpen && _targetSlotIndex == slotIndex)
        {
            // Already open for this slot, toggle close
            Close();
            return;
        }

        _isOpen = true;
        _targetSlotIndex = slotIndex;
        _outputMode = outputMode;
        _isBuildingPipeline = false;
        Pipeline.Clear();

        Console.WriteLine($"[ProcessorPickerState] Opened for slot {slotIndex}, mode: {outputMode}");
        OnStateChanged(PickerChangeType.Opened);
    }

    /// <summary>
    /// Closes the picker without applying anything.
    /// </summary>
    public void Close()
    {
        if (!_isOpen)
            return;

        _isOpen = false;
        _targetSlotIndex = -1;
        _isBuildingPipeline = false;
        Pipeline.Clear();

        Console.WriteLine("[ProcessorPickerState] Closed");
        OnStateChanged(PickerChangeType.Closed);
    }

    /// <summary>
    /// Adds a processor to the pipeline.
    /// </summary>
    /// <param name="processorName">The processor name to add.</param>
    public void AddToPipeline(string processorName)
    {
        if (!_isOpen || string.IsNullOrEmpty(processorName))
            return;

        Pipeline.Add(processorName);
        Console.WriteLine($"[ProcessorPickerState] Added '{processorName}' to pipeline: {Pipeline}");
        OnStateChanged(PickerChangeType.PipelineChanged);
    }

    /// <summary>
    /// Removes the last processor from the pipeline.
    /// </summary>
    public void RemoveLastFromPipeline()
    {
        if (!_isOpen || Pipeline.IsEmpty)
            return;

        Pipeline.RemoveLast();
        Console.WriteLine($"[ProcessorPickerState] Removed last from pipeline: {Pipeline}");
        OnStateChanged(PickerChangeType.PipelineChanged);
    }

    /// <summary>
    /// Cycles the output mode to the next value.
    /// </summary>
    public void CycleOutputMode()
    {
        OutputMode = OutputMode switch
        {
            ProcessorOutputMode.Replace => ProcessorOutputMode.Paste,
            ProcessorOutputMode.Paste => ProcessorOutputMode.NewSlot,
            ProcessorOutputMode.NewSlot => ProcessorOutputMode.Replace,
            _ => ProcessorOutputMode.Replace
        };
        Console.WriteLine($"[ProcessorPickerState] Output mode cycled to: {OutputMode}");
    }

    /// <summary>
    /// Gets the display string for the current output mode.
    /// </summary>
    public string GetOutputModeDisplay() => OutputMode switch
    {
        ProcessorOutputMode.Replace => "→ Slot",
        ProcessorOutputMode.Paste => "→ Paste",
        ProcessorOutputMode.NewSlot => "→ New",
        _ => "→ Slot"
    };

    private void OnStateChanged(PickerChangeType changeType)
    {
        StateChanged?.Invoke(this, new PickerStateChangedEventArgs(changeType));
    }
}

/// <summary>
/// Event args for processor picker state changes.
/// </summary>
public class PickerStateChangedEventArgs : EventArgs
{
    public PickerChangeType ChangeType { get; }

    public PickerStateChangedEventArgs(PickerChangeType changeType)
    {
        ChangeType = changeType;
    }
}

/// <summary>
/// Types of picker state changes.
/// </summary>
public enum PickerChangeType
{
    /// <summary>Picker was opened.</summary>
    Opened,

    /// <summary>Picker was closed.</summary>
    Closed,

    /// <summary>Output mode changed.</summary>
    OutputModeChanged,

    /// <summary>Pipeline was modified.</summary>
    PipelineChanged,

    /// <summary>Pipeline building mode changed.</summary>
    PipelineModeChanged
}
