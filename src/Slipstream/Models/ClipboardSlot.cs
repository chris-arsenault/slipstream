using System.Text.Json.Serialization;
using Slipstream.Content;

namespace Slipstream.Models;

public class ClipboardSlot
{
    public int Index { get; set; }
    public ClipboardType Type { get; set; } = ClipboardType.Empty;
    public DateTime Timestamp { get; set; }
    public string? Label { get; set; }
    public bool IsLocked { get; set; }

    // Content storage - only one will be populated based on Type
    // These are retained for JSON serialization compatibility
    public string? TextContent { get; set; }
    public string? RichTextContent { get; set; }
    public string? HtmlContent { get; set; }
    public byte[]? ImageContent { get; set; }
    public string[]? FileListContent { get; set; }

    // Cached content hash for duplicate detection (computed on set, not serialized)
    private string? _contentHash;

    /// <summary>
    /// Gets a hash of the slot's content for duplicate detection.
    /// Computed lazily and cached until content changes.
    /// </summary>
    [JsonIgnore]
    public string ContentHash => _contentHash ??= ComputeContentHash();

    [JsonIgnore]
    public bool HasContent => Type != ClipboardType.Empty;

    [JsonIgnore]
    public string Preview => GetContent()?.Preview ?? "";

    [JsonIgnore]
    public string TypeGlyph => GetContent()?.Glyph ?? "-";

    public void Clear()
    {
        Type = ClipboardType.Empty;
        Timestamp = default;
        Label = null;
        TextContent = null;
        RichTextContent = null;
        HtmlContent = null;
        ImageContent = null;
        FileListContent = null;
        _contentHash = null; // Invalidate cached hash
    }

    public void SetText(string text)
    {
        Clear();
        Type = ClipboardType.Text;
        TextContent = text;
        Timestamp = DateTime.UtcNow;
    }

    public void SetRichText(string text, string? richText)
    {
        Clear();
        Type = ClipboardType.RichText;
        TextContent = text;
        RichTextContent = richText;
        Timestamp = DateTime.UtcNow;
    }

    public void SetHtml(string text, string? html)
    {
        Clear();
        Type = ClipboardType.Html;
        TextContent = text;
        HtmlContent = html;
        Timestamp = DateTime.UtcNow;
    }

    public void SetImage(byte[] imageData)
    {
        Clear();
        Type = ClipboardType.Image;
        ImageContent = imageData;
        Timestamp = DateTime.UtcNow;
    }

    public void SetFileList(string[] files)
    {
        Clear();
        Type = ClipboardType.FileList;
        FileListContent = files;
        Timestamp = DateTime.UtcNow;
    }

    private static string TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";

        // Replace newlines with spaces for preview
        var normalized = text.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");

        if (normalized.Length <= maxLength) return normalized;
        return normalized[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// Computes a hash of the slot's content for duplicate detection.
    /// Delegates to the polymorphic IClipboardContent.ContentHash.
    /// </summary>
    private string ComputeContentHash() => GetContent()?.ContentHash ?? string.Empty;

    /// <summary>
    /// Checks if this slot has the same content as another slot.
    /// </summary>
    public bool HasSameContent(ClipboardSlot other)
    {
        if (other == null) return false;
        if (Type != other.Type) return false;
        if (Type == ClipboardType.Empty) return true;

        return ContentHash == other.ContentHash;
    }

    /// <summary>
    /// Gets the content as an IClipboardContent instance.
    /// </summary>
    public IClipboardContent? GetContent()
    {
        return Type switch
        {
            ClipboardType.Text => new TextContent(TextContent ?? ""),
            ClipboardType.RichText => new Content.RichTextContent(TextContent ?? "", RichTextContent),
            ClipboardType.Html => new Content.HtmlContent(TextContent ?? "", HtmlContent),
            ClipboardType.Image when ImageContent != null => new Content.ImageContent(ImageContent),
            ClipboardType.FileList when FileListContent != null => new Content.FileListContent(FileListContent),
            _ => null
        };
    }

    /// <summary>
    /// Sets the slot content from an IClipboardContent instance.
    /// Uses polymorphic dispatch - each content type knows how to populate a slot.
    /// </summary>
    public void SetContent(IClipboardContent content)
    {
        Clear();
        Timestamp = content.Timestamp;
        content.PopulateSlot(this);
    }
}
