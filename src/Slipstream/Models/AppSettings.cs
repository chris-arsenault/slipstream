using System.Text.Json.Serialization;

namespace Slipstream.Models;

public class AppSettings
{
    public int SlotCount { get; set; } = 10;
    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimized { get; set; } = true;
    public bool ShowHudOnStart { get; set; } = false;
    public SlotFillMode FillMode { get; set; } = SlotFillMode.RoundRobin;

    // Temp slot behavior
    public bool AutoPromote { get; set; } = false; // Auto-promote temp slot to numbered slot on capture
    public SlotBehavior SlotBehavior { get; set; } = SlotBehavior.RoundRobin; // How promote picks target slot

    // HUD settings
    public HudPosition HudPosition { get; set; } = HudPosition.BottomRight;
    public float HudOpacity { get; set; } = 0.9f;
    public bool HudClickThrough { get; set; } = false;

    // Appearance
    public ColorPalette ColorPalette { get; set; } = ColorPalette.Dark;

    // Window positions (null = use default positioning)
    public double? HudWindowX { get; set; } = null;
    public double? HudWindowY { get; set; } = null;
    public double? SettingsWindowX { get; set; } = null;
    public double? SettingsWindowY { get; set; } = null;

    // Hotkey bindings: action name -> binding
    public Dictionary<string, HotkeyBinding> HotkeyBindings { get; set; } = new()
    {
        // Copy to slot (Ctrl+Alt+1-0)
        ["CopyToSlot1"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.D1),
        ["CopyToSlot2"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.D2),
        ["CopyToSlot3"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.D3),
        ["CopyToSlot4"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.D4),
        ["CopyToSlot5"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.D5),
        ["CopyToSlot6"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.D6),
        ["CopyToSlot7"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.D7),
        ["CopyToSlot8"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.D8),
        ["CopyToSlot9"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.D9),
        ["CopyToSlot10"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.D0),

        // Paste from slot (Ctrl+Shift+1-0)
        ["PasteFromSlot1"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.D1),
        ["PasteFromSlot2"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.D2),
        ["PasteFromSlot3"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.D3),
        ["PasteFromSlot4"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.D4),
        ["PasteFromSlot5"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.D5),
        ["PasteFromSlot6"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.D6),
        ["PasteFromSlot7"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.D7),
        ["PasteFromSlot8"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.D8),
        ["PasteFromSlot9"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.D9),
        ["PasteFromSlot10"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Shift, VirtualKey.D0),

        // Numpad: Copy to slot (Ctrl+Alt+Numpad1-0) - NumLock ON
        // Alt doesn't affect numpad translation, so VK_NUMPAD# works
        ["CopyToSlotNumpad1"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.NumPad1),
        ["CopyToSlotNumpad2"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.NumPad2),
        ["CopyToSlotNumpad3"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.NumPad3),
        ["CopyToSlotNumpad4"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.NumPad4),
        ["CopyToSlotNumpad5"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.NumPad5),
        ["CopyToSlotNumpad6"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.NumPad6),
        ["CopyToSlotNumpad7"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.NumPad7),
        ["CopyToSlotNumpad8"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.NumPad8),
        ["CopyToSlotNumpad9"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.NumPad9),
        ["CopyToSlotNumpad10"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.NumPad0),

        // Numpad: Paste from slot (Ctrl+Numpad1-0) - NumLock ON
        ["PasteFromSlotNumpad1"] = new HotkeyBinding(ModifierKeys.Control, VirtualKey.NumPad1),
        ["PasteFromSlotNumpad2"] = new HotkeyBinding(ModifierKeys.Control, VirtualKey.NumPad2),
        ["PasteFromSlotNumpad3"] = new HotkeyBinding(ModifierKeys.Control, VirtualKey.NumPad3),
        ["PasteFromSlotNumpad4"] = new HotkeyBinding(ModifierKeys.Control, VirtualKey.NumPad4),
        ["PasteFromSlotNumpad5"] = new HotkeyBinding(ModifierKeys.Control, VirtualKey.NumPad5),
        ["PasteFromSlotNumpad6"] = new HotkeyBinding(ModifierKeys.Control, VirtualKey.NumPad6),
        ["PasteFromSlotNumpad7"] = new HotkeyBinding(ModifierKeys.Control, VirtualKey.NumPad7),
        ["PasteFromSlotNumpad8"] = new HotkeyBinding(ModifierKeys.Control, VirtualKey.NumPad8),
        ["PasteFromSlotNumpad9"] = new HotkeyBinding(ModifierKeys.Control, VirtualKey.NumPad9),
        ["PasteFromSlotNumpad10"] = new HotkeyBinding(ModifierKeys.Control, VirtualKey.NumPad0),

        // Control hotkeys
        ["ToggleHud"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.H),
        ["CycleForward"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.Down),
        ["CycleBackward"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.Up),

        // Promote temp slot to next available numbered slot (Ctrl+Alt+C after Ctrl+C)
        ["PromoteTempSlot"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.C),

        // Paste from active slot (Ctrl+Alt+V)
        ["PasteFromActiveSlot"] = new HotkeyBinding(ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.V),
    };

