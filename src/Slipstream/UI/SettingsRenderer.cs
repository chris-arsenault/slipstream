using SkiaSharp;
using Slipstream.Models;
using Slipstream.Processing;
using Slipstream.Services;

namespace Slipstream.UI;

public class SettingsRenderer : BaseRenderer
{
    private AppSettings _settings;
    private readonly MidiPresets? _midiPresets;

    // Tab state
    private int _activeTab = 0;
    private static readonly string[] TabNames = { "General", "Behavior", "MIDI", "Hotkeys" };

    // MIDI state (updated externally)
    private IReadOnlyList<string> _midiDevices = Array.Empty<string>();
    private string? _currentMidiDevice;
    private bool _midiConnected;

    // Layout constants
    private const float TitleBarHeight = 40f;
    private const float TabBarHeight = 32f;
    private const float CornerRadius = 12f;
    private const float Padding = 16f;
    private const float SectionSpacing = 16f;
    private const float ItemSpacing = 8f;

    // Hotkey editing state
    private string? _editingHotkeyId;
    private float _hotkeyScrollOffset = 0f;
    private float _hotkeyContentHeight = 0f;
    private SKRect _hotkeyScrollArea;

    // Button action dispatch
    private readonly Dictionary<string, Action> _buttonActions;
    private readonly List<(string Prefix, Action<string> Handler)> _prefixHandlers;

    // Interactive elements
    private readonly List<ButtonRect> _buttons = new();
    private SKRect _closeButtonRect;

    private bool _closeButtonHovered;
    private string? _hoveredButton;
    private string? _pressedButton;
    private bool _presetDropdownOpen;
    private SKRect _presetDropdownAnchor;

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
    public event Action<string, HotkeyBinding>? HotkeyBindingChanged;

    public bool IsStickyAppInputFocused => _stickyAppInputFocused;
    public bool IsEditingHotkey => _editingHotkeyId != null;
    public string? EditingHotkeyId => _editingHotkeyId;

    public SettingsRenderer(AppSettings settings, MidiPresets? midiPresets = null)
        : base(ColorTheme.GetTheme(settings.ColorPalette))
    {
        _settings = settings;
        _midiPresets = midiPresets;

        _buttonActions = InitializeButtonActions();
        _prefixHandlers = InitializePrefixHandlers();
    }

    private Dictionary<string, Action> InitializeButtonActions() => new()
    {
        ["close"] = () => CloseRequested?.Invoke(),
        ["clearAllSlots"] = () => ClearAllSlotsRequested?.Invoke(),
        ["resetHotkeys"] = () => ResetHotkeysRequested?.Invoke(),
        ["startWithWindows"] = () => { _settings.StartWithWindows = !_settings.StartWithWindows; SettingsChanged?.Invoke(_settings); },
        ["startMinimized"] = () => { _settings.StartMinimized = !_settings.StartMinimized; SettingsChanged?.Invoke(_settings); },
        ["showHudOnStart"] = () => { _settings.ShowHudOnStart = !_settings.ShowHudOnStart; SettingsChanged?.Invoke(_settings); },
        ["hudClickThrough"] = () => { _settings.HudClickThrough = !_settings.HudClickThrough; SettingsChanged?.Invoke(_settings); },
        ["autoPromote"] = () => { _settings.AutoPromote = !_settings.AutoPromote; SettingsChanged?.Invoke(_settings); },
        ["slotBehaviorRoundRobin"] = () => { _settings.SlotBehavior = SlotBehavior.RoundRobin; SettingsChanged?.Invoke(_settings); },
        ["slotBehaviorFixed"] = () => { _settings.SlotBehavior = SlotBehavior.Fixed; SettingsChanged?.Invoke(_settings); },
        ["paletteDark"] = () => { _settings.ColorPalette = ColorPalette.Dark; SetTheme(ColorPalette.Dark); SettingsChanged?.Invoke(_settings); },
        ["paletteLight"] = () => { _settings.ColorPalette = ColorPalette.Light; SetTheme(ColorPalette.Light); SettingsChanged?.Invoke(_settings); },
        ["paletteTerminal"] = () => { _settings.ColorPalette = ColorPalette.Terminal; SetTheme(ColorPalette.Terminal); SettingsChanged?.Invoke(_settings); },
        ["midiEnabled"] = () => { _settings.MidiSettings.Enabled = !_settings.MidiSettings.Enabled; SettingsChanged?.Invoke(_settings); },
        ["presetDropdownToggle"] = () => _presetDropdownOpen = !_presetDropdownOpen,
        ["editMidiPreset"] = () => EditMidiPresetRequested?.Invoke(),
        ["newMidiPreset"] = () => NewMidiPresetRequested?.Invoke(),
        ["stickyAppInput"] = () => _stickyAppInputFocused = true,
        ["addStickyApp"] = () => { if (!string.IsNullOrWhiteSpace(_stickyAppInput)) { AddStickyApp(_stickyAppInput.Trim()); _stickyAppInput = ""; } },
        ["tab0"] = () => { _activeTab = 0; _presetDropdownOpen = false; },
        ["tab1"] = () => { _activeTab = 1; _presetDropdownOpen = false; },
        ["tab2"] = () => { _activeTab = 2; _presetDropdownOpen = false; },
        ["tab3"] = () => { _activeTab = 3; _presetDropdownOpen = false; _editingHotkeyId = null; },
        ["cancelHotkeyEdit"] = () => _editingHotkeyId = null,
    };

