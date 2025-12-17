namespace Slipstream.Commands;

/// <summary>
/// Base interface for all commands in the application.
/// Commands encapsulate actions that can be triggered by hotkeys, MIDI, or UI.
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Unique name identifying this command (e.g., "CopyToSlot1", "ToggleHud").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description of what this command does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Checks if the command can currently be executed.
    /// </summary>
    bool CanExecute();

    /// <summary>
    /// Executes the command.
    /// </summary>
    void Execute();
}

/// <summary>
/// Interface for commands that operate on a specific slot.
/// </summary>
public interface ISlotCommand : ICommand
{
    /// <summary>
    /// The 0-based slot index this command operates on.
    /// </summary>
    int SlotIndex { get; }
}

/// <summary>
/// Provides context for command execution (services, state).
/// </summary>
public interface ICommandContext
{
    Services.SlotManager SlotManager { get; }
    Services.ClipboardMonitor ClipboardMonitor { get; }
    Services.PasteEngine PasteEngine { get; }
    Services.KeyboardSequencer KeyboardSequencer { get; }
    UI.HudWindow? HudWindow { get; }
    HashSet<string> StickyApps { get; }
}
