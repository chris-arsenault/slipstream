using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Slipstream.Models;

public class ClipboardSlot
{
    public int Index { get; set; }
    public ClipboardType Type { get; set; } = ClipboardType.Empty;
    public DateTime Timestamp { get; set; }
    public string? Label { get; set; }
    public bool IsLocked { get; set; }

    // Content storage - only one will be populated based on Type
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
    public string Preview
    {
        get
        {
            return Type switch
            {
                ClipboardType.Text => TruncateText(TextContent, 50),
                ClipboardType.RichText => TruncateText(TextContent ?? RichTextContent, 50),
                ClipboardType.Html => TruncateText(TextContent ?? HtmlContent, 50),
                ClipboardType.Image => "[Image]",
                ClipboardType.FileList => FileListContent?.Length > 0
                    ? $"[{FileListContent.Length} file(s)]"
                    : "[Files]",
                _ => ""
            };
        }
    }

    [JsonIgnore]
    public string TypeGlyph
    {
        get
        {
            return Type switch
            {
                ClipboardType.Text => "T",
                ClipboardType.RichText => "R",
                ClipboardType.Html => "H",
                ClipboardType.Image => "I",
                ClipboardType.FileList => "F",
                _ => "-"
            };
        }
    }

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
    /// </summary>
    private string ComputeContentHash()
    {
        if (Type == ClipboardType.Empty)
            return string.Empty;

        using var sha = SHA256.Create();
        byte[] hashBytes;

        switch (Type)
        {
            case ClipboardType.Text:
                hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(TextContent ?? ""));
                break;

            case ClipboardType.RichText:
                var rtfContent = (TextContent ?? "") + (RichTextContent ?? "");
                hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(rtfContent));
                break;

            case ClipboardType.Html:
                var htmlContent = (TextContent ?? "") + (HtmlContent ?? "");
                hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(htmlContent));
                break;

            case ClipboardType.Image:
                // For images, hash the raw bytes directly
                hashBytes = ImageContent != null
                    ? sha.ComputeHash(ImageContent)
                    : sha.ComputeHash(Array.Empty<byte>());
                break;

            case ClipboardType.FileList:
                var fileListContent = FileListContent != null
                    ? string.Join("|", FileListContent)
                    : "";
                hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(fileListContent));
                break;

            default:
                return string.Empty;
        }

        return Convert.ToBase64String(hashBytes);
    }

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
}
