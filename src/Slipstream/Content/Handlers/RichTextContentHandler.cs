using System.Windows;
using SkiaSharp;

namespace Slipstream.Content.Handlers;

/// <summary>
/// Handler for Rich Text Format (RTF) clipboard content.
/// </summary>
public class RichTextContentHandler : IContentHandler
{
    public string TypeName => "RichText";
    public int DetectionPriority => 70;

    public bool CanDetect(IDataObject dataObject)
    {
        return dataObject.GetDataPresent(DataFormats.Rtf);
    }

    public IClipboardContent? Detect(IDataObject dataObject)
    {
        if (!dataObject.GetDataPresent(DataFormats.Rtf))
            return null;

        var rtf = dataObject.GetData(DataFormats.Rtf) as string;

        // Also get plain text fallback
        string? plainText = null;
        if (dataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            plainText = dataObject.GetData(DataFormats.UnicodeText) as string;
        }
        else if (dataObject.GetDataPresent(DataFormats.Text))
        {
            plainText = dataObject.GetData(DataFormats.Text) as string;
        }

        // Need at least one form of content
        if (string.IsNullOrEmpty(rtf) && string.IsNullOrEmpty(plainText))
            return null;

        return new RichTextContent(plainText ?? "", rtf);
    }

    public void WriteToClipboard(IClipboardContent content, DataObject dataObject)
    {
        if (content is not RichTextContent rtfContent)
            return;

        if (!string.IsNullOrEmpty(rtfContent.RichText))
        {
            dataObject.SetText(rtfContent.RichText, TextDataFormat.Rtf);
        }

        if (!string.IsNullOrEmpty(rtfContent.PlainText))
        {
            dataObject.SetText(rtfContent.PlainText, TextDataFormat.UnicodeText);
        }
    }

    public void RenderPreview(SKCanvas canvas, IClipboardContent content, SKRect bounds, SKPaint textPaint)
    {
        canvas.DrawText(content.Preview, bounds.Left, bounds.MidY + textPaint.TextSize / 3, textPaint);
    }
}
