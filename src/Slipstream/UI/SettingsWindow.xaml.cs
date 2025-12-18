using System.Windows;
using System.Windows.Input;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using Slipstream.Models;
using Slipstream.Services;

namespace Slipstream.UI;

public partial class SettingsWindow : Window
{
    private readonly ConfigService _configService;
    private readonly SlotManager _slotManager;
    private readonly HotkeyManager _hotkeyManager;
    private readonly MidiManager? _midiManager;
    private readonly SettingsRenderer _renderer;
    private readonly Action<AppSettings>? _onSettingsChanged;
    private AppSettings _settings;

    private SKPoint _lastMousePosition;
    private string? _lastHoveredButton;
    private float _dpiScale = 1.0f;

    public SettingsWindow(ConfigService configService, SlotManager slotManager, HotkeyManager hotkeyManager, Action<AppSettings>? onSettingsChanged = null, MidiManager? midiManager = null)
    {
        InitializeComponent();
        _configService = configService;
        _slotManager = slotManager;
        _hotkeyManager = hotkeyManager;
        _midiManager = midiManager;
        _onSettingsChanged = onSettingsChanged;
        _settings = configService.LoadSettings();
        _renderer = new SettingsRenderer(_settings, midiManager?.Presets);

        _renderer.CloseRequested += () => Close();
        _renderer.SettingsChanged += OnSettingsChanged;
        _renderer.ClearAllSlotsRequested += OnClearAllSlotsRequested;
        _renderer.ResetHotkeysRequested += OnResetHotkeysRequested;
        _renderer.MidiDeviceSelected += OnMidiDeviceSelected;
        _renderer.MidiPresetSelected += OnMidiPresetSelected;
        _renderer.EditMidiPresetRequested += OnEditMidiPresetRequested;
        _renderer.NewMidiPresetRequested += OnNewMidiPresetRequested;
        _renderer.HotkeyBindingChanged += OnHotkeyBindingChanged;

        // Subscribe to MIDI device changes for real-time updates
        if (_midiManager != null)
        {
            _midiManager.DeviceChanged += OnMidiDeviceChanged;
        }

        // Save position when window is moved
        LocationChanged += OnLocationChanged;

        // Restore position
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Restore saved position if available
        if (_settings.SettingsWindowX.HasValue && _settings.SettingsWindowY.HasValue)
        {
            Left = _settings.SettingsWindowX.Value;
            Top = _settings.SettingsWindowY.Value;

            // Ensure window is still on screen
            var workArea = SystemParameters.WorkArea;
            if (Left < workArea.Left) Left = workArea.Left;
            if (Top < workArea.Top) Top = workArea.Top;
            if (Left + Width > workArea.Right) Left = workArea.Right - Width;
            if (Top + Height > workArea.Bottom) Top = workArea.Bottom - Height;
        }

        // Initialize MIDI state
        UpdateMidiState();
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        // Save position to settings
        _settings.SettingsWindowX = Left;
        _settings.SettingsWindowY = Top;
        _configService.SaveSettings(_settings);
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        _configService.SaveSettings(settings);
        _slotManager.SetSlotBehavior(settings.SlotBehavior);

        // Notify App about settings changes
        _onSettingsChanged?.Invoke(settings);
    }

    private void OnClearAllSlotsRequested()
    {
        _slotManager.ClearAllSlots();
    }

    private void OnResetHotkeysRequested()
    {
        // Reset hotkeys in config
        _configService.ResetHotkeysToDefaults(_settings);

        // Re-register all hotkeys with the new defaults
        _hotkeyManager.UnregisterAll();
        foreach (var binding in _settings.HotkeyBindings)
        {
            _hotkeyManager.Register(binding.Key, binding.Value.Modifiers, binding.Value.Key);
        }

        // Notify about the change
        _onSettingsChanged?.Invoke(_settings);

        // Force redraw
        SkiaCanvas.InvalidateVisual();
    }

    private void UpdateMidiState()
    {
        if (_midiManager == null)
        {
            _renderer.UpdateMidiState(Array.Empty<string>(), null, false);
            return;
        }

        var devices = _midiManager.GetAvailableDevices();
        var currentDevice = _midiManager.CurrentDevice;
        var isConnected = _midiManager.IsActive;

        _renderer.UpdateMidiState(devices, currentDevice, isConnected);
        SkiaCanvas.InvalidateVisual();
    }

    private void OnMidiDeviceSelected(string deviceName)
    {
        if (_midiManager == null) return;

        _midiManager.SelectDevice(deviceName);
        _settings.MidiSettings.DeviceName = deviceName;
        _configService.SaveSettings(_settings);
        _onSettingsChanged?.Invoke(_settings);
        UpdateMidiState();
    }

