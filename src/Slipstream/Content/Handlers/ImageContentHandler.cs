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

            // 2. Set PNG format - raw PNG byte stream (critical for browsers like Chrome, Edge, ChatGPT web)
            byte[] pngBytes;
            using (var pngOutStream = new MemoryStream())
            {
                var pngEncoder = new PngBitmapEncoder();
                pngEncoder.Frames.Add(BitmapFrame.Create(convertedBitmap));
                pngEncoder.Save(pngOutStream);
                pngBytes = pngOutStream.ToArray();
            }
            // Use MemoryStream for PNG (some apps expect stream, not byte[])
            var pngMemStream = new MemoryStream(pngBytes);
            dataObject.SetData("PNG", pngMemStream, false);  // false = don't auto-convert

            // Also set image/png MIME type (some web apps check this)
            dataObject.SetData("image/png", new MemoryStream(pngBytes), false);

            // 3. Set CF_DIB for legacy Win32 apps (MS Paint, etc.)
            var dibData = CreateDibFromBitmap(convertedBitmap);
            if (dibData != null)
            {
                dataObject.SetData(DataFormats.Dib, dibData);
            }

            // 4. Set CF_DIBV5 for modern Win32 apps (registered as separate format)
            var dibV5Data = CreateDibV5FromBitmap(convertedBitmap);
            if (dibV5Data != null)
            {
                // Register and use CF_DIBV5 format name
                dataObject.SetData("CF_DIBV5", dibV5Data);
            }

            // 5. Set HTML Format for browsers and Electron apps
            var htmlData = CreateHtmlClipboardFormat(pngBytes);
            dataObject.SetData("HTML Format", htmlData);

            // 6. Set FileDrop with temp PNG file (WhatsApp Desktop prefers file-based clipboard data)
            var tempFile = CreateTempPngFile(pngBytes);
            if (tempFile != null)
            {
                dataObject.SetData(DataFormats.FileDrop, new string[] { tempFile });
            }

            Console.WriteLine($"[ImageContentHandler] Set clipboard formats: CF_BITMAP, PNG, image/png, CF_DIB, CF_DIBV5, HTML Format, FileDrop");
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
    /// Returns MemoryStream as required by WPF's DataObject.SetData for DIB format.
    /// </summary>
    private static MemoryStream? CreateDibFromBitmap(BitmapSource source)
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

            var ms = new MemoryStream();
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

            ms.Position = 0;
            return ms;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ImageContentHandler] Error creating DIB: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates a DIBV5 (BITMAPV5HEADER) format for modern Win32 apps.
    /// 32-bit BGRA with proper alpha channel support.
    /// </summary>
    private static MemoryStream? CreateDibV5FromBitmap(BitmapSource source)
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

            var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, System.Text.Encoding.Default, leaveOpen: true);

            // BITMAPV5HEADER (124 bytes)
            writer.Write(124);                   // bV5Size
            writer.Write(width);                 // bV5Width
            writer.Write(height);                // bV5Height (positive = bottom-up)
            writer.Write((short)1);              // bV5Planes
            writer.Write((short)32);             // bV5BitCount
            writer.Write(3);                     // bV5Compression = BI_BITFIELDS
            writer.Write(flippedPixels.Length);  // bV5SizeImage
            writer.Write(0);                     // bV5XPelsPerMeter
            writer.Write(0);                     // bV5YPelsPerMeter
            writer.Write(0);                     // bV5ClrUsed
            writer.Write(0);                     // bV5ClrImportant

            // Color masks for BGRA
            writer.Write(0x00FF0000);            // bV5RedMask
            writer.Write(0x0000FF00);            // bV5GreenMask
            writer.Write(0x000000FF);            // bV5BlueMask
            writer.Write(0xFF000000u);           // bV5AlphaMask

            writer.Write(0x73524742);            // bV5CSType = LCS_sRGB

            // CIEXYZTRIPLE bV5Endpoints (36 bytes - 9 DWORDs)
            for (int i = 0; i < 9; i++)
                writer.Write(0);

            writer.Write(0);                     // bV5GammaRed
            writer.Write(0);                     // bV5GammaGreen
            writer.Write(0);                     // bV5GammaBlue
            writer.Write(4);                     // bV5Intent = LCS_GM_IMAGES
            writer.Write(0);                     // bV5ProfileData
            writer.Write(0);                     // bV5ProfileSize
            writer.Write(0);                     // bV5Reserved

            // Write pixel data
            writer.Write(flippedPixels);

            ms.Position = 0;
            return ms;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ImageContentHandler] Error creating DIBV5: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates a temporary PNG file for FileDrop clipboard format.
    /// Some apps like WhatsApp Desktop prefer file-based clipboard data.
    /// </summary>
    private static string? CreateTempPngFile(byte[] pngBytes)
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "Slipstream");
            Directory.CreateDirectory(tempDir);
            var tempFile = Path.Combine(tempDir, $"clipboard_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(tempFile, pngBytes);
            return tempFile;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ImageContentHandler] Error creating temp PNG file: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates HTML clipboard format with proper CF_HTML headers.
    /// Format: Version, StartHTML, EndHTML, StartFragment, EndFragment offsets, then HTML content.
    /// </summary>
    private static string CreateHtmlClipboardFormat(byte[] pngBytes)
    {
        string base64 = Convert.ToBase64String(pngBytes);
        string fragment = $"<img src=\"data:image/png;base64,{base64}\"/>";

        // Build the HTML content with fragment markers
        string htmlContent = $"<html>\r\n<body>\r\n<!--StartFragment-->{fragment}<!--EndFragment-->\r\n</body>\r\n</html>";

        // CF_HTML format requires a header with byte offsets
        // Header format (each field is padded to fixed width for easier calculation):
        // Version:0.9
        // StartHTML:00000000
        // EndHTML:00000000
        // StartFragment:00000000
        // EndFragment:00000000

        const string headerFormat = "Version:0.9\r\nStartHTML:{0:D8}\r\nEndHTML:{1:D8}\r\nStartFragment:{2:D8}\r\nEndFragment:{3:D8}\r\n";

        // Calculate header length (with placeholder values to get exact length)
        string placeholderHeader = string.Format(headerFormat, 0, 0, 0, 0);
        int headerLength = System.Text.Encoding.UTF8.GetByteCount(placeholderHeader);

        // Calculate actual offsets
        int startHtml = headerLength;
        int endHtml = headerLength + System.Text.Encoding.UTF8.GetByteCount(htmlContent);

        // Find fragment positions within htmlContent
        string startMarker = "<!--StartFragment-->";
        string endMarker = "<!--EndFragment-->";
        int fragmentStartInHtml = htmlContent.IndexOf(startMarker) + startMarker.Length;
        int fragmentEndInHtml = htmlContent.IndexOf(endMarker);

        int startFragment = headerLength + System.Text.Encoding.UTF8.GetByteCount(htmlContent.Substring(0, fragmentStartInHtml));
        int endFragment = headerLength + System.Text.Encoding.UTF8.GetByteCount(htmlContent.Substring(0, fragmentEndInHtml));

        // Build final header with actual offsets
        string header = string.Format(headerFormat, startHtml, endHtml, startFragment, endFragment);

        return header + htmlContent;
    }
}
