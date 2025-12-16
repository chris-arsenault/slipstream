using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using Slipstream.Native;
using Slipstream.Services;

namespace Slipstream.UI;

public partial class HudWindow : Window
{
    private readonly SlotManager _slotManager;
    private readonly HudRenderer _renderer;

    public HudWindow(SlotManager slotManager)
    {
        InitializeComponent();
        _slotManager = slotManager;
        _renderer = new HudRenderer();

        // Subscribe to slot changes
        _slotManager.SlotChanged += (_, _) => Refresh();
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

        int slotCount = _slotManager.SlotCount;
        bool hasTempSlot = true; // Always have temp slot

        // Calculate content height
        int totalSlots = slotCount + (hasTempSlot ? 1 : 0);
        float contentHeight = padding * 2 + totalSlots * slotHeight + (totalSlots - 1) * slotSpacing + separatorHeight;

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
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Bottom - Height - 20;
    }

    public void Refresh()
    {
        Dispatcher.Invoke(() => SkiaCanvas.InvalidateVisual());
    }

    private void SkiaCanvas_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var size = new SKSize(e.Info.Width, e.Info.Height);

        // Get DPI scale
        var source = PresentationSource.FromVisual(this);
        float dpiScale = (float)(source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0);

        bool isRoundRobinMode = _slotManager.SlotBehavior == Models.SlotBehavior.RoundRobin;
        _renderer.Render(canvas, size, _slotManager.GetAllSlots(), _slotManager.ActiveSlotIndex,
            _slotManager.TempSlot, dpiScale, _slotManager.NextRoundRobinIndex, isRoundRobinMode);
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
            _slotManager.PromoteTempSlot();
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

        // Otherwise, allow window drag
        DragMove();
    }
}
