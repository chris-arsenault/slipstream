using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using Slipstream.Models;
using Slipstream.Services;

namespace Slipstream.UI;

public partial class MidiEditorWindow : Window
{
    private readonly MidiManager _midiManager;
    private readonly MidiPresets _midiPresets;
    private readonly ConfigService _configService;
    private readonly MidiEditorRenderer _renderer;
    private readonly Action? _onPresetChanged;
    private readonly DispatcherTimer _cursorBlinkTimer;

    private SKPoint _lastMousePosition;
    private string? _lastHoveredButton;
    private float _dpiScale = 1.0f;

    public MidiEditorWindow(
        MidiManager midiManager,
        MidiPresets midiPresets,
        ConfigService configService,
        MidiControlScheme? presetToEdit = null,
        Action? onPresetChanged = null)
    {
        InitializeComponent();

        _midiManager = midiManager;
        _midiPresets = midiPresets;
        _configService = configService;
        _onPresetChanged = onPresetChanged;

        // Create renderer with preset to edit (or new empty preset)
        var preset = presetToEdit ?? CreateNewPreset();
        _renderer = new MidiEditorRenderer(preset, _midiPresets, _configService);

        // Wire up renderer events
        _renderer.CloseRequested += () => Close();
        _renderer.PresetSaved += OnPresetSaved;

        // Subscribe to raw MIDI events for visual feedback
        _midiManager.RawNoteReceived += OnRawNoteReceived;

        // Enable edit mode (disables normal Slipstream actions)
        _midiManager.SetEditMode(true);

        // Timer for cursor blink animation
        _cursorBlinkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _cursorBlinkTimer.Tick += (_, _) => SkiaCanvas.InvalidateVisual();
        _cursorBlinkTimer.Start();

        // Handle keyboard input
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewTextInput += OnPreviewTextInput;

        Closed += OnWindowClosed;
    }

    private static MidiControlScheme CreateNewPreset()
    {
        return new MidiControlScheme
        {
            Name = "New Preset",
            Description = "Custom MIDI mapping",
            DeviceHint = "",
            Mappings = new Dictionary<string, MidiTrigger>(),
            CopyModifier = null
        };
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        // Disable edit mode (re-enable normal Slipstream actions)
        _midiManager.SetEditMode(false);
        _midiManager.RawNoteReceived -= OnRawNoteReceived;
        _cursorBlinkTimer.Stop();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_renderer.HasActiveTextField) return;

        switch (e.Key)
        {
            case Key.Back:
                _renderer.HandleBackspace();
                SkiaCanvas.InvalidateVisual();
                e.Handled = true;
                break;
            case Key.Delete:
                _renderer.HandleDelete();
                SkiaCanvas.InvalidateVisual();
                e.Handled = true;
                break;
            case Key.Left:
                _renderer.HandleArrowKey(true);
                SkiaCanvas.InvalidateVisual();
                e.Handled = true;
                break;
            case Key.Right:
                _renderer.HandleArrowKey(false);
                SkiaCanvas.InvalidateVisual();
                e.Handled = true;
                break;
            case Key.Escape:
                // Deselect text field by clicking outside
                _renderer.HandleMouseDown(new SKPoint(-1, -1));
                SkiaCanvas.InvalidateVisual();
                e.Handled = true;
                break;
        }
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (!_renderer.HasActiveTextField) return;

        foreach (char c in e.Text)
        {
            if (!char.IsControl(c))
            {
                _renderer.HandleKeyInput(c);
            }
        }
        SkiaCanvas.InvalidateVisual();
        e.Handled = true;
    }

    private void OnRawNoteReceived(object? sender, MidiNoteEventArgs e)
    {
        // Update renderer with pressed note state (on UI thread)
        Dispatcher.Invoke(() =>
        {
            _renderer.SetNotePressed(e.NoteNumber, e.IsNoteOn);
            if (e.IsNoteOn)
            {
                _renderer.SelectNote(e.NoteNumber);
            }
            SkiaCanvas.InvalidateVisual();
        });
    }

    private void OnPresetSaved(MidiControlScheme preset)
    {
        _onPresetChanged?.Invoke();
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
        return new SKPoint((float)wpfPoint.X, (float)wpfPoint.Y);
    }

    private void SkiaCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(SkiaCanvas);
        var skPos = ToSkiaPoint(pos);

        // Check if clicking on title bar for drag
        if (_renderer.IsInTitleBar(skPos))
        {
            DragMove();
            return;
        }

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

        var newHovered = _renderer.GetHoveredButton(_lastMousePosition);
        if (newHovered != _lastHoveredButton)
        {
            _lastHoveredButton = newHovered;
            _renderer.HandleMouseMove(_lastMousePosition);
            SkiaCanvas.InvalidateVisual();
        }
    }
}
