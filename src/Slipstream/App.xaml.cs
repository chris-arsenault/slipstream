using System.Windows;
using System.Windows.Threading;
using Slipstream.Models;
using Slipstream.Services;
using Slipstream.UI;

namespace Slipstream;

public partial class App : Application
{
    private MessageWindow? _messageWindow;
    private TrayManager? _trayManager;
    private HudWindow? _hudWindow;
    private ConfigService? _configService;
    private AppSettings? _settings;
    private SlotManager? _slotManager;
    private ClipboardMonitor? _clipboardMonitor;
    private HotkeyManager? _hotkeyManager;
    private PasteEngine? _pasteEngine;
    private KeyboardSequencer? _keyboardSequencer;
    private DispatcherTimer? _modifierCleanupTimer;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Create hidden message window for receiving Windows messages
        _messageWindow = new MessageWindow();
        _messageWindow.Show(); // Must show to pump messages, but it's invisible

        var hwnd = _messageWindow.Hwnd;
        var hwndSource = _messageWindow.HwndSource!;

        // Initialize services
        _configService = new ConfigService();
        _settings = _configService.LoadSettings();
        var slots = _configService.LoadSlots(_settings.SlotCount);

        _slotManager = new SlotManager(slots, _settings.SlotCount, _settings.SlotBehavior);
        _slotManager.SlotChanged += OnSlotChanged;

        _keyboardSequencer = new KeyboardSequencer(new KeyboardSimulator());
        _pasteEngine = new PasteEngine(_keyboardSequencer);

        // Create HUD window
        _hudWindow = new HudWindow(_slotManager, _configService, _settings);

        // Initialize clipboard monitor using message window
        _clipboardMonitor = new ClipboardMonitor(hwnd, hwndSource);
        _clipboardMonitor.ClipboardChanged += OnClipboardChanged;

        // Connect paste engine to clipboard monitor so it can suppress capture during paste
        _pasteEngine.SetClipboardMonitor(_clipboardMonitor);

        // Initialize hotkey manager using message window
        _hotkeyManager = new HotkeyManager(hwnd, hwndSource);
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
        RegisterDefaultHotkeys(_settings);

        // Initialize tray
        _trayManager = new TrayManager(
            onShowHud: () => _hudWindow?.Show(),
            onHideHud: () => _hudWindow?.Hide(),
            onOpenSettings: OpenSettings,
            onQuit: Shutdown
        );

        // Start clipboard monitoring (always on)
        _clipboardMonitor.Start();

        // Start periodic modifier cleanup timer to fix stuck keys
        _modifierCleanupTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _modifierCleanupTimer.Tick += OnModifierCleanupTick;
        _modifierCleanupTimer.Start();

