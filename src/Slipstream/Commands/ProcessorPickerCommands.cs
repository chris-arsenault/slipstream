using Slipstream.Native;
using Slipstream.Processing;
using Slipstream.Services.Keyboard;

namespace Slipstream.Commands;

/// <summary>
/// Command to open the processor picker for the active slot.
/// </summary>
public class OpenProcessorPickerCommand : ICommand
{
    private readonly ICommandContext _context;
    private readonly ProcessorPickerState _pickerState;
    private readonly ProcessorOutputMode _initialMode;

    public string Name => _initialMode == ProcessorOutputMode.Paste
        ? "OpenProcessorPickerPaste"
        : "OpenProcessorPicker";

    public string Description => _initialMode == ProcessorOutputMode.Paste
        ? "Open processor picker in Paste mode"
        : "Open processor picker for active slot";

    public OpenProcessorPickerCommand(
        ICommandContext context,
        ProcessorPickerState pickerState,
        ProcessorOutputMode initialMode = ProcessorOutputMode.Replace)
    {
        _context = context;
        _pickerState = pickerState;
        _initialMode = initialMode;
    }

    public bool CanExecute()
    {
        var slot = _context.SlotManager.GetSlot(_context.SlotManager.ActiveSlotIndex);
        return slot?.HasContent == true;
    }

    public void Execute()
    {
        var slotIndex = _context.SlotManager.ActiveSlotIndex;
        _pickerState.Open(slotIndex, _initialMode);
    }
}

/// <summary>
/// Command to close the processor picker without applying.
/// </summary>
public class CloseProcessorPickerCommand : ICommand
{
    private readonly ProcessorPickerState _pickerState;

    public string Name => "CloseProcessorPicker";
    public string Description => "Close processor picker";

    public CloseProcessorPickerCommand(ProcessorPickerState pickerState)
    {
        _pickerState = pickerState;
    }

    public bool CanExecute() => _pickerState.IsOpen;

    public void Execute()
    {
        _pickerState.Close();
    }
}

/// <summary>
/// Command to apply a processor by index (1-9) when the picker is open.
/// </summary>
public class ApplyProcessorCommand : ICommand
{
    private readonly ICommandContext _context;
    private readonly ProcessorPickerState _pickerState;
    private readonly ProcessorRegistry _processorRegistry;
    private readonly int _processorIndex; // 1-based index

    public string Name => $"ApplyProcessor{_processorIndex}";
    public string Description => $"Apply processor {_processorIndex} from picker";

    public ApplyProcessorCommand(
        ICommandContext context,
        ProcessorPickerState pickerState,
        ProcessorRegistry processorRegistry,
        int processorIndex)
    {
        _context = context;
        _pickerState = pickerState;
        _processorRegistry = processorRegistry;
        _processorIndex = processorIndex;
    }

    public bool CanExecute() => _pickerState.IsOpen;

    public void Execute()
    {
        if (!_pickerState.IsOpen)
            return;

        // Get available processors for the target slot's content
        var slot = GetTargetSlot();
        if (slot == null || !slot.HasContent)
            return;

        var content = slot.GetContent();
        if (content == null)
            return;

        var availableProcessors = _processorRegistry.GetProcessorsFor(content).ToList();
        if (_processorIndex < 1 || _processorIndex > availableProcessors.Count)
            return;

        var processor = availableProcessors[_processorIndex - 1];

        // Check if Shift is currently held - if so, add to pipeline instead of applying immediately
        bool isShiftHeld =
            Win32.IsKeyPhysicallyDown(VirtualKeys.Shift) ||
            Win32.IsKeyPhysicallyDown(VirtualKeys.LeftShift) ||
            Win32.IsKeyPhysicallyDown(VirtualKeys.RightShift);
        if (isShiftHeld || _pickerState.IsBuildingPipeline)
        {
            // Update building pipeline state based on Shift key
            if (isShiftHeld && !_pickerState.IsBuildingPipeline)
            {
                _pickerState.IsBuildingPipeline = true;
            }

            // Add to pipeline and stay open
            _pickerState.AddToPipeline(processor.Name);
        }
        else
        {
            // Apply immediately and close
            ApplyProcessor(processor.Name, content, slot);
            _pickerState.Close();
        }
    }

    private Models.ClipboardSlot? GetTargetSlot()
    {
        if (_pickerState.TargetSlotIndex == Services.SlotManager.TempSlotIndex)
            return _context.SlotManager.TempSlot;

        return _context.SlotManager.GetSlot(_pickerState.TargetSlotIndex);
    }

    private void ApplyProcessor(string processorName, Content.IClipboardContent content, Models.ClipboardSlot slot)
    {
        var processed = _processorRegistry.Process(processorName, content);
        if (processed == null)
            return;

        HandleOutput(processed, slot);
    }

    private void HandleOutput(Content.IClipboardContent processed, Models.ClipboardSlot slot)
    {
        switch (_pickerState.OutputMode)
        {
            case ProcessorOutputMode.Replace:
                slot.SetContent(processed);
                Console.WriteLine($"[ApplyProcessorCommand] Replaced slot content");
                break;

            case ProcessorOutputMode.Paste:
                // TODO: Paste to active window
                PasteContent(processed);
                break;

            case ProcessorOutputMode.NewSlot:
                // Store in next available slot
                StoreInNewSlot(processed);
                break;
        }
    }

