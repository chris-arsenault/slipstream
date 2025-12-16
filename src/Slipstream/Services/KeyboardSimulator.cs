using Slipstream.Native;

namespace Slipstream.Services;

/// <summary>
/// Production implementation using Win32 keybd_event API.
/// </summary>
public class KeyboardSimulator : IKeyboardSimulator
{
    public void KeyDown(byte virtualKey)
    {
        Win32.keybd_event(virtualKey, 0, 0, UIntPtr.Zero);
    }

    public void KeyUp(byte virtualKey)
    {
        Win32.keybd_event(virtualKey, 0, Win32.KEYEVENTF_KEYUP_EVENT, UIntPtr.Zero);
    }

    public void Sleep(int milliseconds)
    {
        Thread.Sleep(milliseconds);
    }
}

/// <summary>
/// Generates keyboard sequences for copy/paste operations.
/// Handles modifier key state management to work correctly when user is holding hotkey modifiers.
/// </summary>
public class KeyboardSequencer
{
    private readonly IKeyboardSimulator _simulator;

    // Virtual key codes
    public const byte VK_CONTROL = 0x11;
    public const byte VK_SHIFT = 0x10;
    public const byte VK_MENU = 0x12; // Alt
    public const byte VK_C = 0x43;
    public const byte VK_V = 0x56;

    public KeyboardSequencer(IKeyboardSimulator simulator)
    {
        _simulator = simulator;
    }

    /// <summary>
    /// Sends Ctrl+C to copy selection.
    /// Releases Ctrl+Alt modifiers first (user may be holding them from hotkey),
    /// sends clean Ctrl+C, then re-presses Ctrl+Alt to match physical key state.
    /// </summary>
    public void SendCopyWithModifierRelease()
    {
        // Release any held modifiers first (user may still be holding Ctrl+Alt from hotkey)
        _simulator.KeyUp(VK_CONTROL);
        _simulator.KeyUp(VK_MENU);
        _simulator.KeyUp(VK_SHIFT);

        _simulator.Sleep(10);

        // Now send clean Ctrl+C
        _simulator.KeyDown(VK_CONTROL);
        _simulator.KeyDown(VK_C);
        _simulator.KeyUp(VK_C);
        _simulator.KeyUp(VK_CONTROL);

        _simulator.Sleep(10);

        // Re-press Ctrl+Alt so they match user's physical key state
        _simulator.KeyDown(VK_CONTROL);
        _simulator.KeyDown(VK_MENU);
    }

    /// <summary>
    /// Sends Ctrl+V to paste.
    /// Releases Ctrl+Shift modifiers first (user may be holding them from hotkey),
    /// sends clean Ctrl+V, then re-presses Ctrl+Shift to match physical key state.
    /// </summary>
    public void SendPasteWithModifierRelease()
    {
        // Release any held modifiers first (user may still be holding Ctrl+Shift from hotkey)
        _simulator.KeyUp(VK_CONTROL);
        _simulator.KeyUp(VK_SHIFT);
        _simulator.KeyUp(VK_MENU);

        _simulator.Sleep(10);

        // Now send clean Ctrl+V
        _simulator.KeyDown(VK_CONTROL);
        _simulator.KeyDown(VK_V);
        _simulator.KeyUp(VK_V);
        _simulator.KeyUp(VK_CONTROL);

        _simulator.Sleep(10);

        // Re-press Ctrl+Shift so they match user's physical key state
        _simulator.KeyDown(VK_CONTROL);
        _simulator.KeyDown(VK_SHIFT);
    }
}
