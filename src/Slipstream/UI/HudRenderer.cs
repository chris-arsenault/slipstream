using SkiaSharp;
using Slipstream.Models;

namespace Slipstream.UI;

public class HudRenderer
{
    // Colors - modern dark theme
    private static readonly SKColor BackgroundColor = new(30, 30, 30, 230);
    private static readonly SKColor SlotBackgroundColor = new(45, 45, 45, 255);
    private static readonly SKColor SlotHoverColor = new(55, 55, 55, 255);
    private static readonly SKColor ActiveSlotColor = new(70, 130, 180, 255); // Steel blue accent
    private static readonly SKColor TempSlotColor = new(80, 70, 50, 255); // Warm amber/orange tint for temp slot
    private static readonly SKColor TempSlotBorderColor = new(180, 140, 60, 255); // Amber border
    private static readonly SKColor LockedIndicatorColor = new(220, 80, 80, 255);
    private static readonly SKColor TextColor = new(220, 220, 220, 255);
    private static readonly SKColor SecondaryTextColor = new(150, 150, 150, 255);
    private static readonly SKColor TypeGlyphColor = new(100, 100, 100, 255);
    private static readonly SKColor BorderColor = new(60, 60, 60, 255);

    // Layout constants
    private const float CornerRadius = 12f;
    private const float Padding = 12f;
    private const float SlotHeight = 32f;
    private const float SlotSpacing = 4f;
    private const float SlotCornerRadius = 6f;
    private const float IndexWidth = 24f;
    private const float TypeGlyphWidth = 20f;
    private const float LockIconSize = 16f;
    private const float LockIconPadding = 8f;

    // Hit testing - store lock button rects
    private readonly List<SKRect> _lockButtonRects = new();

    // Pooled paints for performance
    private readonly SKPaint _backgroundPaint;
    private readonly SKPaint _slotPaint;
    private readonly SKPaint _activeSlotPaint;
    private readonly SKPaint _tempSlotPaint;
    private readonly SKPaint _tempSlotBorderPaint;
    private readonly SKPaint _textPaint;
    private readonly SKPaint _secondaryTextPaint;
    private readonly SKPaint _indexPaint;
    private readonly SKPaint _typeGlyphPaint;
    private readonly SKPaint _lockIndicatorPaint;
    private readonly SKPaint _borderPaint;

    // Hit testing for temp slot promote button
    private SKRect _tempSlotPromoteRect;

