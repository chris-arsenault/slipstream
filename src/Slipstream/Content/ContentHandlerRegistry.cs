using System.Windows;
using Slipstream.Content.Handlers;

namespace Slipstream.Content;

/// <summary>
/// Central registry for content handlers.
/// Manages detection priority and provides handler lookup by type name.
/// </summary>
public class ContentHandlerRegistry
{
    private readonly List<IContentHandler> _handlers = new();
    private readonly Dictionary<string, IContentHandler> _handlersByType = new(StringComparer.OrdinalIgnoreCase);

    public ContentHandlerRegistry()
    {
        // Register built-in handlers in priority order (highest first)
        Register(new FileListContentHandler());   // Priority 100
        Register(new ImageContentHandler());      // Priority 90
        Register(new HtmlContentHandler());       // Priority 80
        Register(new RichTextContentHandler());   // Priority 70
        Register(new TextContentHandler());       // Priority 60
    }

    /// <summary>
    /// Registers a content handler.
    /// </summary>
    public void Register(IContentHandler handler)
    {
        _handlers.Add(handler);
        _handlers.Sort((a, b) => b.DetectionPriority.CompareTo(a.DetectionPriority));
        _handlersByType[handler.TypeName] = handler;
    }

    /// <summary>
    /// Gets a handler by type name.
    /// </summary>
    public IContentHandler? GetHandler(string typeName)
    {
        return _handlersByType.TryGetValue(typeName, out var handler) ? handler : null;
    }

    /// <summary>
    /// Gets a handler for the given content.
    /// </summary>
    public IContentHandler? GetHandler(IClipboardContent content)
    {
        return GetHandler(content.TypeName);
    }

    /// <summary>
    /// Detects content from an IDataObject by trying handlers in priority order.
    /// </summary>
    public IClipboardContent? DetectContent(IDataObject dataObject)
    {
        foreach (var handler in _handlers)
        {
            try
            {
                if (handler.CanDetect(dataObject))
                {
                    var content = handler.Detect(dataObject);
                    if (content != null)
                    {
                        Console.WriteLine($"[ContentHandlerRegistry] Detected {handler.TypeName} content");
                        return content;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ContentHandlerRegistry] Error in {handler.TypeName} detection: {ex.Message}");
            }
        }

        return null;
    }

    /// <summary>
    /// Writes content to a DataObject using the appropriate handler.
    /// </summary>
    public bool WriteToClipboard(IClipboardContent content, DataObject dataObject)
    {
        var handler = GetHandler(content.TypeName);
        if (handler == null)
        {
            Console.WriteLine($"[ContentHandlerRegistry] No handler for type: {content.TypeName}");
            return false;
        }

        try
        {
            handler.WriteToClipboard(content, dataObject);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ContentHandlerRegistry] Error writing {content.TypeName}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets all registered type names.
    /// </summary>
    public IEnumerable<string> GetTypeNames() => _handlersByType.Keys;
}
