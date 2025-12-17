using System.IO;
using SkiaSharp;
using Slipstream.Content;

namespace Slipstream.Processing.Processors;

/// <summary>
/// Converts image to grayscale.
/// </summary>
public class GrayscaleProcessor : IContentProcessor
{
    public string Name => "Grayscale";
    public string Description => "Convert image to grayscale";
    public string[] SupportedTypes => ["Image"];

    public bool CanProcess(IClipboardContent content) => content is ImageContent;

    public IClipboardContent? Process(IClipboardContent content, ProcessorOptions? options = null)
    {
        if (content is not ImageContent image)
            return null;

        try
        {
            using var inputStream = new MemoryStream(image.ImageData);
            using var bitmap = SKBitmap.Decode(inputStream);
            if (bitmap == null)
                return null;

            // Create grayscale bitmap
            using var grayscaleBitmap = new SKBitmap(bitmap.Width, bitmap.Height);

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    // Standard grayscale conversion weights
                    var gray = (byte)(0.299 * pixel.Red + 0.587 * pixel.Green + 0.114 * pixel.Blue);
                    grayscaleBitmap.SetPixel(x, y, new SKColor(gray, gray, gray, pixel.Alpha));
                }
            }

            // Encode back to PNG
            using var outputStream = new MemoryStream();
            grayscaleBitmap.Encode(outputStream, SKEncodedImageFormat.Png, 100);
            return new ImageContent(outputStream.ToArray());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GrayscaleProcessor] Error: {ex.Message}");
            return null;
        }
    }
}

/// <summary>
/// Inverts image colors.
/// </summary>
public class InvertColorsProcessor : IContentProcessor
{
    public string Name => "InvertColors";
    public string Description => "Invert image colors";
    public string[] SupportedTypes => ["Image"];

    public bool CanProcess(IClipboardContent content) => content is ImageContent;

    public IClipboardContent? Process(IClipboardContent content, ProcessorOptions? options = null)
    {
        if (content is not ImageContent image)
            return null;

        try
        {
            using var inputStream = new MemoryStream(image.ImageData);
            using var bitmap = SKBitmap.Decode(inputStream);
            if (bitmap == null)
                return null;

            using var invertedBitmap = new SKBitmap(bitmap.Width, bitmap.Height);

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    invertedBitmap.SetPixel(x, y, new SKColor(
                        (byte)(255 - pixel.Red),
                        (byte)(255 - pixel.Green),
                        (byte)(255 - pixel.Blue),
                        pixel.Alpha));
                }
            }

            using var outputStream = new MemoryStream();
            invertedBitmap.Encode(outputStream, SKEncodedImageFormat.Png, 100);
            return new ImageContent(outputStream.ToArray());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InvertColorsProcessor] Error: {ex.Message}");
            return null;
        }
    }
}

/// <summary>
/// Rotates image 90 degrees clockwise.
/// </summary>
public class RotateClockwiseProcessor : IContentProcessor
{
    public string Name => "RotateClockwise";
    public string Description => "Rotate image 90° clockwise";
    public string[] SupportedTypes => ["Image"];

    public bool CanProcess(IClipboardContent content) => content is ImageContent;

    public IClipboardContent? Process(IClipboardContent content, ProcessorOptions? options = null)
    {
        if (content is not ImageContent image)
            return null;

        try
        {
            using var inputStream = new MemoryStream(image.ImageData);
            using var bitmap = SKBitmap.Decode(inputStream);
            if (bitmap == null)
                return null;

            // Create rotated bitmap (width/height swapped)
            using var rotatedBitmap = new SKBitmap(bitmap.Height, bitmap.Width);

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    // Rotate 90° clockwise: (x,y) -> (height-1-y, x) but with swapped dimensions
                    rotatedBitmap.SetPixel(bitmap.Height - 1 - y, x, bitmap.GetPixel(x, y));
                }
            }

            using var outputStream = new MemoryStream();
            rotatedBitmap.Encode(outputStream, SKEncodedImageFormat.Png, 100);
            return new ImageContent(outputStream.ToArray());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RotateClockwiseProcessor] Error: {ex.Message}");
            return null;
        }
    }
}

/// <summary>
/// Flips image horizontally.
/// </summary>
public class FlipHorizontalProcessor : IContentProcessor
{
    public string Name => "FlipHorizontal";
    public string Description => "Flip image horizontally";
    public string[] SupportedTypes => ["Image"];

    public bool CanProcess(IClipboardContent content) => content is ImageContent;

    public IClipboardContent? Process(IClipboardContent content, ProcessorOptions? options = null)
    {
        if (content is not ImageContent image)
            return null;

        try
        {
            using var inputStream = new MemoryStream(image.ImageData);
            using var bitmap = SKBitmap.Decode(inputStream);
            if (bitmap == null)
                return null;

            using var flippedBitmap = new SKBitmap(bitmap.Width, bitmap.Height);

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    flippedBitmap.SetPixel(bitmap.Width - 1 - x, y, bitmap.GetPixel(x, y));
                }
            }

            using var outputStream = new MemoryStream();
            flippedBitmap.Encode(outputStream, SKEncodedImageFormat.Png, 100);
            return new ImageContent(outputStream.ToArray());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FlipHorizontalProcessor] Error: {ex.Message}");
            return null;
        }
    }
}
