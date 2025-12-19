namespace Slipstream.Services.Keyboard;

/// <summary>
/// Virtual key codes used for keyboard simulation.
/// Single source of truth for all VK constants.
/// </summary>
public static class VirtualKeys
{
    // Modifier keys - generic
    public const byte Control = 0x11;
    public const byte Shift = 0x10;
    public const byte Alt = 0x12;  // VK_MENU

    // Modifier keys - left variants
    public const byte LeftControl = 0xA2;
    public const byte LeftShift = 0xA0;
    public const byte LeftAlt = 0xA4;  // VK_LMENU

    // Modifier keys - right variants
    public const byte RightControl = 0xA3;
    public const byte RightShift = 0xA1;
    public const byte RightAlt = 0xA5;  // VK_RMENU

    // Common keys
    public const byte C = 0x43;
    public const byte V = 0x56;

    /// <summary>
    /// All modifier key codes (generic + left + right variants).
    /// </summary>
    public static ReadOnlySpan<byte> AllModifiers => new byte[]
    {
        Control, Shift, Alt,
        LeftControl, LeftShift, LeftAlt,
        RightControl, RightShift, RightAlt
    };
}
