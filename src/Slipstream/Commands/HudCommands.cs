using System.Windows;

namespace Slipstream.Commands;

/// <summary>
/// Command to toggle HUD visibility.
/// </summary>
public class ToggleHudCommand : ICommand
{
    private readonly ICommandContext _context;

    public string Name => "ToggleHud";
    public string Description => "Toggle HUD visibility";

    public ToggleHudCommand(ICommandContext context)
    {
        _context = context;
    }

    public bool CanExecute() => _context.HudWindow != null;

    public void Execute()
    {
        if (_context.HudWindow == null) return;

        // Must run on UI thread since we're accessing Window properties
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_context.HudWindow.IsVisible)
                _context.HudWindow.Hide();
            else
                _context.HudWindow.Show();
        });
    }
}
