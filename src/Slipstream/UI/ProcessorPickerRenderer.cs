using SkiaSharp;
using Slipstream.Models;
using Slipstream.Processing;

namespace Slipstream.UI;

/// <summary>
/// Renders the processor picker overlay within the HUD.
/// </summary>
public class ProcessorPickerRenderer : BaseRenderer
{
    // Layout constants
    private const float CornerRadius = 8f;
    private const float Padding = 10f;
    private const float ItemHeight = 24f;
    private const float ItemSpacing = 4f;
    private const float HeaderHeight = 20f;
    private const float KeyWidth = 24f;
    private const float ProcessorNameLeftPad = 4f;

    // Hit testing for processor buttons
    private readonly List<SKRect> _processorButtonRects = new();

    // Picker-specific paints
    private readonly SKPaint _pickerBackgroundPaint;
    private readonly SKPaint _pickerBorderPaint;
    private readonly SKPaint _headerPaint;
    private readonly SKPaint _itemPaint;
    private readonly SKPaint _itemHoverPaint;
    private readonly SKPaint _keyPaint;
    private readonly SKPaint _processorNamePaint;
    private readonly SKPaint _pipelinePaint;
    private readonly SKPaint _outputModePaint;

    public ProcessorPickerRenderer() : base(ColorTheme.Dark)
    {
        _pickerBackgroundPaint = CreateFillPaint(_theme.Background.WithAlpha(240));
        _pickerBorderPaint = CreateStrokePaint(_theme.Accent, 2f);
        _headerPaint = CreateTextPaint(_theme.TextSecondary, 10f, SKFontStyle.Bold);
        _itemPaint = CreateFillPaint(_theme.SlotBackground);
        _itemHoverPaint = CreateFillPaint(_theme.SlotActive);
        _keyPaint = CreateTextPaint(_theme.Accent, 11f, SKFontStyle.Bold);
        _processorNamePaint = CreateTextPaint(_theme.Text, 11f);
        _pipelinePaint = CreateTextPaint(_theme.TextSecondary, 10f);
        _outputModePaint = CreateTextPaint(_theme.Accent, 10f);
    }

    public void SetTheme(ColorPalette palette)
    {
        _theme = ColorTheme.GetTheme(palette);
        UpdatePaintColors();
    }

    protected override void UpdatePaintColors()
    {
        base.UpdatePaintColors();

        _pickerBackgroundPaint.Color = _theme.Background.WithAlpha(240);
        _pickerBorderPaint.Color = _theme.Accent;
        _headerPaint.Color = _theme.TextSecondary;
        _itemPaint.Color = _theme.SlotBackground;
        _itemHoverPaint.Color = _theme.SlotActive;
        _keyPaint.Color = _theme.Accent;
        _processorNamePaint.Color = _theme.Text;
        _pipelinePaint.Color = _theme.TextSecondary;
        _outputModePaint.Color = _theme.Accent;
    }

