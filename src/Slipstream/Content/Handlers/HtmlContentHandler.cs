using System.Windows;
using SkiaSharp;

namespace Slipstream.Content.Handlers;

/// <summary>
/// Handler for HTML clipboard content.
/// </summary>
public class HtmlContentHandler : IContentHandler
{
    public string TypeName => "Html";
    public int DetectionPriority => 80;

    public bool CanDetect(IDataObject dataObject)
    {
        return dataObject.GetDataPresent(DataFormats.Html);
    }

    public IClipboardContent? Detect(IDataObject dataObject)
    {
        if (!dataObject.GetDataPresent(DataFormats.Html))
            return null;

        var html = dataObject.GetData(DataFormats.Html) as string;

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
        if (string.IsNullOrEmpty(html) && string.IsNullOrEmpty(plainText))
            return null;

        return new HtmlContent(plainText ?? "", html);
    }

    public void WriteToClipboard(IClipboardContent content, DataObject dataObject)
    {
        if (content is not HtmlContent htmlContent)
            return;

        if (!string.IsNullOrEmpty(htmlContent.Html))
        {
            dataObject.SetText(htmlContent.Html, TextDataFormat.Html);
        }

        if (!string.IsNullOrEmpty(htmlContent.PlainText))
        {
            dataObject.SetText(htmlContent.PlainText, TextDataFormat.UnicodeText);
        }
    }

    public void RenderPreview(SKCanvas canvas, IClipboardContent content, SKRect bounds, SKPaint textPaint)
    {
        canvas.DrawText(content.Preview, bounds.Left, bounds.MidY + textPaint.TextSize / 3, textPaint);
    }
}
