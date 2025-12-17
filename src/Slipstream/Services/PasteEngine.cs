using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
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
                    SetImageToDataObject(dataObject, slot.ImageContent);
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

    /// <summary>
    /// Sets image data to the DataObject in multiple formats for maximum compatibility.
    /// MS Paint and many other apps require DIB format, not just CF_BITMAP.
    /// </summary>
    private static void SetImageToDataObject(DataObject dataObject, byte[] pngData)
    {
        try
        {
            // Decode PNG to BitmapSource
            using var pngStream = new MemoryStream(pngData);
            var decoder = BitmapDecoder.Create(pngStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var bitmapSource = decoder.Frames[0];

            Console.WriteLine($"[PasteEngine] Image: {bitmapSource.PixelWidth}x{bitmapSource.PixelHeight}, Format={bitmapSource.Format}");

            // Convert to Bgra32 for consistent handling
            var convertedBitmap = new FormatConvertedBitmap(bitmapSource, System.Windows.Media.PixelFormats.Bgra32, null, 0);

            // Set the standard WPF image format
            dataObject.SetImage(convertedBitmap);

            // Also set as PNG for apps that support it
            using var pngOutStream = new MemoryStream();
            var pngEncoder = new PngBitmapEncoder();
            pngEncoder.Frames.Add(BitmapFrame.Create(convertedBitmap));
            pngEncoder.Save(pngOutStream);
            dataObject.SetData("PNG", pngOutStream.ToArray());

            // Create DIB (Device Independent Bitmap) for MS Paint and other legacy apps
            var dibData = CreateDibFromBitmap(convertedBitmap);
            if (dibData != null)
            {
                dataObject.SetData(DataFormats.Dib, dibData);
                Console.WriteLine($"[PasteEngine] DIB format set: {dibData.Length} bytes");
            }

            Console.WriteLine("[PasteEngine] Image formats set: Bitmap, PNG, DIB");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PasteEngine] Error setting image: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a DIB (Device Independent Bitmap) byte array from a BitmapSource.
    /// DIB format is required by MS Paint and many other applications.
    /// </summary>
    private static MemoryStream? CreateDibFromBitmap(BitmapSource source)
    {
        try
        {
            int width = source.PixelWidth;
            int height = source.PixelHeight;
            int stride = width * 4; // BGRA = 4 bytes per pixel

            // Get pixel data
            byte[] pixels = new byte[height * stride];
            source.CopyPixels(pixels, stride, 0);

            // DIB is stored bottom-up, so we need to flip the rows
            byte[] flippedPixels = new byte[pixels.Length];
            for (int y = 0; y < height; y++)
            {
                int srcOffset = y * stride;
                int dstOffset = (height - 1 - y) * stride;
                Array.Copy(pixels, srcOffset, flippedPixels, dstOffset, stride);
            }

            // Create BITMAPINFOHEADER (40 bytes)
            var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, System.Text.Encoding.Default, leaveOpen: true);

            // BITMAPINFOHEADER
            writer.Write(40);              // biSize
            writer.Write(width);           // biWidth
            writer.Write(height);          // biHeight
            writer.Write((short)1);        // biPlanes
            writer.Write((short)32);       // biBitCount (32-bit BGRA)
            writer.Write(0);               // biCompression (BI_RGB = 0)
            writer.Write(flippedPixels.Length); // biSizeImage
            writer.Write(0);               // biXPelsPerMeter
            writer.Write(0);               // biYPelsPerMeter
            writer.Write(0);               // biClrUsed
            writer.Write(0);               // biClrImportant

            // Pixel data (already in BGRA format, flipped)
            writer.Write(flippedPixels);

            ms.Position = 0;
            return ms;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PasteEngine] Error creating DIB: {ex.Message}");
            return null;
        }
    }
}