    public HudRenderer()
    {
        _backgroundPaint = new SKPaint
        {
            Color = BackgroundColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        _slotPaint = new SKPaint
        {
            Color = SlotBackgroundColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        _activeSlotPaint = new SKPaint
        {
            Color = ActiveSlotColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        _tempSlotPaint = new SKPaint
        {
            Color = TempSlotColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        _tempSlotBorderPaint = new SKPaint
        {
            Color = TempSlotBorderColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f
        };

        _textPaint = new SKPaint
        {
            Color = TextColor,
            IsAntialias = true,
            TextSize = 13f,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        };

        _secondaryTextPaint = new SKPaint
        {
            Color = SecondaryTextColor,
            IsAntialias = true,
            TextSize = 11f,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        };

        _indexPaint = new SKPaint
        {
            Color = SecondaryTextColor,
            IsAntialias = true,
            TextSize = 11f,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };

        _typeGlyphPaint = new SKPaint
        {
            Color = TypeGlyphColor,
            IsAntialias = true,
            TextSize = 10f,
            Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Normal),
            TextAlign = SKTextAlign.Center
        };

        _lockIndicatorPaint = new SKPaint
        {
            Color = LockedIndicatorColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        _borderPaint = new SKPaint
        {
            Color = BorderColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f
        };
    }

    public void Render(SKCanvas canvas, SKSize size, List<ClipboardSlot> slots, int activeSlotIndex, ClipboardSlot? tempSlot = null, float dpiScale = 1.0f, int nextRoundRobinIndex = -1, bool isRoundRobinMode = true)
    {
        canvas.Clear(SKColors.Transparent);

        // Apply DPI scaling
        canvas.Save();
        canvas.Scale(dpiScale);

        // Adjust size for DPI-independent layout
        size = new SKSize(size.Width / dpiScale, size.Height / dpiScale);

        // Clear hit testing rects
        _lockButtonRects.Clear();
        _tempSlotPromoteRect = SKRect.Empty;

        // Calculate content height (temp slot + separator + numbered slots)
        int totalSlots = slots.Count + (tempSlot != null ? 1 : 0);
        float separatorHeight = tempSlot != null ? 8f : 0f;
        float contentHeight = Padding * 2 + totalSlots * SlotHeight + (totalSlots - 1) * SlotSpacing + separatorHeight;
        float actualHeight = Math.Min(contentHeight, size.Height);

        // Draw background with rounded corners
        var backgroundRect = new SKRoundRect(
            new SKRect(0, 0, size.Width, actualHeight),
            CornerRadius
        );
        canvas.DrawRoundRect(backgroundRect, _backgroundPaint);
        canvas.DrawRoundRect(backgroundRect, _borderPaint);

        float y = Padding;

        // Draw temp slot first (at top) if it exists
        if (tempSlot != null)
        {
            DrawTempSlot(canvas, tempSlot, y, size.Width - Padding * 2);
            y += SlotHeight + SlotSpacing + separatorHeight;

            // Draw subtle separator line
            float separatorY = y - separatorHeight / 2 - SlotSpacing / 2;
            canvas.DrawLine(
                Padding + 10, separatorY,
                size.Width - Padding - 10, separatorY,
                _borderPaint);
        }

        // Draw numbered slots
        for (int i = 0; i < slots.Count; i++)
        {
            bool isNextRoundRobin = isRoundRobinMode && i == nextRoundRobinIndex;
            DrawSlot(canvas, slots[i], i, y, size.Width - Padding * 2, i == activeSlotIndex, isNextRoundRobin);
            y += SlotHeight + SlotSpacing;
        }

        canvas.Restore();
    }

    /// <summary>
    /// Returns the slot index if the point is over a lock button, or -1 otherwise.
    /// </summary>
    public int HitTestLockButton(float x, float y)
    {
        for (int i = 0; i < _lockButtonRects.Count; i++)
        {
            if (_lockButtonRects[i].Contains(x, y))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Returns true if the point is over the temp slot promote button.
    /// </summary>
    public bool HitTestTempSlotPromote(float x, float y)
    {
        return !_tempSlotPromoteRect.IsEmpty && _tempSlotPromoteRect.Contains(x, y);
    }

    private void DrawTempSlot(SKCanvas canvas, ClipboardSlot slot, float y, float width)
    {
        float x = Padding;

        // Temp slot background with distinct styling
        var slotRect = new SKRoundRect(
            new SKRect(x, y, x + width, y + SlotHeight),
            SlotCornerRadius
        );
        canvas.DrawRoundRect(slotRect, _tempSlotPaint);
        canvas.DrawRoundRect(slotRect, _tempSlotBorderPaint);

        // "T" label for temp slot
        float indexX = x + IndexWidth / 2;
        float textY = y + SlotHeight / 2 + _indexPaint.TextSize / 3;
        canvas.DrawText("T", indexX, textY, _indexPaint);

        // Type glyph
        float typeX = x + IndexWidth + TypeGlyphWidth / 2;
        canvas.DrawText(slot.TypeGlyph, typeX, textY, _typeGlyphPaint);

        // Promote button (arrow icon) instead of lock
        float promoteX = x + width - LockIconSize - LockIconPadding;
        float promoteY = y + (SlotHeight - LockIconSize) / 2;
        var promoteRect = new SKRect(promoteX, promoteY, promoteX + LockIconSize, promoteY + LockIconSize);
        _tempSlotPromoteRect = promoteRect;

        // Draw promote icon (down arrow with plus)
        if (slot.HasContent)
        {
            DrawPromoteIcon(canvas, promoteRect);
        }

        // Content preview
        float previewX = x + IndexWidth + TypeGlyphWidth + 8;
        float previewWidth = width - IndexWidth - TypeGlyphWidth - LockIconSize - LockIconPadding - 16;

        if (slot.HasContent)
        {
            var preview = TruncateToWidth(slot.Preview, previewWidth, _textPaint);
            canvas.DrawText(preview, previewX, textY, _textPaint);
        }
        else
        {
            canvas.DrawText("Clipboard", previewX, textY, _secondaryTextPaint);
        }
    }

    private void DrawPromoteIcon(SKCanvas canvas, SKRect rect)
    {
        float cx = rect.MidX;
        float cy = rect.MidY;
        float size = rect.Width * 0.6f;

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            Color = TempSlotBorderColor,
            StrokeCap = SKStrokeCap.Round
        };

        // Draw down arrow
        var path = new SKPath();
        path.MoveTo(cx, cy - size / 2);
        path.LineTo(cx, cy + size / 2);
        path.MoveTo(cx - size / 3, cy + size / 6);
        path.LineTo(cx, cy + size / 2);
        path.LineTo(cx + size / 3, cy + size / 6);
        canvas.DrawPath(path, paint);
    }

    private void DrawRoundRobinIndicator(SKCanvas canvas, float slotX, float slotY)
    {
        // Draw a small arrow/chevron on the left side of the slot
        float indicatorSize = 6f;
        float cx = slotX - 4f; // Just outside the slot
        float cy = slotY + SlotHeight / 2;

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = TempSlotBorderColor // Amber color to match temp slot
        };

        // Draw a right-pointing triangle
        var path = new SKPath();
        path.MoveTo(cx - indicatorSize / 2, cy - indicatorSize);
        path.LineTo(cx + indicatorSize / 2, cy);
        path.LineTo(cx - indicatorSize / 2, cy + indicatorSize);
        path.Close();
        canvas.DrawPath(path, paint);
    }

    private void DrawSlot(SKCanvas canvas, ClipboardSlot slot, int index, float y, float width, bool isActive, bool isNextRoundRobin = false)
    {
        float x = Padding;

        // Slot background
        var slotRect = new SKRoundRect(
            new SKRect(x, y, x + width, y + SlotHeight),
            SlotCornerRadius
        );

        if (isActive)
        {
            canvas.DrawRoundRect(slotRect, _activeSlotPaint);
        }
        else
        {
            canvas.DrawRoundRect(slotRect, _slotPaint);
        }

        // Round-robin target indicator (arrow on left edge)
        if (isNextRoundRobin && !slot.IsLocked)
        {
            DrawRoundRobinIndicator(canvas, x, y);
        }

        // Slot index (1-based display)
        float indexX = x + IndexWidth / 2;
        float textY = y + SlotHeight / 2 + _indexPaint.TextSize / 3;
        canvas.DrawText($"{index + 1}", indexX, textY, _indexPaint);

        // Type glyph
        float typeX = x + IndexWidth + TypeGlyphWidth / 2;
        canvas.DrawText(slot.TypeGlyph, typeX, textY, _typeGlyphPaint);

        // Lock icon area (always visible, changes appearance based on state)
        float lockX = x + width - LockIconSize - LockIconPadding;
        float lockY = y + (SlotHeight - LockIconSize) / 2;
        var lockRect = new SKRect(lockX, lockY, lockX + LockIconSize, lockY + LockIconSize);
        _lockButtonRects.Add(lockRect);

        // Draw lock icon
        DrawLockIcon(canvas, lockRect, slot.IsLocked);

        // Content preview (adjust width to account for lock icon)
        float previewX = x + IndexWidth + TypeGlyphWidth + 8;
        float previewWidth = width - IndexWidth - TypeGlyphWidth - LockIconSize - LockIconPadding - 16;

        if (slot.HasContent)
        {
            var preview = TruncateToWidth(slot.Preview, previewWidth, _textPaint);
            canvas.DrawText(preview, previewX, textY, _textPaint);
        }
        else
        {
            canvas.DrawText("Empty", previewX, textY, _secondaryTextPaint);
        }
    }

    private void DrawLockIcon(SKCanvas canvas, SKRect rect, bool isLocked)
    {
        float cx = rect.MidX;
        float cy = rect.MidY;
        float size = rect.Width;

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = isLocked ? SKPaintStyle.Fill : SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = isLocked ? LockedIndicatorColor : new SKColor(100, 100, 100, 180)
        };

        // Lock body (rectangle at bottom)
        float bodyWidth = size * 0.7f;
        float bodyHeight = size * 0.5f;
        float bodyLeft = cx - bodyWidth / 2;
        float bodyTop = cy;
        var bodyRect = new SKRect(bodyLeft, bodyTop, bodyLeft + bodyWidth, bodyTop + bodyHeight);
        canvas.DrawRoundRect(new SKRoundRect(bodyRect, 2), paint);

        // Lock shackle (arc at top)
        float shackleWidth = size * 0.5f;
        float shackleHeight = size * 0.4f;
        float shackleLeft = cx - shackleWidth / 2;
        float shackleTop = cy - shackleHeight;

        using var shacklePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            Color = paint.Color
        };

        var shacklePath = new SKPath();
        if (isLocked)
        {
            // Closed shackle
            shacklePath.MoveTo(shackleLeft, cy);
            shacklePath.LineTo(shackleLeft, shackleTop + 3);
            shacklePath.ArcTo(new SKRect(shackleLeft, shackleTop, shackleLeft + shackleWidth, shackleTop + shackleHeight), 180, 180, false);
            shacklePath.LineTo(shackleLeft + shackleWidth, cy);
        }
        else
        {
            // Open shackle (shifted right)
            shacklePath.MoveTo(shackleLeft, cy);
            shacklePath.LineTo(shackleLeft, shackleTop + 3);
            shacklePath.ArcTo(new SKRect(shackleLeft, shackleTop, shackleLeft + shackleWidth, shackleTop + shackleHeight), 180, 180, false);
            shacklePath.LineTo(shackleLeft + shackleWidth, shackleTop + shackleHeight / 2);
        }
        canvas.DrawPath(shacklePath, shacklePaint);
    }

    private static string TruncateToWidth(string text, float maxWidth, SKPaint paint)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        float width = paint.MeasureText(text);
        if (width <= maxWidth)
            return text;

        // Binary search for truncation point
        int low = 0;
        int high = text.Length;
        const string ellipsis = "...";
        float ellipsisWidth = paint.MeasureText(ellipsis);

        while (low < high)
        {
            int mid = (low + high + 1) / 2;
            float testWidth = paint.MeasureText(text.AsSpan(0, mid)) + ellipsisWidth;

            if (testWidth <= maxWidth)
                low = mid;
            else
                high = mid - 1;
        }

        return low > 0 ? text[..low] + ellipsis : ellipsis;
    }

    public void Dispose()
    {
        _backgroundPaint.Dispose();
        _slotPaint.Dispose();
        _activeSlotPaint.Dispose();
        _tempSlotPaint.Dispose();
        _tempSlotBorderPaint.Dispose();
        _textPaint.Dispose();
        _secondaryTextPaint.Dispose();
        _indexPaint.Dispose();
        _typeGlyphPaint.Dispose();
        _lockIndicatorPaint.Dispose();
        _borderPaint.Dispose();
    }
}
