using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using Slipstream.Commands;
using Slipstream.Models;

namespace Slipstream.Services;

/// <summary>
/// Manages MIDI input devices and converts MIDI events to Slipstream actions
/// </summary>
public class MidiManager : IDisposable
{
    private InputDevice? _inputDevice;
    private MidiSettings _settings;
    private readonly MidiPresets _presets;
    private readonly CommandRegistry _commandRegistry;
    private Dictionary<string, MidiTrigger> _activeMappings = new();
    private Dictionary<string, MidiTrigger> _processorChords = new();
    private readonly HashSet<string> _heldProcessorChords = new();
    private MidiTrigger? _copyModifier;
    private bool _copyModifierHeld = false;
    private readonly object _lock = new();
    private Timer? _devicePollTimer;
    private readonly Dictionary<int, int> _lastCcValues = new(); // Track CC values for threshold detection
    private bool _isDisposed;
    private HashSet<string> _lastKnownDevices = new(); // Track devices for change detection
    private const int PollIntervalMs = 1000; // Poll every 1 second for faster hot-plug response
    private bool _editMode; // When true, fires raw events instead of actions

    /// <summary>
    /// Fires when device connection status changes
    /// </summary>
    public event EventHandler<MidiDeviceEventArgs>? DeviceChanged;

    /// <summary>
    /// Fires for raw MIDI note events when in edit mode
    /// </summary>
    public event EventHandler<MidiNoteEventArgs>? RawNoteReceived;

    /// <summary>
    /// Fires when processor chord held state changes
    /// </summary>
    public event EventHandler? ProcessorChordsChanged;

    /// <summary>
    /// Currently connected device name, or null if none
    /// </summary>
    public string? CurrentDevice => _inputDevice?.Name;

    /// <summary>
    /// Gets the set of currently held processor chord names (e.g., "Uppercase", "Grayscale")
    /// </summary>
    public IReadOnlySet<string> HeldProcessorChords => _heldProcessorChords;

    /// <summary>
    /// Whether MIDI is currently active and listening
    /// </summary>
    public bool IsActive => _inputDevice != null;

    /// <summary>
    /// Gets the MidiPresets instance for accessing available presets
    /// </summary>
    public MidiPresets Presets => _presets;

    /// <summary>
    /// Sets edit mode - when enabled, raw note events are fired instead of actions
    /// </summary>
    public void SetEditMode(bool enabled)
    {
        _editMode = enabled;
        Console.WriteLine($"[MIDI] Edit mode: {enabled}");
    }

    public MidiManager(MidiSettings settings, MidiPresets presets, CommandRegistry commandRegistry)
    {
        _settings = settings;
        _presets = presets;
        _commandRegistry = commandRegistry;
        LoadMappings();
    }

    /// <summary>
    /// Start listening for MIDI input
    /// </summary>
    public void Start()
    {
        // Initialize device list
        _lastKnownDevices = new HashSet<string>(GetAvailableDevices());

        if (_settings.Enabled)
        {
            ConnectToDevice();
        }

        // Start polling for device changes (hot-plug support)
        _devicePollTimer = new Timer(PollForDeviceChanges, null,
            TimeSpan.FromMilliseconds(PollIntervalMs), TimeSpan.FromMilliseconds(PollIntervalMs));
    }

    /// <summary>
    /// Stop listening
    /// </summary>
    public void Stop()
    {
        _devicePollTimer?.Dispose();
        _devicePollTimer = null;
        DisconnectDevice();
    }

