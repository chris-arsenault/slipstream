using System.Windows.Interop;
using Slipstream.Commands;
using Slipstream.Models;
using Slipstream.Native;

namespace Slipstream.Services;

public class HotkeyManager : IDisposable
{
    private HwndSource? _hwndSource;
    private IntPtr _hwnd;
    private readonly Dictionary<int, string> _registeredHotkeys = new();
    private readonly CommandRegistry _commandRegistry;
    private int _nextHotkeyId = 1;

    public HotkeyManager(IntPtr hwnd, HwndSource hwndSource, CommandRegistry commandRegistry)
    {
        _hwnd = hwnd;
        _hwndSource = hwndSource;
        _commandRegistry = commandRegistry;
        _hwndSource.AddHook(WndProc);
    }

    public bool Register(string actionName, ModifierKeys modifiers, VirtualKey key)
    {
        // Validate the action name can be parsed by the command registry
        var command = _commandRegistry.CreateCommand(actionName);
        if (command == null)
        {
            System.Diagnostics.Debug.WriteLine($"[Hotkey] Unknown action: {actionName}");
            return false;
        }

        uint win32Modifiers = ConvertModifiers(modifiers);
        int hotkeyId = _nextHotkeyId++;

        bool success = Win32.RegisterHotKey(_hwnd, hotkeyId, win32Modifiers | Win32.MOD_NOREPEAT, (uint)key);
        if (success)
        {
            _registeredHotkeys[hotkeyId] = actionName;
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
            .Where(kvp => kvp.Value.Equals(actionName, StringComparison.OrdinalIgnoreCase))
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
        return _registeredHotkeys.Values.Any(v => v.Equals(actionName, StringComparison.OrdinalIgnoreCase));
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32.WM_HOTKEY)
        {
            int hotkeyId = wParam.ToInt32();
            uint modifiers = (uint)(lParam.ToInt32() & 0xFFFF);
            uint vk = (uint)(lParam.ToInt32() >> 16);
            Console.WriteLine($"[Hotkey] WM_HOTKEY received, id={hotkeyId}, mods=0x{modifiers:X}, vk=0x{vk:X}");

            if (_registeredHotkeys.TryGetValue(hotkeyId, out var actionName))
            {
                Console.WriteLine($"[Hotkey] Executing: {actionName}");
                _commandRegistry.Execute(actionName);
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

    public void Dispose()
    {
        UnregisterAll();
        _hwndSource?.RemoveHook(WndProc);
    }
}