    private void PasteContent(Content.IClipboardContent content)
    {
        // Copy to system clipboard and paste
        try
        {
            content.PopulateSlot(_context.SlotManager.TempSlot);
            _context.PasteEngine.PasteFromSlot(_context.SlotManager.TempSlot);
            Console.WriteLine("[ApplyProcessorCommand] Pasted processed content");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ApplyProcessorCommand] Paste failed: {ex.Message}");
        }
    }

    private void StoreInNewSlot(Content.IClipboardContent content)
    {
        // Find next unlocked slot
        for (int i = 0; i < _context.SlotManager.SlotCount; i++)
        {
            var targetSlot = _context.SlotManager.GetSlot(i);
            if (targetSlot != null && !targetSlot.IsLocked)
            {
                targetSlot.SetContent(content);
                Console.WriteLine($"[ApplyProcessorCommand] Stored in slot {i}");
                return;
            }
        }
        Console.WriteLine("[ApplyProcessorCommand] No available slot for new content");
    }
}

/// <summary>
/// Command to execute the current pipeline in the picker.
/// </summary>
public class ExecutePipelineCommand : ICommand
{
    private readonly ICommandContext _context;
    private readonly ProcessorPickerState _pickerState;
    private readonly ProcessorRegistry _processorRegistry;

    public string Name => "ExecutePipeline";
    public string Description => "Execute the processor pipeline";

    public ExecutePipelineCommand(
        ICommandContext context,
        ProcessorPickerState pickerState,
        ProcessorRegistry processorRegistry)
    {
        _context = context;
        _pickerState = pickerState;
        _processorRegistry = processorRegistry;
    }

    public bool CanExecute() => _pickerState.IsOpen && !_pickerState.Pipeline.IsEmpty;

    public void Execute()
    {
        if (!CanExecute())
            return;

        var slot = GetTargetSlot();
        if (slot == null || !slot.HasContent)
            return;

        var content = slot.GetContent();
        if (content == null)
            return;

        var processed = _processorRegistry.ExecutePipeline(_pickerState.Pipeline, content);
        if (processed != null)
        {
            HandleOutput(processed, slot);
            Console.WriteLine($"[ExecutePipelineCommand] Executed pipeline: {_pickerState.Pipeline}");
        }

        _pickerState.Close();
    }

    private Models.ClipboardSlot? GetTargetSlot()
    {
        if (_pickerState.TargetSlotIndex == Services.SlotManager.TempSlotIndex)
            return _context.SlotManager.TempSlot;

        return _context.SlotManager.GetSlot(_pickerState.TargetSlotIndex);
    }

    private void HandleOutput(Content.IClipboardContent processed, Models.ClipboardSlot slot)
    {
        switch (_pickerState.OutputMode)
        {
            case ProcessorOutputMode.Replace:
                slot.SetContent(processed);
                break;

            case ProcessorOutputMode.Paste:
                PasteContent(processed);
                break;

            case ProcessorOutputMode.NewSlot:
                StoreInNewSlot(processed);
                break;
        }
    }

    private void PasteContent(Content.IClipboardContent content)
    {
        try
        {
            content.PopulateSlot(_context.SlotManager.TempSlot);
            _context.PasteEngine.PasteFromSlot(_context.SlotManager.TempSlot);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ExecutePipelineCommand] Paste failed: {ex.Message}");
        }
    }

    private void StoreInNewSlot(Content.IClipboardContent content)
    {
        for (int i = 0; i < _context.SlotManager.SlotCount; i++)
        {
            var targetSlot = _context.SlotManager.GetSlot(i);
            if (targetSlot != null && !targetSlot.IsLocked)
            {
                targetSlot.SetContent(content);
                return;
            }
        }
    }
}

/// <summary>
/// Command to remove the last processor from the pipeline.
/// </summary>
public class RemoveLastFromPipelineCommand : ICommand
{
    private readonly ProcessorPickerState _pickerState;

    public string Name => "RemoveLastFromPipeline";
    public string Description => "Remove the last processor from the pipeline";

    public RemoveLastFromPipelineCommand(ProcessorPickerState pickerState)
    {
        _pickerState = pickerState;
    }

    public bool CanExecute() => _pickerState.IsOpen && !_pickerState.Pipeline.IsEmpty;

    public void Execute()
    {
        _pickerState.RemoveLastFromPipeline();
    }
}

/// <summary>
/// Command to cycle the output mode in the picker.
/// </summary>
public class CycleOutputModeCommand : ICommand
{
    private readonly ProcessorPickerState _pickerState;

    public string Name => "CycleOutputMode";
    public string Description => "Cycle processor output mode";

    public CycleOutputModeCommand(ProcessorPickerState pickerState)
    {
        _pickerState = pickerState;
    }

    public bool CanExecute() => _pickerState.IsOpen;

    public void Execute()
    {
        _pickerState.CycleOutputMode();
    }
}

/// <summary>
/// Command to set the pipeline building mode (when Shift is held).
/// </summary>
public class SetPipelineModeCommand : ICommand
{
    private readonly ProcessorPickerState _pickerState;
    private readonly bool _enabled;

    public string Name => _enabled ? "EnablePipelineMode" : "DisablePipelineMode";
    public string Description => _enabled ? "Enable pipeline building mode" : "Disable pipeline building mode";

    public SetPipelineModeCommand(ProcessorPickerState pickerState, bool enabled)
    {
        _pickerState = pickerState;
        _enabled = enabled;
    }

    public bool CanExecute() => _pickerState.IsOpen;

    public void Execute()
    {
        _pickerState.IsBuildingPipeline = _enabled;
    }
}
