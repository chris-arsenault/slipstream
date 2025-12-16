using System.Windows;
using System.Windows.Interop;

namespace Slipstream.UI;

/// <summary>
/// A hidden window used solely for receiving Windows messages (hotkeys, clipboard notifications).
/// This window is never shown but maintains an active message pump.
/// </summary>
public class MessageWindow : Window
{
    private readonly HwndSource _hwndSource;

    public MessageWindow()
    {
        // Make window tiny and off-screen, but NOT Visibility.Hidden
        // (Hidden windows don't pump messages properly)
        Width = 1;
        Height = 1;
        Left = -10000;
        Top = -10000;
        WindowStyle = WindowStyle.None;
        ShowInTaskbar = false;
        ShowActivated = false;
        AllowsTransparency = true;
        Opacity = 0;

        // Ensure handle is created immediately
        var helper = new WindowInteropHelper(this);
        helper.EnsureHandle();

        _hwndSource = HwndSource.FromHwnd(helper.Handle)
            ?? throw new InvalidOperationException("Failed to get HwndSource");
    }

    public IntPtr Hwnd => new WindowInteropHelper(this).Handle;

    public HwndSource HwndSource => _hwndSource;
}