    /// <summary>
    /// Get list of available MIDI input devices
    /// </summary>
    public IReadOnlyList<string> GetAvailableDevices()
    {
        try
        {
            return InputDevice.GetAll().Select(d => d.Name).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MIDI] Error enumerating devices: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Select a specific device by name
    /// </summary>
    public bool SelectDevice(string deviceName)
    {
        lock (_lock)
        {
            _settings.DeviceName = deviceName;
            DisconnectDevice();
            return ConnectToDevice();
        }
    }

    /// <summary>
    /// Apply new settings (reloads mappings, reconnects if needed)
    /// </summary>
    public void ApplySettings(MidiSettings settings)
    {
        lock (_lock)
        {
            bool deviceChanged = _settings.DeviceName != settings.DeviceName;
            bool enabledChanged = _settings.Enabled != settings.Enabled;
            _settings = settings;
            LoadMappings();

            if (!_settings.Enabled)
            {
                DisconnectDevice();
                return;
            }

            if (deviceChanged || enabledChanged || !IsActive)
            {
                DisconnectDevice();
                ConnectToDevice();
            }
        }
    }

    private void LoadMappings()
    {
        // Start with preset mappings
        var preset = _presets.GetPreset(_settings.ActivePreset);
        _activeMappings = new Dictionary<string, MidiTrigger>(preset.Mappings);
        _copyModifier = preset.CopyModifier;
        _processorChords = new Dictionary<string, MidiTrigger>(preset.ProcessorChords);

        // Override with custom mappings
        foreach (var custom in _settings.CustomMappings)
        {
            _activeMappings[custom.Key] = custom.Value;
        }

        // Override copy modifier if set
        if (_settings.CopyModifier != null)
        {
            _copyModifier = _settings.CopyModifier;
        }

        // Override processor chords if set
        if (_settings.ProcessorChords != null)
        {
            foreach (var chord in _settings.ProcessorChords)
            {
                _processorChords[chord.Key] = chord.Value;
            }
        }

        Console.WriteLine($"[MIDI] Loaded {_activeMappings.Count} mappings, {_processorChords.Count} processor chords from preset '{_settings.ActivePreset}'");
    }

    private bool ConnectToDevice()
    {
        if (!_settings.Enabled) return false;

        try
        {
            InputDevice? device = null;

            var allDevices = InputDevice.GetAll().ToList();
            Console.WriteLine($"[MIDI] Available devices: {string.Join(", ", allDevices.Select(d => d.Name))}");

            if (!string.IsNullOrEmpty(_settings.DeviceName))
            {
                device = allDevices.FirstOrDefault(d => d.Name == _settings.DeviceName);
                if (device == null)
                {
                    Console.WriteLine($"[MIDI] Specified device '{_settings.DeviceName}' not found");
                }
            }

            // Auto-select first available if no specific device or specified device not found
            device ??= allDevices.FirstOrDefault();

            if (device == null)
            {
                Console.WriteLine("[MIDI] No MIDI devices found");
                DeviceChanged?.Invoke(this, new MidiDeviceEventArgs(null, false, "No MIDI device found"));
                return false;
            }

            _inputDevice = device;
            _inputDevice.EventReceived += OnMidiEventReceived;
            _inputDevice.StartEventsListening();

            Console.WriteLine($"[MIDI] Connected to: {_inputDevice.Name}");
            DeviceChanged?.Invoke(this, new MidiDeviceEventArgs(_inputDevice.Name, true));
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MIDI] Connection failed: {ex.Message}");
            DeviceChanged?.Invoke(this, new MidiDeviceEventArgs(null, false, ex.Message));
            return false;
        }
    }

