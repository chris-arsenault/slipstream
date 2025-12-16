using SkiaSharp;
using Slipstream.Models;

namespace Slipstream.UI;

public class SettingsRenderer
{
    private AppSettings _settings;
    private ColorTheme _theme;

    // Layout
    private const float TitleBarHeight = 40f;
    private const float CornerRadius = 12f;
    private const float Padding = 16f;
    private const float ColumnGap = 20f;
    private const float SectionSpacing = 16f;
    private const float ItemSpacing = 8f;

    // Paints
    private readonly SKPaint _backgroundPaint;
    private readonly SKPaint _titleBarPaint;
    private readonly SKPaint _titlePaint;
    private readonly SKPaint _textPaint;
    private readonly SKPaint _secondaryTextPaint;
    private readonly SKPaint _borderPaint;

    // Interactive elements
    private readonly List<ButtonRect> _buttons = new();
    private SKRect _closeButtonRect;

    private bool _closeButtonHovered;
    private string? _hoveredButton;
    private string? _pressedButton;

    public event Action? CloseRequested;
    public event Action<AppSettings>? SettingsChanged;
    public event Action? ClearAllSlotsRequested;

    public SettingsRenderer(AppSettings settings)
    {
        _settings = settings;
        _theme = ColorTheme.GetTheme(settings.ColorPalette);

        _backgroundPaint = new SKPaint
        {
            Color = _theme.Background,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        _titleBarPaint = new SKPaint
        {
            Color = _theme.TitleBar,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        _titlePaint = new SKPaint
        {
            Color = _theme.Text,
            IsAntialias = true,
            TextSize = 14f,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };

        _textPaint = new SKPaint
        {
            Color = _theme.Text,
            IsAntialias = true,
            TextSize = 12f,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        };

        _secondaryTextPaint = new SKPaint
        {
            Color = _theme.TextSecondary,
            IsAntialias = true,
            TextSize = 10f,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        };

        _borderPaint = new SKPaint
        {
            Color = _theme.Border,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f
        };
    }

    public void SetTheme(ColorPalette palette)
    {
        _theme = ColorTheme.GetTheme(palette);
        UpdatePaintColors();
    }

    private void UpdatePaintColors()
    {
        _backgroundPaint.Color = _theme.Background;
        _titleBarPaint.Color = _theme.TitleBar;
        _titlePaint.Color = _theme.Text;
        _textPaint.Color = _theme.Text;
        _secondaryTextPaint.Color = _theme.TextSecondary;
        _borderPaint.Color = _theme.Border;
    }

    public bool IsInTitleBar(SKPoint point)
    {
        return point.Y < TitleBarHeight && !_closeButtonRect.Contains(point);
    }

    public string? GetHoveredButton(SKPoint point)
    {
        if (_closeButtonRect.Contains(point))
            return "close";

        foreach (var btn in _buttons)
        {
            if (btn.Rect.Contains(point))
                return btn.Id;
        }

        return null;
    }

    public void HandleMouseMove(SKPoint point)
    {
        _closeButtonHovered = _closeButtonRect.Contains(point);

        _hoveredButton = null;
        foreach (var btn in _buttons)
        {
            if (btn.Rect.Contains(point))
            {
                _hoveredButton = btn.Id;
                break;
            }
        }
    }

    public void HandleMouseDown(SKPoint point)
    {
        if (_closeButtonRect.Contains(point))
        {
            _pressedButton = "close";
            return;
        }

        foreach (var btn in _buttons)
        {
            if (btn.Rect.Contains(point))
            {
                _pressedButton = btn.Id;
                break;
            }
        }
    }

    public void HandleMouseUp(SKPoint point)
    {
        if (_pressedButton == null)
            return;

        bool stillOverButton = false;
        if (_pressedButton == "close")
        {
            stillOverButton = _closeButtonRect.Contains(point);
        }
        else
        {
            foreach (var btn in _buttons)
            {
                if (btn.Id == _pressedButton && btn.Rect.Contains(point))
                {
                    stillOverButton = true;
                    break;
                }
            }
        }

        if (stillOverButton)
        {
            ExecuteButtonAction(_pressedButton);
        }

        _pressedButton = null;
    }

    private void ExecuteButtonAction(string buttonId)
    {
        switch (buttonId)
        {
            case "close":
                CloseRequested?.Invoke();
                break;

            case "clearAllSlots":
                ClearAllSlotsRequested?.Invoke();
                break;

            case "startWithWindows":
                _settings.StartWithWindows = !_settings.StartWithWindows;
                SettingsChanged?.Invoke(_settings);
                break;

            case "startMinimized":
                _settings.StartMinimized = !_settings.StartMinimized;
                SettingsChanged?.Invoke(_settings);
                break;

            case "showHudOnStart":
                _settings.ShowHudOnStart = !_settings.ShowHudOnStart;
                SettingsChanged?.Invoke(_settings);
                break;

            case "hudClickThrough":
                _settings.HudClickThrough = !_settings.HudClickThrough;
                SettingsChanged?.Invoke(_settings);
                break;

            case "autoPromote":
                _settings.AutoPromote = !_settings.AutoPromote;
                SettingsChanged?.Invoke(_settings);
                break;

            case "slotBehaviorRoundRobin":
                _settings.SlotBehavior = SlotBehavior.RoundRobin;
                SettingsChanged?.Invoke(_settings);
                break;

            case "slotBehaviorFixed":
                _settings.SlotBehavior = SlotBehavior.Fixed;
                SettingsChanged?.Invoke(_settings);
                break;

            case "paletteDark":
                _settings.ColorPalette = ColorPalette.Dark;
                SetTheme(ColorPalette.Dark);
                SettingsChanged?.Invoke(_settings);
                break;

            case "paletteLight":
                _settings.ColorPalette = ColorPalette.Light;
                SetTheme(ColorPalette.Light);
                SettingsChanged?.Invoke(_settings);
                break;

            case "paletteTerminal":
                _settings.ColorPalette = ColorPalette.Terminal;
                SetTheme(ColorPalette.Terminal);
                SettingsChanged?.Invoke(_settings);
                break;
        }
    }

    public void Render(SKCanvas canvas, SKSize size, SKPoint mousePos, float dpiScale = 1.0f)
    {
        _buttons.Clear();
        canvas.Clear(SKColors.Transparent);

        canvas.Save();
        canvas.Scale(dpiScale);

        size = new SKSize(size.Width / dpiScale, size.Height / dpiScale);
        mousePos = new SKPoint(mousePos.X / dpiScale, mousePos.Y / dpiScale);

        // Main background
        var bgRect = new SKRoundRect(new SKRect(0, 0, size.Width, size.Height), CornerRadius);
        canvas.DrawRoundRect(bgRect, _backgroundPaint);

        // Title bar
        DrawTitleBar(canvas, size);

        // Two-column layout
        float contentTop = TitleBarHeight + Padding;
        float contentWidth = size.Width - Padding * 2;
        float columnWidth = (contentWidth - ColumnGap) / 2;
        float leftColumnX = Padding;
        float rightColumnX = Padding + columnWidth + ColumnGap;

        // === LEFT COLUMN ===
        float leftY = contentTop;

        // Slot Behavior section
        leftY = DrawSectionHeader(canvas, "SLOT BEHAVIOR", leftColumnX, leftY, columnWidth);
        leftY = DrawCompactToggle(canvas, "Auto-promote", _settings.AutoPromote, leftColumnX, leftY, columnWidth, "autoPromote");
        leftY = DrawCompactRadioGroup(canvas, "Promote target:", leftColumnX, leftY, columnWidth,
            ("Round Robin", "slotBehaviorRoundRobin", _settings.SlotBehavior == SlotBehavior.RoundRobin),
            ("Fixed", "slotBehaviorFixed", _settings.SlotBehavior == SlotBehavior.Fixed));
        leftY += SectionSpacing;

        // Startup section
        leftY = DrawSectionHeader(canvas, "STARTUP", leftColumnX, leftY, columnWidth);
        leftY = DrawCompactToggle(canvas, "Start with Windows", _settings.StartWithWindows, leftColumnX, leftY, columnWidth, "startWithWindows");
        leftY = DrawCompactToggle(canvas, "Start minimized", _settings.StartMinimized, leftColumnX, leftY, columnWidth, "startMinimized");
        leftY += SectionSpacing;

        // HUD section
        leftY = DrawSectionHeader(canvas, "HUD", leftColumnX, leftY, columnWidth);
        leftY = DrawCompactToggle(canvas, "Show on start", _settings.ShowHudOnStart, leftColumnX, leftY, columnWidth, "showHudOnStart");
        leftY = DrawCompactToggle(canvas, "Click-through", _settings.HudClickThrough, leftColumnX, leftY, columnWidth, "hudClickThrough");
        leftY += SectionSpacing;

        // Data section
        leftY = DrawSectionHeader(canvas, "DATA", leftColumnX, leftY, columnWidth);
        leftY = DrawCompactButton(canvas, "Clear All Slots", leftColumnX, leftY, columnWidth, "clearAllSlots", true);

        // === RIGHT COLUMN ===
        float rightY = contentTop;

        // Appearance section
        rightY = DrawSectionHeader(canvas, "APPEARANCE", rightColumnX, rightY, columnWidth);
        rightY = DrawPaletteSelectorCompact(canvas, rightColumnX, rightY, columnWidth);
        rightY += SectionSpacing;

        // Hotkeys section
        rightY = DrawSectionHeader(canvas, "HOTKEYS", rightColumnX, rightY, columnWidth);
        rightY = DrawHotkeyInfoCompact(canvas, rightColumnX, rightY, columnWidth);

        // Border
        canvas.DrawRoundRect(bgRect, _borderPaint);

        canvas.Restore();
    }

    private void DrawTitleBar(SKCanvas canvas, SKSize size)
    {
        var titleBarPath = new SKPath();
        titleBarPath.AddRoundRect(new SKRect(0, 0, size.Width, TitleBarHeight + CornerRadius),
            CornerRadius, CornerRadius);
        titleBarPath.AddRect(new SKRect(0, CornerRadius, size.Width, TitleBarHeight + CornerRadius));
        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, size.Width, TitleBarHeight));
        canvas.DrawPath(titleBarPath, _titleBarPaint);
        canvas.Restore();

        canvas.DrawText("Slipstream Settings", Padding, TitleBarHeight / 2 + _titlePaint.TextSize / 3, _titlePaint);

        float closeSize = 20f;
        _closeButtonRect = new SKRect(
            size.Width - Padding - closeSize,
            (TitleBarHeight - closeSize) / 2,
            size.Width - Padding,
            (TitleBarHeight + closeSize) / 2
        );

        if (_closeButtonHovered)
        {
            using var hoverPaint = new SKPaint { Color = _theme.DangerHover, IsAntialias = true };
            canvas.DrawRoundRect(new SKRoundRect(_closeButtonRect, 4), hoverPaint);
        }

        using var xPaint = new SKPaint
        {
            Color = _theme.Text,
            IsAntialias = true,
            StrokeWidth = 2f,
            Style = SKPaintStyle.Stroke
        };
        float cx = _closeButtonRect.MidX;
        float cy = _closeButtonRect.MidY;
        float xSize = 5f;
        canvas.DrawLine(cx - xSize, cy - xSize, cx + xSize, cy + xSize, xPaint);
        canvas.DrawLine(cx + xSize, cy - xSize, cx - xSize, cy + xSize, xPaint);
    }

