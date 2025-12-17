using System.Windows;
using SkiaSharp;

namespace Slipstream.Content;

/// <summary>
/// Handles detection, clipboard I/O, and rendering for a specific content type.
/// Each content type (Text, Image, etc.) has its own handler implementation.
/// </summary>
public interface IContentHandler
{
    /// <summary>
    /// Type name this handler supports (e.g., "Text", "Image").
    /// </summary>
    string TypeName { get; }

    /// <summary>
    /// Detection priority (higher values checked first).
    /// FileList should be highest, then Image, Html, RichText, Text.
    /// </summary>
    int DetectionPriority { get; }

    /// <summary>
    /// Checks if this handler can detect content from the given IDataObject.
    /// </summary>
    bool CanDetect(IDataObject dataObject);

    /// <summary>
    /// Detects and extracts content from the clipboard IDataObject.
    /// Returns null if content cannot be extracted.
    /// </summary>
    IClipboardContent? Detect(IDataObject dataObject);

    /// <summary>
    /// Writes content to a DataObject for pasting.
    /// </summary>
    void WriteToClipboard(IClipboardContent content, DataObject dataObject);

    /// <summary>
    /// Renders a preview of the content to a SkiaSharp canvas.
    /// Used by HudRenderer for slot display.
    /// </summary>
    void RenderPreview(SKCanvas canvas, IClipboardContent content, SKRect bounds, SKPaint textPaint);
}
