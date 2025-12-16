using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;

namespace Slipstream.UI;

public class TrayManager : IDisposable
{
    private readonly TaskbarIcon _trayIcon;
    private readonly Action _onShowHud;
    private readonly Action _onHideHud;
    private readonly Action _onOpenSettings;
    private readonly Action _onQuit;

    private MenuItem? _hudMenuItem;
    private bool _isHudVisible = false;

    public TrayManager(
        Action onShowHud,
        Action onHideHud,
        Action onOpenSettings,
        Action onQuit)
    {
        _onShowHud = onShowHud;
        _onHideHud = onHideHud;
        _onOpenSettings = onOpenSettings;
        _onQuit = onQuit;

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Slipstream - Clipboard Manager",
            Icon = CreateDefaultIcon(),
            ContextMenu = CreateContextMenu()
        };

        // Single-click toggles HUD visibility
        _trayIcon.TrayLeftMouseUp += (_, _) => ToggleHud();
    }

    private void ToggleHud()
    {
        if (_isHudVisible)
            _onHideHud();
        else
            _onShowHud();

        _isHudVisible = !_isHudVisible;
        UpdateHudMenuItem();
    }

    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu();

        _hudMenuItem = new MenuItem
        {
            Header = "Show HUD"
        };
        _hudMenuItem.Click += (_, _) => ToggleHud();
        menu.Items.Add(_hudMenuItem);

        menu.Items.Add(new Separator());

        var settingsItem = new MenuItem { Header = "Settings..." };
        settingsItem.Click += (_, _) => _onOpenSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new Separator());

        var quitItem = new MenuItem { Header = "Quit" };
        quitItem.Click += (_, _) => _onQuit();
        menu.Items.Add(quitItem);

        return menu;
    }

    public void UpdateHudVisibility(bool isVisible)
    {
        _isHudVisible = isVisible;
        UpdateHudMenuItem();
    }

    private void UpdateHudMenuItem()
    {
        if (_hudMenuItem != null)
        {
            _hudMenuItem.Header = _isHudVisible ? "Hide HUD" : "Show HUD";
        }
    }

    public void ShowBalloon(string title, string message, BalloonIcon icon = BalloonIcon.Info)
    {
        _trayIcon.ShowBalloonTip(title, message, icon);
    }

    private static Icon CreateDefaultIcon()
    {
        // Create a simple programmatic icon (blue square with S)
        using var bitmap = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bitmap);

        g.Clear(Color.FromArgb(70, 130, 180)); // Steel blue
        g.FillRectangle(new SolidBrush(Color.FromArgb(70, 130, 180)), 0, 0, 16, 16);

        using var font = new Font("Segoe UI", 9, System.Drawing.FontStyle.Bold);
        using var brush = new SolidBrush(Color.White);
        g.DrawString("S", font, brush, 2, 0);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        _trayIcon.Dispose();
    }
}
