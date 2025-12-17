using Slipstream.Content;

namespace Slipstream.Processing.Processors;

/// <summary>
/// Trims leading and trailing whitespace from text content.
/// </summary>
public class TrimWhitespaceProcessor : IContentProcessor
{
    public string Name => "TrimWhitespace";
    public string Description => "Remove leading and trailing whitespace";
    public string[] SupportedTypes => ["Text", "RichText", "Html"];

    public bool CanProcess(IClipboardContent content) =>
        content is TextContent or RichTextContent or HtmlContent;

    public IClipboardContent? Process(IClipboardContent content, ProcessorOptions? options = null)
    {
        return content switch
        {
            TextContent text => new TextContent(text.Text.Trim()),
            RichTextContent rtf => new RichTextContent(rtf.PlainText.Trim(), rtf.RichText),
            HtmlContent html => new HtmlContent(html.PlainText.Trim(), html.Html),
            _ => null
        };
    }
}

/// <summary>
/// Converts text to uppercase.
/// </summary>
public class UppercaseProcessor : IContentProcessor
{
    public string Name => "Uppercase";
    public string Description => "Convert text to UPPERCASE";
    public string[] SupportedTypes => ["Text"];

    public bool CanProcess(IClipboardContent content) => content is TextContent;

    public IClipboardContent? Process(IClipboardContent content, ProcessorOptions? options = null)
    {
        if (content is not TextContent text)
            return null;

        return new TextContent(text.Text.ToUpperInvariant());
    }
}

/// <summary>
/// Converts text to lowercase.
/// </summary>
public class LowercaseProcessor : IContentProcessor
{
    public string Name => "Lowercase";
    public string Description => "Convert text to lowercase";
    public string[] SupportedTypes => ["Text"];

    public bool CanProcess(IClipboardContent content) => content is TextContent;

    public IClipboardContent? Process(IClipboardContent content, ProcessorOptions? options = null)
    {
        if (content is not TextContent text)
            return null;

        return new TextContent(text.Text.ToLowerInvariant());
    }
}

/// <summary>
/// Reverses the text content.
/// </summary>
public class ReverseTextProcessor : IContentProcessor
{
    public string Name => "ReverseText";
    public string Description => "Reverse the text";
    public string[] SupportedTypes => ["Text"];

    public bool CanProcess(IClipboardContent content) => content is TextContent;

    public IClipboardContent? Process(IClipboardContent content, ProcessorOptions? options = null)
    {
        if (content is not TextContent text)
            return null;

        var chars = text.Text.ToCharArray();
        Array.Reverse(chars);
        return new TextContent(new string(chars));
    }
}

/// <summary>
/// Removes all line breaks from text.
/// </summary>
public class RemoveLineBreaksProcessor : IContentProcessor
{
    public string Name => "RemoveLineBreaks";
    public string Description => "Remove all line breaks";
    public string[] SupportedTypes => ["Text"];

    public bool CanProcess(IClipboardContent content) => content is TextContent;

    public IClipboardContent? Process(IClipboardContent content, ProcessorOptions? options = null)
    {
        if (content is not TextContent text)
            return null;

        var result = text.Text
            .Replace("\r\n", " ")
            .Replace("\r", " ")
            .Replace("\n", " ");

        return new TextContent(result);
    }
}

/// <summary>
/// Extracts plain text from rich content (strips formatting).
/// </summary>
public class StripFormattingProcessor : IContentProcessor
{
    public string Name => "StripFormatting";
    public string Description => "Convert to plain text (strip formatting)";
    public string[] SupportedTypes => ["RichText", "Html"];

    public bool CanProcess(IClipboardContent content) =>
        content is RichTextContent or HtmlContent;

    public IClipboardContent? Process(IClipboardContent content, ProcessorOptions? options = null)
    {
        return content switch
        {
            RichTextContent rtf => new TextContent(rtf.PlainText),
            HtmlContent html => new TextContent(html.PlainText),
            _ => null
        };
    }
}
