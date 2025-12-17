using SkiaSharp;
using Slipstream.Models;
using Slipstream.Services;

namespace Slipstream.UI;

public class SettingsRenderer
{
    private AppSettings _settings;
    private ColorTheme _theme;
    private readonly MidiPresets? _midiPresets;

    // MIDI state (updated externally)
    private IReadOnlyList<string> _midiDevices = Array.Empty<string>();
    private string? _currentMidiDevice;
    private bool _midiConnected;

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
    private bool _presetDropdownOpen;
    private SKRect _presetDropdownAnchor; // Position of the dropdown button for overlay

    // Sticky app input state
    private string _stickyAppInput = "";
    private SKRect _stickyAppInputRect;
    private bool _stickyAppInputFocused;

    public event Action? CloseRequested;
    public event Action<AppSettings>? SettingsChanged;
    public event Action? ClearAllSlotsRequested;
    public event Action? ResetHotkeysRequested;
    public event Action<string>? MidiDeviceSelected;
    public event Action<string>? MidiPresetSelected;
    public event Action? EditMidiPresetRequested;
    public event Action? NewMidiPresetRequested;

    public bool IsStickyAppInputFocused => _stickyAppInputFocused;

    public SettingsRenderer(AppSettings settings, MidiPresets? midiPresets = null)
    {
        _settings = settings;
        _midiPresets = midiPresets;
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

    public void UpdateMidiState(IReadOnlyList<string> devices, string? currentDevice, bool connected)
    {
        _midiDevices = devices;
        _currentMidiDevice = currentDevice;
        _midiConnected = connected;
    }

    public void HandleTextInput(string text)
    {
        if (_stickyAppInputFocused)
        {
            _stickyAppInput += text;
        }
    }

    public void HandleKeyDown(System.Windows.Input.Key key)
    {
        if (!_stickyAppInputFocused) return;

        switch (key)
        {
            case System.Windows.Input.Key.Back:
                if (_stickyAppInput.Length > 0)
                    _stickyAppInput = _stickyAppInput[..^1];
                break;

            case System.Windows.Input.Key.Enter:
                if (!string.IsNullOrWhiteSpace(_stickyAppInput))
                {
                    AddStickyApp(_stickyAppInput.Trim());
                    _stickyAppInput = "";
                }
                break;

            case System.Windows.Input.Key.Escape:
                _stickyAppInputFocused = false;
                _stickyAppInput = "";
                break;
        }
    }

    private void AddStickyApp(string processName)
    {
        if (!_settings.StickyApps.Contains(processName, StringComparer.OrdinalIgnoreCase))
        {
            _settings.StickyApps.Add(processName);
            SettingsChanged?.Invoke(_settings);
        }
    }

    private void RemoveStickyApp(string processName)
    {
        _settings.StickyApps.RemoveAll(s => s.Equals(processName, StringComparison.OrdinalIgnoreCase));
        SettingsChanged?.Invoke(_settings);
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

            case "resetHotkeys":
                ResetHotkeysRequested?.Invoke();
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

            case "midiEnabled":
                _settings.MidiSettings.Enabled = !_settings.MidiSettings.Enabled;
                SettingsChanged?.Invoke(_settings);
                break;

            case "presetDropdownToggle":
                _presetDropdownOpen = !_presetDropdownOpen;
                break;

            case "editMidiPreset":
                EditMidiPresetRequested?.Invoke();
                break;

            case "newMidiPreset":
                NewMidiPresetRequested?.Invoke();
                break;

            case "stickyAppInput":
                _stickyAppInputFocused = true;
                break;

            case "addStickyApp":
                if (!string.IsNullOrWhiteSpace(_stickyAppInput))
                {
                    AddStickyApp(_stickyAppInput.Trim());
                    _stickyAppInput = "";
                }
                break;

            default:
                // Handle MIDI device selection
                if (buttonId.StartsWith("midiDevice_"))
                {
                    var deviceName = buttonId.Substring("midiDevice_".Length);
                    MidiDeviceSelected?.Invoke(deviceName);
                }
                // Handle MIDI preset selection
                else if (buttonId.StartsWith("midiPreset_"))
                {
                    var presetName = buttonId.Substring("midiPreset_".Length);
                    _settings.MidiSettings.ActivePreset = presetName;
                    _presetDropdownOpen = false; // Close dropdown after selection
                    MidiPresetSelected?.Invoke(presetName);
                    SettingsChanged?.Invoke(_settings);
                }
                // Handle sticky app removal
                else if (buttonId.StartsWith("removeStickyApp_"))
                {
                    var appName = buttonId.Substring("removeStickyApp_".Length);
                    RemoveStickyApp(appName);
                }
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

        // Appearance section
        leftY = DrawSectionHeader(canvas, "APPEARANCE", leftColumnX, leftY, columnWidth);
        leftY = DrawPaletteSelectorCompact(canvas, leftColumnX, leftY, columnWidth);
        leftY += SectionSpacing;

        // Data section
        leftY = DrawSectionHeader(canvas, "DATA", leftColumnX, leftY, columnWidth);
        leftY = DrawCompactButton(canvas, "Clear All Slots", leftColumnX, leftY, columnWidth, "clearAllSlots", true);
        leftY += SectionSpacing;

        // Sticky Apps section
        leftY = DrawSectionHeader(canvas, "STICKY APPS", leftColumnX, leftY, columnWidth);
        leftY = DrawStickyAppsSection(canvas, leftColumnX, leftY, columnWidth);

        // === RIGHT COLUMN ===
        float rightY = contentTop;

        // MIDI section
        rightY = DrawSectionHeader(canvas, "MIDI", rightColumnX, rightY, columnWidth);
        rightY = DrawMidiSection(canvas, rightColumnX, rightY, columnWidth);
        rightY += SectionSpacing;

        // Hotkeys section
        rightY = DrawSectionHeader(canvas, "HOTKEYS", rightColumnX, rightY, columnWidth);
        rightY = DrawHotkeyInfoCompact(canvas, rightColumnX, rightY, columnWidth);
        rightY += 4;
        rightY = DrawCompactButton(canvas, "Reset to Defaults", rightColumnX, rightY, columnWidth, "resetHotkeys", false);

        // Border
        canvas.DrawRoundRect(bgRect, _borderPaint);

        // Draw dropdown overlay last so it appears on top
        if (_presetDropdownOpen)
        {
            DrawPresetDropdownOverlay(canvas, columnWidth);
        }

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

    private float DrawStickyAppsSection(SKCanvas canvas, float x, float y, float width)
    {
        float inputHeight = 24f;
        float itemHeight = 22f;
        float buttonSize = 18f;

        // Description
        canvas.DrawText("Apps that reuse a single slot:", x, y + _secondaryTextPaint.TextSize, _secondaryTextPaint);
        y += _secondaryTextPaint.TextSize + 6;

        // List of sticky apps
        foreach (var app in _settings.StickyApps)
        {
            var itemRect = new SKRect(x, y, x + width, y + itemHeight);

            // Background
            bool isHovered = _hoveredButton == $"removeStickyApp_{app}";
            using var itemBgPaint = new SKPaint
            {
                Color = _theme.SlotBackground,
                IsAntialias = true
            };
            canvas.DrawRoundRect(new SKRoundRect(itemRect, 3), itemBgPaint);

            // App name
            using var appPaint = new SKPaint
            {
                Color = _theme.Text,
                IsAntialias = true,
                TextSize = 11f,
                Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Normal)
            };

            // Truncate if needed
            string displayName = app;
            float maxTextWidth = width - buttonSize - 16;
            while (appPaint.MeasureText(displayName) > maxTextWidth && displayName.Length > 3)
            {
                displayName = displayName[..^4] + "...";
            }
            canvas.DrawText(displayName, x + 6, y + itemHeight / 2 + 4, appPaint);

            // Remove button (X)
            var removeRect = new SKRect(x + width - buttonSize - 2, y + (itemHeight - buttonSize) / 2,
                                        x + width - 2, y + (itemHeight + buttonSize) / 2);

            using var removeBgPaint = new SKPaint
            {
                Color = isHovered ? _theme.DangerHover : SKColors.Transparent,
                IsAntialias = true
            };
            canvas.DrawRoundRect(new SKRoundRect(removeRect, 3), removeBgPaint);

            // X icon
            using var xPaint = new SKPaint
            {
                Color = isHovered ? _theme.Text : _theme.TextSecondary,
                IsAntialias = true,
                StrokeWidth = 1.5f,
                Style = SKPaintStyle.Stroke
            };
            float cx = removeRect.MidX;
            float cy = removeRect.MidY;
            float xSize = 4f;
            canvas.DrawLine(cx - xSize, cy - xSize, cx + xSize, cy + xSize, xPaint);
            canvas.DrawLine(cx + xSize, cy - xSize, cx - xSize, cy + xSize, xPaint);

            _buttons.Add(new ButtonRect($"removeStickyApp_{app}", removeRect));
            y += itemHeight + 2;
        }

        // Input field for adding new app
        y += 4;
        _stickyAppInputRect = new SKRect(x, y, x + width - 40, y + inputHeight);
        var addButtonRect = new SKRect(x + width - 36, y, x + width, y + inputHeight);

        // Input field background
        using var inputBgPaint = new SKPaint
        {
            Color = _stickyAppInputFocused ? _theme.Background : _theme.SlotBackground,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(_stickyAppInputRect, 3), inputBgPaint);

        // Input field border
        using var inputBorderPaint = new SKPaint
        {
            Color = _stickyAppInputFocused ? _theme.Accent : _theme.Border,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f
        };
        canvas.DrawRoundRect(new SKRoundRect(_stickyAppInputRect, 3), inputBorderPaint);

        // Input text or placeholder
        using var inputTextPaint = new SKPaint
        {
            Color = string.IsNullOrEmpty(_stickyAppInput) ? _theme.TextSecondary : _theme.Text,
            IsAntialias = true,
            TextSize = 11f,
            Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Normal)
        };
        string displayText = string.IsNullOrEmpty(_stickyAppInput) ? "Process name..." : _stickyAppInput;

        // Add cursor if focused
        if (_stickyAppInputFocused && !string.IsNullOrEmpty(_stickyAppInput))
        {
            displayText = _stickyAppInput + "|";
        }
        else if (_stickyAppInputFocused)
        {
            displayText = "|";
            inputTextPaint.Color = _theme.Text;
        }

        canvas.DrawText(displayText, x + 6, y + inputHeight / 2 + 4, inputTextPaint);
        _buttons.Add(new ButtonRect("stickyAppInput", _stickyAppInputRect));

        // Add button
        bool addHovered = _hoveredButton == "addStickyApp";
        using var addBgPaint = new SKPaint
        {
            Color = addHovered ? _theme.Accent : _theme.Button,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(addButtonRect, 3), addBgPaint);

        using var addTextPaint = new SKPaint
        {
            Color = _theme.Text,
            IsAntialias = true,
            TextSize = 11f,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };
        canvas.DrawText("+", addButtonRect.MidX, addButtonRect.MidY + 4, addTextPaint);
        _buttons.Add(new ButtonRect("addStickyApp", addButtonRect));

        y += inputHeight + 4;

        // Helper text
        canvas.DrawText("Check console for process names", x, y + _secondaryTextPaint.TextSize, _secondaryTextPaint);
        y += _secondaryTextPaint.TextSize;

        return y + ItemSpacing;
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

    private float DrawMidiSection(SKCanvas canvas, float x, float y, float width)
    {
        // Enable/Disable toggle
        y = DrawCompactToggle(canvas, "Enable MIDI", _settings.MidiSettings.Enabled, x, y, width, "midiEnabled");

        // Connection status indicator
        float statusY = y;
        using var statusPaint = new SKPaint
        {
            Color = _midiConnected ? new SKColor(0x4C, 0xAF, 0x50) : _theme.TextSecondary, // Green if connected
            IsAntialias = true,
            TextSize = 10f,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        };

        float indicatorSize = 8f;
        var indicatorColor = _midiConnected ? new SKColor(0x4C, 0xAF, 0x50) : new SKColor(0x9E, 0x9E, 0x9E);
        using var indicatorPaint = new SKPaint { Color = indicatorColor, IsAntialias = true };
        canvas.DrawCircle(x + indicatorSize / 2, statusY + 5, indicatorSize / 2, indicatorPaint);

        string statusText = _midiConnected
            ? $"Connected: {_currentMidiDevice ?? "Unknown"}"
            : "No device connected";
        canvas.DrawText(statusText, x + indicatorSize + 6, statusY + statusPaint.TextSize, statusPaint);
        y = statusY + 18;

        // Only show device and preset selection if MIDI is enabled
        if (!_settings.MidiSettings.Enabled)
        {
            return y + ItemSpacing;
        }

        // Device selection
        y += 4;
        canvas.DrawText("Device:", x, y + _textPaint.TextSize, _textPaint);
        y += _textPaint.TextSize + 4;

        if (_midiDevices.Count == 0)
        {
            canvas.DrawText("No MIDI devices found", x + 8, y + _secondaryTextPaint.TextSize, _secondaryTextPaint);
            y += _secondaryTextPaint.TextSize + 8;
        }
        else
        {
            foreach (var device in _midiDevices)
            {
                bool isSelected = device == _currentMidiDevice;
                bool isHovered = _hoveredButton == $"midiDevice_{device}";

                float itemHeight = 22f;
                var itemRect = new SKRect(x, y, x + width, y + itemHeight);

                // Background for selected/hovered
                if (isSelected || isHovered)
                {
                    var bgColor = isSelected ? _theme.Accent.WithAlpha(60) : _theme.Button;
                    using var bgPaint = new SKPaint { Color = bgColor, IsAntialias = true };
                    canvas.DrawRoundRect(new SKRoundRect(itemRect, 3), bgPaint);
                }

                // Selection indicator
                if (isSelected)
                {
                    using var checkPaint = new SKPaint
                    {
                        Color = _theme.Accent,
                        IsAntialias = true,
                        TextSize = 11f,
                        Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
                    };
                    canvas.DrawText("✓", x + 4, y + 15, checkPaint);
                }

                // Device name
                float textX = x + (isSelected ? 18 : 8);
                using var devicePaint = new SKPaint
                {
                    Color = isSelected ? _theme.Text : _theme.TextSecondary,
                    IsAntialias = true,
                    TextSize = 11f,
                    Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
                };

                // Truncate long device names
                string displayName = device;
                float maxTextWidth = width - 24;
                while (devicePaint.MeasureText(displayName) > maxTextWidth && displayName.Length > 3)
                {
                    displayName = displayName.Substring(0, displayName.Length - 4) + "...";
                }
                canvas.DrawText(displayName, textX, y + 15, devicePaint);

                _buttons.Add(new ButtonRect($"midiDevice_{device}", itemRect));
                y += itemHeight + 2;
            }
        }

        // Preset selection - dropdown style
        y += 8;
        y = DrawPresetDropdown(canvas, x, y, width);

        return y + ItemSpacing;
    }

    private float DrawPresetDropdown(SKCanvas canvas, float x, float y, float width)
    {
        float dropdownHeight = 28f;
        float buttonWidth = 36f;
        float buttonSpacing = 4f;

        // Label with Edit/New buttons on the right
        canvas.DrawText("Preset:", x, y + _textPaint.TextSize, _textPaint);

        // New button
        float btnY = y - 2;
        float newBtnX = x + width - buttonWidth;
        var newRect = new SKRect(newBtnX, btnY, newBtnX + buttonWidth, btnY + 20f);
        bool newHovered = _hoveredButton == "newMidiPreset";
        using var newPaint = new SKPaint
        {
            Color = newHovered ? _theme.ButtonHover : _theme.Button,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(newRect, 3), newPaint);
        using var btnTextPaint = new SKPaint
        {
            Color = _theme.TextSecondary,
            IsAntialias = true,
            TextSize = 9f,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        };
        canvas.DrawText("New", newRect.MidX, newRect.MidY + 3, btnTextPaint);
        _buttons.Add(new ButtonRect("newMidiPreset", newRect));

        // Edit button
        float editBtnX = newBtnX - buttonWidth - buttonSpacing;
        var editRect = new SKRect(editBtnX, btnY, editBtnX + buttonWidth, btnY + 20f);
        bool editHovered = _hoveredButton == "editMidiPreset";
        using var editPaint = new SKPaint
        {
            Color = editHovered ? _theme.Accent : _theme.Button,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(editRect, 3), editPaint);
        canvas.DrawText("Edit", editRect.MidX, editRect.MidY + 3, btnTextPaint);
        _buttons.Add(new ButtonRect("editMidiPreset", editRect));

        y += _textPaint.TextSize + 4;

        // Get current preset name
        var presets = _midiPresets?.GetAllPresets() ?? Array.Empty<MidiControlScheme>();
        var currentPreset = presets.FirstOrDefault(p => p.Name == _settings.MidiSettings.ActivePreset);
        string displayName = currentPreset?.Name ?? _settings.MidiSettings.ActivePreset ?? "Select...";

        // Dropdown button
        var dropdownRect = new SKRect(x, y, x + width, y + dropdownHeight);
        _presetDropdownAnchor = dropdownRect; // Store for overlay positioning
        bool isHovered = _hoveredButton == "presetDropdownToggle";

        // Background
        using var bgPaint = new SKPaint
        {
            Color = isHovered ? _theme.ButtonHover : _theme.Button,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(dropdownRect, 4), bgPaint);

        // Border
        using var borderPaint = new SKPaint
        {
            Color = _presetDropdownOpen ? _theme.Accent : _theme.Border,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f
        };
        canvas.DrawRoundRect(new SKRoundRect(dropdownRect, 4), borderPaint);

        // Text
        using var textPaint = new SKPaint
        {
            Color = _theme.Text,
            IsAntialias = true,
            TextSize = 11f,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        };
        canvas.DrawText(displayName, x + 8, y + dropdownHeight / 2 + 4, textPaint);

        // Dropdown arrow
        float arrowX = x + width - 16;
        float arrowY = y + dropdownHeight / 2;
        using var arrowPaint = new SKPaint
        {
            Color = _theme.TextSecondary,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            StrokeCap = SKStrokeCap.Round
        };
        if (_presetDropdownOpen)
        {
            // Up arrow
            canvas.DrawLine(arrowX - 4, arrowY + 2, arrowX, arrowY - 2, arrowPaint);
            canvas.DrawLine(arrowX, arrowY - 2, arrowX + 4, arrowY + 2, arrowPaint);
        }
        else
        {
            // Down arrow
            canvas.DrawLine(arrowX - 4, arrowY - 2, arrowX, arrowY + 2, arrowPaint);
            canvas.DrawLine(arrowX, arrowY + 2, arrowX + 4, arrowY - 2, arrowPaint);
        }

        _buttons.Add(new ButtonRect("presetDropdownToggle", dropdownRect));
        y += dropdownHeight;

        return y;
    }

    private void DrawPresetDropdownOverlay(SKCanvas canvas, float width)
    {
        float itemHeight = 24f;
        var presets = _midiPresets?.GetAllPresets() ?? Array.Empty<MidiControlScheme>();
        if (presets.Count == 0) return;

        float x = _presetDropdownAnchor.Left;
        float y = _presetDropdownAnchor.Bottom + 2;
        float menuHeight = presets.Count * itemHeight + 4;
        var menuRect = new SKRect(x, y, x + width, y + menuHeight);

        // Shadow
        using var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 40),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3)
        };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(x + 2, y + 2, x + width + 2, y + menuHeight + 2), 4), shadowPaint);

        // Menu background
        using var menuBgPaint = new SKPaint
        {
            Color = _theme.Background,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(menuRect, 4), menuBgPaint);

        // Menu border
        using var menuBorderPaint = new SKPaint
        {
            Color = _theme.Border,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f
        };
        canvas.DrawRoundRect(new SKRoundRect(menuRect, 4), menuBorderPaint);

        float itemY = y + 2;
        foreach (var preset in presets)
        {
            bool isSelected = preset.Name == _settings.MidiSettings.ActivePreset;
            bool itemHovered = _hoveredButton == $"midiPreset_{preset.Name}";

            var itemRect = new SKRect(x + 2, itemY, x + width - 2, itemY + itemHeight);

            // Item hover/selected background
            if (itemHovered || isSelected)
            {
                var itemBgColor = isSelected ? _theme.Accent.WithAlpha(60) : _theme.ButtonHover;
                using var itemBgPaint = new SKPaint { Color = itemBgColor, IsAntialias = true };
                canvas.DrawRoundRect(new SKRoundRect(itemRect, 3), itemBgPaint);
            }

            // Item text
            using var itemTextPaint = new SKPaint
            {
                Color = isSelected ? _theme.Text : _theme.TextSecondary,
                IsAntialias = true,
                TextSize = 11f,
                Typeface = SKTypeface.FromFamilyName("Segoe UI", isSelected ? SKFontStyle.Bold : SKFontStyle.Normal)
            };
            canvas.DrawText(preset.Name, x + 10, itemY + itemHeight / 2 + 4, itemTextPaint);

            _buttons.Add(new ButtonRect($"midiPreset_{preset.Name}", itemRect));
            itemY += itemHeight;
        }
    }

    private record ButtonRect(string Id, SKRect Rect);
}
