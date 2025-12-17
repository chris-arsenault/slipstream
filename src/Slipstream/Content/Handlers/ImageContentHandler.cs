using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace Slipstream.Content.Handlers;

/// <summary>
/// Handler for image clipboard content.
/// </summary>
public class ImageContentHandler : IContentHandler
{
    public string TypeName => "Image";
    public int DetectionPriority => 90;

    public bool CanDetect(IDataObject dataObject)
    {
        return dataObject.GetDataPresent(DataFormats.Bitmap);
    }

    public IClipboardContent? Detect(IDataObject dataObject)
    {
        if (!dataObject.GetDataPresent(DataFormats.Bitmap))
            return null;

        var image = dataObject.GetData(DataFormats.Bitmap) as BitmapSource;
        if (image == null)
            return null;

        var pngData = BitmapSourceToPng(image);
        if (pngData == null)
            return null;

        return new ImageContent(pngData);
    }

    public void WriteToClipboard(IClipboardContent content, DataObject dataObject)
    {
        if (content is not ImageContent imageContent)
            return;

        if (imageContent.ImageData == null || imageContent.ImageData.Length == 0)
            return;

        try
        {
            // Decode PNG to BitmapSource
            using var pngStream = new MemoryStream(imageContent.ImageData);
            var decoder = BitmapDecoder.Create(pngStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var bitmapSource = decoder.Frames[0];

            // Convert to Bgra32 for consistent handling
            var convertedBitmap = new FormatConvertedBitmap(bitmapSource, System.Windows.Media.PixelFormats.Bgra32, null, 0);

            // 1. Set CF_BITMAP via WPF (for legacy GDI apps)
            dataObject.SetImage(convertedBitmap);

            // 2. Set PNG format (raw PNG bytes for modern apps)
            byte[] pngBytes;
            using (var pngOutStream = new MemoryStream())
            {
                var pngEncoder = new PngBitmapEncoder();
                pngEncoder.Frames.Add(BitmapFrame.Create(convertedBitmap));
                pngEncoder.Save(pngOutStream);
                pngBytes = pngOutStream.ToArray();
            }
            dataObject.SetData("PNG", pngBytes);

            // 3. Set CF_DIB for legacy Win32 apps (MS Paint, etc.)
            var dibBytes = CreateDibFromBitmap(convertedBitmap);
            if (dibBytes != null)
            {
                dataObject.SetData(DataFormats.Dib, dibBytes);
            }

            // 4. Set HTML Format for Chromium/Electron/web apps (critical for ChatGPT, etc.)
            var htmlData = CreateHtmlImageFormat(pngBytes);
            dataObject.SetData(DataFormats.Html, htmlData);

            Console.WriteLine($"[ImageContentHandler] Set clipboard formats: CF_BITMAP, PNG, CF_DIB, HTML Format");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ImageContentHandler] Error writing image: {ex.Message}");
        }
    }

    public void RenderPreview(SKCanvas canvas, IClipboardContent content, SKRect bounds, SKPaint textPaint)
    {
        // Image preview is handled specially by HudRenderer (draws thumbnail)
        // This is just a fallback text display
        canvas.DrawText("[Image]", bounds.Left, bounds.MidY + textPaint.TextSize / 3, textPaint);
    }

    private static byte[]? BitmapSourceToPng(BitmapSource source)
    {
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));

            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ImageContentHandler] Error encoding PNG: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates a DIB (BITMAPINFOHEADER) format for legacy Win32 apps like MS Paint.
    /// Returns byte[] to ensure data is fully materialized.
    /// </summary>
    private static byte[]? CreateDibFromBitmap(BitmapSource source)
    {
        try
        {
            int width = source.PixelWidth;
            int height = source.PixelHeight;
            int stride = width * 4;

            byte[] pixels = new byte[height * stride];
            source.CopyPixels(pixels, stride, 0);

            // DIB is stored bottom-up, so flip the rows
            byte[] flippedPixels = new byte[pixels.Length];
            for (int y = 0; y < height; y++)
            {
                int srcOffset = y * stride;
                int dstOffset = (height - 1 - y) * stride;
                Array.Copy(pixels, srcOffset, flippedPixels, dstOffset, stride);
            }

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, System.Text.Encoding.Default, leaveOpen: true);

            // BITMAPINFOHEADER (40 bytes)
            writer.Write(40);                    // biSize
            writer.Write(width);                 // biWidth
            writer.Write(height);                // biHeight (positive = bottom-up)
            writer.Write((short)1);              // biPlanes
            writer.Write((short)32);             // biBitCount
            writer.Write(0);                     // biCompression (BI_RGB)
            writer.Write(flippedPixels.Length);  // biSizeImage
            writer.Write(0);                     // biXPelsPerMeter
            writer.Write(0);                     // biYPelsPerMeter
            writer.Write(0);                     // biClrUsed
            writer.Write(0);                     // biClrImportant

            // Write pixel data
            writer.Write(flippedPixels);

            return ms.ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ImageContentHandler] Error creating DIB: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates HTML clipboard format with embedded base64 PNG.
    /// This is critical for Chromium/Electron/web-based apps (ChatGPT, Slack, Discord, etc.)
    /// </summary>
    private static string CreateHtmlImageFormat(byte[] pngBytes)
    {
        string base64 = Convert.ToBase64String(pngBytes);
        string imgTag = $"<img src=\"data:image/png;base64,{base64}\"/>";

        // HTML clipboard format requires specific header with byte offsets
        // The offsets must be calculated precisely
        string htmlBody = $"<html><body><!--StartFragment-->{imgTag}<!--EndFragment--></body></html>";

        // Build header - offsets are byte positions in the final UTF-8 encoded string
        const string version = "Version:0.9\r\n";
        const string startHtmlMarker = "StartHTML:";
        const string endHtmlMarker = "EndHTML:";
        const string startFragmentMarker = "StartFragment:";
        const string endFragmentMarker = "EndFragment:";

        // Calculate header length (with placeholder digits)
        // Each offset is 10 digits (padded)
        string headerTemplate = version +
                               startHtmlMarker + "0000000000\r\n" +
                               endHtmlMarker + "0000000000\r\n" +
                               startFragmentMarker + "0000000000\r\n" +
                               endFragmentMarker + "0000000000\r\n";

        int headerLength = System.Text.Encoding.UTF8.GetByteCount(headerTemplate);

        // Calculate offsets
        int startHtml = headerLength;
        int endHtml = headerLength + System.Text.Encoding.UTF8.GetByteCount(htmlBody);

        // Find fragment markers within the HTML body
        string fragmentStart = "<!--StartFragment-->";
        string fragmentEnd = "<!--EndFragment-->";
        int startFragmentOffset = htmlBody.IndexOf(fragmentStart) + fragmentStart.Length;
        int endFragmentOffset = htmlBody.IndexOf(fragmentEnd);

        int startFragment = headerLength + System.Text.Encoding.UTF8.GetByteCount(htmlBody.Substring(0, startFragmentOffset));
        int endFragment = headerLength + System.Text.Encoding.UTF8.GetByteCount(htmlBody.Substring(0, endFragmentOffset));

        // Build final header with actual offsets
        string header = version +
                       $"{startHtmlMarker}{startHtml:D10}\r\n" +
                       $"{endHtmlMarker}{endHtml:D10}\r\n" +
                       $"{startFragmentMarker}{startFragment:D10}\r\n" +
                       $"{endFragmentMarker}{endFragment:D10}\r\n";

        return header + htmlBody;
    }
}