    private float DrawSectionHeader(SKCanvas canvas, string title, float x, float y, float width)
    {
        using var paint = new SKPaint
        {
            Color = _theme.Accent,
            IsAntialias = true,
            TextSize = 10f,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };

        canvas.DrawText(title, x, y + paint.TextSize, paint);

        float textWidth = paint.MeasureText(title);
        using var linePaint = new SKPaint
        {
            Color = _theme.Accent.WithAlpha(80),
            IsAntialias = true,
            StrokeWidth = 1f
        };
        canvas.DrawLine(x, y + paint.TextSize + 3, x + textWidth, y + paint.TextSize + 3, linePaint);

        return y + paint.TextSize + 10;
    }

    private float DrawCompactToggle(SKCanvas canvas, string label, bool value, float x, float y, float width, string id)
    {
        float toggleWidth = 36f;
        float toggleHeight = 18f;
        float rowHeight = 22f;

        canvas.DrawText(label, x, y + _textPaint.TextSize, _textPaint);

        var toggleRect = new SKRect(x + width - toggleWidth, y + (rowHeight - toggleHeight) / 2, x + width, y + (rowHeight - toggleHeight) / 2 + toggleHeight);

        var trackColor = value ? _theme.Accent : _theme.Button;
        if (_hoveredButton == id)
        {
            trackColor = value ? _theme.Accent.WithAlpha(220) : _theme.ButtonHover;
        }
        using var trackPaint = new SKPaint { Color = trackColor, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(toggleRect, toggleHeight / 2), trackPaint);

        float thumbRadius = toggleHeight / 2 - 2;
        float thumbX = value ? toggleRect.Right - thumbRadius - 3 : toggleRect.Left + thumbRadius + 3;
        using var thumbPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawCircle(thumbX, toggleRect.MidY, thumbRadius, thumbPaint);

        _buttons.Add(new ButtonRect(id, new SKRect(x, y, x + width, y + rowHeight)));

        return y + rowHeight + ItemSpacing;
    }

