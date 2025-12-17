namespace Slipstream.Commands;

/// <summary>
/// Command to promote temp slot content to a numbered slot.
/// </summary>
public class PromoteTempSlotCommand : ICommand
{
    private readonly ICommandContext _context;

    public string Name => "PromoteTempSlot";
    public string Description => "Promote temp slot to numbered slot";

    public PromoteTempSlotCommand(ICommandContext context)
    {
        _context = context;
    }

    public bool CanExecute() => _context.SlotManager.TempSlot.HasContent;

    public void Execute()
    {
        if (!CanExecute()) return;

        var promotedIndex = _context.SlotManager.PromoteTempSlot(_context.StickyApps);
        Console.WriteLine($"[PromoteTempSlotCommand] Promoted to slot {promotedIndex}");
    }
}

/// <summary>
/// Command to clear all unlocked slots.
/// </summary>
public class ClearAllCommand : ICommand
{
    private readonly ICommandContext _context;

    public string Name => "ClearAll";
    public string Description => "Clear all unlocked slots";

    public ClearAllCommand(ICommandContext context)
    {
        _context = context;
    }

    public bool CanExecute() => true;

    public void Execute()
    {
        _context.SlotManager.ClearAllUnlocked();
    }
}

/// <summary>
/// Command to paste from the currently active slot.
/// </summary>
public class PasteFromActiveSlotCommand : ICommand
{
    private readonly ICommandContext _context;

    public string Name => "PasteFromActiveSlot";
    public string Description => "Paste from the currently active slot";

    public PasteFromActiveSlotCommand(ICommandContext context)
    {
        _context = context;
    }

    public bool CanExecute()
    {
        var activeSlot = _context.SlotManager.GetSlot(_context.SlotManager.ActiveSlotIndex);
        return activeSlot?.HasContent == true;
    }

    public void Execute()
    {
        var activeSlot = _context.SlotManager.GetSlot(_context.SlotManager.ActiveSlotIndex);
        if (activeSlot?.HasContent != true) return;

        Console.WriteLine($"[PasteFromActiveSlotCommand] Pasting from active slot {_context.SlotManager.ActiveSlotIndex}: Type={activeSlot.Type}");

        // Run paste on background thread to avoid blocking message pump
        var slotToPaste = activeSlot;
        Task.Run(() => _context.PasteEngine.PasteFromSlot(slotToPaste));
    }
}
