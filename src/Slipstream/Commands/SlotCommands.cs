namespace Slipstream.Commands;

/// <summary>
/// Command to copy the current selection to a specific slot.
/// Sends Ctrl+C and captures clipboard content to the target slot.
/// </summary>
public class CopyToSlotCommand : ISlotCommand
{
    private readonly ICommandContext _context;

    public int SlotIndex { get; }
    public string Name => $"CopyToSlot{SlotIndex + 1}";
    public string Description => $"Copy selection to slot {SlotIndex + 1}";

    public CopyToSlotCommand(ICommandContext context, int slotIndex)
    {
        _context = context;
        SlotIndex = slotIndex;
    }

    public bool CanExecute() => SlotIndex >= 0 && SlotIndex < _context.SlotManager.SlotCount;

    public void Execute()
    {
        if (!CanExecute()) return;

        // Set the target slot BEFORE sending Ctrl+C
        // The clipboard monitor will use this when WM_CLIPBOARDUPDATE arrives
        _context.ClipboardMonitor.SetPendingTargetSlot(SlotIndex);

        // Send Ctrl+C to copy selection
        _context.KeyboardSequencer.SendCopyWithModifierRelease();
    }
}

/// <summary>
/// Command to paste content from a specific slot.
/// Writes slot content to clipboard and sends Ctrl+V.
/// </summary>
public class PasteFromSlotCommand : ISlotCommand
{
    private readonly ICommandContext _context;

    public int SlotIndex { get; }
    public string Name => $"PasteFromSlot{SlotIndex + 1}";
    public string Description => $"Paste from slot {SlotIndex + 1}";

    public PasteFromSlotCommand(ICommandContext context, int slotIndex)
    {
        _context = context;
        SlotIndex = slotIndex;
    }

    public bool CanExecute()
    {
        if (SlotIndex < 0 || SlotIndex >= _context.SlotManager.SlotCount)
            return false;

        var slot = _context.SlotManager.GetSlot(SlotIndex);
        return slot?.HasContent == true;
    }

    public void Execute()
    {
        var slot = _context.SlotManager.GetSlot(SlotIndex);
        if (slot?.HasContent != true) return;

        Console.WriteLine($"[PasteFromSlotCommand] Pasting from slot {SlotIndex}: Type={slot.Type}");

        // Run paste on background thread to avoid blocking message pump
        var slotToPaste = slot;
        Task.Run(() => _context.PasteEngine.PasteFromSlot(slotToPaste));
    }
}

/// <summary>
/// Command to toggle the lock state of a specific slot.
/// </summary>
public class LockSlotCommand : ISlotCommand
{
    private readonly ICommandContext _context;

    public int SlotIndex { get; }
    public string Name => $"LockSlot{SlotIndex + 1}";
    public string Description => $"Toggle lock on slot {SlotIndex + 1}";

    public LockSlotCommand(ICommandContext context, int slotIndex)
    {
        _context = context;
        SlotIndex = slotIndex;
    }

    public bool CanExecute() => SlotIndex >= 0 && SlotIndex < _context.SlotManager.SlotCount;

    public void Execute()
    {
        if (!CanExecute()) return;
        _context.SlotManager.ToggleLock(SlotIndex);
    }
}

/// <summary>
/// Command to clear a specific slot's content.
/// </summary>
public class ClearSlotCommand : ISlotCommand
{
    private readonly ICommandContext _context;

    public int SlotIndex { get; }
    public string Name => $"ClearSlot{SlotIndex + 1}";
    public string Description => $"Clear slot {SlotIndex + 1}";

    public ClearSlotCommand(ICommandContext context, int slotIndex)
    {
        _context = context;
        SlotIndex = slotIndex;
    }

    public bool CanExecute()
    {
        if (SlotIndex < 0 || SlotIndex >= _context.SlotManager.SlotCount)
            return false;

        var slot = _context.SlotManager.GetSlot(SlotIndex);
        return slot != null && !slot.IsLocked;
    }

    public void Execute()
    {
        if (!CanExecute()) return;
        _context.SlotManager.ClearSlot(SlotIndex);
    }
}
