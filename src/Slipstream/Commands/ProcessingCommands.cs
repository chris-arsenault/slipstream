using Slipstream.Processing;

namespace Slipstream.Commands;

/// <summary>
/// Command to process the content of a specific slot using a named processor.
/// </summary>
public class ProcessSlotCommand : ISlotCommand
{
    private readonly ICommandContext _context;
    private readonly ProcessorRegistry _processorRegistry;
    private readonly string _processorName;

    public int SlotIndex { get; }
    public string Name => $"Process{_processorName}Slot{SlotIndex + 1}";
    public string Description => $"Apply {_processorName} to slot {SlotIndex + 1}";

    public ProcessSlotCommand(ICommandContext context, ProcessorRegistry processorRegistry, int slotIndex, string processorName)
    {
        _context = context;
        _processorRegistry = processorRegistry;
        SlotIndex = slotIndex;
        _processorName = processorName;
    }

    public bool CanExecute()
    {
        if (SlotIndex < 0 || SlotIndex >= _context.SlotManager.SlotCount)
            return false;

        var slot = _context.SlotManager.GetSlot(SlotIndex);
        if (slot == null || !slot.HasContent || slot.IsLocked)
            return false;

        var content = slot.GetContent();
        if (content == null)
            return false;

        var processor = _processorRegistry.GetProcessor(_processorName);
        return processor?.CanProcess(content) == true;
    }

    public void Execute()
    {
        var slot = _context.SlotManager.GetSlot(SlotIndex);
        if (slot == null || !slot.HasContent)
            return;

        var content = slot.GetContent();
        if (content == null)
            return;

        var processed = _processorRegistry.Process(_processorName, content);
        if (processed != null)
        {
            slot.SetContent(processed);
            Console.WriteLine($"[ProcessSlotCommand] Processed slot {SlotIndex} with {_processorName}");
        }
    }
}

/// <summary>
/// Command to process the active slot's content using a named processor.
/// </summary>
public class ProcessActiveSlotCommand : ICommand
{
    private readonly ICommandContext _context;
    private readonly ProcessorRegistry _processorRegistry;
    private readonly string _processorName;

    public string Name => $"ProcessActive{_processorName}";
    public string Description => $"Apply {_processorName} to active slot";

    public ProcessActiveSlotCommand(ICommandContext context, ProcessorRegistry processorRegistry, string processorName)
    {
        _context = context;
        _processorRegistry = processorRegistry;
        _processorName = processorName;
    }

    public bool CanExecute()
    {
        var slot = _context.SlotManager.GetSlot(_context.SlotManager.ActiveSlotIndex);
        if (slot == null || !slot.HasContent || slot.IsLocked)
            return false;

        var content = slot.GetContent();
        if (content == null)
            return false;

        var processor = _processorRegistry.GetProcessor(_processorName);
        return processor?.CanProcess(content) == true;
    }

    public void Execute()
    {
        var slot = _context.SlotManager.GetSlot(_context.SlotManager.ActiveSlotIndex);
        if (slot == null || !slot.HasContent)
            return;

        var content = slot.GetContent();
        if (content == null)
            return;

        var processed = _processorRegistry.Process(_processorName, content);
        if (processed != null)
        {
            slot.SetContent(processed);
            Console.WriteLine($"[ProcessActiveSlotCommand] Processed active slot with {_processorName}");
        }
    }
}

/// <summary>
/// Command to process the temp slot's content using a named processor.
/// </summary>
public class ProcessTempSlotCommand : ICommand
{
    private readonly ICommandContext _context;
    private readonly ProcessorRegistry _processorRegistry;
    private readonly string _processorName;

    public string Name => $"ProcessTemp{_processorName}";
    public string Description => $"Apply {_processorName} to temp slot";

    public ProcessTempSlotCommand(ICommandContext context, ProcessorRegistry processorRegistry, string processorName)
    {
        _context = context;
        _processorRegistry = processorRegistry;
        _processorName = processorName;
    }

    public bool CanExecute()
    {
        var tempSlot = _context.SlotManager.TempSlot;
        if (!tempSlot.HasContent)
            return false;

        var content = tempSlot.GetContent();
        if (content == null)
            return false;

        var processor = _processorRegistry.GetProcessor(_processorName);
        return processor?.CanProcess(content) == true;
    }

    public void Execute()
    {
        var tempSlot = _context.SlotManager.TempSlot;
        if (!tempSlot.HasContent)
            return;

        var content = tempSlot.GetContent();
        if (content == null)
            return;

        var processed = _processorRegistry.Process(_processorName, content);
        if (processed != null)
        {
            tempSlot.SetContent(processed);
            Console.WriteLine($"[ProcessTempSlotCommand] Processed temp slot with {_processorName}");
        }
    }
}
