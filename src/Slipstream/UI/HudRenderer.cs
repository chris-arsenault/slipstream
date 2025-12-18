using System.IO;
using SkiaSharp;
using Slipstream.Models;
using Slipstream.Processing;

namespace Slipstream.UI;

public class HudRenderer : BaseRenderer
{

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

    // Hit testing - store lock button rects and slot rects
    private readonly List<SKRect> _lockButtonRects = new();
    private readonly List<SKRect> _slotRects = new();

    // Image thumbnail cache
    private readonly Dictionary<int, SKBitmap?> _thumbnailCache = new();
    private readonly Dictionary<int, int> _thumbnailHashes = new(); // Track content hash to invalidate cache

    // HudRenderer-specific paints (base class provides _backgroundPaint, _borderPaint)
    private readonly SKPaint _slotPaint;
    private readonly SKPaint _activeSlotPaint;
    private readonly SKPaint _tempSlotPaint;
    private readonly SKPaint _tempSlotBorderPaint;
    private readonly SKPaint _indexPaint;
    private readonly SKPaint _typeGlyphPaint;
    private readonly SKPaint _lockIndicatorPaint;
    private readonly SKPaint _hudTextPaint;
    private readonly SKPaint _hudSecondaryTextPaint;

    // Hit testing for temp slot promote button
    private SKRect _tempSlotPromoteRect;

    public HudRenderer() : base(ColorTheme.Dark)
    {
        _slotPaint = CreateFillPaint(_theme.SlotBackground);
        _activeSlotPaint = CreateFillPaint(_theme.SlotActive);
        _tempSlotPaint = CreateFillPaint(_theme.Accent.WithAlpha(40));
        _tempSlotBorderPaint = CreateStrokePaint(_theme.Accent, 2f);

        // HudRenderer uses slightly different text sizes than base class
        _hudTextPaint = CreateTextPaint(_theme.Text, 13f);
        _hudSecondaryTextPaint = CreateTextPaint(_theme.TextSecondary, 11f);

        _indexPaint = new SKPaint
        {
            Color = _theme.TextSecondary,
            IsAntialias = true,
            TextSize = 11f,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };

        _typeGlyphPaint = new SKPaint
        {
            Color = _theme.Accent,
            IsAntialias = true,
            TextSize = 10f,
            Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Normal),
            TextAlign = SKTextAlign.Center
        };

