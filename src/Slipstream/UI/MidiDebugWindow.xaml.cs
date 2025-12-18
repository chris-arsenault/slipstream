using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using Slipstream.Models;
using Slipstream.Native;
using Slipstream.Services;

namespace Slipstream.UI;

/// <summary>
/// Always-on-top MIDI debug window showing piano keys and current activity.
/// Useful for demos, screen recordings, and debugging.
/// </summary>
public partial class MidiDebugWindow : Window
{
    private readonly MidiManager _midiManager;
    private readonly MidiPresets _midiPresets;
    private readonly MidiSettings _midiSettings;
    private readonly AppSettings _appSettings;
    private readonly ConfigService _configService;
    private readonly MidiDebugRenderer _renderer;
    private float _dpiScale = 1.0f;

    public MidiDebugWindow(MidiManager midiManager, MidiPresets midiPresets, MidiSettings midiSettings,
        AppSettings appSettings, ConfigService configService)
    {
        InitializeComponent();

        _midiManager = midiManager;
        _midiPresets = midiPresets;
        _midiSettings = midiSettings;
        _appSettings = appSettings;
        _configService = configService;
        _renderer = new MidiDebugRenderer();

        // Load current scheme
        UpdateScheme();

        // Subscribe to MIDI events (not in edit mode - we want to see live activity)
        _midiManager.RawNoteReceived += OnRawNoteReceived;
        _midiManager.ProcessorChordsChanged += OnProcessorChordsChanged;
        _midiManager.DeviceChanged += OnDeviceChanged;

        // Save position when window is moved
        LocationChanged += OnLocationChanged;

        Loaded += OnLoaded;
        Closed += OnWindowClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Make window not show in alt-tab
        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, exStyle | Win32.WS_EX_TOOLWINDOW);

        // Restore saved position or use default
        PositionWindow();

        // Update current processor chords state
        UpdateProcessorChords();
    }

    private void PositionWindow()
    {
        // Restore saved position if available
        if (_appSettings.MidiDebugWindowX.HasValue && _appSettings.MidiDebugWindowY.HasValue)
        {
            Left = _appSettings.MidiDebugWindowX.Value;
            Top = _appSettings.MidiDebugWindowY.Value;

            // Ensure window is still on screen
            var workArea = SystemParameters.WorkArea;
            if (Left < workArea.Left) Left = workArea.Left;
            if (Top < workArea.Top) Top = workArea.Top;
            if (Left + Width > workArea.Right) Left = workArea.Right - Width;
            if (Top + Height > workArea.Bottom) Top = workArea.Bottom - Height;
        }
        else
        {
            // Default to bottom center
            var workArea = SystemParameters.WorkArea;
            Left = (workArea.Width - Width) / 2 + workArea.Left;
            Top = workArea.Bottom - Height - 60;
        }
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        // Save position to settings
        _appSettings.MidiDebugWindowX = Left;
        _appSettings.MidiDebugWindowY = Top;
        _configService.SaveSettings(_appSettings);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _midiManager.RawNoteReceived -= OnRawNoteReceived;
        _midiManager.ProcessorChordsChanged -= OnProcessorChordsChanged;
        _midiManager.DeviceChanged -= OnDeviceChanged;
    }

    private void UpdateScheme()
    {
        var scheme = _midiPresets.GetPreset(_midiSettings.ActivePreset);
        _renderer.SetScheme(scheme);
    }

    private void UpdateProcessorChords()
    {
        _renderer.SetActiveProcessors(_midiManager.HeldProcessorChords);
        Dispatcher.Invoke(() => SkiaCanvas.InvalidateVisual());
    }

    private void OnRawNoteReceived(object? sender, MidiNoteEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _renderer.SetNotePressed(e.NoteNumber, e.IsNoteOn);

            // Determine what action this note would trigger
            if (e.IsNoteOn)
            {
                var scheme = _midiPresets.GetPreset(_midiSettings.ActivePreset);

                // Check if it's a processor chord
                string? processor = null;
                foreach (var (proc, trigger) in scheme.ProcessorChords)
                {
                    if (trigger.Number == e.NoteNumber)
                    {
                        processor = proc;
                        break;
                    }
                }

                // Check if it's copy modifier
                bool isCopyMod = scheme.CopyModifier?.Number == e.NoteNumber;
                _renderer.SetCopyModifierHeld(isCopyMod);

                // Check if it's an action
                string? action = null;
                foreach (var (act, trigger) in scheme.Mappings)
                {
                    if (trigger.Number == e.NoteNumber)
                    {
                        action = act;
                        break;
                    }
                }

                _renderer.SetPendingAction(action);
            }
            else
            {
                // On note release, check if we should clear pending action
                var scheme = _midiPresets.GetPreset(_midiSettings.ActivePreset);
                bool isCopyMod = scheme.CopyModifier?.Number == e.NoteNumber;
                if (isCopyMod)
                {
                    _renderer.SetCopyModifierHeld(false);
                }

                // Clear pending action if it was from this note
                string? action = null;
                foreach (var (act, trigger) in scheme.Mappings)
                {
                    if (trigger.Number == e.NoteNumber)
                    {
                        action = act;
                        break;
                    }
                }
                if (action != null)
                {
                    _renderer.SetPendingAction(null);
                }
            }

            SkiaCanvas.InvalidateVisual();
        });
    }

    private void OnProcessorChordsChanged(object? sender, EventArgs e)
    {
        UpdateProcessorChords();
    }

    private void OnDeviceChanged(object? sender, MidiDeviceEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            // Clear all notes when device changes
            _renderer.ClearAllNotes();
            _renderer.SetActiveProcessors(Array.Empty<string>());
            _renderer.SetPendingAction(null);
            _renderer.SetCopyModifierHeld(false);
            SkiaCanvas.InvalidateVisual();
        });
    }

    private void SkiaCanvas_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var size = new SKSize(e.Info.Width, e.Info.Height);

        var source = PresentationSource.FromVisual(this);
        _dpiScale = (float)(source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0);

        _renderer.Render(canvas, size, _dpiScale);
    }

    private void SkiaCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Allow dragging the window
        DragMove();
    }
}
