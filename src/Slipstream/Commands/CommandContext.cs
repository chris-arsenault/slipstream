using Slipstream.Processing;
using Slipstream.Services;
using Slipstream.UI;

namespace Slipstream.Commands;

/// <summary>
/// Default implementation of ICommandContext providing access to application services.
/// </summary>
public class CommandContext : ICommandContext
{
    public SlotManager SlotManager { get; }
    public ClipboardMonitor ClipboardMonitor { get; }
    public PasteEngine PasteEngine { get; }
    public KeyboardSequencer KeyboardSequencer { get; }
    public ProcessorToggleState ProcessorToggleState { get; }
    public ProcessorActivation ProcessorActivation { get; }
    public ProcessorRegistry ProcessorRegistry { get; }
    public HudWindow? HudWindow { get; set; }
    public HashSet<string> StickyApps { get; set; }

    public CommandContext(
        SlotManager slotManager,
        ClipboardMonitor clipboardMonitor,
        PasteEngine pasteEngine,
        KeyboardSequencer keyboardSequencer,
        ProcessorToggleState processorToggleState,
        ProcessorActivation processorActivation,
        ProcessorRegistry processorRegistry,
        HudWindow? hudWindow = null,
        HashSet<string>? stickyApps = null)
    {
        SlotManager = slotManager;
        ClipboardMonitor = clipboardMonitor;
        PasteEngine = pasteEngine;
        KeyboardSequencer = keyboardSequencer;
        ProcessorToggleState = processorToggleState;
        ProcessorActivation = processorActivation;
        ProcessorRegistry = processorRegistry;
        HudWindow = hudWindow;
        StickyApps = stickyApps ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}
