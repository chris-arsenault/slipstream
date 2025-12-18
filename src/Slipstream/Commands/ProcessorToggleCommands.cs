using Slipstream.Processing;

namespace Slipstream.Commands;

/// <summary>
/// Command to toggle a processor on/off for all paste operations.
/// </summary>
public class ToggleProcessorCommand : ICommand
{
    private readonly ProcessorToggleState _toggleState;
    private readonly string _processorName;

    public string Name => $"ToggleProcessor{_processorName}";
    public string Description => $"Toggle {_processorName} processor on/off";

    public ToggleProcessorCommand(ProcessorToggleState toggleState, string processorName)
    {
        _toggleState = toggleState;
        _processorName = processorName;
    }

    public bool CanExecute() => ProcessorDefinitions.GetByName(_processorName) != null;

    public void Execute()
    {
        if (!CanExecute()) return;
        _toggleState.Toggle(_processorName);
    }
}

/// <summary>
/// Command to clear all processor toggles.
/// </summary>
public class ClearProcessorTogglesCommand : ICommand
{
    private readonly ProcessorToggleState _toggleState;

    public string Name => "ClearProcessorToggles";
    public string Description => "Clear all processor toggles";

    public ClearProcessorTogglesCommand(ProcessorToggleState toggleState)
    {
        _toggleState = toggleState;
    }

    public bool CanExecute() => true;

    public void Execute()
    {
        _toggleState.ClearAll();
    }
}
