using Slipstream.Content;

namespace Slipstream.Processing;

/// <summary>
/// Where the result of processor execution should go.
/// </summary>
public enum ProcessorOutputMode
{
    /// <summary>
    /// Result overwrites the source slot.
    /// </summary>
    Replace,

    /// <summary>
    /// Result is pasted to the active window immediately.
    /// </summary>
    Paste,

    /// <summary>
    /// Result goes to the next available slot.
    /// </summary>
    NewSlot
}

/// <summary>
/// Represents an ordered sequence of processors to apply to content.
/// </summary>
public class ProcessorPipeline
{
    private readonly List<string> _processorNames = new();

    /// <summary>
    /// The ordered list of processor names in this pipeline.
    /// </summary>
    public IReadOnlyList<string> ProcessorNames => _processorNames;

    /// <summary>
    /// Number of processors in the pipeline.
    /// </summary>
    public int Count => _processorNames.Count;

    /// <summary>
    /// Whether the pipeline is empty.
    /// </summary>
    public bool IsEmpty => _processorNames.Count == 0;

    /// <summary>
    /// Adds a processor to the end of the pipeline.
    /// </summary>
    public void Add(string processorName)
    {
        if (!string.IsNullOrEmpty(processorName))
        {
            _processorNames.Add(processorName);
        }
    }

    /// <summary>
    /// Removes the last processor from the pipeline.
    /// </summary>
    public void RemoveLast()
    {
        if (_processorNames.Count > 0)
        {
            _processorNames.RemoveAt(_processorNames.Count - 1);
        }
    }

    /// <summary>
    /// Clears all processors from the pipeline.
    /// </summary>
    public void Clear()
    {
        _processorNames.Clear();
    }

    /// <summary>
    /// Executes all processors in sequence on the given content.
    /// </summary>
    /// <param name="registry">The processor registry to use.</param>
    /// <param name="content">The content to process.</param>
    /// <param name="options">Optional processor options.</param>
    /// <returns>The final processed content, or null if any processor fails.</returns>
    public IClipboardContent? Execute(ProcessorRegistry registry, IClipboardContent content, ProcessorOptions? options = null)
    {
        if (registry == null || content == null)
            return null;

        var current = content;

        foreach (var processorName in _processorNames)
        {
            var processor = registry.GetProcessor(processorName);
            if (processor == null)
            {
                Console.WriteLine($"[ProcessorPipeline] Unknown processor: {processorName}");
                return null;
            }

            if (!processor.CanProcess(current))
            {
                Console.WriteLine($"[ProcessorPipeline] Processor '{processorName}' cannot process {current.TypeName}");
                return null;
            }

            try
            {
                var result = processor.Process(current, options);
                if (result == null)
                {
                    Console.WriteLine($"[ProcessorPipeline] Processor '{processorName}' returned null");
                    return null;
                }
                current = result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessorPipeline] Error in '{processorName}': {ex.Message}");
                return null;
            }
        }

        return current;
    }

    /// <summary>
    /// Validates that all processors in the pipeline exist and can process the given content type.
    /// </summary>
    /// <param name="registry">The processor registry to check against.</param>
    /// <param name="content">The initial content to validate against.</param>
    /// <returns>True if the pipeline is valid, false otherwise.</returns>
    public bool Validate(ProcessorRegistry registry, IClipboardContent content)
    {
        if (registry == null || content == null || IsEmpty)
            return false;

        var currentType = content.TypeName;

        foreach (var processorName in _processorNames)
        {
            var processor = registry.GetProcessor(processorName);
            if (processor == null || !processor.SupportedTypes.Contains(currentType))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns a string representation of the pipeline (e.g., "Trim → Upper → StripFormat").
    /// </summary>
    public override string ToString()
    {
        if (IsEmpty)
            return "(none)";

        return string.Join(" → ", _processorNames);
    }
}
