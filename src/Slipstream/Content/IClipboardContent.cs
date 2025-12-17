using System.Security.Cryptography;
using System.Text;
using Slipstream.Models;

namespace Slipstream.Content;

/// <summary>
/// Represents clipboard content of any type.
/// All content types implement this interface, enabling polymorphic handling.
/// </summary>
public interface IClipboardContent
{
    /// <summary>
    /// Type identifier for this content (e.g., "Text", "Image", "FileList").
    /// </summary>
    string TypeName { get; }

    /// <summary>
    /// Single-character glyph representing this content type for UI display.
    /// </summary>
    string Glyph { get; }

    /// <summary>
    /// Human-readable preview of the content (truncated if necessary).
    /// </summary>
    string Preview { get; }

    /// <summary>
    /// Hash of the content for duplicate detection.
    /// </summary>
    string ContentHash { get; }

    /// <summary>
    /// When this content was captured.
    /// </summary>
    DateTime Timestamp { get; }

    /// <summary>
    /// Checks if this content is equivalent to another content instance.
    /// </summary>
    bool HasSameContent(IClipboardContent? other);

    /// <summary>
    /// Populates a ClipboardSlot with this content's data.
    /// Each content type knows how to write itself to a slot.
    /// </summary>
    void PopulateSlot(ClipboardSlot slot);
}

/// <summary>
/// Base class for clipboard content implementations with common functionality.
/// </summary>
public abstract class ClipboardContentBase : IClipboardContent
{
    public abstract string TypeName { get; }
    public abstract string Glyph { get; }
    public abstract string Preview { get; }
    public DateTime Timestamp { get; }

    private string? _contentHash;
    public string ContentHash => _contentHash ??= ComputeHash();

    protected ClipboardContentBase()
    {
        Timestamp = DateTime.UtcNow;
    }

    protected abstract byte[] GetHashableBytes();

    private string ComputeHash()
    {
        using var sha = SHA256.Create();
        var bytes = GetHashableBytes();
        var hashBytes = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hashBytes);
    }

    public bool HasSameContent(IClipboardContent? other)
    {
        if (other == null) return false;
        if (TypeName != other.TypeName) return false;
        return ContentHash == other.ContentHash;
    }

    /// <summary>
    /// Populates a ClipboardSlot with this content's data.
    /// </summary>
    public abstract void PopulateSlot(ClipboardSlot slot);

    protected static string TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";

        // Replace newlines with spaces for preview
        var normalized = text.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");

        if (normalized.Length <= maxLength) return normalized;
        return normalized[..(maxLength - 3)] + "...";
    }
}

/// <summary>
/// Plain text clipboard content.
/// </summary>
public class TextContent : ClipboardContentBase
{
    public string Text { get; }

    public override string TypeName => "Text";
    public override string Glyph => "T";
    public override string Preview => TruncateText(Text, 50);

    public TextContent(string text)
    {
        Text = text;
    }

    protected override byte[] GetHashableBytes() =>
        Encoding.UTF8.GetBytes(Text ?? "");

    public override void PopulateSlot(ClipboardSlot slot)
    {
        slot.Type = ClipboardType.Text;
        slot.TextContent = Text;
    }
}

/// <summary>
/// Rich text (RTF) clipboard content.
/// </summary>
public class RichTextContent : ClipboardContentBase
{
    public string PlainText { get; }
    public string? RichText { get; }

    public override string TypeName => "RichText";
    public override string Glyph => "R";
    public override string Preview => TruncateText(PlainText ?? RichText, 50);

    public RichTextContent(string plainText, string? richText)
    {
        PlainText = plainText;
        RichText = richText;
    }

    protected override byte[] GetHashableBytes() =>
        Encoding.UTF8.GetBytes((PlainText ?? "") + (RichText ?? ""));

    public override void PopulateSlot(ClipboardSlot slot)
    {
        slot.Type = ClipboardType.RichText;
        slot.TextContent = PlainText;
        slot.RichTextContent = RichText;
    }
}

/// <summary>
/// HTML clipboard content.
/// </summary>
public class HtmlContent : ClipboardContentBase
{
    public string PlainText { get; }
    public string? Html { get; }

    public override string TypeName => "Html";
    public override string Glyph => "H";
    public override string Preview => TruncateText(PlainText ?? Html, 50);

    public HtmlContent(string plainText, string? html)
    {
        PlainText = plainText;
        Html = html;
    }

    protected override byte[] GetHashableBytes() =>
        Encoding.UTF8.GetBytes((PlainText ?? "") + (Html ?? ""));

    public override void PopulateSlot(ClipboardSlot slot)
    {
        slot.Type = ClipboardType.Html;
        slot.TextContent = PlainText;
        slot.HtmlContent = Html;
    }
}

/// <summary>
/// Image clipboard content stored as PNG bytes.
/// </summary>
public class ImageContent : ClipboardContentBase
{
    public byte[] ImageData { get; }

    public override string TypeName => "Image";
    public override string Glyph => "I";
    public override string Preview => "[Image]";

    public ImageContent(byte[] imageData)
    {
        ImageData = imageData;
    }

    protected override byte[] GetHashableBytes() => ImageData ?? Array.Empty<byte>();

    public override void PopulateSlot(ClipboardSlot slot)
    {
        slot.Type = ClipboardType.Image;
        slot.ImageContent = ImageData;
    }
}

/// <summary>
/// File list clipboard content.
/// </summary>
public class FileListContent : ClipboardContentBase
{
    public string[] FilePaths { get; }

    public override string TypeName => "FileList";
    public override string Glyph => "F";
    public override string Preview => FilePaths?.Length > 0
        ? $"[{FilePaths.Length} file(s)]"
        : "[Files]";

    public FileListContent(string[] filePaths)
    {
        FilePaths = filePaths;
    }

    protected override byte[] GetHashableBytes() =>
        Encoding.UTF8.GetBytes(FilePaths != null ? string.Join("|", FilePaths) : "");

    public override void PopulateSlot(ClipboardSlot slot)
    {
        slot.Type = ClipboardType.FileList;
        slot.FileListContent = FilePaths;
    }
}

/// <summary>
/// Represents empty/cleared clipboard content.
/// </summary>
public class EmptyContent : IClipboardContent
{
    public static readonly EmptyContent Instance = new();

    public string TypeName => "Empty";
    public string Glyph => "-";
    public string Preview => "";
    public string ContentHash => "";
    public DateTime Timestamp => DateTime.MinValue;

    private EmptyContent() { }

    public bool HasSameContent(IClipboardContent? other) =>
        other is EmptyContent;

    public void PopulateSlot(ClipboardSlot slot)
    {
        slot.Type = ClipboardType.Empty;
    }
}