    private void DisconnectDevice()
    {
        if (_inputDevice != null)
        {
            var oldDevice = _inputDevice.Name;
            try
            {
                _inputDevice.EventReceived -= OnMidiEventReceived;
                _inputDevice.StopEventsListening();
                _inputDevice.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MIDI] Error during disconnect: {ex.Message}");
            }
            finally
            {
                _inputDevice = null;
                _copyModifierHeld = false;
                if (_heldProcessorChords.Count > 0)
                {
                    _heldProcessorChords.Clear();
                    ProcessorChordsChanged?.Invoke(this, EventArgs.Empty);
                }
                Console.WriteLine($"[MIDI] Disconnected from: {oldDevice}");
                DeviceChanged?.Invoke(this, new MidiDeviceEventArgs(oldDevice, false));
            }
        }
    }

    private void PollForDeviceChanges(object? state)
    {
        if (_isDisposed) return;

        try
        {
            lock (_lock)
            {
                var currentDevices = new HashSet<string>(GetAvailableDevices());

                // Detect newly connected devices
                var newDevices = currentDevices.Except(_lastKnownDevices).ToList();
                foreach (var device in newDevices)
                {
                    Console.WriteLine($"[MIDI] New device detected: {device}");
                }

                // Detect disconnected devices
                var removedDevices = _lastKnownDevices.Except(currentDevices).ToList();
                foreach (var device in removedDevices)
                {
                    Console.WriteLine($"[MIDI] Device removed: {device}");
                }

                _lastKnownDevices = currentDevices;

                // Check if current device is still available
                if (_inputDevice != null)
                {
                    if (!currentDevices.Contains(_inputDevice.Name))
                    {
                        // Our device was disconnected
                        Console.WriteLine($"[MIDI] Connected device '{_inputDevice.Name}' was disconnected");
                        DisconnectDevice();
                    }
                }

                // Auto-connect to new device if we're not connected and MIDI is enabled
                if (_inputDevice == null && _settings.Enabled && currentDevices.Count > 0)
                {
                    // Prefer specified device if available
                    if (!string.IsNullOrEmpty(_settings.DeviceName) && currentDevices.Contains(_settings.DeviceName))
                    {
                        Console.WriteLine($"[MIDI] Preferred device '{_settings.DeviceName}' is available, connecting...");
                        ConnectToDevice();
                    }
                    // Or connect to any new device that just appeared
                    else if (newDevices.Count > 0)
                    {
                        Console.WriteLine($"[MIDI] New device available, connecting to: {newDevices[0]}");
                        ConnectToDevice();
                    }
                    // Or connect to any available device if we just started
                    else if (_lastKnownDevices.Count == 0 || removedDevices.Count > 0)
                    {
                        // Don't spam connection attempts - only try when device list changes
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MIDI] Poll error: {ex.Message}");
        }
    }

    private void OnMidiEventReceived(object? sender, MidiEventReceivedEventArgs e)
    {
        try
        {
            switch (e.Event)
            {
                case NoteOnEvent noteOn:
                    HandleNoteOn(noteOn);
                    break;

                case NoteOffEvent noteOff:
                    HandleNoteOff(noteOff);
                    break;

                case ControlChangeEvent cc:
                    HandleControlChange(cc);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MIDI] Event handling error: {ex.Message}");
        }
    }

    private void HandleNoteOn(NoteOnEvent noteOn)
    {
        int noteNumber = (int)noteOn.NoteNumber;
        int velocity = noteOn.Velocity;
        int channel = noteOn.Channel;

        // Note On with velocity 0 is treated as Note Off
        if (velocity == 0)
        {
            HandleNoteOff(new NoteOffEvent((Melanchall.DryWetMidi.Common.SevenBitNumber)noteNumber,
                (Melanchall.DryWetMidi.Common.SevenBitNumber)0) { Channel = (Melanchall.DryWetMidi.Common.FourBitNumber)channel });
            return;
        }

        // Check velocity threshold
        if (velocity < _settings.VelocityThreshold)
            return;

        Console.WriteLine($"[MIDI] Note On: note={noteNumber}, velocity={velocity}, channel={channel}");

        // Always fire raw events for visualization (debug window, etc.)
        RawNoteReceived?.Invoke(this, new MidiNoteEventArgs(noteNumber, velocity, channel, true));

        // In edit mode, don't process actions (just visualize)
        if (_editMode)
            return;

        // Check if this is the copy modifier
        if (IsTriggerMatch(_copyModifier, MidiTriggerType.NoteOn, channel, noteNumber, velocity))
        {
            _copyModifierHeld = true;
            Console.WriteLine("[MIDI] Copy modifier held");
            return;
        }

        // Check if this is a processor chord
        foreach (var (processorName, trigger) in _processorChords)
        {
            if (IsTriggerMatch(trigger, MidiTriggerType.NoteOn, channel, noteNumber, velocity))
            {
                if (_heldProcessorChords.Add(processorName))
                {
                    Console.WriteLine($"[MIDI] Processor chord held: {processorName}");
                    ProcessorChordsChanged?.Invoke(this, EventArgs.Empty);
                }
                return;
            }
        }

        // Find matching action
        foreach (var (actionName, trigger) in _activeMappings)
        {
            if (IsTriggerMatch(trigger, MidiTriggerType.NoteOn, channel, noteNumber, velocity))
            {
                FireAction(actionName);
                return;
            }
        }
    }

    private void HandleNoteOff(NoteOffEvent noteOff)
    {
        int noteNumber = (int)noteOff.NoteNumber;
        int channel = noteOff.Channel;

        // Always fire raw events for visualization
        RawNoteReceived?.Invoke(this, new MidiNoteEventArgs(noteNumber, 0, channel, false));

        // In edit mode, don't process actions
        if (_editMode)
            return;

        // Check if copy modifier released
        if (_copyModifier != null &&
            _copyModifier.Type == MidiTriggerType.NoteOn &&
            MatchesChannelAndNumber(_copyModifier, channel, noteNumber))
        {
            _copyModifierHeld = false;
            Console.WriteLine("[MIDI] Copy modifier released");
        }

        // Check if processor chord released
        foreach (var (processorName, trigger) in _processorChords)
        {
            if (trigger.Type == MidiTriggerType.NoteOn && MatchesChannelAndNumber(trigger, channel, noteNumber))
            {
                if (_heldProcessorChords.Remove(processorName))
                {
                    Console.WriteLine($"[MIDI] Processor chord released: {processorName}");
                    ProcessorChordsChanged?.Invoke(this, EventArgs.Empty);
                }
                return;
            }
        }
    }

    private void HandleControlChange(ControlChangeEvent cc)
    {
        int ccNumber = (int)cc.ControlNumber;
        int ccValue = cc.ControlValue;
        int channel = cc.Channel;

        // Get previous value for edge detection
        int key = (channel << 8) | ccNumber;
        _lastCcValues.TryGetValue(key, out int lastValue);
        _lastCcValues[key] = ccValue;

        foreach (var (actionName, trigger) in _activeMappings)
        {
            if (trigger.Type != MidiTriggerType.ControlChange)
                continue;

            if (!MatchesChannelAndNumber(trigger, channel, ccNumber))
                continue;

            // Check threshold crossing
            if (trigger.TriggerOnRise)
            {
                // Rising edge: was below threshold, now at or above
                if (lastValue < trigger.Threshold && ccValue >= trigger.Threshold)
                {
                    Console.WriteLine($"[MIDI] CC rising edge: cc={ccNumber}, value={ccValue}, threshold={trigger.Threshold}");
                    FireAction(actionName);
                    return;
                }
            }
            else
            {
                // Falling edge: was at or above threshold, now below
                if (lastValue >= trigger.Threshold && ccValue < trigger.Threshold)
                {
                    Console.WriteLine($"[MIDI] CC falling edge: cc={ccNumber}, value={ccValue}");
                    FireAction(actionName);
                    return;
                }
            }
        }
    }

    private bool IsTriggerMatch(MidiTrigger? trigger, MidiTriggerType type, int channel, int number, int value)
    {
        if (trigger == null || trigger.Type != type)
            return false;

        if (!MatchesChannelAndNumber(trigger, channel, number))
            return false;

        return value >= trigger.Threshold;
    }

    private bool MatchesChannelAndNumber(MidiTrigger trigger, int channel, int number)
    {
        if (trigger.Channel.HasValue && trigger.Channel.Value != channel)
            return false;

        return trigger.Number == number;
    }

    private void FireAction(string actionName)
    {
        // Apply copy modifier: if held, transform Paste -> Copy
        string effectiveAction = actionName;
        if (_copyModifierHeld && actionName.StartsWith("PasteFromSlot"))
        {
            // Extract slot number and transform to CopyToSlot
            var suffix = actionName.Substring("PasteFromSlot".Length);
            effectiveAction = "CopyToSlot" + suffix;
            Console.WriteLine($"[MIDI] Copy modifier active: {actionName} -> {effectiveAction}");
        }

        // Execute command via unified command registry
        Console.WriteLine($"[MIDI] Executing action: {effectiveAction}");
        if (!_commandRegistry.Execute(effectiveAction))
        {
            Console.WriteLine($"[MIDI] Unknown or failed action: {effectiveAction}");
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
    }
}

/// <summary>
/// Event args for MIDI device connection changes
/// </summary>
public class MidiDeviceEventArgs : EventArgs
{
    public string? DeviceName { get; }
    public bool IsConnected { get; }
    public string? ErrorMessage { get; }

    public MidiDeviceEventArgs(string? deviceName, bool isConnected, string? error = null)
    {
        DeviceName = deviceName;
        IsConnected = isConnected;
        ErrorMessage = error;
    }
}

/// <summary>
/// Event args for raw MIDI note events (used in edit mode)
/// </summary>
public class MidiNoteEventArgs : EventArgs
{
    public int NoteNumber { get; }
    public int Velocity { get; }
    public int Channel { get; }
    public bool IsNoteOn { get; }

    public MidiNoteEventArgs(int noteNumber, int velocity, int channel, bool isNoteOn)
    {
        NoteNumber = noteNumber;
        Velocity = velocity;
        Channel = channel;
        IsNoteOn = isNoteOn;
    }
}
