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

    public byte[]? GetKeyboardState()
    {
        var state = new byte[256];
        return Win32.GetKeyboardState(state) ? state : null;
    }

    public void SetKeyboardState(byte[] state)
    {
        Win32.SetKeyboardState(state);
    }
}

/// <summary>
/// Generates keyboard sequences for copy/paste operations.
/// Uses snapshot/restore approach: captures modifier state before operation,
/// clears modifiers, performs operation, then restores original state.
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

    // Virtual key codes - left/right specific (for physical state detection and release)
    public const byte VK_LCONTROL = 0xA2;
    public const byte VK_RCONTROL = 0xA3;
    public const byte VK_LSHIFT = 0xA0;
    public const byte VK_RSHIFT = 0xA1;
    public const byte VK_LMENU = 0xA4; // Left Alt
    public const byte VK_RMENU = 0xA5; // Right Alt

    // Modifier key indices for keyboard state array (high bit = key down)
    private static readonly byte[] ModifierKeys = { VK_CONTROL, VK_SHIFT, VK_MENU, VK_LCONTROL, VK_RCONTROL, VK_LSHIFT, VK_RSHIFT, VK_LMENU, VK_RMENU };

    public KeyboardSequencer(IKeyboardSimulator simulator)
    {
        _simulator = simulator;
    }

    /// <summary>
    /// Sends Ctrl+C to copy selection.
    /// Snapshots current modifier state, clears modifiers, sends Ctrl+C, restores state.
    /// </summary>
    public void SendCopyWithModifierRelease()
    {
        // Snapshot which modifiers are physically held
        var heldModifiers = SnapshotHeldModifiers();

        // Release all modifiers
        ReleaseAllModifiers();
        _simulator.Sleep(5);

        // Send clean Ctrl+C
        _simulator.KeyDown(VK_CONTROL);
        _simulator.KeyDown(VK_C);
        _simulator.KeyUp(VK_C);
        _simulator.KeyUp(VK_CONTROL);

        // Restore modifiers that were physically held
        RestoreModifiers(heldModifiers);
    }

    /// <summary>
    /// Sends Ctrl+V to paste.
    /// Snapshots current modifier state, clears modifiers, sends Ctrl+V, restores state.
    /// </summary>
    public void SendPasteWithModifierRelease()
    {
        // Snapshot which modifiers are physically held
        var heldModifiers = SnapshotHeldModifiers();

        // Release all modifiers
        ReleaseAllModifiers();
        _simulator.Sleep(5);

        // Send clean Ctrl+V
        _simulator.KeyDown(VK_CONTROL);
        _simulator.KeyDown(VK_V);
        _simulator.KeyUp(VK_V);
        _simulator.KeyUp(VK_CONTROL);

        // Restore modifiers that were physically held
        RestoreModifiers(heldModifiers);
    }

    /// <summary>
    /// Captures which modifier keys are currently physically held down.
    /// </summary>
    private ModifierSnapshot SnapshotHeldModifiers()
    {
        return new ModifierSnapshot
        {
            Control = _simulator.IsKeyPhysicallyDown(VK_LCONTROL) || _simulator.IsKeyPhysicallyDown(VK_RCONTROL) || _simulator.IsKeyPhysicallyDown(VK_CONTROL),
            Shift = _simulator.IsKeyPhysicallyDown(VK_LSHIFT) || _simulator.IsKeyPhysicallyDown(VK_RSHIFT) || _simulator.IsKeyPhysicallyDown(VK_SHIFT),
            Alt = _simulator.IsKeyPhysicallyDown(VK_LMENU) || _simulator.IsKeyPhysicallyDown(VK_RMENU) || _simulator.IsKeyPhysicallyDown(VK_MENU)
        };
    }

    /// <summary>
    /// Re-presses modifiers that were held before the operation.
    /// </summary>
    private void RestoreModifiers(ModifierSnapshot snapshot)
    {
        if (snapshot.Control)
            _simulator.KeyDown(VK_CONTROL);

        if (snapshot.Shift)
            _simulator.KeyDown(VK_SHIFT);

        if (snapshot.Alt)
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
        bool ctrlHeld = _simulator.IsKeyPhysicallyDown(VK_LCONTROL) || _simulator.IsKeyPhysicallyDown(VK_RCONTROL) || _simulator.IsKeyPhysicallyDown(VK_CONTROL);
        bool shiftHeld = _simulator.IsKeyPhysicallyDown(VK_LSHIFT) || _simulator.IsKeyPhysicallyDown(VK_RSHIFT) || _simulator.IsKeyPhysicallyDown(VK_SHIFT);
        bool altHeld = _simulator.IsKeyPhysicallyDown(VK_LMENU) || _simulator.IsKeyPhysicallyDown(VK_RMENU) || _simulator.IsKeyPhysicallyDown(VK_MENU);

        if (!ctrlHeld)
        {
            _simulator.KeyUp(VK_CONTROL);
            _simulator.KeyUp(VK_LCONTROL);
            _simulator.KeyUp(VK_RCONTROL);
        }

        if (!shiftHeld)
        {
            _simulator.KeyUp(VK_SHIFT);
            _simulator.KeyUp(VK_LSHIFT);
            _simulator.KeyUp(VK_RSHIFT);
        }

        if (!altHeld)
        {
            _simulator.KeyUp(VK_MENU);
            _simulator.KeyUp(VK_LMENU);
            _simulator.KeyUp(VK_RMENU);
        }
    }

    private struct ModifierSnapshot
    {
        public bool Control;
        public bool Shift;
        public bool Alt;
    }
}
