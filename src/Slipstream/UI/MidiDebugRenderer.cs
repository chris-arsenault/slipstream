using SkiaSharp;
using Slipstream.Models;
using Slipstream.Processing;

namespace Slipstream.UI;

/// <summary>
/// Renderer for the MIDI debug/demo overlay showing piano keys and current activity.
/// </summary>
public class MidiDebugRenderer : BaseRenderer
{
    private readonly HashSet<int> _pressedNotes = new();
    private readonly List<PianoKey> _pianoKeys = new();

    // Current scheme for determining what notes do
    private MidiControlScheme? _currentScheme;

    // Layout constants
    private const float Padding = 8f;
    private const float CornerRadius = 8f;

    // Piano constants (slightly smaller than editor)
    private const float WhiteKeyWidth = 20f;
    private const float WhiteKeyHeight = 80f;
    private const float BlackKeyWidth = 14f;
    private const float BlackKeyHeight = 50f;
    private const int VisibleWhiteKeys = 21; // 3 octaves

    // Status area
    private const float StatusHeight = 32f;

    // Base octave for display
    private int _baseOctave = 2;

    // Current status info
    private readonly List<string> _activeProcessors = new();
    private string? _pendingAction;
    private bool _copyModifierHeld;

    public MidiDebugRenderer() : base(ColorTheme.Dark)
    {
    }

    public void SetScheme(MidiControlScheme? scheme)
    {
        _currentScheme = scheme;
    }

    public void SetNotePressed(int note, bool pressed)
    {
        if (pressed)
        {
            _pressedNotes.Add(note);
            // Auto-scroll to show pressed note
            int noteOctave = note / 12;
            if (noteOctave < _baseOctave || noteOctave >= _baseOctave + 3)
            {
                _baseOctave = Math.Clamp(noteOctave - 1, 0, 8);
            }
        }
        else
        {
            _pressedNotes.Remove(note);
        }
    }

    public void SetCopyModifierHeld(bool held)
    {
        _copyModifierHeld = held;
    }

    public void SetActiveProcessors(IEnumerable<string> processors)
    {
        _activeProcessors.Clear();
        _activeProcessors.AddRange(processors);
    }

    public void SetPendingAction(string? action)
    {
        _pendingAction = action;
    }

    public void ClearAllNotes()
    {
        _pressedNotes.Clear();
    }

    /// <summary>
    /// Get the total required height for the debug display.
    /// </summary>
    public static float GetRequiredHeight()
    {
        return Padding * 3 + StatusHeight + WhiteKeyHeight;
    }

    /// <summary>
    /// Get the total required width for the debug display.
    /// </summary>
    public static float GetRequiredWidth()
    {
        return Padding * 2 + VisibleWhiteKeys * WhiteKeyWidth;
    }

    public void Render(SKCanvas canvas, SKSize size, float dpiScale = 1.0f)
    {
        _pianoKeys.Clear();
        canvas.Clear(SKColors.Transparent);

        canvas.Save();
        canvas.Scale(dpiScale);

        size = new SKSize(size.Width / dpiScale, size.Height / dpiScale);

        // Background
        var bgRect = new SKRoundRect(new SKRect(0, 0, size.Width, size.Height), CornerRadius);
        canvas.DrawRoundRect(bgRect, _backgroundPaint);

        float y = Padding;

        // Status bar showing current composition
        y = DrawStatusBar(canvas, Padding, y, size.Width - Padding * 2);

        // Piano keyboard
        y += Padding;
        DrawPianoKeyboard(canvas, Padding, y, size.Width - Padding * 2);

        // Border
        canvas.DrawRoundRect(bgRect, _borderPaint);

        canvas.Restore();
    }