    private void OnMidiPresetSelected(string presetName)
    {
        // Settings are already saved by the renderer, just apply to MidiManager
        _midiManager?.ApplySettings(_settings.MidiSettings);
    }

    private void OnEditMidiPresetRequested()
    {
        if (_midiManager == null) return;

        // Get current preset to edit
        var currentPreset = _midiManager.Presets.GetPreset(_settings.MidiSettings.ActivePreset);

        var editorWindow = new MidiEditorWindow(
            _midiManager,
            _midiManager.Presets,
            _configService,
            currentPreset,
            OnMidiPresetEditorClosed
        );
        editorWindow.Owner = this;
        editorWindow.ShowDialog();
    }

    private void OnNewMidiPresetRequested()
    {
        if (_midiManager == null) return;

        var editorWindow = new MidiEditorWindow(
            _midiManager,
            _midiManager.Presets,
            _configService,
            null, // null = create new preset
            OnMidiPresetEditorClosed
        );
        editorWindow.Owner = this;
        editorWindow.ShowDialog();
    }

    private void OnMidiPresetEditorClosed()
    {
        // Reload presets after editor closes
        _midiManager?.Presets.Reload();
        _midiManager?.ApplySettings(_settings.MidiSettings);
        SkiaCanvas.InvalidateVisual();
    }

    private void OnMidiDeviceChanged(object? sender, MidiDeviceEventArgs e)
    {
        // Update UI on the UI thread
        Dispatcher.Invoke(() =>
        {
            UpdateMidiState();
        });
    }

    private void OnHotkeyBindingChanged(string actionName, HotkeyBinding binding)
    {
        // Re-register the hotkey with the new binding
        _hotkeyManager.Unregister(actionName);
        _hotkeyManager.Register(actionName, binding.Modifiers, binding.Key);
    }

    private void SkiaCanvas_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var size = new SKSize(e.Info.Width, e.Info.Height);

        // Get DPI scale
        var source = PresentationSource.FromVisual(this);
        _dpiScale = (float)(source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0);

