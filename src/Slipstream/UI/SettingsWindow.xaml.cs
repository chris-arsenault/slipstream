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
    private readonly SettingsRenderer _renderer;
    private readonly Action<AppSettings>? _onSettingsChanged;
    private AppSettings _settings;

    private SKPoint _lastMousePosition;
    private string? _lastHoveredButton;
    private float _dpiScale = 1.0f;

    public SettingsWindow(ConfigService configService, SlotManager slotManager, HotkeyManager hotkeyManager, Action<AppSettings>? onSettingsChanged = null)
    {
        InitializeComponent();
        _configService = configService;
        _slotManager = slotManager;
        _hotkeyManager = hotkeyManager;
        _onSettingsChanged = onSettingsChanged;
        _settings = configService.LoadSettings();
        _renderer = new SettingsRenderer(_settings);

        _renderer.CloseRequested += () => Close();
        _renderer.SettingsChanged += OnSettingsChanged;
        _renderer.ClearAllSlotsRequested += OnClearAllSlotsRequested;
        _renderer.ResetHotkeysRequested += OnResetHotkeysRequested;

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
}