    /// <summary>
    /// Renders the processor picker overlay.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="pickerState">Current picker state.</param>
    /// <param name="processors">Available processors for the current content type.</param>
    /// <param name="startY">Y position to start drawing (below the active slot).</param>
    /// <param name="width">Available width.</param>
    /// <param name="dpiScale">DPI scale factor.</param>
    /// <returns>The height of the rendered picker.</returns>
    public float Render(
        SKCanvas canvas,
        ProcessorPickerState pickerState,
        IReadOnlyList<IContentProcessor> processors,
        float startY,
        float width,
        float dpiScale = 1.0f)
    {
        if (!pickerState.IsOpen || processors.Count == 0)
            return 0;

        _processorButtonRects.Clear();

        // Calculate picker dimensions
        int itemsPerRow = 3;
        int rowCount = (int)Math.Ceiling(processors.Count / (float)itemsPerRow);
        float itemWidth = (width - Padding * 2 - ItemSpacing * (itemsPerRow - 1)) / itemsPerRow;
        float contentHeight = HeaderHeight + rowCount * (ItemHeight + ItemSpacing) + Padding * 2;

        // Add space for pipeline display if building
        if (pickerState.IsBuildingPipeline || !pickerState.Pipeline.IsEmpty)
        {
            contentHeight += ItemHeight;
        }

        // Add footer for output mode and instructions
        contentHeight += ItemHeight;

        float pickerHeight = contentHeight;
        float x = Padding;
        float y = startY;

        // Draw picker background
        var pickerRect = new SKRect(x, y, x + width - Padding * 2, y + pickerHeight);
        var roundedRect = new SKRoundRect(pickerRect, CornerRadius);
        canvas.DrawRoundRect(roundedRect, _pickerBackgroundPaint);
        canvas.DrawRoundRect(roundedRect, _pickerBorderPaint);

        float currentY = y + Padding;

        // Draw header
        canvas.DrawText("PROCESSORS", x + Padding, currentY + HeaderHeight * 0.7f, _headerPaint);

        // Draw separator line
        currentY += HeaderHeight;
        canvas.DrawLine(
            x + Padding,
            currentY,
            x + width - Padding * 3,
            currentY,
            _borderPaint);
        currentY += ItemSpacing;

        // Draw processor items in grid
        for (int i = 0; i < processors.Count && i < 9; i++)
        {
            int row = i / itemsPerRow;
            int col = i % itemsPerRow;

            float itemX = x + Padding + col * (itemWidth + ItemSpacing);
            float itemY = currentY + row * (ItemHeight + ItemSpacing);

            DrawProcessorItem(canvas, processors[i], i + 1, itemX, itemY, itemWidth);
        }

        currentY += rowCount * (ItemHeight + ItemSpacing);

        // Draw pipeline if building
        if (pickerState.IsBuildingPipeline || !pickerState.Pipeline.IsEmpty)
        {
            currentY += ItemSpacing;
            string pipelineText = $"Pipeline: {pickerState.Pipeline}";
            canvas.DrawText(pipelineText, x + Padding, currentY + ItemHeight * 0.6f, _pipelinePaint);
            currentY += ItemHeight;
        }

        // Draw footer with output mode and controls
        currentY += ItemSpacing;
        float footerY = currentY;

        // Output mode on left
        string outputModeText = pickerState.GetOutputModeDisplay();
        canvas.DrawText(outputModeText, x + Padding, footerY + ItemHeight * 0.6f, _outputModePaint);

        // Controls hint on right
        string controlsHint = "[Esc] Cancel";
        if (!pickerState.Pipeline.IsEmpty)
        {
            controlsHint = "[Enter] Execute  [Esc] Cancel";
        }
        float controlsWidth = _pipelinePaint.MeasureText(controlsHint);
        canvas.DrawText(controlsHint, x + width - Padding * 3 - controlsWidth, footerY + ItemHeight * 0.6f, _pipelinePaint);

        return pickerHeight;
    }

    private void DrawProcessorItem(
        SKCanvas canvas,
        IContentProcessor processor,
        int keyNumber,
        float x,
        float y,
        float width)
    {
        // Store rect for hit testing
        var itemRect = new SKRect(x, y, x + width, y + ItemHeight);
        _processorButtonRects.Add(itemRect);

        // Draw item background
        var roundedRect = new SKRoundRect(itemRect, 4f);
        canvas.DrawRoundRect(roundedRect, _itemPaint);

        // Draw key number
        float keyX = x + KeyWidth / 2;
        float textY = y + ItemHeight * 0.65f;
        canvas.DrawText($"[{keyNumber}]", keyX, textY, _keyPaint);

        // Draw processor name
        float nameX = x + KeyWidth + ProcessorNameLeftPad;
        float maxNameWidth = width - KeyWidth - ProcessorNameLeftPad - 4;
        string displayName = TruncateToWidth(processor.Name, maxNameWidth, _processorNamePaint);
        canvas.DrawText(displayName, nameX, textY, _processorNamePaint);
    }

    /// <summary>
    /// Returns the processor index (1-9) if the point is over a processor button, or -1 otherwise.
    /// </summary>
    public int HitTestProcessor(float x, float y)
    {
        for (int i = 0; i < _processorButtonRects.Count; i++)
        {
            if (_processorButtonRects[i].Contains(x, y))
                return i + 1; // Return 1-based index
        }
        return -1;
    }

    private static string TruncateToWidth(string text, float maxWidth, SKPaint paint)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        float width = paint.MeasureText(text);
        if (width <= maxWidth)
            return text;

        const string ellipsis = "...";
        float ellipsisWidth = paint.MeasureText(ellipsis);

        int len = text.Length;
        while (len > 0)
        {
            len--;
            if (paint.MeasureText(text.AsSpan(0, len)) + ellipsisWidth <= maxWidth)
                return text[..len] + ellipsis;
        }

        return ellipsis;
    }

    public void Dispose()
    {
        _pickerBackgroundPaint.Dispose();
        _pickerBorderPaint.Dispose();
        _headerPaint.Dispose();
        _itemPaint.Dispose();
        _itemHoverPaint.Dispose();
        _keyPaint.Dispose();
        _processorNamePaint.Dispose();
        _pipelinePaint.Dispose();
        _outputModePaint.Dispose();
    }
}
