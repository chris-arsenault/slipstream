using System.Text.RegularExpressions;
using Slipstream.Processing;

namespace Slipstream.Commands;

/// <summary>
/// Central registry for all commands. Provides parsing of action names to commands
/// and registration of custom command factories for extensibility.
/// </summary>
public partial class CommandRegistry
{
    private readonly ICommandContext _context;
    private readonly ProcessorRegistry _processorRegistry;
    private readonly Dictionary<string, Func<ICommand>> _exactCommands = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Prefix, Func<string, int, ICommand?> Factory)> _slotCommandFactories = new();

    // Regex patterns for parsing slot commands
    [GeneratedRegex(@"^(.+?)(?:Numpad)?(\d+)$", RegexOptions.Compiled)]
    private static partial Regex SlotCommandPattern();

    // Regex for processor commands: ProcessActive{ProcessorName} or Process{ProcessorName}Slot{N}
    [GeneratedRegex(@"^ProcessActive(.+)$", RegexOptions.Compiled)]
    private static partial Regex ProcessActivePattern();

    [GeneratedRegex(@"^ProcessTemp(.+)$", RegexOptions.Compiled)]
    private static partial Regex ProcessTempPattern();

    [GeneratedRegex(@"^Process(.+?)Slot(\d+)$", RegexOptions.Compiled)]
    private static partial Regex ProcessSlotPattern();

    public CommandRegistry(ICommandContext context, ProcessorRegistry? processorRegistry = null)
    {
        _context = context;
        _processorRegistry = processorRegistry ?? new ProcessorRegistry();
        RegisterBuiltInCommands();
    }

    /// <summary>
    /// Gets the processor registry for accessing available processors.
    /// </summary>
    public ProcessorRegistry ProcessorRegistry => _processorRegistry;

    private void RegisterBuiltInCommands()
    {
        // Register exact-match commands (no slot index)
        RegisterExact("ToggleHud", () => new ToggleHudCommand(_context));
        RegisterExact("CycleForward", () => new CycleSlotCommand(_context, 1));
        RegisterExact("CycleBackward", () => new CycleSlotCommand(_context, -1));
        RegisterExact("ClearAll", () => new ClearAllCommand(_context));
        RegisterExact("PromoteTempSlot", () => new PromoteTempSlotCommand(_context));
        RegisterExact("PasteFromActiveSlot", () => new PasteFromActiveSlotCommand(_context));

        // Register slot-based command factories
        RegisterSlotFactory("CopyToSlot", (name, slot) => new CopyToSlotCommand(_context, slot));
        RegisterSlotFactory("PasteFromSlot", (name, slot) => new PasteFromSlotCommand(_context, slot));
        RegisterSlotFactory("LockSlot", (name, slot) => new LockSlotCommand(_context, slot));
        RegisterSlotFactory("ClearSlot", (name, slot) => new ClearSlotCommand(_context, slot));
    }

    /// <summary>
    /// Registers a command with an exact name match.
    /// </summary>
    public void RegisterExact(string name, Func<ICommand> factory)
    {
        _exactCommands[name] = factory;
    }

    /// <summary>
    /// Registers a factory for slot-based commands (e.g., "CopyToSlot1", "PasteFromSlotNumpad5").
    /// </summary>
    public void RegisterSlotFactory(string prefix, Func<string, int, ICommand?> factory)
    {
        _slotCommandFactories.Add((prefix, factory));
    }

    /// <summary>
    /// Creates a command from an action name string.
    /// Handles exact matches (e.g., "ToggleHud") and parameterized slot commands (e.g., "CopyToSlot1", "PasteFromSlotNumpad5").
    /// Also handles processor commands (e.g., "ProcessActiveGrayscale", "ProcessUppercaseSlot1").
    /// </summary>
    /// <param name="actionName">The action name to parse (e.g., "CopyToSlot1", "ToggleHud")</param>
    /// <returns>The command instance, or null if the action name is not recognized</returns>
    public ICommand? CreateCommand(string actionName)
    {
        if (string.IsNullOrEmpty(actionName))
            return null;

        // Try exact match first
        if (_exactCommands.TryGetValue(actionName, out var factory))
        {
            return factory();
        }

        // Try processor commands (e.g., "ProcessActiveGrayscale", "ProcessTempUppercase", "ProcessGrayscaleSlot1")
        var processActiveMatch = ProcessActivePattern().Match(actionName);
        if (processActiveMatch.Success)
        {
            var processorName = processActiveMatch.Groups[1].Value;
            if (_processorRegistry.GetProcessor(processorName) != null)
            {
                return new ProcessActiveSlotCommand(_context, _processorRegistry, processorName);
            }
        }

        var processTempMatch = ProcessTempPattern().Match(actionName);
        if (processTempMatch.Success)
        {
            var processorName = processTempMatch.Groups[1].Value;
            if (_processorRegistry.GetProcessor(processorName) != null)
            {
                return new ProcessTempSlotCommand(_context, _processorRegistry, processorName);
            }
        }

        var processSlotMatch = ProcessSlotPattern().Match(actionName);
        if (processSlotMatch.Success)
        {
            var processorName = processSlotMatch.Groups[1].Value;
            if (int.TryParse(processSlotMatch.Groups[2].Value, out int slotNumber))
            {
                int slotIndex = slotNumber - 1;
                if (_processorRegistry.GetProcessor(processorName) != null)
                {
                    return new ProcessSlotCommand(_context, _processorRegistry, slotIndex, processorName);
                }
            }
        }

        // Try slot-based commands (e.g., "CopyToSlot1", "CopyToSlotNumpad1")
        var match = SlotCommandPattern().Match(actionName);
        if (match.Success)
        {
            string prefix = match.Groups[1].Value;
            if (int.TryParse(match.Groups[2].Value, out int slotNumber))
            {
                int slotIndex = slotNumber - 1; // Convert 1-based to 0-based

                foreach (var (factoryPrefix, slotFactory) in _slotCommandFactories)
                {
                    if (prefix.Equals(factoryPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return slotFactory(actionName, slotIndex);
                    }
                }
            }
        }

        Console.WriteLine($"[CommandRegistry] Unknown action: {actionName}");
        return null;
    }

    /// <summary>
    /// Executes a command by action name. Returns true if the command was found and executed.
    /// </summary>
    public bool Execute(string actionName)
    {
        var command = CreateCommand(actionName);
        if (command == null)
            return false;

        if (command.CanExecute())
        {
            command.Execute();
            return true;
        }

        Console.WriteLine($"[CommandRegistry] Command cannot execute: {actionName}");
        return false;
    }

    /// <summary>
    /// Gets all registered command names (exact matches only).
    /// </summary>
    public IEnumerable<string> GetExactCommandNames() => _exactCommands.Keys;

    /// <summary>
    /// Gets all registered slot command prefixes.
    /// </summary>
    public IEnumerable<string> GetSlotCommandPrefixes() => _slotCommandFactories.Select(f => f.Prefix);
}
