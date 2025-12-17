namespace Slipstream.Commands;

/// <summary>
/// Command to cycle through slots in a given direction.
/// </summary>
public class CycleSlotCommand : ICommand
{
    private readonly ICommandContext _context;
    private readonly int _direction;

    public string Name => _direction > 0 ? "CycleForward" : "CycleBackward";
    public string Description => _direction > 0 ? "Cycle to next slot" : "Cycle to previous slot";

    public CycleSlotCommand(ICommandContext context, int direction)
    {
        _context = context;
        _direction = direction;
    }

    public bool CanExecute() => _context.SlotManager.SlotCount > 0;

    public void Execute()
    {
        if (!CanExecute()) return;
        _context.SlotManager.CycleActiveSlot(_direction);
    }
}

/// <summary>
/// Command to set a specific slot as active.
/// </summary>
public class SetActiveSlotCommand : ISlotCommand
{
    private readonly ICommandContext _context;

    public int SlotIndex { get; }
    public string Name => $"SetActiveSlot{SlotIndex + 1}";
    public string Description => $"Set slot {SlotIndex + 1} as active";

    public SetActiveSlotCommand(ICommandContext context, int slotIndex)
    {
        _context = context;
        SlotIndex = slotIndex;
    }

    public bool CanExecute() => SlotIndex >= 0 && SlotIndex < _context.SlotManager.SlotCount;

    public void Execute()
    {
        if (!CanExecute()) return;
        _context.SlotManager.SetActiveSlot(SlotIndex);
    }
}
