using System.Windows;
using SkiaSharp;

namespace Slipstream.Content.Handlers;

/// <summary>
/// Handler for plain text clipboard content.
/// </summary>
public class TextContentHandler : IContentHandler
{
    public string TypeName => "Text";
    public int DetectionPriority => 60; // Lowest priority (fallback)

    public bool CanDetect(IDataObject dataObject)
    {
        return dataObject.GetDataPresent(DataFormats.UnicodeText) ||
               dataObject.GetDataPresent(DataFormats.Text);
    }

    public IClipboardContent? Detect(IDataObject dataObject)
    {
        string? text = null;

        if (dataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            text = dataObject.GetData(DataFormats.UnicodeText) as string;
        }
        else if (dataObject.GetDataPresent(DataFormats.Text))
        {
            text = dataObject.GetData(DataFormats.Text) as string;
        }

        if (string.IsNullOrEmpty(text))
            return null;

        return new TextContent(text);
    }

    public void WriteToClipboard(IClipboardContent content, DataObject dataObject)
    {
        if (content is not TextContent textContent)
            return;

        dataObject.SetText(textContent.Text, TextDataFormat.UnicodeText);
    }

    public void RenderPreview(SKCanvas canvas, IClipboardContent content, SKRect bounds, SKPaint textPaint)
    {
        if (content is not TextContent textContent)
            return;

        canvas.DrawText(textContent.Preview, bounds.Left, bounds.MidY + textPaint.TextSize / 3, textPaint);
    }
}
