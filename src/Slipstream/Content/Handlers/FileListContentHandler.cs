using System.Collections.Specialized;
using System.Windows;
using SkiaSharp;

namespace Slipstream.Content.Handlers;

/// <summary>
/// Handler for file list (file drop) clipboard content.
/// </summary>
public class FileListContentHandler : IContentHandler
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico", ".tiff", ".tif"
    };

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

        // If clipboard also has image data AND files are all images, prefer image detection.
        // This handles Electron/WhatsApp which put both FileDrop (temp image files) AND bitmap data.
        // We want the bitmap data because it pastes more reliably to apps like MS Paint.
        if (dataObject.GetDataPresent(DataFormats.Bitmap) && AreAllImageFiles(files))
        {
            Console.WriteLine($"[FileListContentHandler] FileDrop contains images and clipboard has bitmap data - deferring to ImageContentHandler");
            return null; // Let ImageContentHandler handle it
        }

        return new FileListContent(files);
    }

    /// <summary>
    /// Checks if all files in the list are image files based on extension.
    /// </summary>
    private static bool AreAllImageFiles(string[] files)
    {
        foreach (var file in files)
        {
            var ext = System.IO.Path.GetExtension(file);
            if (string.IsNullOrEmpty(ext) || !ImageExtensions.Contains(ext))
                return false;
        }
        return true;
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
