using SkiaSharp;
using Slipstream.Models;

namespace Slipstream.UI;

public class SettingsRenderer
{
    private AppSettings _settings;

    // Colors
    private static readonly SKColor BackgroundColor = new(35, 35, 35, 255);
    private static readonly SKColor TitleBarColor = new(25, 25, 25, 255);
    private static readonly SKColor TextColor = new(220, 220, 220, 255);
    private static readonly SKColor SecondaryTextColor = new(150, 150, 150, 255);
    private static readonly SKColor AccentColor = new(70, 130, 180, 255);
    private static readonly SKColor ButtonColor = new(55, 55, 55, 255);
    private static readonly SKColor ButtonHoverColor = new(70, 70, 70, 255);
    private static readonly SKColor BorderColor = new(60, 60, 60, 255);
    private static readonly SKColor CloseButtonHoverColor = new(200, 60, 60, 255);

    // Layout
    private const float TitleBarHeight = 40f;
    private const float CornerRadius = 12f;
    private const float Padding = 20f;
    private const float SectionSpacing = 24f;
    private const float ItemSpacing = 12f;

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

    public SettingsRenderer(AppSettings settings)
    {
        _settings = settings;

        _backgroundPaint = new SKPaint
        {
            Color = BackgroundColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        _titleBarPaint = new SKPaint
        {
            Color = TitleBarColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        _titlePaint = new SKPaint
        {
            Color = TextColor,
            IsAntialias = true,
            TextSize = 14f,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
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

        _borderPaint = new SKPaint
        {
            Color = BorderColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f
        };
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

        // Check if still over the same button
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

            case "slotMinus":
                if (_settings.SlotCount > 1)
                {
                    _settings.SlotCount--;
                    SettingsChanged?.Invoke(_settings);
                }
                break;

            case "slotPlus":
                if (_settings.SlotCount < 20)
                {
                    _settings.SlotCount++;
                    SettingsChanged?.Invoke(_settings);
                }
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
        }
    }

    public void Render(SKCanvas canvas, SKSize size, SKPoint mousePos, float dpiScale = 1.0f)
    {
        _buttons.Clear();
        canvas.Clear(SKColors.Transparent);

        // Apply DPI scaling
        canvas.Save();
        canvas.Scale(dpiScale);

        // Adjust size and mouse position for DPI-independent layout
        size = new SKSize(size.Width / dpiScale, size.Height / dpiScale);
        mousePos = new SKPoint(mousePos.X / dpiScale, mousePos.Y / dpiScale);

        // Main background with rounded corners
        var bgRect = new SKRoundRect(new SKRect(0, 0, size.Width, size.Height), CornerRadius);
        canvas.DrawRoundRect(bgRect, _backgroundPaint);

        // Title bar
        DrawTitleBar(canvas, size);

        // Content area
        float y = TitleBarHeight + Padding;
        float contentWidth = size.Width - Padding * 2;

        // Section: General
        y = DrawSection(canvas, "General", y, contentWidth);
        y = DrawSlotCountSetting(canvas, y, contentWidth);
        y += SectionSpacing;

        // Section: Slot Behavior
        y = DrawSection(canvas, "Slot Behavior", y, contentWidth);
        y = DrawToggleSetting(canvas, "Auto-promote to numbered slot", _settings.AutoPromote, y, contentWidth, "autoPromote");
        y = DrawRadioSetting(canvas, "Promote target", y, contentWidth,
            ("Round Robin", "slotBehaviorRoundRobin", _settings.SlotBehavior == SlotBehavior.RoundRobin),
            ("Fixed (active slot)", "slotBehaviorFixed", _settings.SlotBehavior == SlotBehavior.Fixed));
        y += SectionSpacing;

        // Section: Startup
        y = DrawSection(canvas, "Startup", y, contentWidth);
        y = DrawToggleSetting(canvas, "Start with Windows", _settings.StartWithWindows, y, contentWidth, "startWithWindows");
        y = DrawToggleSetting(canvas, "Start minimized", _settings.StartMinimized, y, contentWidth, "startMinimized");
        y += SectionSpacing;

        // Section: HUD
        y = DrawSection(canvas, "HUD", y, contentWidth);
        y = DrawToggleSetting(canvas, "Show HUD on start", _settings.ShowHudOnStart, y, contentWidth, "showHudOnStart");
        y = DrawToggleSetting(canvas, "Click-through mode", _settings.HudClickThrough, y, contentWidth, "hudClickThrough");
        y += SectionSpacing;

        // Section: Hotkeys
        y = DrawSection(canvas, "Hotkeys", y, contentWidth);
        y = DrawHotkeyInfo(canvas, y, contentWidth);

        // Border
        canvas.DrawRoundRect(bgRect, _borderPaint);

        canvas.Restore();
    }

    private void DrawTitleBar(SKCanvas canvas, SKSize size)
    {
        // Title bar background
        var titleBarPath = new SKPath();
        titleBarPath.AddRoundRect(new SKRect(0, 0, size.Width, TitleBarHeight + CornerRadius),
            CornerRadius, CornerRadius);
        titleBarPath.AddRect(new SKRect(0, CornerRadius, size.Width, TitleBarHeight + CornerRadius));
        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, size.Width, TitleBarHeight));
        canvas.DrawPath(titleBarPath, _titleBarPaint);
        canvas.Restore();

        // Title text
        canvas.DrawText("Slipstream Settings", Padding, TitleBarHeight / 2 + _titlePaint.TextSize / 3, _titlePaint);

        // Close button
        float closeSize = 20f;
        _closeButtonRect = new SKRect(
            size.Width - Padding - closeSize,
            (TitleBarHeight - closeSize) / 2,
            size.Width - Padding,
            (TitleBarHeight + closeSize) / 2
        );

        if (_closeButtonHovered)
        {
            using var hoverPaint = new SKPaint { Color = CloseButtonHoverColor, IsAntialias = true };
            canvas.DrawRoundRect(new SKRoundRect(_closeButtonRect, 4), hoverPaint);
        }

        // Draw X
        using var xPaint = new SKPaint
        {
            Color = TextColor,
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

    private float DrawSection(SKCanvas canvas, string title, float y, float width)
    {
        using var sectionPaint = new SKPaint
        {
            Color = AccentColor,
            IsAntialias = true,
            TextSize = 12f,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };

        canvas.DrawText(title.ToUpperInvariant(), Padding, y + sectionPaint.TextSize, sectionPaint);

        // Underline
        float textWidth = sectionPaint.MeasureText(title.ToUpperInvariant());
        using var linePaint = new SKPaint
        {
            Color = AccentColor.WithAlpha(100),
            IsAntialias = true,
            StrokeWidth = 1f
        };
        canvas.DrawLine(Padding, y + sectionPaint.TextSize + 4, Padding + textWidth, y + sectionPaint.TextSize + 4, linePaint);

        return y + sectionPaint.TextSize + ItemSpacing + 8;
    }

    private float DrawSlotCountSetting(SKCanvas canvas, float y, float width)
    {
        canvas.DrawText("Number of slots", Padding, y + _textPaint.TextSize, _textPaint);

        float btnSize = 28f;
        float rightX = Padding + width;
        float valueWidth = 40f;

        var minusRect = new SKRect(rightX - btnSize - valueWidth - btnSize, y, rightX - valueWidth - btnSize, y + btnSize);
        var plusRect = new SKRect(rightX - btnSize, y, rightX, y + btnSize);

        // Draw minus button
        var minusBtnColor = _hoveredButton == "slotMinus" ? ButtonHoverColor : ButtonColor;
        using var minusPaint = new SKPaint { Color = minusBtnColor, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(minusRect, 4), minusPaint);

        using var minusTextPaint = new SKPaint
        {
            Color = TextColor,
            IsAntialias = true,
            TextSize = 16f,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        };
        canvas.DrawText("-", minusRect.MidX, minusRect.MidY + 6, minusTextPaint);
        _buttons.Add(new ButtonRect("slotMinus", minusRect));

        // Draw value
        using var valuePaint = new SKPaint
        {
            Color = TextColor,
            IsAntialias = true,
            TextSize = 14f,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };
        canvas.DrawText(_settings.SlotCount.ToString(), (minusRect.Right + plusRect.Left) / 2, y + btnSize / 2 + 5, valuePaint);

        // Draw plus button
        var plusBtnColor = _hoveredButton == "slotPlus" ? ButtonHoverColor : ButtonColor;
        using var plusPaint = new SKPaint { Color = plusBtnColor, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(plusRect, 4), plusPaint);
        canvas.DrawText("+", plusRect.MidX, plusRect.MidY + 6, minusTextPaint);
        _buttons.Add(new ButtonRect("slotPlus", plusRect));

        return y + btnSize + ItemSpacing;
    }

    private float DrawToggleSetting(SKCanvas canvas, string label, bool value, float y, float width, string id)
    {
        canvas.DrawText(label, Padding, y + _textPaint.TextSize, _textPaint);

        float toggleWidth = 44f;
        float toggleHeight = 22f;
        float rightX = Padding + width;

        var toggleRect = new SKRect(rightX - toggleWidth, y, rightX, y + toggleHeight);

        // Track - highlight if hovered
        var trackColor = value ? AccentColor : new SKColor(80, 80, 80);
        if (_hoveredButton == id)
        {
            trackColor = value ? AccentColor.WithAlpha(220) : new SKColor(100, 100, 100);
        }
        using var trackPaint = new SKPaint
        {
            Color = trackColor,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(toggleRect, toggleHeight / 2), trackPaint);

        // Thumb
        float thumbRadius = toggleHeight / 2 - 3;
        float thumbX = value ? toggleRect.Right - thumbRadius - 4 : toggleRect.Left + thumbRadius + 4;
        using var thumbPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawCircle(thumbX, toggleRect.MidY, thumbRadius, thumbPaint);

        _buttons.Add(new ButtonRect(id, toggleRect));

        return y + toggleHeight + ItemSpacing;
    }

    private float DrawRadioSetting(SKCanvas canvas, string label, float y, float width, params (string Label, string Id, bool Selected)[] options)
    {
        canvas.DrawText(label, Padding, y + _textPaint.TextSize, _textPaint);
        y += _textPaint.TextSize + 8;

        float radioSize = 18f;
        float optionSpacing = 16f;

        foreach (var (optionLabel, id, selected) in options)
        {
            float optionX = Padding + 8;

            // Radio circle outline
            var radioRect = new SKRect(optionX, y, optionX + radioSize, y + radioSize);
            var isHovered = _hoveredButton == id;

            using var outlinePaint = new SKPaint
            {
                Color = isHovered ? AccentColor : new SKColor(120, 120, 120),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f
            };
            canvas.DrawOval(radioRect, outlinePaint);

            // Radio fill if selected
            if (selected)
            {
                using var fillPaint = new SKPaint
                {
                    Color = AccentColor,
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };
                var innerRect = new SKRect(optionX + 4, y + 4, optionX + radioSize - 4, y + radioSize - 4);
                canvas.DrawOval(innerRect, fillPaint);
            }

            // Option label
            canvas.DrawText(optionLabel, optionX + radioSize + 8, y + _textPaint.TextSize, _textPaint);

            // Clickable area includes label
            float labelWidth = _textPaint.MeasureText(optionLabel);
            var clickRect = new SKRect(optionX, y, optionX + radioSize + 8 + labelWidth + 8, y + radioSize);
            _buttons.Add(new ButtonRect(id, clickRect));

            y += radioSize + optionSpacing;
        }

        return y;
    }

    private float DrawHotkeyInfo(SKCanvas canvas, float y, float width)
    {
        var hotkeys = new[]
        {
            ("Copy to slot 1-10", "Ctrl+Alt+1-0"),
            ("Paste from slot 1-10", "Ctrl+Shift+1-0"),
            ("Promote temp slot", "Ctrl+Alt+C"),
            ("Paste from active slot", "Ctrl+Alt+V"),
            ("Cycle slots", "Ctrl+Alt+Up/Down"),
            ("Toggle HUD", "Ctrl+Alt+H"),
        };

        foreach (var (action, keys) in hotkeys)
        {
            canvas.DrawText(action, Padding, y + _textPaint.TextSize, _textPaint);

            using var keyPaint = new SKPaint
            {
                Color = SecondaryTextColor,
                IsAntialias = true,
                TextSize = 12f,
                TextAlign = SKTextAlign.Right,
                Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Normal)
            };
            canvas.DrawText(keys, Padding + width, y + _textPaint.TextSize, keyPaint);

            y += _textPaint.TextSize + 8;
        }

        y += ItemSpacing;

        // Note about customization
        canvas.DrawText("Edit config.json to customize hotkeys", Padding, y + _secondaryTextPaint.TextSize, _secondaryTextPaint);
        y += _secondaryTextPaint.TextSize + ItemSpacing;

        return y;
    }

    private record ButtonRect(string Id, SKRect Rect);
}
