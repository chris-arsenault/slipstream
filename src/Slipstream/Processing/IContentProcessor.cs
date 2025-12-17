using Slipstream.Content;

namespace Slipstream.Processing;

/// <summary>
/// Interface for content processors that transform clipboard content.
/// Processors can modify text, images, or other content types.
/// </summary>
public interface IContentProcessor
{
    /// <summary>
    /// Unique name identifying this processor (e.g., "Grayscale", "Uppercase").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description of what this processor does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Content type names this processor supports (e.g., ["Image"], ["Text", "RichText"]).
    /// </summary>
    string[] SupportedTypes { get; }

    /// <summary>
    /// Checks if this processor can process the given content.
    /// </summary>
    bool CanProcess(IClipboardContent content);

    /// <summary>
    /// Processes the content and returns the transformed result.
    /// Returns null if processing fails.
    /// </summary>
    IClipboardContent? Process(IClipboardContent content, ProcessorOptions? options = null);
}

/// <summary>
/// Options for content processors.
/// </summary>
public class ProcessorOptions
{
    /// <summary>
    /// Generic parameters dictionary for processor-specific settings.
    /// </summary>
    public Dictionary<string, object> Parameters { get; } = new();

    /// <summary>
    /// Gets a parameter value with type conversion.
    /// </summary>
    public T? Get<T>(string key, T? defaultValue = default)
    {
        if (Parameters.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Sets a parameter value.
    /// </summary>
    public ProcessorOptions Set(string key, object value)
    {
        Parameters[key] = value;
        return this;
    }
}
