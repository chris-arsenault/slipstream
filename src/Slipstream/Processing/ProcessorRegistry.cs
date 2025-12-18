using Slipstream.Content;
using Slipstream.Processing.Processors;

namespace Slipstream.Processing;

/// <summary>
/// Central registry for content processors.
/// </summary>
public class ProcessorRegistry
{
    private readonly Dictionary<string, IContentProcessor> _processors = new(StringComparer.OrdinalIgnoreCase);

    public ProcessorRegistry()
    {
        // Register built-in text processors
        Register(new TrimWhitespaceProcessor());
        Register(new UppercaseProcessor());
        Register(new LowercaseProcessor());
        Register(new ReverseTextProcessor());
        Register(new RemoveLineBreaksProcessor());
        Register(new StripFormattingProcessor());

        // Register built-in image processors
        Register(new GrayscaleProcessor());
        Register(new InvertColorsProcessor());
        Register(new RotateClockwiseProcessor());
        Register(new FlipHorizontalProcessor());
    }

    /// <summary>
    /// Registers a content processor.
    /// </summary>
    public void Register(IContentProcessor processor)
    {
        _processors[processor.Name] = processor;
    }

    /// <summary>
    /// Gets a processor by name.
    /// </summary>
    public IContentProcessor? GetProcessor(string name)
    {
        return _processors.TryGetValue(name, out var processor) ? processor : null;
    }

    /// <summary>
    /// Gets all processors that can handle the given content.
    /// </summary>
    public IEnumerable<IContentProcessor> GetProcessorsFor(IClipboardContent content)
    {
        return _processors.Values.Where(p => p.CanProcess(content));
    }

    /// <summary>
    /// Gets all registered processor names.
    /// </summary>
    public IEnumerable<string> GetProcessorNames() => _processors.Keys;

    /// <summary>
    /// Gets all registered processors.
    /// </summary>
    public IEnumerable<IContentProcessor> GetAllProcessors() => _processors.Values;

    /// <summary>
    /// Executes a set of processors in priority order on the given content.
    /// Returns the transformed content, or null if any processor fails.
    /// </summary>
    public IClipboardContent? ExecuteActiveSet(IClipboardContent content, IReadOnlySet<string> activeProcessors)
    {
        if (activeProcessors.Count == 0)
            return content;

        var current = content;

        // Get definitions for active processors, sorted by priority
        var activeDefinitions = ProcessorDefinitions.All
            .Where(d => activeProcessors.Contains(d.Name))
            .OrderBy(d => d.Priority);

        foreach (var definition in activeDefinitions)
        {
            var processor = GetProcessor(definition.Name);
            if (processor == null)
            {
                Console.WriteLine($"[ProcessorRegistry] Processor not found: {definition.Name}");
                continue;
            }

            if (!processor.CanProcess(current))
            {
                Console.WriteLine($"[ProcessorRegistry] Processor '{definition.Name}' cannot process {current.TypeName}, skipping");
                continue;
            }

            try
            {
                var result = processor.Process(current);
                if (result == null)
                {
                    Console.WriteLine($"[ProcessorRegistry] Processor '{definition.Name}' returned null");
                    return null;
                }

                Console.WriteLine($"[ProcessorRegistry] Applied '{definition.Name}' (priority {definition.Priority}): {current.TypeName} -> {result.TypeName}");
                current = result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessorRegistry] Error in '{definition.Name}': {ex.Message}");
                return null;
            }
        }

        return current;
    }

    /// <summary>
    /// Processes content using the named processor.
    /// Returns the processed content, or null if processing fails.
    /// </summary>
    public IClipboardContent? Process(string processorName, IClipboardContent content, ProcessorOptions? options = null)
    {
        var processor = GetProcessor(processorName);
        if (processor == null)
        {
            Console.WriteLine($"[ProcessorRegistry] Unknown processor: {processorName}");
            return null;
        }

        if (!processor.CanProcess(content))
        {
            Console.WriteLine($"[ProcessorRegistry] Processor '{processorName}' cannot process {content.TypeName}");
            return null;
        }

        try
        {
            var result = processor.Process(content, options);
            if (result != null)
            {
                Console.WriteLine($"[ProcessorRegistry] Processed with '{processorName}': {content.TypeName} -> {result.TypeName}");
            }
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ProcessorRegistry] Error in '{processorName}': {ex.Message}");
            return null;
        }
    }
}
