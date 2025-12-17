using System.Collections.Specialized;
using System.Windows;
using SkiaSharp;

namespace Slipstream.Content.Handlers;

/// <summary>
/// Handler for file list (file drop) clipboard content.
/// </summary>
public class FileListContentHandler : IContentHandler
{
    public string TypeName => "FileList";
    public int DetectionPriority => 100; // Highest priority

    public bool CanDetect(IDataObject dataObject)
    {
        return dataObject.GetDataPresent(DataFormats.FileDrop);
    }

    public IClipboardContent? Detect(IDataObject dataObject)
    {
        if (!dataObject.GetDataPresent(DataFormats.FileDrop))
            return null;

        var files = dataObject.GetData(DataFormats.FileDrop) as string[];
        if (files == null || files.Length == 0)
            return null;

        return new FileListContent(files);
    }

    public void WriteToClipboard(IClipboardContent content, DataObject dataObject)
    {
        if (content is not FileListContent fileContent)
            return;

        if (fileContent.FilePaths == null || fileContent.FilePaths.Length == 0)
            return;

        var files = new StringCollection();
        files.AddRange(fileContent.FilePaths);
        dataObject.SetFileDropList(files);
    }

    public void RenderPreview(SKCanvas canvas, IClipboardContent content, SKRect bounds, SKPaint textPaint)
    {
        canvas.DrawText(content.Preview, bounds.Left, bounds.MidY + textPaint.TextSize / 3, textPaint);
    }
}