    private float DrawStatusBar(SKCanvas canvas, float x, float y, float width)
    {
        // Background for status area
        var statusRect = new SKRect(x, y, x + width, y + StatusHeight);
        using var statusBg = new SKPaint
        {
            Color = new SKColor(30, 30, 35),
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(statusRect, 4), statusBg);

        // Build status text
        var parts = new List<string>();

        // Active processors (transforms)
        foreach (var processor in _activeProcessors)
        {
            var def = ProcessorDefinitions.GetByName(processor);
            parts.Add(def?.DisplayName ?? processor);
        }

        // Copy modifier indicator
        if (_copyModifierHeld)
        {
            parts.Add("[COPY]");
        }

        // Pending action
        if (!string.IsNullOrEmpty(_pendingAction))
        {
            parts.Add(FormatAction(_pendingAction));
        }

        // Draw the composition
        if (parts.Count > 0)
        {
            string statusText = string.Join(" + ", parts);

            using var textPaint = new SKPaint
            {
                Color = _theme.Text,
                IsAntialias = true,
                TextSize = 14f,
                Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold),
                TextAlign = SKTextAlign.Center
            };

            canvas.DrawText(statusText, statusRect.MidX, statusRect.MidY + 5, textPaint);
        }
        else
        {
            // Show idle state
            using var idlePaint = new SKPaint
            {
                Color = _theme.TextSecondary,
                IsAntialias = true,
                TextSize = 12f,
                Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal),
                TextAlign = SKTextAlign.Center
            };
            canvas.DrawText("Press MIDI keys...", statusRect.MidX, statusRect.MidY + 4, idlePaint);
        }

        // Draw processor badges on the left
        float badgeX = x + 6;
        const float badgeSize = 20f;
        const float badgeSpacing = 4f;

        foreach (var processor in _activeProcessors)
        {
            var def = ProcessorDefinitions.GetByName(processor);
            if (def == null) continue;

            var badgeRect = new SKRoundRect(new SKRect(badgeX, y + 6, badgeX + badgeSize, y + 6 + badgeSize), 4);

            using var badgePaint = new SKPaint
            {
                Color = new SKColor(220, 80, 120),
                IsAntialias = true
            };
            canvas.DrawRoundRect(badgeRect, badgePaint);

            using var badgeTextPaint = new SKPaint
            {
                Color = SKColors.White,
                IsAntialias = true,
                TextSize = 11f,
                Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold),
                TextAlign = SKTextAlign.Center
            };
            canvas.DrawText(def.Badge, badgeX + badgeSize / 2, y + 6 + badgeSize / 2 + 4, badgeTextPaint);

            badgeX += badgeSize + badgeSpacing;
        }

        return y + StatusHeight;
    }

    private void DrawPianoKeyboard(SKCanvas canvas, float x, float y, float width)
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
                    if (note == 1 || note == 6) blackKeyX -= 1;
                    if (note == 3 || note == 10) blackKeyX += 1;
                    DrawBlackKey(canvas, blackKeyX, y, midiNote);
                }
                else
                {
                    whiteKeyX += WhiteKeyWidth;
                }
            }
        }

        // Draw octave indicator
        using var octavePaint = new SKPaint
        {
            Color = _theme.TextSecondary,
            IsAntialias = true,
            TextSize = 10f,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal),
            TextAlign = SKTextAlign.Center
        };
        canvas.DrawText($"C{_baseOctave}-C{_baseOctave + 3}", x + width / 2, y + WhiteKeyHeight + 12, octavePaint);
    }

    private void DrawWhiteKey(SKCanvas canvas, float x, float y, int note)
    {
        var keyRect = new SKRect(x, y, x + WhiteKeyWidth - 1, y + WhiteKeyHeight);

        bool isPressed = _pressedNotes.Contains(note);
        string? mappedAction = GetActionForNote(note);
        string? mappedProcessor = GetProcessorForNote(note);
        bool isCopyModifier = _currentScheme?.CopyModifier?.Number == note;

        _pianoKeys.Add(new PianoKey(note, keyRect, false));

        // Determine key color based on state
        SKColor keyColor;
        if (isPressed)
        {
            // Bright highlight when pressed
            if (mappedProcessor != null)
                keyColor = new SKColor(255, 150, 180); // Pink for processor
            else if (isCopyModifier)
                keyColor = new SKColor(220, 180, 255); // Purple for copy mod
            else if (mappedAction != null)
                keyColor = GetActionColor(mappedAction).WithAlpha(200);
            else
                keyColor = new SKColor(255, 255, 150); // Yellow default
        }
        else
        {
            // Subtle tint when not pressed
            if (mappedAction != null)
                keyColor = GetActionColor(mappedAction).WithAlpha(60);
            else if (mappedProcessor != null)
                keyColor = new SKColor(220, 80, 120).WithAlpha(60);
            else if (isCopyModifier)
                keyColor = new SKColor(200, 150, 255).WithAlpha(80);
            else
                keyColor = SKColors.White;
        }

        using var keyPaint = new SKPaint { Color = keyColor, IsAntialias = true };
        var keyRoundRect = new SKRoundRect();
        keyRoundRect.SetRectRadii(keyRect, new[]
        {
            new SKPoint(0, 0),
            new SKPoint(0, 0),
            new SKPoint(2, 2),
            new SKPoint(2, 2)
        });
        canvas.DrawRoundRect(keyRoundRect, keyPaint);

        // Border
        using var borderPaint = new SKPaint
        {
            Color = isPressed ? _theme.Accent : new SKColor(180, 180, 180),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = isPressed ? 2f : 1f
        };
        canvas.DrawRoundRect(keyRoundRect, borderPaint);

        // Note name at bottom
        string noteName = GetNoteName(note);
        using var notePaint = new SKPaint
        {
            Color = SKColors.Black.WithAlpha(180),
            IsAntialias = true,
            TextSize = 8f,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
        };
        canvas.DrawText(noteName, keyRect.MidX, keyRect.Bottom - 4, notePaint);

        // Action indicator dot
        if (mappedAction != null || mappedProcessor != null || isCopyModifier)
        {
            var dotColor = isCopyModifier ? new SKColor(150, 100, 200)
                : mappedProcessor != null ? new SKColor(220, 80, 120)
                : GetActionColor(mappedAction!);
            using var dotPaint = new SKPaint { Color = dotColor, IsAntialias = true };
            canvas.DrawCircle(keyRect.MidX, keyRect.Top + 8, 4, dotPaint);
        }
    }

    private void DrawBlackKey(SKCanvas canvas, float x, float y, int note)
    {
        var keyRect = new SKRect(x, y, x + BlackKeyWidth, y + BlackKeyHeight);

        bool isPressed = _pressedNotes.Contains(note);
        string? mappedAction = GetActionForNote(note);
        string? mappedProcessor = GetProcessorForNote(note);
        bool isCopyModifier = _currentScheme?.CopyModifier?.Number == note;

        _pianoKeys.Add(new PianoKey(note, keyRect, true));

        // Determine key color
        SKColor keyColor;
        if (isPressed)
        {
            if (mappedProcessor != null)
                keyColor = new SKColor(255, 120, 160);
            else if (isCopyModifier)
                keyColor = new SKColor(180, 140, 220);
            else if (mappedAction != null)
                keyColor = GetActionColor(mappedAction);
            else
                keyColor = new SKColor(200, 200, 100);
        }
        else
        {
            if (mappedAction != null)
                keyColor = GetActionColor(mappedAction).WithAlpha(150);
            else if (mappedProcessor != null)
                keyColor = new SKColor(220, 80, 120).WithAlpha(150);
            else if (isCopyModifier)
                keyColor = new SKColor(120, 80, 160);
            else
                keyColor = new SKColor(30, 30, 35);
        }

        using var keyPaint = new SKPaint { Color = keyColor, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(keyRect, 2), keyPaint);

        // Border when pressed
        if (isPressed)
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

        // Action indicator dot
        if (mappedAction != null || mappedProcessor != null || isCopyModifier)
        {
            var dotColor = isCopyModifier ? new SKColor(150, 100, 200)
                : mappedProcessor != null ? new SKColor(220, 80, 120)
                : GetActionColor(mappedAction!);
            using var dotPaint = new SKPaint { Color = dotColor, IsAntialias = true };
            canvas.DrawCircle(keyRect.MidX, keyRect.Top + 6, 3, dotPaint);
        }
    }

    private string? GetActionForNote(int note)
    {
        if (_currentScheme == null) return null;

        foreach (var (action, trigger) in _currentScheme.Mappings)
        {
            if (trigger.Number == note)
                return action;
        }
        return null;
    }

    private string? GetProcessorForNote(int note)
    {
        if (_currentScheme == null) return null;

        foreach (var (processor, trigger) in _currentScheme.ProcessorChords)
        {
            if (trigger.Number == note)
                return processor;
        }
        return null;
    }

    private static string FormatAction(string action)
    {
        if (action.StartsWith("PasteFromSlot"))
            return $"Paste {action.Replace("PasteFromSlot", "")}";
        if (action.StartsWith("CopyToSlot"))
            return $"Copy {action.Replace("CopyToSlot", "")}";
        return action switch
        {
            "ToggleHud" => "Toggle HUD",
            "CycleForward" => "Cycle +",
            "CycleBackward" => "Cycle -",
            "PromoteTempSlot" => "Promote",
            "PasteFromActiveSlot" => "Paste Active",
            "ClearAll" => "Clear All",
            _ => action
        };
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

    private record PianoKey(int Note, SKRect Rect, bool IsBlack);
}
