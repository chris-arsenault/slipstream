using System.Runtime.InteropServices;
using Slipstream.Models;
using Slipstream.Native;

namespace Slipstream.Services;

public class PasteEngine
{
    private ClipboardMonitor? _clipboardMonitor;
    private readonly KeyboardSequencer _keyboardSequencer;

    public PasteEngine() : this(new KeyboardSequencer(new KeyboardSimulator()))
    {
    }

    public PasteEngine(KeyboardSequencer keyboardSequencer)
    {
        _keyboardSequencer = keyboardSequencer;
    }

    public void SetClipboardMonitor(ClipboardMonitor monitor)
    {
        _clipboardMonitor = monitor;
    }

    /// <summary>
    /// Pastes content from a slot by setting clipboard and sending Ctrl+V.
    /// </summary>
    /// <param name="slot">The slot to paste from</param>
    public void PasteFromSlot(ClipboardSlot slot)
    {
        if (!slot.HasContent)
            return;

        Console.WriteLine($"[PasteEngine] PasteFromSlot: Type={slot.Type}");

        // Tell clipboard monitor to ignore this change
        _clipboardMonitor?.SetOwnClipboardChange(true);

        // Build the data object
        var dataObject = new System.Windows.DataObject();

        switch (slot.Type)
        {
            case ClipboardType.Text:
                dataObject.SetText(slot.TextContent ?? "", System.Windows.TextDataFormat.UnicodeText);
                break;

            case ClipboardType.RichText:
                if (!string.IsNullOrEmpty(slot.RichTextContent))
                    dataObject.SetText(slot.RichTextContent, System.Windows.TextDataFormat.Rtf);
                if (!string.IsNullOrEmpty(slot.TextContent))
                    dataObject.SetText(slot.TextContent, System.Windows.TextDataFormat.UnicodeText);
                break;

            case ClipboardType.Html:
                if (!string.IsNullOrEmpty(slot.HtmlContent))
                    dataObject.SetText(slot.HtmlContent, System.Windows.TextDataFormat.Html);
                if (!string.IsNullOrEmpty(slot.TextContent))
                    dataObject.SetText(slot.TextContent, System.Windows.TextDataFormat.UnicodeText);
                break;

            case ClipboardType.Image:
                if (slot.ImageContent != null)
                {
                    var image = BytesToBitmapSource(slot.ImageContent);
                    if (image != null)
                    {
                        dataObject.SetImage(image);
                    }
                }
                break;

            case ClipboardType.FileList:
                if (slot.FileListContent != null)
                {
                    var files = new System.Collections.Specialized.StringCollection();
                    files.AddRange(slot.FileListContent);
                    dataObject.SetFileDropList(files);
                }
                break;
        }

        // Try to set clipboard with retries
        bool success = false;
        for (int i = 0; i < 10 && !success; i++)
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.Clipboard.SetDataObject(dataObject, false); // false = don't flush
                });
                success = true;
                Console.WriteLine($"[PasteEngine] Clipboard set on attempt {i + 1}");
            }
            catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x800401D0))
            {
                Console.WriteLine($"[PasteEngine] Clipboard busy, retry {i + 1}/10");
                Thread.Sleep(10);
            }
        }

        if (!success)
        {
            Console.WriteLine($"[PasteEngine] Failed to set clipboard after 10 attempts");
            return;
        }

        // Small delay for clipboard to be ready
        Thread.Sleep(20);

        // Send Ctrl+V (snapshots and restores modifier state automatically)
        _keyboardSequencer.SendPasteWithModifierRelease();

        Console.WriteLine($"[PasteEngine] Paste complete");
    }

    private static System.Windows.Media.Imaging.BitmapSource? BytesToBitmapSource(byte[] imageData)
    {
        try
        {
            using var stream = new System.IO.MemoryStream(imageData);
            var decoder = new System.Windows.Media.Imaging.PngBitmapDecoder(
                stream,
                System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            return decoder.Frames[0];
        }
        catch
        {
            return null;
        }
    }
}
