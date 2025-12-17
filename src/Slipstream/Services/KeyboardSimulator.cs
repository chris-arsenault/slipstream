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

    public bool IsKeyPhysicallyDown(byte virtualKey)
    {
        return Win32.IsKeyPhysicallyDown(virtualKey);
    }
}

/// <summary>
/// Generates keyboard sequences for copy/paste operations.
/// Handles modifier key state management to work correctly when user is holding hotkey modifiers.
/// </summary>
public class KeyboardSequencer
{
    private readonly IKeyboardSimulator _simulator;

    // Virtual key codes - generic (used for simulation)
    public const byte VK_CONTROL = 0x11;
    public const byte VK_SHIFT = 0x10;
    public const byte VK_MENU = 0x12; // Alt
    public const byte VK_C = 0x43;
    public const byte VK_V = 0x56;

    // Virtual key codes - left/right specific (for physical state detection)
    public const byte VK_LCONTROL = 0xA2;
    public const byte VK_RCONTROL = 0xA3;
    public const byte VK_LSHIFT = 0xA0;
    public const byte VK_RSHIFT = 0xA1;
    public const byte VK_LMENU = 0xA4; // Left Alt
    public const byte VK_RMENU = 0xA5; // Right Alt

    public KeyboardSequencer(IKeyboardSimulator simulator)
    {
        _simulator = simulator;
    }

    /// <summary>
    /// Sends Ctrl+C to copy selection.
    /// Releases Ctrl+Alt modifiers first (user may be holding them from hotkey),
    /// sends clean Ctrl+C, then re-presses Ctrl+Alt (the copy hotkey modifiers).
    /// </summary>
    public void SendCopyWithModifierRelease()
    {
        // Release any held modifiers first - both generic and left/right specific
        ReleaseAllModifiers();

        _simulator.Sleep(5);

        // Now send clean Ctrl+C
        _simulator.KeyDown(VK_CONTROL);
        _simulator.KeyDown(VK_C);
        _simulator.KeyUp(VK_C);
        _simulator.KeyUp(VK_CONTROL);

        // Re-press the modifiers that were used for the copy hotkey (Ctrl+Alt)
        // We always do this because GetAsyncKeyState can't reliably detect physical state
        // after we've sent synthetic key-up events
        _simulator.KeyDown(VK_CONTROL);
        _simulator.KeyDown(VK_MENU);
    }

    /// <summary>
    /// Sends Ctrl+V to paste.
    /// Releases modifiers first (user may be holding them from hotkey),
    /// sends clean Ctrl+V, then re-presses the specified hotkey modifiers.
    /// </summary>
    /// <param name="hotkeyHasShift">Whether the hotkey that triggered this paste included Shift</param>
    /// <param name="hotkeyHasAlt">Whether the hotkey that triggered this paste included Alt</param>
    public void SendPasteWithModifierRelease(bool hotkeyHasShift = true, bool hotkeyHasAlt = false)
    {
        // Release any held modifiers first - both generic and left/right specific
        ReleaseAllModifiers();

        _simulator.Sleep(5);

        // Now send clean Ctrl+V
        _simulator.KeyDown(VK_CONTROL);
        _simulator.KeyDown(VK_V);
        _simulator.KeyUp(VK_V);
        _simulator.KeyUp(VK_CONTROL);

        // Re-press the modifiers that were used for the paste hotkey
        // We always do this because GetAsyncKeyState can't reliably detect physical state
        // after we've sent synthetic key-up events
        _simulator.KeyDown(VK_CONTROL);
        if (hotkeyHasShift)
            _simulator.KeyDown(VK_SHIFT);
        if (hotkeyHasAlt)
            _simulator.KeyDown(VK_MENU);
    }

    /// <summary>
    /// Checks if either left or right variant of Control is physically held.
    /// </summary>
    private bool IsControlPhysicallyDown()
    {
        return _simulator.IsKeyPhysicallyDown(VK_LCONTROL) ||
               _simulator.IsKeyPhysicallyDown(VK_RCONTROL) ||
               _simulator.IsKeyPhysicallyDown(VK_CONTROL);
    }

    /// <summary>
    /// Checks if either left or right variant of Shift is physically held.
    /// </summary>
    private bool IsShiftPhysicallyDown()
    {
        return _simulator.IsKeyPhysicallyDown(VK_LSHIFT) ||
               _simulator.IsKeyPhysicallyDown(VK_RSHIFT) ||
               _simulator.IsKeyPhysicallyDown(VK_SHIFT);
    }

    /// <summary>
    /// Checks if either left or right variant of Alt is physically held.
    /// </summary>
    private bool IsAltPhysicallyDown()
    {
        return _simulator.IsKeyPhysicallyDown(VK_LMENU) ||
               _simulator.IsKeyPhysicallyDown(VK_RMENU) ||
               _simulator.IsKeyPhysicallyDown(VK_MENU);
    }

    /// <summary>
    /// Re-presses modifier keys that are still physically held by the user.
    /// This allows chaining operations (e.g., Ctrl+Shift+1 then Ctrl+Shift+2).
    /// </summary>
    private void RestorePhysicallyHeldModifiers()
    {
        if (IsControlPhysicallyDown())
            _simulator.KeyDown(VK_CONTROL);

        if (IsShiftPhysicallyDown())
            _simulator.KeyDown(VK_SHIFT);

        if (IsAltPhysicallyDown())
            _simulator.KeyDown(VK_MENU);
    }

    /// <summary>
    /// Releases all modifier keys unconditionally (both generic and left/right variants).
    /// Call this on app shutdown or when modifiers appear stuck.
    /// </summary>
    public void ReleaseAllModifiers()
    {
        // Release generic keys
        _simulator.KeyUp(VK_CONTROL);
        _simulator.KeyUp(VK_SHIFT);
        _simulator.KeyUp(VK_MENU);

        // Release left variants
        _simulator.KeyUp(VK_LCONTROL);
        _simulator.KeyUp(VK_LSHIFT);
        _simulator.KeyUp(VK_LMENU);

        // Release right variants
        _simulator.KeyUp(VK_RCONTROL);
        _simulator.KeyUp(VK_RSHIFT);
        _simulator.KeyUp(VK_RMENU);
    }

    /// <summary>
    /// Releases any modifier keys that aren't physically held down.
    /// Safe to call periodically to clean up stuck keys.
    /// </summary>
    public void CleanupStuckModifiers()
    {
        if (!IsControlPhysicallyDown())
        {
            _simulator.KeyUp(VK_CONTROL);
            _simulator.KeyUp(VK_LCONTROL);
            _simulator.KeyUp(VK_RCONTROL);
        }

        if (!IsShiftPhysicallyDown())
        {
            _simulator.KeyUp(VK_SHIFT);
            _simulator.KeyUp(VK_LSHIFT);
            _simulator.KeyUp(VK_RSHIFT);
        }

        if (!IsAltPhysicallyDown())
        {
            _simulator.KeyUp(VK_MENU);
            _simulator.KeyUp(VK_LMENU);
            _simulator.KeyUp(VK_RMENU);
        }
    }
}