    private float DrawCompactRadioGroup(SKCanvas canvas, string label, float x, float y, float width, params (string Label, string Id, bool Selected)[] options)
    {
        canvas.DrawText(label, x, y + _textPaint.TextSize, _textPaint);
        y += _textPaint.TextSize + 6;

        float radioSize = 14f;
        float optionX = x + 8;

        foreach (var (optionLabel, id, selected) in options)
        {
            var isHovered = _hoveredButton == id;

            var radioRect = new SKRect(optionX, y, optionX + radioSize, y + radioSize);

            using var outlinePaint = new SKPaint
            {
                Color = isHovered ? _theme.Accent : _theme.TextSecondary,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f
            };
            canvas.DrawOval(radioRect, outlinePaint);

            if (selected)
            {
                using var fillPaint = new SKPaint
                {
                    Color = _theme.Accent,
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };
                var innerRect = new SKRect(optionX + 3, y + 3, optionX + radioSize - 3, y + radioSize - 3);
                canvas.DrawOval(innerRect, fillPaint);
            }

            float labelX = optionX + radioSize + 4;
            canvas.DrawText(optionLabel, labelX, y + _textPaint.TextSize - 2, _textPaint);

            float labelWidth = _textPaint.MeasureText(optionLabel);
            var clickRect = new SKRect(optionX, y - 2, labelX + labelWidth + 4, y + radioSize + 2);
            _buttons.Add(new ButtonRect(id, clickRect));

            optionX = labelX + labelWidth + 16;
        }

        return y + radioSize + ItemSpacing;
    }