        _renderer.Render(canvas, size, _lastMousePosition, _dpiScale);
    }

    private SKPoint ToSkiaPoint(Point wpfPoint)
    {
        // Return WPF coordinates directly - the renderer scales everything internally
        // so hit testing needs coordinates in the same DPI-independent space
        return new SKPoint((float)wpfPoint.X, (float)wpfPoint.Y);
    }

    private void SkiaCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(SkiaCanvas);
        var skPos = ToSkiaPoint(pos);

        // Check if clicking on title bar for drag
        if (_renderer.IsInTitleBar(skPos))
        {
            // Use WPF's built-in drag
            DragMove();
            return;
        }

        // Handle button clicks
        _renderer.HandleMouseDown(skPos);
        SkiaCanvas.InvalidateVisual();
    }

    private void SkiaCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(SkiaCanvas);
        var skPos = ToSkiaPoint(pos);

        _renderer.HandleMouseUp(skPos);
        SkiaCanvas.InvalidateVisual();
    }

    private void SkiaCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(SkiaCanvas);
        _lastMousePosition = ToSkiaPoint(pos);

        // Only invalidate if hover state actually changed
        var newHovered = _renderer.GetHoveredButton(_lastMousePosition);
        if (newHovered != _lastHoveredButton)
        {
            _lastHoveredButton = newHovered;
            _renderer.HandleMouseMove(_lastMousePosition);
            SkiaCanvas.InvalidateVisual();
        }
    }

    protected override void OnTextInput(TextCompositionEventArgs e)
    {
        base.OnTextInput(e);

        if (_renderer.IsStickyAppInputFocused && !string.IsNullOrEmpty(e.Text))
        {
            _renderer.HandleTextInput(e.Text);
            SkiaCanvas.InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        // Handle hotkey editing - capture key press to set new binding
        if (_renderer.IsEditingHotkey)
        {
            if (e.Key == Key.Escape)
            {
                _renderer.CancelHotkeyEdit();
                SkiaCanvas.InvalidateVisual();
                e.Handled = true;
                return;
            }

            // Get virtual key and modifiers
            var vk = ConvertToVirtualKey(e.Key);
            if (vk.HasValue)
            {
                var modifiers = GetCurrentModifiers();
                _renderer.SetHotkeyBinding(modifiers, vk.Value);
                SkiaCanvas.InvalidateVisual();
                e.Handled = true;
            }
            return;
        }

        if (_renderer.IsStickyAppInputFocused)
        {
            _renderer.HandleKeyDown(e.Key);
            SkiaCanvas.InvalidateVisual();

            // Handle these keys to prevent them from doing other things
            if (e.Key == Key.Back || e.Key == Key.Enter || e.Key == Key.Escape)
            {
                e.Handled = true;
            }
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        _renderer.HandleScroll(e.Delta / 120f);
        SkiaCanvas.InvalidateVisual();
        e.Handled = true;
    }

    private Models.ModifierKeys GetCurrentModifiers()
    {
        var modifiers = Models.ModifierKeys.None;
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            modifiers |= Models.ModifierKeys.Control;
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            modifiers |= Models.ModifierKeys.Alt;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            modifiers |= Models.ModifierKeys.Shift;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
            modifiers |= Models.ModifierKeys.Win;
        return modifiers;
    }

    private VirtualKey? ConvertToVirtualKey(Key key)
    {
        // Skip modifier-only keys
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin ||
            key == Key.System)
            return null;

        return key switch
        {
            Key.D0 => VirtualKey.D0,
            Key.D1 => VirtualKey.D1,
            Key.D2 => VirtualKey.D2,
            Key.D3 => VirtualKey.D3,
            Key.D4 => VirtualKey.D4,
            Key.D5 => VirtualKey.D5,
            Key.D6 => VirtualKey.D6,
            Key.D7 => VirtualKey.D7,
            Key.D8 => VirtualKey.D8,
            Key.D9 => VirtualKey.D9,
            Key.A => VirtualKey.A,
            Key.B => VirtualKey.B,
            Key.C => VirtualKey.C,
            Key.D => VirtualKey.D,
            Key.E => VirtualKey.E,
            Key.F => VirtualKey.F,
            Key.G => VirtualKey.G,
            Key.H => VirtualKey.H,
            Key.I => VirtualKey.I,
            Key.J => VirtualKey.J,
            Key.K => VirtualKey.K,
            Key.L => VirtualKey.L,
            Key.M => VirtualKey.M,
            Key.N => VirtualKey.N,
            Key.O => VirtualKey.O,
            Key.P => VirtualKey.P,
            Key.Q => VirtualKey.Q,
            Key.R => VirtualKey.R,
            Key.S => VirtualKey.S,
            Key.T => VirtualKey.T,
            Key.U => VirtualKey.U,
            Key.V => VirtualKey.V,
            Key.W => VirtualKey.W,
            Key.X => VirtualKey.X,
            Key.Y => VirtualKey.Y,
            Key.Z => VirtualKey.Z,
            Key.Up => VirtualKey.Up,
            Key.Down => VirtualKey.Down,
            Key.Left => VirtualKey.Left,
            Key.Right => VirtualKey.Right,
            Key.Space => VirtualKey.Space,
            Key.Enter => VirtualKey.Enter,
            Key.Tab => VirtualKey.Tab,
            Key.Delete => VirtualKey.Delete,
            Key.Insert => VirtualKey.Insert,
            Key.Home => VirtualKey.Home,
            Key.End => VirtualKey.End,
            Key.PageUp => VirtualKey.PageUp,
            Key.PageDown => VirtualKey.PageDown,
            Key.F1 => VirtualKey.F1,
            Key.F2 => VirtualKey.F2,
            Key.F3 => VirtualKey.F3,
            Key.F4 => VirtualKey.F4,
            Key.F5 => VirtualKey.F5,
            Key.F6 => VirtualKey.F6,
            Key.F7 => VirtualKey.F7,
            Key.F8 => VirtualKey.F8,
            Key.F9 => VirtualKey.F9,
            Key.F10 => VirtualKey.F10,
            Key.F11 => VirtualKey.F11,
            Key.F12 => VirtualKey.F12,
            Key.NumPad0 => VirtualKey.NumPad0,
            Key.NumPad1 => VirtualKey.NumPad1,
            Key.NumPad2 => VirtualKey.NumPad2,
            Key.NumPad3 => VirtualKey.NumPad3,
            Key.NumPad4 => VirtualKey.NumPad4,
            Key.NumPad5 => VirtualKey.NumPad5,
            Key.NumPad6 => VirtualKey.NumPad6,
            Key.NumPad7 => VirtualKey.NumPad7,
            Key.NumPad8 => VirtualKey.NumPad8,
            Key.NumPad9 => VirtualKey.NumPad9,
            Key.Oem3 => VirtualKey.OemTilde, // ` ~ key
            _ => null
        };
    }
}