        // Show HUD on startup if configured
        if (_settings.ShowHudOnStart)
        {
            _hudWindow.Show();
            _trayManager.UpdateHudVisibility(true);
        }
    }

    private void OnModifierCleanupTick(object? sender, EventArgs e)
    {
        // Periodically release any modifier keys that aren't physically held
        // This catches race conditions where modifiers get stuck
        _keyboardSequencer?.CleanupStuckModifiers();
    }

    private void RegisterDefaultHotkeys(AppSettings settings)
    {
        if (_hotkeyManager == null) return;

        // Register hotkeys from settings or use defaults
        foreach (var binding in settings.HotkeyBindings)
        {
            _hotkeyManager.Register(binding.Key, binding.Value.Modifiers, binding.Value.Key);
        }
    }

    private void OnClipboardChanged(object? sender, ClipboardChangedEventArgs e)
    {
        if (_slotManager == null) return;

        if (e.TargetSlotIndex.HasValue)
        {
            // Targeted copy (Ctrl+Alt+#) - goes directly to numbered slot
            _slotManager.CaptureToSlot(e.TargetSlotIndex.Value, e.Data, e.Type);
        }
        else
        {
            // Normal Ctrl+C or external clipboard change - goes to temp slot
            _slotManager.CaptureToTempSlot(e.Data, e.Type);

            // Auto-promote if enabled
            if (_settings?.AutoPromote == true)
            {
                var promotedIndex = _slotManager.PromoteTempSlot();
                Console.WriteLine($"[App] AutoPromote: promoted to slot {promotedIndex}");
            }
        }
    }

    private void OnSlotChanged(object? sender, SlotChangedEventArgs e)
    {
        _hudWindow?.Refresh();

        // Debounced save
        _configService?.SaveSlotsDebounced(_slotManager!.GetAllSlots());
    }

    private void CopySelectionToSlot(int slotIndex)
    {
        // Set the target slot BEFORE sending Ctrl+C
        // The clipboard monitor will use this when WM_CLIPBOARDUPDATE arrives
        _clipboardMonitor?.SetPendingTargetSlot(slotIndex);

        // Send Ctrl+C to copy selection
        SendCtrlC();
    }

    private void SendCtrlC()
    {
        _keyboardSequencer?.SendCopyWithModifierRelease();
    }

    private void OnHotkeyPressed(object? sender, HotkeyEventArgs e)
    {
        switch (e.Action)
        {
            case HotkeyAction.CopyToSlot:
                // Send Ctrl+C then capture to specific slot
                CopySelectionToSlot(e.SlotIndex);
                break;

            case HotkeyAction.PasteFromSlot:
                // Directly inject text from slot (no clipboard involved for text)
                Console.WriteLine($"[App] PasteFromSlot triggered for slot {e.SlotIndex}");
                var slot = _slotManager?.GetSlot(e.SlotIndex);
                Console.WriteLine($"[App] Slot found: {slot != null}, HasContent: {slot?.HasContent}");
                if (slot?.HasContent == true)
                {
                    Console.WriteLine($"[App] Pasting: Type={slot.Type}, Text={slot.TextContent?.Substring(0, Math.Min(50, slot.TextContent?.Length ?? 0))}...");
                    _pasteEngine?.PasteFromSlot(slot);
                }
                break;

            case HotkeyAction.CycleForward:
                _slotManager?.CycleActiveSlot(1);
                break;

            case HotkeyAction.CycleBackward:
                _slotManager?.CycleActiveSlot(-1);
                break;

            case HotkeyAction.ToggleHud:
                if (_hudWindow?.IsVisible == true)
                    _hudWindow.Hide();
                else
                    _hudWindow?.Show();
                break;

            case HotkeyAction.LockSlot:
                _slotManager?.ToggleLock(e.SlotIndex);
                break;

            case HotkeyAction.ClearSlot:
                _slotManager?.ClearSlot(e.SlotIndex);
                break;

            case HotkeyAction.PromoteTempSlot:
                var promotedIndex = _slotManager?.PromoteTempSlot();
                Console.WriteLine($"[App] PromoteTempSlot: promoted to slot {promotedIndex}");
                break;

            case HotkeyAction.PasteFromActiveSlot:
                var activeSlot = _slotManager?.GetSlot(_slotManager.ActiveSlotIndex);
                Console.WriteLine($"[App] PasteFromActiveSlot triggered, activeIndex={_slotManager?.ActiveSlotIndex}");
                if (activeSlot?.HasContent == true)
                {
                    Console.WriteLine($"[App] Pasting from active slot: Type={activeSlot.Type}");
                    _pasteEngine?.PasteFromSlot(activeSlot);
                }
                break;
        }
    }

    private void OpenSettings()
    {
        var settingsWindow = new SettingsWindow(_configService!, _slotManager!, _hotkeyManager!, OnSettingsUpdated);
        settingsWindow.Show();
    }

    private void OnSettingsUpdated(AppSettings settings)
    {
        // Update our local settings reference so AutoPromote works
        _settings = settings;

        // Update HUD theme when palette changes
        _hudWindow?.SetTheme(settings.ColorPalette);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Stop cleanup timer
        _modifierCleanupTimer?.Stop();

        // Release any stuck modifier keys before shutting down
        _keyboardSequencer?.ReleaseAllModifiers();

        // Clean shutdown
        _hotkeyManager?.Dispose();
        _clipboardMonitor?.Dispose();
        _trayManager?.Dispose();
        _hudWindow?.Close();
        _messageWindow?.Close();

        // Final save
        if (_slotManager != null && _configService != null)
        {
            _configService.SaveSlots(_slotManager.GetAllSlots());
        }

        base.OnExit(e);
    }
}
