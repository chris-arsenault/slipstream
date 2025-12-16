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
        _renderer = new SettingsRenderer(configService.LoadSettings());

        _renderer.CloseRequested += () => Close();
        _renderer.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        _configService.SaveSettings(settings);
        _slotManager.SetSlotCount(settings.SlotCount);
        _slotManager.SetSlotBehavior(settings.SlotBehavior);

        // Notify App about settings changes
        _onSettingsChanged?.Invoke(settings);
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
