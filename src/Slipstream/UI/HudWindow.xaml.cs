using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using Slipstream.Models;
using Slipstream.Native;
using Slipstream.Processing;
using Slipstream.Services;

namespace Slipstream.UI;

public partial class HudWindow : Window
{
    private readonly SlotManager _slotManager;
    private readonly HudRenderer _renderer;
    private readonly ConfigService _configService;
    private readonly AppSettings _settings;
    private readonly HashSet<string> _stickyApps;
    private ProcessorToggleState? _processorToggleState;
    private ProcessorActivation? _processorActivation;

    public HudWindow(SlotManager slotManager, ConfigService configService, AppSettings settings)
    {
        InitializeComponent();
        _slotManager = slotManager;
        _configService = configService;
        _settings = settings;
        _renderer = new HudRenderer();
        _stickyApps = new HashSet<string>(settings.StickyApps, StringComparer.OrdinalIgnoreCase);

        // Apply initial theme from settings
        _renderer.SetTheme(settings.ColorPalette);

        // Subscribe to slot changes
        _slotManager.SlotChanged += (_, _) => Refresh();

        // Save position when window is moved
        LocationChanged += OnLocationChanged;
    }

    /// <summary>
    /// Sets the processor toggle state for displaying armed processor badges.
    /// </summary>
    public void SetProcessorToggleState(ProcessorToggleState toggleState)
    {
        _processorToggleState = toggleState;
        _processorToggleState.StateChanged += (_, _) => Refresh();
    }

    /// <summary>
    /// Sets the processor activation for getting MIDI chord processors.
    /// </summary>
    public void SetProcessorActivation(ProcessorActivation activation)
    {
        _processorActivation = activation;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Make window not show in alt-tab
        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, exStyle | Win32.WS_EX_TOOLWINDOW);

        // Calculate proper window size based on slot count
        UpdateWindowSize();

        // Position window at bottom-right by default
        PositionWindow();

        // Initial render
        Refresh();
    }

    private void UpdateWindowSize()
    {
        // HUD layout constants (must match HudRenderer)
        const float padding = 12f;
        const float slotHeight = 32f;
        const float slotSpacing = 4f;
        const float separatorHeight = 8f;
        const float headerHeight = 24f; // Reserved space for processor badges

        int slotCount = _slotManager.SlotCount;
        bool hasTempSlot = true; // Always have temp slot

        // Calculate content height (includes header space for processor badges)
        int totalSlots = slotCount + (hasTempSlot ? 1 : 0);
        float contentHeight = padding * 2 + headerHeight + totalSlots * slotHeight + (totalSlots - 1) * slotSpacing + separatorHeight;

        // Set window size (WPF uses device-independent units, which matches our drawing)
        Height = contentHeight;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Hide instead of close when user tries to close
        e.Cancel = true;
        Hide();
    }

    private void PositionWindow()
    {
        // Restore saved position if available
        if (_settings.HudWindowX.HasValue && _settings.HudWindowY.HasValue)
        {
            Left = _settings.HudWindowX.Value;
            Top = _settings.HudWindowY.Value;

            // Ensure window is still on screen
            var workArea = SystemParameters.WorkArea;
            if (Left < workArea.Left) Left = workArea.Left;
            if (Top < workArea.Top) Top = workArea.Top;
            if (Left + Width > workArea.Right) Left = workArea.Right - Width;
            if (Top + Height > workArea.Bottom) Top = workArea.Bottom - Height;
        }
        else
        {
            // Default to bottom-right
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 20;
            Top = workArea.Bottom - Height - 20;
        }
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        // Save position to settings
        _settings.HudWindowX = Left;
        _settings.HudWindowY = Top;
        _configService.SaveSettings(_settings);
    }

    public void Refresh()
    {
        Dispatcher.Invoke(() => SkiaCanvas.InvalidateVisual());
    }

    public void SetTheme(ColorPalette palette)
    {
        _renderer.SetTheme(palette);
        Refresh();
    }

    private void SkiaCanvas_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var size = new SKSize(e.Info.Width, e.Info.Height);

        // Get DPI scale
        var source = PresentationSource.FromVisual(this);
        float dpiScale = (float)(source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0);

        bool isRoundRobinMode = _slotManager.SlotBehavior == Models.SlotBehavior.RoundRobin;
        var toggledProcessors = _processorToggleState?.GetToggledDefinitions().ToList();
        var chordProcessors = _processorActivation?.GetMidiChords();
        List<ProcessorDefinition>? chordDefinitions = null;
        if (chordProcessors?.Count > 0)
        {
            chordDefinitions = ProcessorDefinitions.All.Where(d => chordProcessors.Contains(d.Name)).ToList();
        }
        _renderer.Render(canvas, size, _slotManager.GetAllSlots(), _slotManager.ActiveSlotIndex,
            _slotManager.TempSlot, dpiScale, _slotManager.NextRoundRobinIndex, isRoundRobinMode, toggledProcessors, chordDefinitions);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        // Get click position relative to the SkiaCanvas
        // Use WPF coordinates directly since renderer scales internally
        var pos = e.GetPosition(SkiaCanvas);
        float skX = (float)pos.X;
        float skY = (float)pos.Y;

        // Check if click is on temp slot promote button
        if (_renderer.HitTestTempSlotPromote(skX, skY))
        {
            _slotManager.PromoteTempSlot(_stickyApps);
            e.Handled = true;
            return;
        }

        // Check if click is on a lock button
        int lockSlotIndex = _renderer.HitTestLockButton(skX, skY);
        if (lockSlotIndex >= 0)
        {
            // Toggle the lock state for this slot
            _slotManager.ToggleLock(lockSlotIndex);
            e.Handled = true;
            return;
        }

        // Check if click is on a slot (to set it as active)
        int clickedSlotIndex = _renderer.HitTestSlot(skX, skY);
        if (clickedSlotIndex >= 0)
        {
            _slotManager.SetActiveSlot(clickedSlotIndex);
            e.Handled = true;
            return;
        }

        // Otherwise, allow window drag
        DragMove();
    }
}
