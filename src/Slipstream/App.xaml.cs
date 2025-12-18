using System.Windows;
using System.Windows.Threading;
using Slipstream.Commands;
using Slipstream.Models;
using Slipstream.Processing;
using Slipstream.Services;
using Slipstream.UI;

namespace Slipstream;

public partial class App : Application
{
    private MessageWindow? _messageWindow;
    private TrayManager? _trayManager;
    private HudWindow? _hudWindow;
    private MidiDebugWindow? _midiDebugWindow;
    private ConfigService? _configService;
    private AppSettings? _settings;
    private SlotManager? _slotManager;
    private ClipboardMonitor? _clipboardMonitor;
    private HotkeyManager? _hotkeyManager;
    private MidiPresets? _midiPresets;
    private MidiManager? _midiManager;
    private PasteEngine? _pasteEngine;
    private KeyboardSequencer? _keyboardSequencer;
    private DispatcherTimer? _modifierCleanupTimer;
    private HashSet<string>? _stickyApps;
    private CommandRegistry? _commandRegistry;
    private CommandContext? _commandContext;
    private ProcessorToggleState? _processorToggleState;
    private ProcessorActivation? _processorActivation;
    private ProcessorRegistry? _processorRegistry;

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

        // Initialize sticky apps from settings (case-insensitive)
        _stickyApps = new HashSet<string>(_settings.StickyApps, StringComparer.OrdinalIgnoreCase);

        // Initialize processor toggle state
        _processorToggleState = new ProcessorToggleState();
        _processorActivation = new ProcessorActivation(_processorToggleState);
        _processorRegistry = new ProcessorRegistry();

        _keyboardSequencer = new KeyboardSequencer(new KeyboardSimulator());
        _pasteEngine = new PasteEngine(_keyboardSequencer);

        // Initialize clipboard monitor using message window
        _clipboardMonitor = new ClipboardMonitor(hwnd, hwndSource);
        _clipboardMonitor.ClipboardChanged += OnClipboardChanged;

        // Connect paste engine to clipboard monitor so it can suppress capture during paste
        _pasteEngine.SetClipboardMonitor(_clipboardMonitor);

        // Create HUD window
        _hudWindow = new HudWindow(_slotManager, _configService, _settings);
        _hudWindow.SetProcessorToggleState(_processorToggleState);
        _hudWindow.SetProcessorActivation(_processorActivation);

        // Initialize command system
        _commandContext = new CommandContext(
            _slotManager,
            _clipboardMonitor,
            _pasteEngine,
            _keyboardSequencer,
            _processorToggleState,
            _processorActivation,
            _processorRegistry,
            _hudWindow,
            _stickyApps);
        _commandRegistry = new CommandRegistry(_commandContext, _processorRegistry);

        // Initialize hotkey manager using message window
        _hotkeyManager = new HotkeyManager(hwnd, hwndSource, _commandRegistry);
        RegisterDefaultHotkeys(_settings);

        // Initialize MIDI manager with presets loaded from JSON config files
        _midiPresets = new MidiPresets(_configService);
        _midiManager = new MidiManager(_settings.MidiSettings, _midiPresets, _commandRegistry);
        _midiManager.DeviceChanged += OnMidiDeviceChanged;
        _midiManager.ProcessorChordsChanged += (_, _) => _hudWindow?.Refresh();
        _midiManager.Start();

        // Connect MIDI chord provider to processor activation
        _processorActivation.SetMidiChordProvider(() => _midiManager.HeldProcessorChords);

        // Initialize tray
        _trayManager = new TrayManager(
            onShowHud: () => _hudWindow?.Show(),
            onHideHud: () => _hudWindow?.Hide(),
            onOpenSettings: OpenSettings,
            onQuit: Shutdown,
            onToggleMidiDebug: ToggleMidiDebug
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
            _slotManager.CaptureToTempSlot(e.Data, e.Type, e.SourceProcessName);

            // Auto-promote if enabled
            if (_settings?.AutoPromote == true)
            {
                var promotedIndex = _slotManager.PromoteTempSlot(_stickyApps);
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

    private void OpenSettings()
    {
        var settingsWindow = new SettingsWindow(_configService!, _slotManager!, _hotkeyManager!, OnSettingsUpdated, _midiManager);
        settingsWindow.Show();
    }

    private void ToggleMidiDebug()
    {
        if (_midiDebugWindow != null)
        {
            // Close existing window
            _midiDebugWindow.Close();
            _midiDebugWindow = null;
            _trayManager?.UpdateMidiDebugVisibility(false);
        }
        else
        {
            // Open new window
            _midiDebugWindow = new MidiDebugWindow(_midiManager!, _midiPresets!, _settings!.MidiSettings, _settings, _configService!);
            _midiDebugWindow.Closed += (_, _) =>
            {
                _midiDebugWindow = null;
                _trayManager?.UpdateMidiDebugVisibility(false);
            };
            _midiDebugWindow.Show();
            _trayManager?.UpdateMidiDebugVisibility(true);
        }
    }

    private void OnSettingsUpdated(AppSettings settings)
    {
        // Update our local settings reference so AutoPromote works
        _settings = settings;

        // Update sticky apps from settings
        _stickyApps = new HashSet<string>(settings.StickyApps, StringComparer.OrdinalIgnoreCase);

        // Update command context with new sticky apps
        if (_commandContext != null)
        {
            _commandContext.StickyApps = _stickyApps;
        }

        // Update HUD theme when palette changes
        _hudWindow?.SetTheme(settings.ColorPalette);

        // Update MIDI manager with new settings
        _midiManager?.ApplySettings(settings.MidiSettings);
    }

    private void OnMidiDeviceChanged(object? sender, MidiDeviceEventArgs e)
    {
        if (e.IsConnected)
        {
            Console.WriteLine($"[App] MIDI device connected: {e.DeviceName}");
        }
        else if (e.DeviceName != null)
        {
            Console.WriteLine($"[App] MIDI device disconnected: {e.DeviceName}");
        }
        else if (e.ErrorMessage != null)
        {
            Console.WriteLine($"[App] MIDI error: {e.ErrorMessage}");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Stop cleanup timer
        _modifierCleanupTimer?.Stop();

        // Release any stuck modifier keys before shutting down
        _keyboardSequencer?.ReleaseAllModifiers();

        // Clean shutdown
        _midiManager?.Dispose();
        _hotkeyManager?.Dispose();
        _clipboardMonitor?.Dispose();
        _trayManager?.Dispose();
        _midiDebugWindow?.Close();
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
