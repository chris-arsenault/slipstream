using System.Windows.Interop;
using Slipstream.Models;
using Slipstream.Native;

namespace Slipstream.Services;

public class HotkeyManager : IDisposable
{
    private HwndSource? _hwndSource;
    private IntPtr _hwnd;
    private readonly Dictionary<int, (string ActionName, HotkeyAction Action, int SlotIndex)> _registeredHotkeys = new();
    private int _nextHotkeyId = 1;

    public event EventHandler<HotkeyEventArgs>? HotkeyPressed;

    public HotkeyManager(IntPtr hwnd, HwndSource hwndSource)
    {
        _hwnd = hwnd;
        _hwndSource = hwndSource;
        _hwndSource.AddHook(WndProc);
    }

    public bool Register(string actionName, ModifierKeys modifiers, VirtualKey key)
    {
        var action = ParseActionName(actionName, out int slotIndex);
        if (action == HotkeyAction.None)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to parse action: {actionName}");
            return false;
        }

        uint win32Modifiers = ConvertModifiers(modifiers);
        int hotkeyId = _nextHotkeyId++;

        bool success = Win32.RegisterHotKey(_hwnd, hotkeyId, win32Modifiers | Win32.MOD_NOREPEAT, (uint)key);
        if (success)
        {
            _registeredHotkeys[hotkeyId] = (actionName, action, slotIndex);
            Console.WriteLine($"[Hotkey] Registered: {actionName} (id={hotkeyId}, mods=0x{win32Modifiers:X}, key=0x{(uint)key:X})");
            return true;
        }
        else
        {
            int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            Console.WriteLine($"[Hotkey] FAILED to register: {actionName} (error={error})");
            return false;
        }
    }

    public void Unregister(string actionName)
    {
        var toRemove = _registeredHotkeys
            .Where(kvp => kvp.Value.ActionName == actionName)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in toRemove)
        {
            Win32.UnregisterHotKey(_hwnd, id);
            _registeredHotkeys.Remove(id);
        }
    }

    public void UnregisterAll()
    {
        foreach (var id in _registeredHotkeys.Keys.ToList())
        {
            Win32.UnregisterHotKey(_hwnd, id);
        }
        _registeredHotkeys.Clear();
    }

    public bool IsRegistered(string actionName)
    {
        return _registeredHotkeys.Values.Any(v => v.ActionName == actionName);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32.WM_HOTKEY)
        {
            int hotkeyId = wParam.ToInt32();
            uint modifiers = (uint)(lParam.ToInt32() & 0xFFFF);
            uint vk = (uint)(lParam.ToInt32() >> 16);
            Console.WriteLine($"[Hotkey] WM_HOTKEY received, id={hotkeyId}, mods=0x{modifiers:X}, vk=0x{vk:X}");
            if (_registeredHotkeys.TryGetValue(hotkeyId, out var registration))
            {
                bool hasShift = (modifiers & Win32.MOD_SHIFT) != 0;
                bool hasAlt = (modifiers & Win32.MOD_ALT) != 0;
                Console.WriteLine($"[Hotkey] Pressed: {registration.ActionName} (action={registration.Action}, slot={registration.SlotIndex}, shift={hasShift}, alt={hasAlt})");
                HotkeyPressed?.Invoke(this, new HotkeyEventArgs(registration.Action, registration.SlotIndex, hasShift, hasAlt));
                handled = true;
            }
            else
            {
                Console.WriteLine($"[Hotkey] WARNING: No registration found for id={hotkeyId}");
            }
        }

        return IntPtr.Zero;
    }

    private static uint ConvertModifiers(ModifierKeys modifiers)
    {
        uint result = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt)) result |= Win32.MOD_ALT;
        if (modifiers.HasFlag(ModifierKeys.Control)) result |= Win32.MOD_CONTROL;
        if (modifiers.HasFlag(ModifierKeys.Shift)) result |= Win32.MOD_SHIFT;
        if (modifiers.HasFlag(ModifierKeys.Win)) result |= Win32.MOD_WIN;
        return result;
    }

    private static HotkeyAction ParseActionName(string actionName, out int slotIndex)
    {
        slotIndex = -1;

        // Handle numpad variants (e.g., "CopyToSlotNumpad1" -> same as "CopyToSlot1")
        if (actionName.StartsWith("CopyToSlotNumpad"))
        {
            if (int.TryParse(actionName.AsSpan(16), out slotIndex))
            {
                slotIndex--; // Convert 1-based to 0-based
                return HotkeyAction.CopyToSlot;
            }
        }
        else if (actionName.StartsWith("CopyToSlot"))
        {
            if (int.TryParse(actionName.AsSpan(10), out slotIndex))
            {
                slotIndex--; // Convert 1-based to 0-based
                return HotkeyAction.CopyToSlot;
            }
        }
        else if (actionName.StartsWith("PasteFromSlotNumpad"))
        {
            if (int.TryParse(actionName.AsSpan(19), out slotIndex))
            {
                slotIndex--; // Convert 1-based to 0-based
                return HotkeyAction.PasteFromSlot;
            }
        }
        else if (actionName.StartsWith("PasteFromSlot"))
        {
            if (int.TryParse(actionName.AsSpan(13), out slotIndex))
            {
                slotIndex--; // Convert 1-based to 0-based
                return HotkeyAction.PasteFromSlot;
            }
        }
        else if (actionName.StartsWith("LockSlot"))
        {
            if (int.TryParse(actionName.AsSpan(8), out slotIndex))
            {
                slotIndex--;
                return HotkeyAction.LockSlot;
            }
        }
        else if (actionName.StartsWith("ClearSlot"))
        {
            if (int.TryParse(actionName.AsSpan(9), out slotIndex))
            {
                slotIndex--;
                return HotkeyAction.ClearSlot;
            }
        }

        return actionName switch
        {
            "ToggleHud" => HotkeyAction.ToggleHud,
            "CycleForward" => HotkeyAction.CycleForward,
            "CycleBackward" => HotkeyAction.CycleBackward,
            "ClearAll" => HotkeyAction.ClearAll,
            "PromoteTempSlot" => HotkeyAction.PromoteTempSlot,
            "PasteFromActiveSlot" => HotkeyAction.PasteFromActiveSlot,
            _ => HotkeyAction.None
        };
    }

    public void Dispose()
    {
        UnregisterAll();
        _hwndSource?.RemoveHook(WndProc);
    }
}

public class HotkeyEventArgs : EventArgs
{
    public HotkeyAction Action { get; }
    public int SlotIndex { get; }
    public bool HasShift { get; }
    public bool HasAlt { get; }
    public InputSource Source { get; }

    public HotkeyEventArgs(HotkeyAction action, int slotIndex = -1, bool hasShift = false, bool hasAlt = false, InputSource source = InputSource.Keyboard)
    {
        Action = action;
        SlotIndex = slotIndex;
        HasShift = hasShift;
        HasAlt = hasAlt;
        Source = source;
    }
}

public enum InputSource
{
    Keyboard,
    Midi
}

public enum HotkeyAction
{
    None,
    CopyToSlot,
    PasteFromSlot,
    PasteFromActiveSlot,
    CycleForward,
    CycleBackward,
    ToggleHud,
    LockSlot,
    ClearSlot,
    ClearAll,
    PromoteTempSlot
}
