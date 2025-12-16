namespace Slipstream.Services;

/// <summary>
/// Abstraction for keyboard simulation to enable unit testing.
/// </summary>
public interface IKeyboardSimulator
{
    void KeyDown(byte virtualKey);
    void KeyUp(byte virtualKey);
    void Sleep(int milliseconds);
    bool IsKeyPhysicallyDown(byte virtualKey);
}

/// <summary>
/// Records keyboard events for testing purposes.
/// </summary>
public record KeyboardEvent(KeyboardEventType Type, byte VirtualKey);

public enum KeyboardEventType
{
    KeyDown,
    KeyUp,
    Sleep
}
