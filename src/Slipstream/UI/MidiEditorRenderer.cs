using SkiaSharp;
using Slipstream.Models;
using Slipstream.Services;

namespace Slipstream.UI;

public class MidiEditorRenderer
{
    private MidiControlScheme _editingPreset;
    private readonly MidiPresets _midiPresets;
    private readonly ConfigService _configService;
    private ColorTheme _theme;

    // State
    private int _selectedNote = -1;
    private readonly HashSet<int> _pressedNotes = new();
    private int _baseOctave = 2; // Start at C2 (note 36)
    private bool _hasUnsavedChanges;
    private string _editingName;
    private string _editingDescription;
    private string _editingDeviceHint;

    // Text input state
    private string? _activeTextField; // "name", "description", or null
    private int _cursorPosition;
    private DateTime _cursorBlinkTime = DateTime.Now;

    // Action selection state
    private ActionCategory _selectedCategory = ActionCategory.None;

    // Layout constants
    private const float TitleBarHeight = 40f;
    private const float CornerRadius = 12f;
    private const float Padding = 16f;
    private const float SectionSpacing = 12f;

    // Piano constants
    private const float WhiteKeyWidth = 28f;
    private const float WhiteKeyHeight = 120f;
    private const float BlackKeyWidth = 18f;
    private const float BlackKeyHeight = 75f;
    private const int VisibleWhiteKeys = 21; // 3 octaves = 21 white keys

    // Interactive elements
    private readonly List<ButtonRect> _buttons = new();
    private readonly List<PianoKey> _pianoKeys = new();
    private SKRect _closeButtonRect;
    private SKRect _saveButtonRect;
    private SKRect _nameFieldRect;
    private SKRect _descriptionFieldRect;

    private bool _closeButtonHovered;
    private string? _hoveredButton;
    private string? _pressedButton;

    // Events
    public event Action? CloseRequested;
    public event Action<MidiControlScheme>? PresetSaved;

    // Action categories
    private enum ActionCategory
    {
        None,
        Paste,
        Copy,
        Control,
        CopyModifier
    }

    // Paints
    private readonly SKPaint _backgroundPaint;
    private readonly SKPaint _titleBarPaint;
    private readonly SKPaint _titlePaint;
    private readonly SKPaint _textPaint;
    private readonly SKPaint _secondaryTextPaint;
    private readonly SKPaint _borderPaint;