    /// <summary>
    /// MIDI input settings
    /// </summary>
    public MidiSettings MidiSettings { get; set; } = new();
}

public enum SlotFillMode
{
    RoundRobin,
    MostRecentlyUsed
}

public enum SlotBehavior
{
    RoundRobin, // Promote cycles through slots sequentially
    Fixed       // Promote always targets the active slot (changed only via Ctrl+Alt+Up/Down)
}

public enum HudPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Floating
}

public enum ColorPalette
{
    Dark,       // Current dark theme with blue accent
    Light,      // Light mode
    Terminal    // Dark with green text (classic terminal)
}

[Flags]
public enum ModifierKeys
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}

public enum VirtualKey
{
    D0 = 0x30,
    D1 = 0x31,
    D2 = 0x32,
    D3 = 0x33,
    D4 = 0x34,
    D5 = 0x35,
    D6 = 0x36,
    D7 = 0x37,
    D8 = 0x38,
    D9 = 0x39,
    A = 0x41,
    B = 0x42,
    C = 0x43,
    D = 0x44,
    E = 0x45,
    F = 0x46,
    G = 0x47,
    H = 0x48,
    I = 0x49,
    J = 0x4A,
    K = 0x4B,
    L = 0x4C,
    M = 0x4D,
    N = 0x4E,
    O = 0x4F,
    P = 0x50,
    Q = 0x51,
    R = 0x52,
    S = 0x53,
    T = 0x54,
    U = 0x55,
    V = 0x56,
    W = 0x57,
    X = 0x58,
    Y = 0x59,
    Z = 0x5A,
    Up = 0x26,
    Down = 0x28,
    Left = 0x25,
    Right = 0x27,
    Space = 0x20,
    Enter = 0x0D,
    Escape = 0x1B,
    Tab = 0x09,
    Delete = 0x2E,
    Insert = 0x2D,
    Home = 0x24,
    End = 0x23,
    PageUp = 0x21,
    PageDown = 0x22,
    Clear = 0x0C, // Numpad 5 without NumLock (VK_CLEAR)
    F1 = 0x70,
    F2 = 0x71,
    F3 = 0x72,
    F4 = 0x73,
    F5 = 0x74,
    F6 = 0x75,
    F7 = 0x76,
    F8 = 0x77,
    F9 = 0x78,
    F10 = 0x79,
    F11 = 0x7A,
    F12 = 0x7B,
    // Numpad keys
    NumPad0 = 0x60,
    NumPad1 = 0x61,
    NumPad2 = 0x62,
    NumPad3 = 0x63,
    NumPad4 = 0x64,
    NumPad5 = 0x65,
    // Special keys
    OemTilde = 0xC0, // ` ~ key (backquote/grave)
    NumPad6 = 0x66,
    NumPad7 = 0x67,
    NumPad8 = 0x68,
    NumPad9 = 0x69
}

public record HotkeyBinding(ModifierKeys Modifiers, VirtualKey Key);
