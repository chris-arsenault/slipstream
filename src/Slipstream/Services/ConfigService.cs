using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Slipstream.Models;

namespace Slipstream.Services;

public class ConfigService
{
    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Slipstream"
    );

    private static readonly string SettingsPath = Path.Combine(AppDataPath, "config.json");
    private static readonly string SlotsPath = Path.Combine(AppDataPath, "slots.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private Timer? _saveDebounceTimer;
    private List<ClipboardSlot>? _pendingSlots;
    private readonly object _saveLock = new();

    public ConfigService()
    {
        EnsureDirectoryExists();
    }

    private static void EnsureDirectoryExists()
    {
        if (!Directory.Exists(AppDataPath))
        {
            Directory.CreateDirectory(AppDataPath);
        }
    }

    public AppSettings LoadSettings()
    {
        var defaults = new AppSettings();

        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

                if (settings != null)
                {
                    // Merge hotkey bindings - add any missing default bindings
                    // This ensures new hotkeys added in updates are available
                    foreach (var defaultBinding in defaults.HotkeyBindings)
                    {
                        if (!settings.HotkeyBindings.ContainsKey(defaultBinding.Key))
                        {
                            settings.HotkeyBindings[defaultBinding.Key] = defaultBinding.Value;
                        }
                    }

                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
        }

        return defaults;
    }

    public void SaveSettings(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
    }

    public List<ClipboardSlot> LoadSlots(int slotCount)
    {
        try
        {
            if (File.Exists(SlotsPath))
            {
                var json = File.ReadAllText(SlotsPath);
                var slots = JsonSerializer.Deserialize<List<ClipboardSlot>>(json, JsonOptions);

                if (slots != null)
                {
                    // Ensure we have the right number of slots
                    while (slots.Count < slotCount)
                    {
                        slots.Add(new ClipboardSlot { Index = slots.Count });
                    }

                    // Trim excess slots (but keep locked ones)
                    while (slots.Count > slotCount)
                    {
                        var lastSlot = slots[^1];
                        if (!lastSlot.IsLocked)
                        {
                            slots.RemoveAt(slots.Count - 1);
                        }
                        else
                        {
                            break;
                        }
                    }

                    // Ensure indices are correct
                    for (int i = 0; i < slots.Count; i++)
                    {
                        slots[i].Index = i;
                    }

                    return slots;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading slots: {ex.Message}");
        }

        // Create default empty slots
        var defaultSlots = new List<ClipboardSlot>();
        for (int i = 0; i < slotCount; i++)
        {
            defaultSlots.Add(new ClipboardSlot { Index = i });
        }
        return defaultSlots;
    }

    public void SaveSlots(List<ClipboardSlot> slots)
    {
        try
        {
            var json = JsonSerializer.Serialize(slots, JsonOptions);
            File.WriteAllText(SlotsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving slots: {ex.Message}");
        }
    }

    public void SaveSlotsDebounced(List<ClipboardSlot> slots, int delayMs = 1000)
    {
        lock (_saveLock)
        {
            _pendingSlots = slots.ToList(); // Clone to avoid modification during save

            _saveDebounceTimer?.Dispose();
            _saveDebounceTimer = new Timer(_ =>
            {
                List<ClipboardSlot>? toSave;
                lock (_saveLock)
                {
                    toSave = _pendingSlots;
                    _pendingSlots = null;
                }

                if (toSave != null)
                {
                    SaveSlots(toSave);
                }
            }, null, delayMs, Timeout.Infinite);
        }
    }

    public void Flush()
    {
        lock (_saveLock)
        {
            _saveDebounceTimer?.Dispose();
            _saveDebounceTimer = null;

            if (_pendingSlots != null)
            {
                SaveSlots(_pendingSlots);
                _pendingSlots = null;
            }
        }
    }

    /// <summary>
    /// Resets hotkey bindings to defaults by replacing the saved hotkeys with new defaults.
    /// </summary>
    public void ResetHotkeysToDefaults(AppSettings settings)
    {
        var defaults = new AppSettings();
        settings.HotkeyBindings = new Dictionary<string, HotkeyBinding>(defaults.HotkeyBindings);
        SaveSettings(settings);
    }
}
