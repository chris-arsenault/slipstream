using System.Runtime.InteropServices;
using Slipstream.Native;

namespace Slipstream.Services.Keyboard;

/// <summary>
/// Represents a single keyboard input event.
/// </summary>
public readonly record struct KeyEvent(byte VirtualKey, bool IsKeyUp)
{
    public static KeyEvent Down(byte vk) => new(vk, false);
    public static KeyEvent Up(byte vk) => new(vk, true);
}

/// <summary>
/// Interface for keyboard input injection to enable testing.
/// </summary>
public interface IInputInjector
{
    /// <summary>
    /// Sends a batch of keyboard events.
    /// Production implementation sends atomically via SendInput.
    /// </summary>
    void SendBatch(ReadOnlySpan<KeyEvent> events);

    /// <summary>
    /// Returns true if the key is physically held down (hardware state).
    /// </summary>
    bool IsKeyPhysicallyDown(byte virtualKey);

    /// <summary>
    /// Returns true if the key is logically down (what Windows believes).
    /// </summary>
    bool IsKeyLogicallyDown(byte virtualKey);
}

/// <summary>
/// Production implementation using batched SendInput.
/// All events in a batch are sent without possibility of interleaving.
/// </summary>
public class AtomicInputInjector : IInputInjector
{
    /// <summary>
    /// Sends a batch of keyboard events atomically.
    /// No user input or other synthetic input can interleave these events.
    /// </summary>
    public void SendBatch(ReadOnlySpan<KeyEvent> events)
    {
        if (events.Length == 0)
            return;

        var inputs = new Win32.INPUT[events.Length];

        for (int i = 0; i < events.Length; i++)
        {
            inputs[i] = CreateKeyboardInput(events[i].VirtualKey, events[i].IsKeyUp);
        }

        uint sent = Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32.INPUT>());

        if (sent != inputs.Length)
        {
            int error = Marshal.GetLastWin32Error();
            Console.WriteLine($"[AtomicInputInjector] SendInput sent {sent}/{inputs.Length} events, error={error}");
        }
    }

    public bool IsKeyPhysicallyDown(byte virtualKey)
    {
        return Win32.IsKeyPhysicallyDown(virtualKey);
    }

    public bool IsKeyLogicallyDown(byte virtualKey)
    {
        return Win32.IsKeyLogicallyDown(virtualKey);
    }

    private static Win32.INPUT CreateKeyboardInput(byte virtualKey, bool isKeyUp)
    {
        return new Win32.INPUT
        {
            type = Win32.INPUT_KEYBOARD,
            union = new Win32.INPUTUNION
            {
                ki = new Win32.KEYBDINPUT
                {
                    wVk = virtualKey,
                    wScan = 0,
                    dwFlags = isKeyUp ? Win32.KEYEVENTF_KEYUP : 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }
}

/// <summary>
/// Builds atomic keyboard operation sequences.
/// Handles modifier state transitions and restoration.
/// </summary>
public class KeyboardOperationBuilder
{
    private readonly List<KeyEvent> _events = new();

    /// <summary>
    /// Adds events to transition from current modifier state to target state.
    /// </summary>
    public KeyboardOperationBuilder TransitionModifiers(ModifierState from, ModifierState to)
    {
        // Release modifiers that are down but shouldn't be
        if (from.Ctrl && !to.Ctrl)
            _events.Add(KeyEvent.Up(VirtualKeys.Control));
        if (from.Shift && !to.Shift)
            _events.Add(KeyEvent.Up(VirtualKeys.Shift));
        if (from.Alt && !to.Alt)
            _events.Add(KeyEvent.Up(VirtualKeys.Alt));

        // Press modifiers that should be down but aren't
        if (!from.Ctrl && to.Ctrl)
            _events.Add(KeyEvent.Down(VirtualKeys.Control));
        if (!from.Shift && to.Shift)
            _events.Add(KeyEvent.Down(VirtualKeys.Shift));
        if (!from.Alt && to.Alt)
            _events.Add(KeyEvent.Down(VirtualKeys.Alt));

        return this;
    }

    /// <summary>
    /// Adds events to release all modifiers (generic + left/right variants).
    /// </summary>
    public KeyboardOperationBuilder ReleaseAllModifiers()
    {
        foreach (byte vk in VirtualKeys.AllModifiers)
        {
            _events.Add(KeyEvent.Up(vk));
        }
        return this;
    }

    /// <summary>
    /// Adds a key press (down + up).
    /// </summary>
    public KeyboardOperationBuilder KeyPress(byte virtualKey)
    {
        _events.Add(KeyEvent.Down(virtualKey));
        _events.Add(KeyEvent.Up(virtualKey));
        return this;
    }

    /// <summary>
    /// Adds a key down event.
    /// </summary>
    public KeyboardOperationBuilder KeyDown(byte virtualKey)
    {
        _events.Add(KeyEvent.Down(virtualKey));
        return this;
    }

    /// <summary>
    /// Adds a key up event.
    /// </summary>
    public KeyboardOperationBuilder KeyUp(byte virtualKey)
    {
        _events.Add(KeyEvent.Up(virtualKey));
        return this;
    }

    /// <summary>
    /// Builds the sequence as a span.
    /// </summary>
    public ReadOnlySpan<KeyEvent> Build() => _events.ToArray();

    /// <summary>
    /// Clears the builder for reuse.
    /// </summary>
    public void Clear() => _events.Clear();

    /// <summary>
    /// Gets the current event count.
    /// </summary>
    public int Count => _events.Count;
}
