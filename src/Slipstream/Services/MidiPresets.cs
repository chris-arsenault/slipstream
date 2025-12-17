using Slipstream.Models;

namespace Slipstream.Services;

/// <summary>
/// Manages MIDI control scheme presets loaded from JSON config files.
/// Presets are stored in %APPDATA%/Slipstream/midi-presets/*.json
/// </summary>
public class MidiPresets
{
    private readonly ConfigService _configService;
    private Dictionary<string, MidiControlScheme> _presets = new();
    private bool _loaded;

    public MidiPresets(ConfigService configService)
    {
        _configService = configService;
    }

    /// <summary>
    /// Ensures presets are loaded from disk
    /// </summary>
    private void EnsureLoaded()
    {
        if (_loaded) return;
        Reload();
    }

    /// <summary>
    /// Reloads all presets from the config directory
    /// </summary>
    public void Reload()
    {
        _presets.Clear();
        var loadedPresets = _configService.LoadAllMidiPresets();
        foreach (var preset in loadedPresets)
        {
            _presets[preset.Name] = preset;
        }
        _loaded = true;
        Console.WriteLine($"[MidiPresets] Loaded {_presets.Count} presets");
    }

    /// <summary>
    /// Gets a preset by name, or the first available preset as fallback
    /// </summary>
    public MidiControlScheme GetPreset(string name)
    {
        EnsureLoaded();

        if (_presets.TryGetValue(name, out var preset))
        {
            return preset;
        }

        // Fallback to first available preset
        if (_presets.Count > 0)
        {
            var fallback = _presets.Values.First();
            Console.WriteLine($"[MidiPresets] Preset '{name}' not found, falling back to '{fallback.Name}'");
            return fallback;
        }

        // No presets at all - return empty scheme
        Console.WriteLine($"[MidiPresets] No presets available, returning empty scheme");
        return new MidiControlScheme
        {
            Name = "Empty",
            Description = "No mappings configured",
            Mappings = new Dictionary<string, MidiTrigger>()
        };
    }

    /// <summary>
    /// Gets all loaded presets
    /// </summary>
    public IReadOnlyList<MidiControlScheme> GetAllPresets()
    {
        EnsureLoaded();
        return _presets.Values.ToList();
    }

    /// <summary>
    /// Gets names of all loaded presets
    /// </summary>
    public IReadOnlyList<string> GetPresetNames()
    {
        EnsureLoaded();
        return _presets.Keys.ToList();
    }

    /// <summary>
    /// Checks if a preset exists
    /// </summary>
    public bool HasPreset(string name)
    {
        EnsureLoaded();
        return _presets.ContainsKey(name);
    }
}