    private float DrawCompactButton(SKCanvas canvas, string label, float x, float y, float width, string id, bool isDanger = false)
    {
        float btnHeight = 26f;
        float btnWidth = Math.Min(width, 110f);

        var btnRect = new SKRect(x, y, x + btnWidth, y + btnHeight);

        var btnColor = isDanger
            ? (_hoveredButton == id ? _theme.DangerHover : _theme.Danger)
            : (_hoveredButton == id ? _theme.ButtonHover : _theme.Button);

        using var btnPaint = new SKPaint { Color = btnColor, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(btnRect, 4), btnPaint);

        using var btnTextPaint = new SKPaint
        {
            Color = _theme.Text,
            IsAntialias = true,
            TextSize = 11f,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        };
        canvas.DrawText(label, btnRect.MidX, btnRect.MidY + 4, btnTextPaint);
        _buttons.Add(new ButtonRect(id, btnRect));

        return y + btnHeight + ItemSpacing;
    }

    private float DrawPaletteSelectorCompact(SKCanvas canvas, float x, float y, float width)
    {
        float swatchWidth = 56f;
        float swatchHeight = 36f;
        float swatchSpacing = 8f;

        var palettes = new[]
        {
            ("Dark", "paletteDark", ColorPalette.Dark, ColorTheme.Dark),
            ("Light", "paletteLight", ColorPalette.Light, ColorTheme.Light),
            ("Terminal", "paletteTerminal", ColorPalette.Terminal, ColorTheme.Terminal)
        };

        float startX = x;

        foreach (var (label, id, palette, theme) in palettes)
        {
            bool isSelected = _settings.ColorPalette == palette;
            bool isHovered = _hoveredButton == id;

            var swatchRect = new SKRect(startX, y, startX + swatchWidth, y + swatchHeight);

            using var bgPaint = new SKPaint { Color = theme.Background, IsAntialias = true };
            canvas.DrawRoundRect(new SKRoundRect(swatchRect, 4), bgPaint);

            // Mini slot
            float miniSlotY = y + 5;
            var miniSlotRect = new SKRect(startX + 4, miniSlotY, startX + swatchWidth - 4, miniSlotY + 8);
            using var slotPaint = new SKPaint { Color = theme.SlotBackground, IsAntialias = true };
            canvas.DrawRoundRect(new SKRoundRect(miniSlotRect, 2), slotPaint);

            // Accent bar
            float accentY = miniSlotY + 10;
            var accentRect = new SKRect(startX + 4, accentY, startX + swatchWidth - 4, accentY + 5);
            using var accentPaint = new SKPaint { Color = theme.Accent, IsAntialias = true };
            canvas.DrawRoundRect(new SKRoundRect(accentRect, 2), accentPaint);

            // Text sample
            using var textPaint = new SKPaint
            {
                Color = theme.Text,
                IsAntialias = true,
                TextSize = 7f,
                TextAlign = SKTextAlign.Center,
                Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
            };
            canvas.DrawText("Abc", swatchRect.MidX, accentY + 14, textPaint);

            // Border
            var borderColor = isSelected ? _theme.Accent : (isHovered ? _theme.Accent.WithAlpha(150) : _theme.Border);
            float borderWidth = isSelected ? 2f : 1f;
            using var borderPaint = new SKPaint
            {
                Color = borderColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = borderWidth
            };
            canvas.DrawRoundRect(new SKRoundRect(swatchRect, 4), borderPaint);

            _buttons.Add(new ButtonRect(id, swatchRect));

            startX += swatchWidth + swatchSpacing;
        }

        // Labels
        y += swatchHeight + 2;
        startX = x;

        using var labelPaint = new SKPaint
        {
            Color = _theme.TextSecondary,
            IsAntialias = true,
            TextSize = 9f,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        };

        foreach (var (label, _, _, _) in palettes)
        {
            canvas.DrawText(label, startX + swatchWidth / 2, y + labelPaint.TextSize, labelPaint);
            startX += swatchWidth + swatchSpacing;
        }

        return y + labelPaint.TextSize + ItemSpacing;
    }