        _lockIndicatorPaint = CreateFillPaint(_theme.SlotLocked);
    }

    public void SetTheme(ColorPalette palette)
    {
        _theme = ColorTheme.GetTheme(palette);
        UpdatePaintColors();
    }

    protected override void UpdatePaintColors()
    {
        base.UpdatePaintColors();

        // Update HudRenderer-specific paints
        _slotPaint.Color = _theme.SlotBackground;
        _activeSlotPaint.Color = _theme.SlotActive;
        _tempSlotPaint.Color = _theme.Accent.WithAlpha(40);
        _tempSlotBorderPaint.Color = _theme.Accent;
        _hudTextPaint.Color = _theme.Text;
        _hudSecondaryTextPaint.Color = _theme.TextSecondary;
        _indexPaint.Color = _theme.TextSecondary;
        _typeGlyphPaint.Color = _theme.Accent;
        _lockIndicatorPaint.Color = _theme.SlotLocked;
    }

    public void Render(SKCanvas canvas, SKSize size, List<ClipboardSlot> slots, int activeSlotIndex, ClipboardSlot? tempSlot = null, float dpiScale = 1.0f, int nextRoundRobinIndex = -1, bool isRoundRobinMode = true, List<ProcessorDefinition>? toggledProcessors = null, List<ProcessorDefinition>? chordProcessors = null)
    {
        canvas.Clear(SKColors.Transparent);

        // Apply DPI scaling
        canvas.Save();
        canvas.Scale(dpiScale);

        // Adjust size for DPI-independent layout
        size = new SKSize(size.Width / dpiScale, size.Height / dpiScale);

        // Clear hit testing rects
        _lockButtonRects.Clear();
        _slotRects.Clear();
        _tempSlotPromoteRect = SKRect.Empty;

        // Determine if we have any processors to show
        int toggledCount = toggledProcessors?.Count ?? 0;
        int chordCount = chordProcessors?.Count ?? 0;
        bool hasProcessors = toggledCount > 0 || chordCount > 0;

        // Calculate header height for armed processors
        float headerHeight = hasProcessors ? 24f : 0f;

        // Calculate content height (header + temp slot + separator + numbered slots)
        int totalSlots = slots.Count + (tempSlot != null ? 1 : 0);
        float separatorHeight = tempSlot != null ? 8f : 0f;
        float contentHeight = Padding * 2 + headerHeight + totalSlots * SlotHeight + (totalSlots - 1) * SlotSpacing + separatorHeight;
        float actualHeight = Math.Min(contentHeight, size.Height);

        // Draw background with rounded corners
        var backgroundRect = new SKRoundRect(
            new SKRect(0, 0, size.Width, actualHeight),
            CornerRadius
        );
        canvas.DrawRoundRect(backgroundRect, _backgroundPaint);
        canvas.DrawRoundRect(backgroundRect, _borderPaint);

        float y = Padding;

        // Draw processor badges if any
        if (hasProcessors)
        {
            DrawProcessorBadges(canvas, toggledProcessors, chordProcessors, y, size.Width - Padding * 2);
            y += headerHeight;
        }

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

    private void DrawProcessorBadges(SKCanvas canvas, List<ProcessorDefinition>? toggledProcessors, List<ProcessorDefinition>? chordProcessors, float y, float width)
    {
        float x = Padding;
        const float badgeHeight = 18f;
        const float badgePadding = 4f;
        const float badgeSpacing = 4f;
        const float badgeCornerRadius = 4f;

        using var solidBadgePaint = CreateFillPaint(_theme.Accent.WithAlpha(180));
        using var outlineBadgePaint = CreateStrokePaint(_theme.Accent, 2f);
        using var solidTextPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            TextSize = 10f,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };
        using var outlineTextPaint = new SKPaint
        {
            Color = _theme.Accent,
            IsAntialias = true,
            TextSize = 10f,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };

        // Track which processors are already drawn to avoid duplicates
        var drawn = new HashSet<string>();

        // Draw toggled processors (solid)
        if (toggledProcessors != null)
        {
            foreach (var processor in toggledProcessors)
            {
                if (!drawn.Add(processor.Name)) continue;

                string badge = processor.Badge;
                float textWidth = solidTextPaint.MeasureText(badge);
                float badgeWidth = Math.Max(textWidth + badgePadding * 2, badgeHeight);

                var badgeRect = new SKRoundRect(
                    new SKRect(x, y + 2, x + badgeWidth, y + 2 + badgeHeight),
                    badgeCornerRadius
                );

                canvas.DrawRoundRect(badgeRect, solidBadgePaint);
                float textX = x + badgeWidth / 2;
                float textY = y + 2 + badgeHeight / 2 + solidTextPaint.TextSize / 3;
                canvas.DrawText(badge, textX, textY, solidTextPaint);

                x += badgeWidth + badgeSpacing;
            }
        }

        // Draw chord processors (outline) - skip if already drawn as toggled
        if (chordProcessors != null)
        {
            foreach (var processor in chordProcessors)
            {
                if (!drawn.Add(processor.Name)) continue;

                string badge = processor.Badge;
                float textWidth = outlineTextPaint.MeasureText(badge);
                float badgeWidth = Math.Max(textWidth + badgePadding * 2, badgeHeight);

                var badgeRect = new SKRoundRect(
                    new SKRect(x, y + 2, x + badgeWidth, y + 2 + badgeHeight),
                    badgeCornerRadius
                );

                canvas.DrawRoundRect(badgeRect, outlineBadgePaint);
                float textX = x + badgeWidth / 2;
                float textY = y + 2 + badgeHeight / 2 + outlineTextPaint.TextSize / 3;
                canvas.DrawText(badge, textX, textY, outlineTextPaint);

                x += badgeWidth + badgeSpacing;
            }
        }
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

    /// <summary>
    /// Returns the slot index if the point is over a slot (not on a button), or -1 otherwise.
    /// </summary>
    public int HitTestSlot(float x, float y)
    {
        // First check if we're on a lock button - those take priority
        if (HitTestLockButton(x, y) >= 0)
            return -1;

        // Check if we're on the temp slot promote button
        if (HitTestTempSlotPromote(x, y))
            return -1;

        // Check slot rects
        for (int i = 0; i < _slotRects.Count; i++)
        {
            if (_slotRects[i].Contains(x, y))
                return i;
        }
        return -1;
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
            var preview = TruncateToWidth(slot.Preview, previewWidth, _hudTextPaint);
            canvas.DrawText(preview, previewX, textY, _hudTextPaint);
        }
        else
        {
            canvas.DrawText("Clipboard", previewX, textY, _hudSecondaryTextPaint);
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
            Color = _theme.Accent,
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
            Color = _theme.Accent
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

        // Store slot rect for hit testing (before lock button area)
        var slotBounds = new SKRect(x, y, x + width, y + SlotHeight);
        _slotRects.Add(slotBounds);

        // Slot background
        var slotRect = new SKRoundRect(slotBounds, SlotCornerRadius);

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
            if (slot.Type == ClipboardType.Image && slot.ImageContent != null)
            {
                // Draw image thumbnail
                DrawImageThumbnail(canvas, slot, index, previewX, y, previewWidth);
            }
            else
            {
                var preview = TruncateToWidth(slot.Preview, previewWidth, _hudTextPaint);
                canvas.DrawText(preview, previewX, textY, _hudTextPaint);
            }
        }
        else
        {
            canvas.DrawText("Empty", previewX, textY, _hudSecondaryTextPaint);
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
            Color = isLocked ? _theme.SlotLocked : _theme.TextSecondary.WithAlpha(180)
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

    private void DrawImageThumbnail(SKCanvas canvas, ClipboardSlot slot, int index, float x, float y, float maxWidth)
    {
        if (slot.ImageContent == null) return;

        // Get or create cached thumbnail
        int contentHash = slot.ImageContent.Length; // Simple hash based on size
        if (!_thumbnailCache.TryGetValue(index, out var thumbnail) ||
            !_thumbnailHashes.TryGetValue(index, out var cachedHash) ||
            cachedHash != contentHash)
        {
            // Dispose old thumbnail
            if (thumbnail != null)
            {
                thumbnail.Dispose();
            }

            thumbnail = CreateThumbnail(slot.ImageContent, (int)SlotHeight - 4);
            _thumbnailCache[index] = thumbnail;
            _thumbnailHashes[index] = contentHash;
        }

        if (thumbnail == null) return;

        // Calculate thumbnail dimensions to fit in slot
        float thumbHeight = SlotHeight - 4;
        float thumbWidth = thumbnail.Width * (thumbHeight / thumbnail.Height);
        if (thumbWidth > maxWidth * 0.4f)
        {
            thumbWidth = maxWidth * 0.4f;
            thumbHeight = thumbnail.Height * (thumbWidth / thumbnail.Width);
        }

        float thumbY = y + (SlotHeight - thumbHeight) / 2;

        // Draw thumbnail with border
        var thumbRect = new SKRect(x, thumbY, x + thumbWidth, thumbY + thumbHeight);

        using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.Medium };
        canvas.DrawBitmap(thumbnail, thumbRect, paint);

        // Draw border around thumbnail
        using var borderPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = _theme.Border
        };
        canvas.DrawRect(thumbRect, borderPaint);

        // Draw image dimensions text next to thumbnail
        float textX = x + thumbWidth + 6;
        float textY = y + SlotHeight / 2 + _hudSecondaryTextPaint.TextSize / 3;

        // Get original image dimensions (not thumbnail size)
        string dimText = GetImageDimensions(slot.ImageContent);
        canvas.DrawText(dimText, textX, textY, _hudSecondaryTextPaint);
    }

    private static string GetImageDimensions(byte[] imageData)
    {
        try
        {
            using var stream = new MemoryStream(imageData);
            using var codec = SKCodec.Create(stream);
            if (codec != null)
            {
                return $"{codec.Info.Width}x{codec.Info.Height}";
            }
        }
        catch { }
        return "[Image]";
    }

    private static SKBitmap? CreateThumbnail(byte[] imageData, int maxHeight)
    {
        try
        {
            using var stream = new MemoryStream(imageData);
            using var codec = SKCodec.Create(stream);
            if (codec == null) return null;

            var info = codec.Info;
            float scale = (float)maxHeight / info.Height;
            int thumbWidth = (int)(info.Width * scale);
            int thumbHeight = maxHeight;

            var bitmap = new SKBitmap(thumbWidth, thumbHeight);
            using var fullBitmap = SKBitmap.Decode(imageData);
            if (fullBitmap == null)
            {
                bitmap.Dispose();
                return null;
            }

            fullBitmap.ScalePixels(bitmap, SKFilterQuality.Medium);
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        // Dispose thumbnail cache
        foreach (var thumbnail in _thumbnailCache.Values)
        {
            thumbnail?.Dispose();
        }
        _thumbnailCache.Clear();
        _thumbnailHashes.Clear();

        // Dispose base class paints
        _backgroundPaint.Dispose();
        _titleBarPaint.Dispose();
        _titlePaint.Dispose();
        _textPaint.Dispose();
        _secondaryTextPaint.Dispose();
        _borderPaint.Dispose();

        // Dispose HudRenderer-specific paints
        _slotPaint.Dispose();
        _activeSlotPaint.Dispose();
        _tempSlotPaint.Dispose();
        _tempSlotBorderPaint.Dispose();
        _hudTextPaint.Dispose();
        _hudSecondaryTextPaint.Dispose();
        _indexPaint.Dispose();
        _typeGlyphPaint.Dispose();
        _lockIndicatorPaint.Dispose();
    }
}