    public MidiEditorRenderer(MidiControlScheme preset, MidiPresets midiPresets, ConfigService configService)
    {
        _editingPreset = preset;
        _midiPresets = midiPresets;
        _configService = configService;
        _editingName = preset.Name;
        _editingDescription = preset.Description;
        _editingDeviceHint = preset.DeviceHint;
        _theme = ColorTheme.Dark;

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

    public void SetNotePressed(int note, bool pressed)
    {
        if (pressed)
            _pressedNotes.Add(note);
        else
            _pressedNotes.Remove(note);
    }

    public void SelectNote(int note)
    {
        _selectedNote = note;
        _activeTextField = null;

        // Auto-scroll to show selected note
        int noteOctave = note / 12;
        if (noteOctave < _baseOctave || noteOctave >= _baseOctave + 3)
        {
            _baseOctave = Math.Clamp(noteOctave - 1, 0, 8);
        }

        // Auto-detect category from current mapping
        var currentAction = GetActionForNote(note);
        if (currentAction != null)
        {
            if (currentAction.StartsWith("PasteFrom"))
                _selectedCategory = ActionCategory.Paste;
            else if (currentAction.StartsWith("CopyTo"))
                _selectedCategory = ActionCategory.Copy;
            else
                _selectedCategory = ActionCategory.Control;
        }
        else if (_editingPreset.CopyModifier?.Number == note)
        {
            _selectedCategory = ActionCategory.CopyModifier;
        }
    }

    public bool IsInTitleBar(SKPoint point)
    {
        return point.Y < TitleBarHeight &&
               !_closeButtonRect.Contains(point) &&
               !_saveButtonRect.Contains(point);
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

        foreach (var key in _pianoKeys)
        {
            if (key.Rect.Contains(point))
                return $"key_{key.Note}";
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

        if (_hoveredButton == null)
        {
            foreach (var key in _pianoKeys)
            {
                if (key.Rect.Contains(point))
                {
                    _hoveredButton = $"key_{key.Note}";
                    break;
                }
            }
        }
    }

    public void HandleMouseDown(SKPoint point)
    {
        // Check text fields first
        if (_nameFieldRect.Contains(point))
        {
            _activeTextField = "name";
            _cursorPosition = _editingName.Length;
            _cursorBlinkTime = DateTime.Now;
            return;
        }
        if (_descriptionFieldRect.Contains(point))
        {
            _activeTextField = "description";
            _cursorPosition = _editingDescription.Length;
            _cursorBlinkTime = DateTime.Now;
            return;
        }

        // Clicking elsewhere deselects text field
        _activeTextField = null;

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
                return;
            }
        }

        // Check piano keys (black keys first - they're on top)
        foreach (var key in _pianoKeys.OrderByDescending(k => k.IsBlack))
        {
            if (key.Rect.Contains(point))
            {
                _pressedButton = $"key_{key.Note}";
                return;
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
        else if (_pressedButton.StartsWith("key_"))
        {
            int note = int.Parse(_pressedButton.Substring(4));
            foreach (var key in _pianoKeys)
            {
                if (key.Note == note && key.Rect.Contains(point))
                {
                    stillOverButton = true;
                    break;
                }
            }
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

    public void HandleKeyInput(char c)
    {
        if (_activeTextField == null) return;

        if (_activeTextField == "name")
        {
            _editingName = _editingName.Insert(_cursorPosition, c.ToString());
            _cursorPosition++;
            _hasUnsavedChanges = true;
        }
        else if (_activeTextField == "description")
        {
            _editingDescription = _editingDescription.Insert(_cursorPosition, c.ToString());
            _cursorPosition++;
            _hasUnsavedChanges = true;
        }
        _cursorBlinkTime = DateTime.Now;
    }

    public void HandleBackspace()
    {
        if (_activeTextField == null || _cursorPosition == 0) return;

        if (_activeTextField == "name" && _editingName.Length > 0)
        {
            _editingName = _editingName.Remove(_cursorPosition - 1, 1);
            _cursorPosition--;
            _hasUnsavedChanges = true;
        }
        else if (_activeTextField == "description" && _editingDescription.Length > 0)
        {
            _editingDescription = _editingDescription.Remove(_cursorPosition - 1, 1);
            _cursorPosition--;
            _hasUnsavedChanges = true;
        }
        _cursorBlinkTime = DateTime.Now;
    }

    public void HandleDelete()
    {
        if (_activeTextField == null) return;

        if (_activeTextField == "name" && _cursorPosition < _editingName.Length)
        {
            _editingName = _editingName.Remove(_cursorPosition, 1);
            _hasUnsavedChanges = true;
        }
        else if (_activeTextField == "description" && _cursorPosition < _editingDescription.Length)
        {
            _editingDescription = _editingDescription.Remove(_cursorPosition, 1);
            _hasUnsavedChanges = true;
        }
        _cursorBlinkTime = DateTime.Now;
    }

    public void HandleArrowKey(bool left)
    {
        if (_activeTextField == null) return;

        string text = _activeTextField == "name" ? _editingName : _editingDescription;
        if (left && _cursorPosition > 0)
            _cursorPosition--;
        else if (!left && _cursorPosition < text.Length)
            _cursorPosition++;
        _cursorBlinkTime = DateTime.Now;
    }

    public bool HasActiveTextField => _activeTextField != null;

    private void ExecuteButtonAction(string buttonId)
    {
        switch (buttonId)
        {
            case "close":
                CloseRequested?.Invoke();
                break;

            case "save":
                SavePreset();
                break;

            case "octaveLeft":
                if (_baseOctave > 0) _baseOctave--;
                break;

            case "octaveRight":
                if (_baseOctave < 8) _baseOctave++;
                break;

            // Category buttons
            case "category_paste":
                _selectedCategory = ActionCategory.Paste;
                break;
            case "category_copy":
                _selectedCategory = ActionCategory.Copy;
                break;
            case "category_control":
                _selectedCategory = ActionCategory.Control;
                break;
            case "category_copymod":
                _selectedCategory = ActionCategory.CopyModifier;
                if (_selectedNote >= 0)
                {
                    SetCopyModifier(_selectedNote);
                }
                break;
            case "category_none":
                _selectedCategory = ActionCategory.None;
                if (_selectedNote >= 0)
                {
                    ClearMappingForNote(_selectedNote);
                    // Also clear copy modifier if this note was the copy modifier
                    if (_editingPreset.CopyModifier?.Number == _selectedNote)
                    {
                        ClearCopyModifier();
                    }
                }
                break;

            // Slot buttons (1-10)
            default:
                if (buttonId.StartsWith("key_"))
                {
                    int note = int.Parse(buttonId.Substring(4));
                    SelectNote(note);
                }
                else if (buttonId.StartsWith("slot_"))
                {
                    int slot = int.Parse(buttonId.Substring(5));
                    if (_selectedNote >= 0)
                    {
                        string action = _selectedCategory switch
                        {
                            ActionCategory.Paste => $"PasteFromSlot{slot}",
                            ActionCategory.Copy => $"CopyToSlot{slot}",
                            _ => ""
                        };
                        if (!string.IsNullOrEmpty(action))
                        {
                            AssignAction(_selectedNote, action);
                        }
                    }
                }
                else if (buttonId.StartsWith("control_"))
                {
                    string action = buttonId.Substring(8);
                    if (_selectedNote >= 0)
                    {
                        AssignAction(_selectedNote, action);
                    }
                }
                break;
        }
    }

    private void AssignAction(int note, string? actionName)
    {
        // Remove any existing mapping for this note
        ClearMappingForNote(note);

        // Also clear copy modifier if this note was the copy modifier
        if (_editingPreset.CopyModifier?.Number == note)
        {
            ClearCopyModifier();
        }

        if (!string.IsNullOrEmpty(actionName))
        {
            var trigger = MidiTrigger.NoteOn(note);
            var newMappings = new Dictionary<string, MidiTrigger>(_editingPreset.Mappings)
            {
                [actionName] = trigger
            };

            _editingPreset = new MidiControlScheme
            {
                Name = _editingPreset.Name,
                Description = _editingPreset.Description,
                DeviceHint = _editingPreset.DeviceHint,
                Mappings = newMappings,
                CopyModifier = _editingPreset.CopyModifier
            };
            _hasUnsavedChanges = true;
        }
    }

    private void ClearMappingForNote(int note)
    {
        var newMappings = new Dictionary<string, MidiTrigger>();
        foreach (var (action, trigger) in _editingPreset.Mappings)
        {
            if (trigger.Number != note)
            {
                newMappings[action] = trigger;
            }
        }

        if (newMappings.Count != _editingPreset.Mappings.Count)
        {
            _editingPreset = new MidiControlScheme
            {
                Name = _editingPreset.Name,
                Description = _editingPreset.Description,
                DeviceHint = _editingPreset.DeviceHint,
                Mappings = newMappings,
                CopyModifier = _editingPreset.CopyModifier
            };
            _hasUnsavedChanges = true;
        }
    }

    private void SetCopyModifier(int note)
    {
        // Clear any existing action mapping for this note first
        ClearMappingForNote(note);

        _editingPreset = new MidiControlScheme
        {
            Name = _editingPreset.Name,
            Description = _editingPreset.Description,
            DeviceHint = _editingPreset.DeviceHint,
            Mappings = _editingPreset.Mappings,
            CopyModifier = MidiTrigger.NoteOn(note)
        };
        _hasUnsavedChanges = true;
    }

    private void ClearCopyModifier()
    {
        _editingPreset = new MidiControlScheme
        {
            Name = _editingPreset.Name,
            Description = _editingPreset.Description,
            DeviceHint = _editingPreset.DeviceHint,
            Mappings = _editingPreset.Mappings,
            CopyModifier = null
        };
        _hasUnsavedChanges = true;
    }

    private void SavePreset()
    {
        _editingPreset = new MidiControlScheme
        {
            Name = _editingName,
            Description = _editingDescription,
            DeviceHint = _editingDeviceHint,
            Mappings = _editingPreset.Mappings,
            CopyModifier = _editingPreset.CopyModifier
        };

        _configService.SaveMidiPreset(_editingPreset);
        _hasUnsavedChanges = false;
        PresetSaved?.Invoke(_editingPreset);
    }

    public void Render(SKCanvas canvas, SKSize size, SKPoint mousePos, float dpiScale = 1.0f)
    {
        _buttons.Clear();
        _pianoKeys.Clear();
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

        float y = TitleBarHeight + Padding;

        // Editable preset name and description
        y = DrawEditablePresetInfo(canvas, Padding, y, size.Width - Padding * 2);

        // Piano keyboard
        y += SectionSpacing;
        y = DrawPianoKeyboard(canvas, Padding, y, size.Width - Padding * 2);

        // Octave navigation
        y += 8;
        y = DrawOctaveNavigation(canvas, Padding, y, size.Width - Padding * 2);

        // Action assignment panel
        y += SectionSpacing;
        y = DrawActionPanel(canvas, Padding, y, size.Width - Padding * 2);

        // Current mappings summary
        y += SectionSpacing;
        DrawMappingsSummary(canvas, Padding, y, size.Width - Padding * 2, size.Height - y - Padding);

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

        string title = _hasUnsavedChanges ? "MIDI Preset Editor *" : "MIDI Preset Editor";
        canvas.DrawText(title, Padding, TitleBarHeight / 2 + _titlePaint.TextSize / 3, _titlePaint);

        // Save button
        float saveWidth = 50f;
        float buttonHeight = 24f;
        float buttonY = (TitleBarHeight - buttonHeight) / 2;
        _saveButtonRect = new SKRect(size.Width - Padding - 30 - 8 - saveWidth, buttonY,
            size.Width - Padding - 30 - 8, buttonY + buttonHeight);

        bool saveHovered = _hoveredButton == "save";
        var successColor = new SKColor(76, 175, 80);
        var successHoverColor = new SKColor(96, 195, 100);
        using var saveBgPaint = new SKPaint
        {
            Color = saveHovered ? successHoverColor : successColor,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(_saveButtonRect, 4), saveBgPaint);

        using var saveTxtPaint = new SKPaint
        {
            Color = _theme.Text,
            IsAntialias = true,
            TextSize = 11f,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        };
        canvas.DrawText("Save", _saveButtonRect.MidX, _saveButtonRect.MidY + 4, saveTxtPaint);
        _buttons.Add(new ButtonRect("save", _saveButtonRect));

        // Close button
        _closeButtonRect = new SKRect(size.Width - Padding - 24, (TitleBarHeight - 24) / 2,
            size.Width - Padding, (TitleBarHeight + 24) / 2);

        if (_closeButtonHovered)
        {
            using var hoverPaint = new SKPaint
            {
                Color = _theme.Danger,
                IsAntialias = true
            };
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

    private float DrawEditablePresetInfo(SKCanvas canvas, float x, float y, float width)
    {
        float fieldHeight = 26f;
        float labelWidth = 50f;

        // Name field
        canvas.DrawText("Name:", x, y + fieldHeight / 2 + 4, _textPaint);

        _nameFieldRect = new SKRect(x + labelWidth, y, x + width * 0.5f, y + fieldHeight);
        DrawTextField(canvas, _nameFieldRect, _editingName, _activeTextField == "name");
        y += fieldHeight + 6;

        // Description field
        canvas.DrawText("Desc:", x, y + fieldHeight / 2 + 4, _textPaint);

        _descriptionFieldRect = new SKRect(x + labelWidth, y, x + width, y + fieldHeight);
        DrawTextField(canvas, _descriptionFieldRect, _editingDescription, _activeTextField == "description");
        y += fieldHeight + 4;

        return y;
    }

    private void DrawTextField(SKCanvas canvas, SKRect rect, string text, bool isActive)
    {
        // Background
        using var bgPaint = new SKPaint
        {
            Color = isActive ? _theme.Button : new SKColor(40, 40, 45),
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(rect, 4), bgPaint);

        // Border
        using var borderPaint = new SKPaint
        {
            Color = isActive ? _theme.Accent : _theme.Border,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = isActive ? 1.5f : 1f
        };
        canvas.DrawRoundRect(new SKRoundRect(rect, 4), borderPaint);

        // Text
        using var textPaint = new SKPaint
        {
            Color = _theme.Text,
            IsAntialias = true,
            TextSize = 11f,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        };

        // Clip text to field
        canvas.Save();
        canvas.ClipRect(new SKRect(rect.Left + 6, rect.Top, rect.Right - 6, rect.Bottom));
        canvas.DrawText(text, rect.Left + 8, rect.MidY + 4, textPaint);

        // Cursor
        if (isActive)
        {
            bool showCursor = (DateTime.Now - _cursorBlinkTime).TotalMilliseconds % 1000 < 500;
            if (showCursor)
            {
                float cursorX = rect.Left + 8 + textPaint.MeasureText(text.Substring(0, _cursorPosition));
                using var cursorPaint = new SKPaint
                {
                    Color = _theme.Text,
                    StrokeWidth = 1f
                };
                canvas.DrawLine(cursorX, rect.Top + 5, cursorX, rect.Bottom - 5, cursorPaint);
            }
        }
        canvas.Restore();
    }

    private float DrawPianoKeyboard(SKCanvas canvas, float x, float y, float width)
    {
        float keyboardWidth = VisibleWhiteKeys * WhiteKeyWidth;
        float keyboardX = x + (width - keyboardWidth) / 2;

        int startNote = _baseOctave * 12;
        float whiteKeyX = keyboardX;

        // Draw white keys first
        for (int octave = 0; octave < 3; octave++)
        {
            for (int note = 0; note < 12; note++)
            {
                int midiNote = startNote + octave * 12 + note;
                if (midiNote > 127) break;

                if (!IsBlackKey(note))
                {
                    DrawWhiteKey(canvas, whiteKeyX, y, midiNote);
                    whiteKeyX += WhiteKeyWidth;
                }
            }
        }

        // Draw black keys on top
        whiteKeyX = keyboardX;
        for (int octave = 0; octave < 3; octave++)
        {
            for (int note = 0; note < 12; note++)
            {
                int midiNote = startNote + octave * 12 + note;
                if (midiNote > 127) break;

                if (IsBlackKey(note))
                {
                    float blackKeyX = whiteKeyX - BlackKeyWidth / 2;
                    if (note == 1 || note == 6) blackKeyX -= 2;
                    if (note == 3 || note == 10) blackKeyX += 2;
                    DrawBlackKey(canvas, blackKeyX, y, midiNote);
                }
                else
                {
                    whiteKeyX += WhiteKeyWidth;
                }
            }
        }

        return y + WhiteKeyHeight;
    }

    private void DrawWhiteKey(SKCanvas canvas, float x, float y, int note)
    {
        var keyRect = new SKRect(x, y, x + WhiteKeyWidth - 1, y + WhiteKeyHeight);

        bool isPressed = _pressedNotes.Contains(note);
        bool isSelected = _selectedNote == note;
        bool isHovered = _hoveredButton == $"key_{note}";
        string? mappedAction = GetActionForNote(note);
        bool isCopyModifier = _editingPreset.CopyModifier?.Number == note;

        _pianoKeys.Add(new PianoKey(note, keyRect, false));

        SKColor keyColor = SKColors.White;
        if (isPressed)
            keyColor = new SKColor(255, 255, 150);
        else if (isSelected)
            keyColor = new SKColor(200, 220, 255);
        else if (mappedAction != null)
            keyColor = GetActionColor(mappedAction).WithAlpha(80);
        else if (isCopyModifier)
            keyColor = new SKColor(200, 150, 255);
        else if (isHovered)
            keyColor = new SKColor(240, 240, 240);

        using var keyPaint = new SKPaint { Color = keyColor, IsAntialias = true };
        var keyRoundRect = new SKRoundRect();
        keyRoundRect.SetRectRadii(keyRect, new[]
        {
            new SKPoint(0, 0),
            new SKPoint(0, 0),
            new SKPoint(3, 3),
            new SKPoint(3, 3)
        });
        canvas.DrawRoundRect(keyRoundRect, keyPaint);

        using var borderPaint = new SKPaint
        {
            Color = isSelected ? _theme.Accent : new SKColor(180, 180, 180),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = isSelected ? 2f : 1f
        };
        canvas.DrawRoundRect(keyRoundRect, borderPaint);

        string noteName = GetNoteName(note);
        using var notePaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            TextSize = 9f,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        };
        canvas.DrawText(noteName, keyRect.MidX, keyRect.Bottom - 6, notePaint);

        if (mappedAction != null || isCopyModifier)
        {
            var dotColor = isCopyModifier ? new SKColor(150, 100, 200) : GetActionColor(mappedAction!);
            using var dotPaint = new SKPaint { Color = dotColor, IsAntialias = true };
            canvas.DrawCircle(keyRect.MidX, keyRect.Top + 12, 5, dotPaint);
        }
    }

    private void DrawBlackKey(SKCanvas canvas, float x, float y, int note)
    {
        var keyRect = new SKRect(x, y, x + BlackKeyWidth, y + BlackKeyHeight);

        bool isPressed = _pressedNotes.Contains(note);
        bool isSelected = _selectedNote == note;
        bool isHovered = _hoveredButton == $"key_{note}";
        string? mappedAction = GetActionForNote(note);
        bool isCopyModifier = _editingPreset.CopyModifier?.Number == note;

        _pianoKeys.Add(new PianoKey(note, keyRect, true));

        SKColor keyColor = new SKColor(30, 30, 35);
        if (isPressed)
            keyColor = new SKColor(200, 200, 100);
        else if (isSelected)
            keyColor = new SKColor(100, 120, 180);
        else if (mappedAction != null)
            keyColor = GetActionColor(mappedAction).WithAlpha(180);
        else if (isCopyModifier)
            keyColor = new SKColor(120, 80, 160);
        else if (isHovered)
            keyColor = new SKColor(50, 50, 55);

        using var keyPaint = new SKPaint { Color = keyColor, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(keyRect, 2), keyPaint);

        if (isSelected)
        {
            using var borderPaint = new SKPaint
            {
                Color = _theme.Accent,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f
            };
            canvas.DrawRoundRect(new SKRoundRect(keyRect, 2), borderPaint);
        }

        if (mappedAction != null || isCopyModifier)
        {
            var dotColor = isCopyModifier ? new SKColor(150, 100, 200) : GetActionColor(mappedAction!);
            using var dotPaint = new SKPaint { Color = dotColor, IsAntialias = true };
            canvas.DrawCircle(keyRect.MidX, keyRect.Top + 10, 4, dotPaint);
        }
    }

    private float DrawOctaveNavigation(SKCanvas canvas, float x, float y, float width)
    {
        float buttonWidth = 30f;
        float buttonHeight = 24f;
        float centerX = x + width / 2;

        // Left arrow
        var leftRect = new SKRect(centerX - 80, y, centerX - 80 + buttonWidth, y + buttonHeight);
        bool leftHovered = _hoveredButton == "octaveLeft";
        using var leftPaint = new SKPaint
        {
            Color = leftHovered ? _theme.ButtonHover : _theme.Button,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(leftRect, 4), leftPaint);

        using var arrowPaint = new SKPaint
        {
            Color = _theme.Text,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            StrokeCap = SKStrokeCap.Round
        };
        float arrowY = leftRect.MidY;
        canvas.DrawLine(leftRect.MidX + 4, arrowY - 5, leftRect.MidX - 4, arrowY, arrowPaint);
        canvas.DrawLine(leftRect.MidX - 4, arrowY, leftRect.MidX + 4, arrowY + 5, arrowPaint);

        _buttons.Add(new ButtonRect("octaveLeft", leftRect));

        // Octave display
        using var octavePaint = new SKPaint
        {
            Color = _theme.Text,
            IsAntialias = true,
            TextSize = 12f,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        };
        canvas.DrawText($"Octave {_baseOctave}", centerX, y + buttonHeight / 2 + 4, octavePaint);

        // Right arrow
        var rightRect = new SKRect(centerX + 80 - buttonWidth, y, centerX + 80, y + buttonHeight);
        bool rightHovered = _hoveredButton == "octaveRight";
        using var rightPaint = new SKPaint
        {
            Color = rightHovered ? _theme.ButtonHover : _theme.Button,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(rightRect, 4), rightPaint);

        canvas.DrawLine(rightRect.MidX - 4, arrowY - 5, rightRect.MidX + 4, arrowY, arrowPaint);
        canvas.DrawLine(rightRect.MidX + 4, arrowY, rightRect.MidX - 4, arrowY + 5, arrowPaint);

        _buttons.Add(new ButtonRect("octaveRight", rightRect));

        return y + buttonHeight;
    }

    private float DrawActionPanel(SKCanvas canvas, float x, float y, float width)
    {
        // Selected note info
        if (_selectedNote >= 0)
        {
            string noteName = GetNoteName(_selectedNote);
            string? currentAction = GetActionForNote(_selectedNote);
            bool isCopyMod = _editingPreset.CopyModifier?.Number == _selectedNote;

            string statusText = isCopyMod ? "Copy Modifier" : (currentAction ?? "No action");
            canvas.DrawText($"Selected: {noteName} (Note {_selectedNote}) - {statusText}",
                x, y + _textPaint.TextSize, _textPaint);
        }
        else
        {
            canvas.DrawText("Click a key or press a MIDI note to select",
                x, y + _textPaint.TextSize, _secondaryTextPaint);
            return y + _textPaint.TextSize + 8;
        }
        y += _textPaint.TextSize + 10;

        // Category buttons row
        float btnWidth = 75f;
        float btnHeight = 28f;
        float btnSpacing = 6f;
        float totalWidth = btnWidth * 5 + btnSpacing * 4;
        float startX = x + (width - totalWidth) / 2;

        var categories = new[]
        {
            ("None", ActionCategory.None, "category_none", _theme.Button),
            ("Paste", ActionCategory.Paste, "category_paste", new SKColor(70, 130, 180)),
            ("Copy", ActionCategory.Copy, "category_copy", new SKColor(76, 175, 80)),
            ("Control", ActionCategory.Control, "category_control", new SKColor(255, 152, 0)),
            ("Modifier", ActionCategory.CopyModifier, "category_copymod", new SKColor(150, 100, 200))
        };

        float btnX = startX;
        foreach (var (label, category, id, color) in categories)
        {
            var btnRect = new SKRect(btnX, y, btnX + btnWidth, y + btnHeight);
            bool isSelected = _selectedCategory == category;
            bool isHovered = _hoveredButton == id;

            SKColor bgColor = isSelected ? color : (isHovered ? color.WithAlpha(100) : _theme.Button);
            using var btnPaint = new SKPaint { Color = bgColor, IsAntialias = true };
            canvas.DrawRoundRect(new SKRoundRect(btnRect, 4), btnPaint);

            if (isSelected)
            {
                using var borderPaint = new SKPaint
                {
                    Color = SKColors.White,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 2f
                };
                canvas.DrawRoundRect(new SKRoundRect(btnRect, 4), borderPaint);
            }

            using var txtPaint = new SKPaint
            {
                Color = _theme.Text,
                IsAntialias = true,
                TextSize = 11f,
                TextAlign = SKTextAlign.Center,
                Typeface = SKTypeface.FromFamilyName("Segoe UI", isSelected ? SKFontStyle.Bold : SKFontStyle.Normal)
            };
            canvas.DrawText(label, btnRect.MidX, btnRect.MidY + 4, txtPaint);

            _buttons.Add(new ButtonRect(id, btnRect));
            btnX += btnWidth + btnSpacing;
        }
        y += btnHeight + 10;

        // Slot selector (for Paste/Copy) or Control action buttons
        if (_selectedCategory == ActionCategory.Paste || _selectedCategory == ActionCategory.Copy)
        {
            y = DrawSlotSelector(canvas, x, y, width);
        }
        else if (_selectedCategory == ActionCategory.Control)
        {
            y = DrawControlActions(canvas, x, y, width);
        }
        else if (_selectedCategory == ActionCategory.CopyModifier)
        {
            // Show info about copy modifier
            canvas.DrawText("Hold this key to switch Paste actions to Copy",
                x, y + _secondaryTextPaint.TextSize, _secondaryTextPaint);
            y += _secondaryTextPaint.TextSize + 4;
        }

        return y;
    }

    private float DrawSlotSelector(SKCanvas canvas, float x, float y, float width)
    {
        string prefix = _selectedCategory == ActionCategory.Paste ? "Paste from" : "Copy to";
        canvas.DrawText($"{prefix} Slot:", x, y + _textPaint.TextSize, _textPaint);
        y += _textPaint.TextSize + 6;

        float slotBtnSize = 36f;
        float slotSpacing = 4f;
        float totalWidth = slotBtnSize * 10 + slotSpacing * 9;
        float startX = x + (width - totalWidth) / 2;

        // Get currently assigned slot
        int currentSlot = -1;
        var currentAction = GetActionForNote(_selectedNote);
        if (currentAction != null)
        {
            if (currentAction.StartsWith("PasteFromSlot") && _selectedCategory == ActionCategory.Paste)
            {
                int.TryParse(currentAction.Replace("PasteFromSlot", ""), out currentSlot);
            }
            else if (currentAction.StartsWith("CopyToSlot") && _selectedCategory == ActionCategory.Copy)
            {
                int.TryParse(currentAction.Replace("CopyToSlot", ""), out currentSlot);
            }
        }

        SKColor categoryColor = _selectedCategory == ActionCategory.Paste
            ? new SKColor(70, 130, 180)
            : new SKColor(76, 175, 80);

        for (int slot = 1; slot <= 10; slot++)
        {
            float slotX = startX + (slot - 1) * (slotBtnSize + slotSpacing);
            var slotRect = new SKRect(slotX, y, slotX + slotBtnSize, y + slotBtnSize);

            bool isSelected = slot == currentSlot;
            bool isHovered = _hoveredButton == $"slot_{slot}";

            SKColor bgColor = isSelected ? categoryColor : (isHovered ? categoryColor.WithAlpha(100) : _theme.Button);
            using var slotPaint = new SKPaint { Color = bgColor, IsAntialias = true };
            canvas.DrawRoundRect(new SKRoundRect(slotRect, 6), slotPaint);

            if (isSelected)
            {
                using var borderPaint = new SKPaint
                {
                    Color = SKColors.White,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 2f
                };
                canvas.DrawRoundRect(new SKRoundRect(slotRect, 6), borderPaint);
            }

            using var numPaint = new SKPaint
            {
                Color = _theme.Text,
                IsAntialias = true,
                TextSize = 14f,
                TextAlign = SKTextAlign.Center,
                Typeface = SKTypeface.FromFamilyName("Segoe UI", isSelected ? SKFontStyle.Bold : SKFontStyle.Normal)
            };
            string slotLabel = slot == 10 ? "0" : slot.ToString();
            canvas.DrawText(slotLabel, slotRect.MidX, slotRect.MidY + 5, numPaint);

            _buttons.Add(new ButtonRect($"slot_{slot}", slotRect));
        }

        return y + slotBtnSize + 8;
    }

    private float DrawControlActions(SKCanvas canvas, float x, float y, float width)
    {
        var controlActions = new[]
        {
            ("ToggleHud", "Toggle HUD"),
            ("CycleForward", "Cycle Next"),
            ("CycleBackward", "Cycle Prev"),
            ("PromoteTempSlot", "Promote"),
            ("PasteFromActiveSlot", "Paste Active"),
            ("ClearAll", "Clear All")
        };

        float btnWidth = 90f;
        float btnHeight = 28f;
        float btnSpacing = 6f;
        int buttonsPerRow = 3;
        float totalRowWidth = btnWidth * buttonsPerRow + btnSpacing * (buttonsPerRow - 1);
        float startX = x + (width - totalRowWidth) / 2;

        var currentAction = GetActionForNote(_selectedNote);
        SKColor controlColor = new SKColor(255, 152, 0);

        for (int i = 0; i < controlActions.Length; i++)
        {
            int row = i / buttonsPerRow;
            int col = i % buttonsPerRow;

            float btnX = startX + col * (btnWidth + btnSpacing);
            float btnY = y + row * (btnHeight + btnSpacing);
            var btnRect = new SKRect(btnX, btnY, btnX + btnWidth, btnY + btnHeight);

            string actionId = controlActions[i].Item1;
            string actionLabel = controlActions[i].Item2;

            bool isSelected = currentAction == actionId;
            bool isHovered = _hoveredButton == $"control_{actionId}";

            SKColor bgColor = isSelected ? controlColor : (isHovered ? controlColor.WithAlpha(100) : _theme.Button);
            using var btnPaint = new SKPaint { Color = bgColor, IsAntialias = true };
            canvas.DrawRoundRect(new SKRoundRect(btnRect, 4), btnPaint);

            if (isSelected)
            {
                using var borderPaint = new SKPaint
                {
                    Color = SKColors.White,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 2f
                };
                canvas.DrawRoundRect(new SKRoundRect(btnRect, 4), borderPaint);
            }

            using var txtPaint = new SKPaint
            {
                Color = _theme.Text,
                IsAntialias = true,
                TextSize = 10f,
                TextAlign = SKTextAlign.Center,
                Typeface = SKTypeface.FromFamilyName("Segoe UI", isSelected ? SKFontStyle.Bold : SKFontStyle.Normal)
            };
            canvas.DrawText(actionLabel, btnRect.MidX, btnRect.MidY + 3, txtPaint);

            _buttons.Add(new ButtonRect($"control_{actionId}", btnRect));
        }

        int numRows = (controlActions.Length + buttonsPerRow - 1) / buttonsPerRow;
        return y + numRows * (btnHeight + btnSpacing);
    }

    private void DrawMappingsSummary(SKCanvas canvas, float x, float y, float width, float maxHeight)
    {
        canvas.DrawText("Current Mappings:", x, y + _textPaint.TextSize, _textPaint);
        y += _textPaint.TextSize + 6;

        float startY = y;
        float columnWidth = width / 2;
        int column = 0;

        // Copy modifier first
        if (_editingPreset.CopyModifier != null)
        {
            string noteName = GetNoteName(_editingPreset.CopyModifier.Number);
            using var modPaint = new SKPaint
            {
                Color = new SKColor(150, 100, 200),
                IsAntialias = true,
                TextSize = 10f,
                Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
            };
            canvas.DrawText($"{noteName} = Copy Modifier", x, y + modPaint.TextSize, modPaint);
            y += modPaint.TextSize + 3;
        }

        // Regular mappings
        foreach (var (action, trigger) in _editingPreset.Mappings.OrderBy(m => m.Value.Number))
        {
            if (y - startY > maxHeight - 20) break;

            string noteName = GetNoteName(trigger.Number);
            var color = GetActionColor(action);

            // Format action name nicely
            string displayAction = FormatActionName(action);

            using var mappingPaint = new SKPaint
            {
                Color = color,
                IsAntialias = true,
                TextSize = 10f,
                Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
            };

            float mappingX = x + column * columnWidth;
            canvas.DrawText($"{noteName} = {displayAction}", mappingX, y + mappingPaint.TextSize, mappingPaint);

            column++;
            if (column >= 2)
            {
                column = 0;
                y += mappingPaint.TextSize + 3;
            }
        }
    }

    private string FormatActionName(string action)
    {
        if (action.StartsWith("PasteFromSlot"))
            return $"Paste {action.Replace("PasteFromSlot", "")}";
        if (action.StartsWith("CopyToSlot"))
            return $"Copy {action.Replace("CopyToSlot", "")}";
        return action switch
        {
            "ToggleHud" => "Toggle HUD",
            "CycleForward" => "Cycle Next",
            "CycleBackward" => "Cycle Prev",
            "PromoteTempSlot" => "Promote",
            "PasteFromActiveSlot" => "Paste Active",
            "ClearAll" => "Clear All",
            _ => action
        };
    }

    private string? GetActionForNote(int note)
    {
        foreach (var (action, trigger) in _editingPreset.Mappings)
        {
            if (trigger.Number == note)
                return action;
        }
        return null;
    }

    private static bool IsBlackKey(int noteInOctave) =>
        noteInOctave is 1 or 3 or 6 or 8 or 10;

    private static string GetNoteName(int midiNote)
    {
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int octave = midiNote / 12 - 1;
        int note = midiNote % 12;
        return $"{noteNames[note]}{octave}";
    }

    private static SKColor GetActionColor(string action)
    {
        if (action.StartsWith("Paste"))
            return new SKColor(70, 130, 180); // Blue
        if (action.StartsWith("Copy"))
            return new SKColor(76, 175, 80); // Green
        return new SKColor(255, 152, 0); // Orange for control
    }

    private record ButtonRect(string Id, SKRect Rect);
    private record PianoKey(int Note, SKRect Rect, bool IsBlack);
}
