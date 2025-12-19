using System.Runtime.InteropServices;

namespace Slipstream.Native;

internal static class Win32
{
    #region Hotkey Registration

    public const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Modifier keys
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    #endregion

    #region Clipboard Monitoring

    public const int WM_CLIPBOARDUPDATE = 0x031D;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    #endregion

    #region Keyboard Input Simulation

    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_UNICODE = 0x0004;

    // INPUT structure must be sized for the largest union member (MOUSEINPUT = 28 bytes on x64)
    // Using explicit layout to ensure correct alignment and size
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public INPUTUNION union;
    }

    // Union must be sized to fit MOUSEINPUT (the largest member)
    // On x64: MOUSEINPUT = 28 bytes, KEYBDINPUT = 24 bytes (with padding), HARDWAREINPUT = 8 bytes
    [StructLayout(LayoutKind.Explicit, Size = 28)]
    public struct INPUTUNION
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    #endregion

    #region Keyboard State

    /// <summary>
    /// Gets the physical state of a key (hardware). If the high-order bit is 1, the key is down.
    /// Use this for detecting what the user is physically pressing.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    /// <summary>
    /// Gets the logical state of a key (what Windows believes). If the high-order bit is 1, the key is down.
    /// Use this for determining what state needs to be corrected before sending input.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern short GetKeyState(int vKey);

    /// <summary>
    /// Returns true if the key is currently physically held down (hardware state).
    /// </summary>
    public static bool IsKeyPhysicallyDown(int vKey)
    {
        return (GetAsyncKeyState(vKey) & 0x8000) != 0;
    }

    /// <summary>
    /// Returns true if the key is logically down (what Windows believes).
    /// This may differ from physical state due to synthetic input or timing.
    /// </summary>
    public static bool IsKeyLogicallyDown(int vKey)
    {
        return (GetKeyState(vKey) & 0x8000) != 0;
    }

    /// <summary>
    /// Copies the status of the 256 virtual keys to the specified buffer.
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetKeyboardState(byte[] lpKeyState);

    /// <summary>
    /// Copies a 256-byte array of keyboard key states into the calling thread's keyboard input-state table.
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetKeyboardState(byte[] lpKeyState);

    #endregion

    #region Window Management

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    // SetWindowPos for reliable topmost handling
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    /// <summary>
    /// Sets a window to be topmost without activating it.
    /// </summary>
    public static void SetTopmost(IntPtr hwnd)
    {
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    #endregion

    #region Clipboard Owner

    [DllImport("user32.dll")]
    public static extern IntPtr GetClipboardOwner();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    #endregion

}