    private float DrawHotkeyInfoCompact(SKCanvas canvas, float x, float y, float width)
    {
        var hotkeys = new[]
        {
            ("Copy to slot", "Ctrl+Alt+1-0"),
            ("Paste from slot", "Ctrl+Shift+1-0"),
            ("Promote temp", "Ctrl+Alt+C"),
            ("Paste active", "Ctrl+Alt+V"),
            ("Cycle slots", "Ctrl+Alt+↑↓"),
            ("Toggle HUD", "Ctrl+Alt+H"),
        };

        using var keyPaint = new SKPaint
        {
            Color = _theme.TextSecondary,
            IsAntialias = true,
            TextSize = 10f,
            TextAlign = SKTextAlign.Right,
            Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Normal)
        };

        foreach (var (action, keys) in hotkeys)
        {
            canvas.DrawText(action, x, y + _secondaryTextPaint.TextSize, _secondaryTextPaint);
            canvas.DrawText(keys, x + width, y + _secondaryTextPaint.TextSize, keyPaint);
            y += _secondaryTextPaint.TextSize + 5;
        }

        y += 4;
        canvas.DrawText("Edit config.json to customize", x, y + _secondaryTextPaint.TextSize, _secondaryTextPaint);

        return y + _secondaryTextPaint.TextSize + ItemSpacing;
    }

    private record ButtonRect(string Id, SKRect Rect);
}
