namespace Slipstream.Settings;

using System.IO;
using System.Text.Json;
using Slipstream.Settings.Sections;

/// <summary>
/// Manages loading, saving, and coordinating settings sections.
/// </summary>
public class SettingsManager
{
    private readonly Dictionary<string, ISettingsSection> _sections = new();
    private readonly string _settingsPath;

    /// <summary>
    /// General application settings.
    /// </summary>
    public GeneralSettings General { get; }

    /// <summary>
    /// HUD appearance and behavior settings.
    /// </summary>
    public HudSettings Hud { get; }

    /// <summary>
    /// Hotkey binding settings.
    /// </summary>
    public HotkeySettings Hotkeys { get; }

    /// <summary>
    /// Creates a new SettingsManager with default settings.
    /// </summary>
    public SettingsManager() : this(GetDefaultSettingsPath())
    {
    }

    /// <summary>
    /// Creates a new SettingsManager with settings at the specified path.
    /// </summary>
    public SettingsManager(string settingsPath)
    {
        _settingsPath = settingsPath;

        // Initialize sections with defaults
        General = new GeneralSettings();
        Hud = new HudSettings();
        Hotkeys = new HotkeySettings();

        // Register sections
        RegisterSection(General);
        RegisterSection(Hud);
        RegisterSection(Hotkeys);
    }

    /// <summary>
    /// Registers a settings section.
    /// </summary>
    public void RegisterSection(ISettingsSection section)
    {
        _sections[section.SectionName] = section;
    }

    /// <summary>
    /// Gets a settings section by name.
    /// </summary>
    public ISettingsSection? GetSection(string name)
    {
        return _sections.TryGetValue(name, out var section) ? section : null;
    }

    /// <summary>
    /// Gets a typed settings section.
    /// </summary>
    public T? GetSection<T>() where T : class, ISettingsSection
    {
        foreach (var section in _sections.Values)
        {
            if (section is T typed)
            {
                return typed;
            }
        }
        return null;
    }

    /// <summary>
    /// Gets all registered sections.
    /// </summary>
    public IEnumerable<ISettingsSection> GetAllSections() => _sections.Values;

    /// <summary>
    /// Applies defaults to all sections.
    /// </summary>
    public void ApplyAllDefaults()
    {
        foreach (var section in _sections.Values)
        {
            section.ApplyDefaults();
        }
    }

    /// <summary>
    /// Loads settings from the settings file.
    /// </summary>
    public async Task LoadAsync()
    {
        if (!File.Exists(_settingsPath))
        {
            ApplyAllDefaults();
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_settingsPath);
            var document = JsonDocument.Parse(json);

            LoadSectionFromJson(document, General);
            LoadSectionFromJson(document, Hud);
            LoadSectionFromJson(document, Hotkeys);
        }
        catch (Exception)
        {
            // If loading fails, use defaults
            ApplyAllDefaults();
        }
    }

    /// <summary>
    /// Saves all settings to the settings file.
    /// </summary>
    public async Task SaveAsync()
    {
        var settingsObject = new Dictionary<string, object>
        {
            ["General"] = General,
            ["HUD"] = Hud,
            ["Hotkeys"] = Hotkeys
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(settingsObject, options);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(_settingsPath, json);
    }

    /// <summary>
    /// Loads settings synchronously.
    /// </summary>
    public void Load()
    {
        LoadAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Saves settings synchronously.
    /// </summary>
    public void Save()
    {
        SaveAsync().GetAwaiter().GetResult();
    }

    private static void LoadSectionFromJson<T>(JsonDocument document, T section) where T : ISettingsSection
    {
        if (document.RootElement.TryGetProperty(section.SectionName, out var sectionElement))
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var sectionJson = sectionElement.GetRawText();
            var loaded = JsonSerializer.Deserialize<T>(sectionJson, options);

            if (loaded != null)
            {
                // Copy properties from loaded to section
                CopyProperties(loaded, section);
            }
        }
    }

    private static void CopyProperties<T>(T source, T destination)
    {
        var properties = typeof(T).GetProperties()
            .Where(p => p.CanRead && p.CanWrite && p.Name != "SectionName");

        foreach (var property in properties)
        {
            var value = property.GetValue(source);
            property.SetValue(destination, value);
        }
    }

    private static string GetDefaultSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Slipstream", "settings.json");
    }
}