    private List<(string Prefix, Action<string> Handler)> InitializePrefixHandlers() =>
    [
        ("midiDevice_", deviceName => MidiDeviceSelected?.Invoke(deviceName)),
        ("midiPreset_", presetName => { _settings.MidiSettings.ActivePreset = presetName; _presetDropdownOpen = false; MidiPresetSelected?.Invoke(presetName); SettingsChanged?.Invoke(_settings); }),
        ("removeStickyApp_", appName => RemoveStickyApp(appName)),
        ("editHotkey_", hotkeyId => StartEditingHotkey(hotkeyId)),
    ];

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

    private void StartEditingHotkey(string hotkeyId)
    {
        _editingHotkeyId = hotkeyId;
    }

    public void CancelHotkeyEdit()
    {
        _editingHotkeyId = null;
    }

    public void SetHotkeyBinding(ModifierKeys modifiers, VirtualKey key)
    {
        if (_editingHotkeyId == null) return;

        var binding = new HotkeyBinding(modifiers, key);
        _settings.HotkeyBindings[_editingHotkeyId] = binding;
        HotkeyBindingChanged?.Invoke(_editingHotkeyId, binding);
        SettingsChanged?.Invoke(_settings);
        _editingHotkeyId = null;
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

    public void HandleScroll(float delta)
    {
        if (_activeTab == 3) // Hotkeys tab
        {
            _hotkeyScrollOffset = Math.Max(0, Math.Min(_hotkeyScrollOffset - delta * 30,
                Math.Max(0, _hotkeyContentHeight - _hotkeyScrollArea.Height)));
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

    protected override void UpdatePaintColors()
    {
        base.UpdatePaintColors();
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
        if (_buttonActions.TryGetValue(buttonId, out var action))
        {
            action();
            return;
        }

        foreach (var (prefix, handler) in _prefixHandlers)
        {
            if (buttonId.StartsWith(prefix))
            {
                handler(buttonId[prefix.Length..]);
                return;
            }
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

        // Tab bar
        DrawTabBar(canvas, size);

        // Content area
        float contentTop = TitleBarHeight + TabBarHeight + Padding;
        float contentWidth = size.Width - Padding * 2;
        float contentHeight = size.Height - contentTop - Padding;

        // Draw active tab content
        switch (_activeTab)
        {
            case 0:
                DrawGeneralTab(canvas, Padding, contentTop, contentWidth, contentHeight);
                break;
            case 1:
                DrawAppearanceTab(canvas, Padding, contentTop, contentWidth, contentHeight);
                break;
            case 2:
                DrawMidiTab(canvas, Padding, contentTop, contentWidth, contentHeight);
                break;
            case 3:
                DrawHotkeysTab(canvas, Padding, contentTop, contentWidth, contentHeight);
                break;
        }

        // Border
        canvas.DrawRoundRect(bgRect, _borderPaint);

        // Draw dropdown overlay last so it appears on top
        if (_presetDropdownOpen && _activeTab == 2)
        {
            DrawPresetDropdownOverlay(canvas, contentWidth);
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

    private void DrawTabBar(SKCanvas canvas, SKSize size)
    {
        float tabY = TitleBarHeight;
        float tabWidth = size.Width / TabNames.Length;
        float tabHeight = TabBarHeight;

        // Tab bar background - full width
        using var tabBarBgPaint = new SKPaint { Color = _theme.TitleBar.WithAlpha(128), IsAntialias = true };
        canvas.DrawRect(0, tabY, size.Width, tabHeight, tabBarBgPaint);

        for (int i = 0; i < TabNames.Length; i++)
        {
            float tabX = i * tabWidth;
            var tabRect = new SKRect(tabX, tabY, tabX + tabWidth, tabY + tabHeight);
            bool isActive = _activeTab == i;
            bool isHovered = _hoveredButton == $"tab{i}";

            // Tab background
            if (isActive)
            {
                using var activePaint = new SKPaint { Color = _theme.Background, IsAntialias = true };
                canvas.DrawRect(tabRect, activePaint);

                // Active tab underline
                using var underlinePaint = new SKPaint { Color = _theme.Accent, IsAntialias = true };
                canvas.DrawRect(tabX, tabY + tabHeight - 2, tabWidth, 2, underlinePaint);
            }
            else if (isHovered)
            {
                using var hoverPaint = new SKPaint { Color = _theme.ButtonHover, IsAntialias = true };
                canvas.DrawRect(tabRect, hoverPaint);
            }

            // Tab text
            using var textPaint = new SKPaint
            {
                Color = isActive ? _theme.Accent : _theme.TextSecondary,
                IsAntialias = true,
                TextSize = 12f,
                TextAlign = SKTextAlign.Center,
                Typeface = SKTypeface.FromFamilyName("Segoe UI", isActive ? SKFontStyle.Bold : SKFontStyle.Normal)
            };
            canvas.DrawText(TabNames[i], tabRect.MidX, tabRect.MidY + textPaint.TextSize / 3, textPaint);

            _buttons.Add(new ButtonRect($"tab{i}", tabRect));
        }
    }

    private void DrawGeneralTab(SKCanvas canvas, float x, float y, float width, float height)
    {
        // Startup section
        y = DrawSectionHeader(canvas, "STARTUP", x, y, width);
        y = DrawCompactToggle(canvas, "Start with Windows", _settings.StartWithWindows, x, y, width, "startWithWindows");
        y = DrawCompactToggle(canvas, "Start minimized", _settings.StartMinimized, x, y, width, "startMinimized");
        y += SectionSpacing;

        // HUD section
        y = DrawSectionHeader(canvas, "HUD", x, y, width);
        y = DrawCompactToggle(canvas, "Show on start", _settings.ShowHudOnStart, x, y, width, "showHudOnStart");
        y = DrawCompactToggle(canvas, "Click-through", _settings.HudClickThrough, x, y, width, "hudClickThrough");
        y += SectionSpacing;

        // Data section
        y = DrawSectionHeader(canvas, "DATA", x, y, width);
        DrawCompactButton(canvas, "Clear All Slots", x, y, width, "clearAllSlots", true);
    }

    private void DrawAppearanceTab(SKCanvas canvas, float x, float y, float width, float height)
    {
        // Appearance section
        y = DrawSectionHeader(canvas, "THEME", x, y, width);
        y = DrawPaletteSelectorCompact(canvas, x, y, width);
        y += SectionSpacing;

        // Slot Behavior section
        y = DrawSectionHeader(canvas, "SLOT BEHAVIOR", x, y, width);
        y = DrawCompactToggle(canvas, "Auto-promote temp slot", _settings.AutoPromote, x, y, width, "autoPromote");
        y = DrawCompactRadioGroup(canvas, "Promote target:", x, y, width,
            ("Round Robin", "slotBehaviorRoundRobin", _settings.SlotBehavior == SlotBehavior.RoundRobin),
            ("Fixed", "slotBehaviorFixed", _settings.SlotBehavior == SlotBehavior.Fixed));
        y += SectionSpacing;

        // Sticky Apps section
        y = DrawSectionHeader(canvas, "STICKY APPS", x, y, width);
        DrawStickyAppsSection(canvas, x, y, width);
    }

    private void DrawMidiTab(SKCanvas canvas, float x, float y, float width, float height)
    {
        // MIDI section
        y = DrawSectionHeader(canvas, "MIDI CONTROLLER", x, y, width);
        y = DrawMidiSection(canvas, x, y, width);
    }

    private void DrawHotkeysTab(SKCanvas canvas, float x, float y, float width, float height)
    {
        // Set up scroll area
        _hotkeyScrollArea = new SKRect(x, y, x + width, y + height - 40);

        // Calculate content height
        var categories = GetHotkeyCategories();
        float itemHeight = 24f;
        float categorySpacing = 16f;
        float totalHeight = 0;
        foreach (var (category, items) in categories)
        {
            totalHeight += 18; // header
            totalHeight += items.Count * itemHeight;
            totalHeight += categorySpacing;
        }
        _hotkeyContentHeight = totalHeight;

        // Clip and draw hotkeys
        canvas.Save();
        canvas.ClipRect(_hotkeyScrollArea);

        float contentY = y - _hotkeyScrollOffset;
        foreach (var (category, items) in categories)
        {
            contentY = DrawHotkeyCategory(canvas, category, items, x, contentY, width, itemHeight);
            contentY += categorySpacing;
        }

        canvas.Restore();

        // Draw scrollbar if needed
        if (_hotkeyContentHeight > _hotkeyScrollArea.Height)
        {
            DrawScrollbar(canvas, _hotkeyScrollArea, _hotkeyScrollOffset, _hotkeyContentHeight);
        }

        // Reset button at bottom
        float btnY = y + height - 30;
        DrawCompactButton(canvas, "Reset to Defaults", x, btnY, width, "resetHotkeys", false);
    }

    private List<(string Category, List<HotkeyDisplayItem> Items)> GetHotkeyCategories()
    {
        var categories = new List<(string Category, List<HotkeyDisplayItem> Items)>();

        // Navigation
        var navigation = new List<HotkeyDisplayItem>
        {
            new("Toggle HUD", "ToggleHud"),
            new("Cycle Forward", "CycleForward"),
            new("Cycle Backward", "CycleBackward"),
            new("Promote Temp", "PromoteTempSlot"),
            new("Paste Active", "PasteFromActiveSlot"),
        };
        categories.Add(("Navigation", navigation));

        // Copy to Slot - combine regular and numpad
        var copySlots = new List<HotkeyDisplayItem>();
        for (int i = 1; i <= 10; i++)
        {
            copySlots.Add(new($"Copy to Slot {i}", $"CopyToSlot{i}", $"CopyToSlotNumpad{i}"));
        }
        categories.Add(("Copy to Slot", copySlots));

        // Paste from Slot - combine regular and numpad
        var pasteSlots = new List<HotkeyDisplayItem>();
        for (int i = 1; i <= 10; i++)
        {
            pasteSlots.Add(new($"Paste Slot {i}", $"PasteFromSlot{i}", $"PasteFromSlotNumpad{i}"));
        }
        categories.Add(("Paste from Slot", pasteSlots));

        // Processor Toggles
        var processors = new List<HotkeyDisplayItem>();
        foreach (var def in ProcessorDefinitions.All)
        {
            processors.Add(new($"Toggle {def.DisplayName}", $"ToggleProcessor{def.Name}"));
        }
        categories.Add(("Processor Toggles", processors));

        return categories;
    }

    private record HotkeyDisplayItem(string Name, string PrimaryId, string? SecondaryId = null);

    private float DrawHotkeyCategory(SKCanvas canvas, string category, List<HotkeyDisplayItem> items, float x, float y, float width, float itemHeight)
    {
        // Skip if completely off screen
        if (y > _hotkeyScrollArea.Bottom || y + 18 + items.Count * itemHeight < _hotkeyScrollArea.Top)
        {
            return y + 18 + items.Count * itemHeight;
        }

        // Category header
        using var headerPaint = new SKPaint
        {
            Color = _theme.Accent,
            IsAntialias = true,
            TextSize = 10f,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };
        canvas.DrawText(category, x, y + headerPaint.TextSize, headerPaint);
        y += 18;

        // Items
        foreach (var item in items)
        {
            if (y < _hotkeyScrollArea.Top - itemHeight || y > _hotkeyScrollArea.Bottom)
            {
                y += itemHeight;
                continue;
            }

            var itemRect = new SKRect(x, y, x + width, y + itemHeight - 2);
            bool isPrimaryHovered = _hoveredButton == $"editHotkey_{item.PrimaryId}";
            bool isSecondaryHovered = item.SecondaryId != null && _hoveredButton == $"editHotkey_{item.SecondaryId}";
            bool isPrimaryEditing = _editingHotkeyId == item.PrimaryId;
            bool isSecondaryEditing = item.SecondaryId != null && _editingHotkeyId == item.SecondaryId;

            // Background for whole row on hover/edit
            if (isPrimaryEditing || isSecondaryEditing)
            {
                using var editBgPaint = new SKPaint { Color = _theme.Accent.WithAlpha(30), IsAntialias = true };
                canvas.DrawRoundRect(new SKRoundRect(itemRect, 3), editBgPaint);
            }
            else if (isPrimaryHovered || isSecondaryHovered)
            {
                using var hoverBgPaint = new SKPaint { Color = _theme.ButtonHover.WithAlpha(128), IsAntialias = true };
                canvas.DrawRoundRect(new SKRoundRect(itemRect, 3), hoverBgPaint);
            }

            // Action name (left side)
            using var namePaint = new SKPaint
            {
                Color = _theme.Text,
                IsAntialias = true,
                TextSize = 10f,
                Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
            };
            float nameWidth = namePaint.MeasureText(item.Name);
            canvas.DrawText(item.Name, x + 4, y + itemHeight / 2 + 3, namePaint);

            // Bindings start after name with small gap
            float bindingX = x + nameWidth + 16;
            float bindingY = y + itemHeight / 2 + 3;

            using var bindingPaint = new SKPaint
            {
                Color = _theme.TextSecondary,
                IsAntialias = true,
                TextSize = 9f,
                Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Normal)
            };

            // Primary binding
            string primaryText;
            if (isPrimaryEditing)
            {
                primaryText = "Press keys...";
                bindingPaint.Color = _theme.Accent;
            }
            else if (_settings.HotkeyBindings.TryGetValue(item.PrimaryId, out var primaryBinding))
            {
                primaryText = FormatHotkeyBinding(primaryBinding);
            }
            else
            {
                primaryText = "Not set";
            }

            // Draw primary binding with click area
            float primaryWidth = bindingPaint.MeasureText(primaryText);
            var primaryRect = new SKRect(bindingX - 4, y, bindingX + primaryWidth + 4, y + itemHeight - 2);

            if (isPrimaryHovered && !isPrimaryEditing)
            {
                using var pillPaint = new SKPaint { Color = _theme.Accent.WithAlpha(40), IsAntialias = true };
                canvas.DrawRoundRect(new SKRoundRect(primaryRect, 3), pillPaint);
            }

            canvas.DrawText(primaryText, bindingX, bindingY, bindingPaint);
            _buttons.Add(new ButtonRect($"editHotkey_{item.PrimaryId}", primaryRect));
            bindingX += primaryWidth + 8;

            // Secondary binding (numpad) if exists
            if (item.SecondaryId != null)
            {
                bindingPaint.Color = _theme.TextSecondary;

                string secondaryText;
                if (isSecondaryEditing)
                {
                    secondaryText = "Press...";
                    bindingPaint.Color = _theme.Accent;
                }
                else if (_settings.HotkeyBindings.TryGetValue(item.SecondaryId, out var secondaryBinding))
                {
                    secondaryText = FormatHotkeyBinding(secondaryBinding);
                }
                else
                {
                    secondaryText = "";
                }

                if (!string.IsNullOrEmpty(secondaryText))
                {
                    float secondaryWidth = bindingPaint.MeasureText(secondaryText);
                    var secondaryRect = new SKRect(bindingX - 4, y, bindingX + secondaryWidth + 4, y + itemHeight - 2);

                    if (isSecondaryHovered && !isSecondaryEditing)
                    {
                        using var pillPaint = new SKPaint { Color = _theme.Accent.WithAlpha(40), IsAntialias = true };
                        canvas.DrawRoundRect(new SKRoundRect(secondaryRect, 3), pillPaint);
                    }

                    canvas.DrawText(secondaryText, bindingX, bindingY, bindingPaint);
                    _buttons.Add(new ButtonRect($"editHotkey_{item.SecondaryId}", secondaryRect));
                }
            }

            y += itemHeight;
        }

        return y;
    }

    private string FormatHotkeyBinding(HotkeyBinding binding)
    {
        var parts = new List<string>();
        if (binding.Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (binding.Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (binding.Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (binding.Modifiers.HasFlag(ModifierKeys.Win)) parts.Add("Win");
        parts.Add(FormatVirtualKey(binding.Key));
        return string.Join("+", parts);
    }

    private string FormatVirtualKey(VirtualKey key)
    {
        return key switch
        {
            >= VirtualKey.D0 and <= VirtualKey.D9 => ((int)key - (int)VirtualKey.D0).ToString(),
            >= VirtualKey.NumPad0 and <= VirtualKey.NumPad9 => $"Num{(int)key - (int)VirtualKey.NumPad0}",
            VirtualKey.Up => "↑",
            VirtualKey.Down => "↓",
            VirtualKey.Left => "←",
            VirtualKey.Right => "→",
            _ => key.ToString()
        };
    }

    private void DrawScrollbar(SKCanvas canvas, SKRect scrollArea, float offset, float contentHeight)
    {
        float scrollbarWidth = 6f;
        float scrollbarX = scrollArea.Right - scrollbarWidth - 2;
        float trackHeight = scrollArea.Height;
        float thumbHeight = Math.Max(20, trackHeight * (scrollArea.Height / contentHeight));
        float maxOffset = contentHeight - scrollArea.Height;
        float thumbY = scrollArea.Top + (offset / maxOffset) * (trackHeight - thumbHeight);

        // Track
        using var trackPaint = new SKPaint { Color = _theme.SlotBackground, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(scrollbarX, scrollArea.Top, scrollbarX + scrollbarWidth, scrollArea.Bottom), 3), trackPaint);

        // Thumb
        using var thumbPaint = new SKPaint { Color = _theme.TextSecondary.WithAlpha(128), IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(scrollbarX, thumbY, scrollbarX + scrollbarWidth, thumbY + thumbHeight), 3), thumbPaint);
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
        float btnWidth = Math.Min(width, 140f);

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
        float swatchSpacing = 8f;
        float availableWidth = width - swatchSpacing * 2;
        float swatchWidth = Math.Min(100f, availableWidth / 3);
        float swatchHeight = 50f;

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
            float miniSlotY = y + 6;
            var miniSlotRect = new SKRect(startX + 6, miniSlotY, startX + swatchWidth - 6, miniSlotY + 10);
            using var slotPaint = new SKPaint { Color = theme.SlotBackground, IsAntialias = true };
            canvas.DrawRoundRect(new SKRoundRect(miniSlotRect, 2), slotPaint);

            // Accent bar
            float accentY = miniSlotY + 12;
            var accentRect = new SKRect(startX + 6, accentY, startX + swatchWidth - 6, accentY + 6);
            using var accentPaint = new SKPaint { Color = theme.Accent, IsAntialias = true };
            canvas.DrawRoundRect(new SKRoundRect(accentRect, 2), accentPaint);

            // Text sample
            using var textPaint = new SKPaint
            {
                Color = theme.Text,
                IsAntialias = true,
                TextSize = 8f,
                TextAlign = SKTextAlign.Center,
                Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
            };
            canvas.DrawText("Abc", swatchRect.MidX, accentY + 18, textPaint);

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

        canvas.DrawText("Apps that reuse a single slot:", x, y + _secondaryTextPaint.TextSize, _secondaryTextPaint);
        y += _secondaryTextPaint.TextSize + 6;

        foreach (var app in _settings.StickyApps)
        {
            var itemRect = new SKRect(x, y, x + width, y + itemHeight);

            bool isHovered = _hoveredButton == $"removeStickyApp_{app}";
            using var itemBgPaint = new SKPaint
            {
                Color = _theme.SlotBackground,
                IsAntialias = true
            };
            canvas.DrawRoundRect(new SKRoundRect(itemRect, 3), itemBgPaint);

            using var appPaint = new SKPaint
            {
                Color = _theme.Text,
                IsAntialias = true,
                TextSize = 11f,
                Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Normal)
            };

            string displayName = app;
            float maxTextWidth = width - buttonSize - 16;
            while (appPaint.MeasureText(displayName) > maxTextWidth && displayName.Length > 3)
            {
                displayName = displayName[..^4] + "...";
            }
            canvas.DrawText(displayName, x + 6, y + itemHeight / 2 + 4, appPaint);

            var removeRect = new SKRect(x + width - buttonSize - 2, y + (itemHeight - buttonSize) / 2,
                                        x + width - 2, y + (itemHeight + buttonSize) / 2);

            using var removeBgPaint = new SKPaint
            {
                Color = isHovered ? _theme.DangerHover : SKColors.Transparent,
                IsAntialias = true
            };
            canvas.DrawRoundRect(new SKRoundRect(removeRect, 3), removeBgPaint);

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

        y += 4;
        _stickyAppInputRect = new SKRect(x, y, x + width - 40, y + inputHeight);
        var addButtonRect = new SKRect(x + width - 36, y, x + width, y + inputHeight);

        using var inputBgPaint = new SKPaint
        {
            Color = _stickyAppInputFocused ? _theme.Background : _theme.SlotBackground,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(_stickyAppInputRect, 3), inputBgPaint);

        using var inputBorderPaint = new SKPaint
        {
            Color = _stickyAppInputFocused ? _theme.Accent : _theme.Border,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f
        };
        canvas.DrawRoundRect(new SKRoundRect(_stickyAppInputRect, 3), inputBorderPaint);

        using var inputTextPaint = new SKPaint
        {
            Color = string.IsNullOrEmpty(_stickyAppInput) ? _theme.TextSecondary : _theme.Text,
            IsAntialias = true,
            TextSize = 11f,
            Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Normal)
        };
        string displayText = string.IsNullOrEmpty(_stickyAppInput) ? "Process name..." : _stickyAppInput;

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

        canvas.DrawText("Check console for process names", x, y + _secondaryTextPaint.TextSize, _secondaryTextPaint);
        y += _secondaryTextPaint.TextSize;

        return y + ItemSpacing;
    }

    private float DrawMidiSection(SKCanvas canvas, float x, float y, float width)
    {
        y = DrawCompactToggle(canvas, "Enable MIDI", _settings.MidiSettings.Enabled, x, y, width, "midiEnabled");

        float statusY = y;
        using var statusPaint = new SKPaint
        {
            Color = _midiConnected ? new SKColor(0x4C, 0xAF, 0x50) : _theme.TextSecondary,
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

        if (!_settings.MidiSettings.Enabled)
        {
            return y + ItemSpacing;
        }

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

                if (isSelected || isHovered)
                {
                    var bgColor = isSelected ? _theme.Accent.WithAlpha(60) : _theme.Button;
                    using var bgPaint = new SKPaint { Color = bgColor, IsAntialias = true };
                    canvas.DrawRoundRect(new SKRoundRect(itemRect, 3), bgPaint);
                }

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

                float textX = x + (isSelected ? 18 : 8);
                using var devicePaint = new SKPaint
                {
                    Color = isSelected ? _theme.Text : _theme.TextSecondary,
                    IsAntialias = true,
                    TextSize = 11f,
                    Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
                };

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

        y += 8;
        y = DrawPresetDropdown(canvas, x, y, width);
        y += SectionSpacing;

        // Processor Chords section
        y = DrawProcessorChordsSection(canvas, x, y, width);

        return y + ItemSpacing;
    }

    private float DrawProcessorChordsSection(SKCanvas canvas, float x, float y, float width)
    {
        // Section header
        using var headerPaint = new SKPaint
        {
            Color = _theme.Accent,
            IsAntialias = true,
            TextSize = 10f,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };
        canvas.DrawText("PROCESSOR CHORDS", x, y + headerPaint.TextSize, headerPaint);
        float textWidth = headerPaint.MeasureText("PROCESSOR CHORDS");
        using var linePaint = new SKPaint
        {
            Color = _theme.Accent.WithAlpha(80),
            IsAntialias = true,
            StrokeWidth = 1f
        };
        canvas.DrawLine(x, y + headerPaint.TextSize + 3, x + textWidth, y + headerPaint.TextSize + 3, linePaint);
        y += headerPaint.TextSize + 10;

        // Get effective processor chords from preset and custom settings
        var presets = _midiPresets?.GetAllPresets() ?? Array.Empty<MidiControlScheme>();
        var currentPreset = presets.FirstOrDefault(p => p.Name == _settings.MidiSettings.ActivePreset);
        var effectiveChords = new Dictionary<string, MidiTrigger>();

        // Start with preset chords
        if (currentPreset?.ProcessorChords != null)
        {
            foreach (var kvp in currentPreset.ProcessorChords)
                effectiveChords[kvp.Key] = kvp.Value;
        }

        // Override with custom settings
        if (_settings.MidiSettings.ProcessorChords != null)
        {
            foreach (var kvp in _settings.MidiSettings.ProcessorChords)
                effectiveChords[kvp.Key] = kvp.Value;
        }

        if (effectiveChords.Count == 0)
        {
            canvas.DrawText("No processor chords configured", x, y + _secondaryTextPaint.TextSize, _secondaryTextPaint);
            y += _secondaryTextPaint.TextSize + 4;
            canvas.DrawText("Use Edit to add MIDI triggers for processors", x, y + _secondaryTextPaint.TextSize, _secondaryTextPaint);
            y += _secondaryTextPaint.TextSize;
        }
        else
        {
            float itemHeight = 20f;
            foreach (var (processorName, trigger) in effectiveChords)
            {
                // Get display name from ProcessorDefinitions
                var def = ProcessorDefinitions.GetByName(processorName);
                string displayName = def?.DisplayName ?? processorName;

                // Format trigger info
                string triggerText = FormatMidiTrigger(trigger);

                // Draw item
                using var namePaint = new SKPaint
                {
                    Color = _theme.Text,
                    IsAntialias = true,
                    TextSize = 10f,
                    Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
                };
                canvas.DrawText(displayName, x + 4, y + itemHeight / 2 + 3, namePaint);

                using var triggerPaint = new SKPaint
                {
                    Color = _theme.TextSecondary,
                    IsAntialias = true,
                    TextSize = 9f,
                    TextAlign = SKTextAlign.Right,
                    Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Normal)
                };
                canvas.DrawText(triggerText, x + width - 4, y + itemHeight / 2 + 3, triggerPaint);

                y += itemHeight;
            }
        }

        return y;
    }

    private static string FormatMidiTrigger(MidiTrigger trigger)
    {
        string type = trigger.Type switch
        {
            MidiTriggerType.NoteOn => "Note",
            MidiTriggerType.NoteOff => "NoteOff",
            MidiTriggerType.ControlChange => "CC",
            _ => "?"
        };
        string channel = trigger.Channel.HasValue ? $"Ch{trigger.Channel + 1}" : "Any";
        return $"{type} {trigger.Number} ({channel})";
    }

    private float DrawPresetDropdown(SKCanvas canvas, float x, float y, float width)
    {
        float dropdownHeight = 28f;
        float buttonWidth = 36f;
        float buttonSpacing = 4f;

        canvas.DrawText("Preset:", x, y + _textPaint.TextSize, _textPaint);

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

        var presets = _midiPresets?.GetAllPresets() ?? Array.Empty<MidiControlScheme>();
        var currentPreset = presets.FirstOrDefault(p => p.Name == _settings.MidiSettings.ActivePreset);
        string displayName = currentPreset?.Name ?? _settings.MidiSettings.ActivePreset ?? "Select...";

        var dropdownRect = new SKRect(x, y, x + width, y + dropdownHeight);
        _presetDropdownAnchor = dropdownRect;
        bool isHovered = _hoveredButton == "presetDropdownToggle";

        using var bgPaint = new SKPaint
        {
            Color = isHovered ? _theme.ButtonHover : _theme.Button,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(dropdownRect, 4), bgPaint);

        using var borderPaint = new SKPaint
        {
            Color = _presetDropdownOpen ? _theme.Accent : _theme.Border,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f
        };
        canvas.DrawRoundRect(new SKRoundRect(dropdownRect, 4), borderPaint);

        using var textPaint = new SKPaint
        {
            Color = _theme.Text,
            IsAntialias = true,
            TextSize = 11f,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        };
        canvas.DrawText(displayName, x + 8, y + dropdownHeight / 2 + 4, textPaint);

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
            canvas.DrawLine(arrowX - 4, arrowY + 2, arrowX, arrowY - 2, arrowPaint);
            canvas.DrawLine(arrowX, arrowY - 2, arrowX + 4, arrowY + 2, arrowPaint);
        }
        else
        {
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

        using var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 40),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3)
        };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(x + 2, y + 2, x + width + 2, y + menuHeight + 2), 4), shadowPaint);

        using var menuBgPaint = new SKPaint
        {
            Color = _theme.Background,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(menuRect, 4), menuBgPaint);

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

            if (itemHovered || isSelected)
            {
                var itemBgColor = isSelected ? _theme.Accent.WithAlpha(60) : _theme.ButtonHover;
                using var itemBgPaint = new SKPaint { Color = itemBgColor, IsAntialias = true };
                canvas.DrawRoundRect(new SKRoundRect(itemRect, 3), itemBgPaint);
            }

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
