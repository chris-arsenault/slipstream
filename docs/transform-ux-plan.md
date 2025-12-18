# Processor System Design

## Overview

Extend Slipstream's processor system with two activation modes: **Toggle Bank** (persistent on/off) and **Chord Keys** (momentary hold). Both modes combine to determine which processors apply on paste.

---

## Design Principles

1. **No modal UI** - Processors activate via toggles and chords, not a picker
2. **Additive combination** - Chord keys ADD to toggle state, never invert
3. **Deterministic order** - Processors have priority; lower runs first
4. **Visual feedback** - HUD shows armed processors at all times
5. **Keyboard-first, MIDI-enhanced** - Full functionality via keyboard; MIDI adds convenience

---

## User Experience

### Core Concepts

| Concept | Description |
|---------|-------------|
| **Processor** | A single transformation (e.g., Uppercase, Trim) with a priority |
| **Toggle State** | Persistent on/off per processor, survives across pastes |
| **Chord Key** | Momentary key/button that activates processor while held |
| **Active Set** | Union of toggled-on processors + chord-held processors |
| **Priority** | Numeric order (lower = runs first); determines pipeline order |

### The Two Activation Modes

**Toggle Bank (Persistent)**
```
Ctrl+Alt+U  → Toggle Uppercase ON (stays on)
Ctrl+Alt+T  → Toggle Trim ON (stays on)
Ctrl+Shift+1 → Paste slot 1 with Uppercase → Trim applied
Ctrl+Alt+U  → Toggle Uppercase OFF
Ctrl+Shift+1 → Paste slot 1 with Trim only
```

**Chord Keys (Momentary)**
```
Hold L + Ctrl+Shift+1 → Paste slot 1 with Lowercase (just this once)
Release L → Lowercase no longer active
```

**Combined**
```
Uppercase toggled ON (priority 10)
Trim toggled ON (priority 50)
Hold L (Lowercase, priority 20) + paste
→ Applies: Uppercase → Lowercase → Trim (priority order: 10, 20, 50)
```

Chord keys are purely additive. Holding a chord key for an already-toggled processor has no effect (it's already in the active set).

---

## Processor Definitions

Each processor declares:
- **Name**: Unique identifier
- **Display Name**: Shown in HUD
- **Chord Key**: Keyboard key for momentary activation (optional)
- **Toggle Hotkey**: Hotkey to toggle persistent state (optional)
- **Priority**: Execution order (lower = earlier)
- **Content Types**: Which content types it supports

### Default Processor Priorities

| Priority | Processor | Chord Key | Toggle Hotkey | Description |
|----------|-----------|-----------|---------------|-------------|
| 10 | Uppercase | U | Ctrl+Alt+U | Convert to uppercase |
| 20 | Lowercase | L | Ctrl+Alt+L | Convert to lowercase |
| 30 | TitleCase | - | Ctrl+Alt+I | Title Case Text |
| 40 | StripFormatting | S | Ctrl+Alt+S | Remove rich text/HTML |
| 50 | Trim | T | Ctrl+Alt+T | Trim whitespace |
| 60 | RemoveNewlines | N | Ctrl+Alt+N | Single line |
| 70 | Reverse | R | Ctrl+Alt+R | Reverse content |
| 100 | Grayscale | G | Ctrl+Alt+G | Image to grayscale |
| 110 | InvertColors | I | Ctrl+Alt+I | Invert image colors |

Priority gaps (10, 20, 30...) allow inserting custom processors later.

---

## HUD Display

### Armed Processors Indicator

When any processor is toggled on, show in HUD header:

```
┌──────────────────────────────────────────────────────────────┐
│ SLIPSTREAM                          [U][T] ←armed         ─  │
├──────────────────────────────────────────────────────────────┤
│ 1 │ T │ Hello World                      [ACTIVE]         🔓 │
...
```

### Chord Keys Held (Transient)

When chord keys are held, show combined active set:

```
┌──────────────────────────────────────────────────────────────┐
│ SLIPSTREAM                          [U][L][T] ←active     ─  │
│                                      ↑held                   │
├──────────────────────────────────────────────────────────────┤
```

Visual distinction: toggled = solid, chord-held = outlined or different color.

---

## Keyboard Shortcuts

### Toggle Hotkeys (Persistent)

| Shortcut | Action |
|----------|--------|
| `Ctrl+Alt+U` | Toggle Uppercase |
| `Ctrl+Alt+L` | Toggle Lowercase |
| `Ctrl+Alt+S` | Toggle Strip Formatting |
| `Ctrl+Alt+T` | Toggle Trim |
| `Ctrl+Alt+N` | Toggle Remove Newlines |
| `Ctrl+Alt+R` | Toggle Reverse |
| `Ctrl+Alt+G` | Toggle Grayscale |
| `Ctrl+Alt+I` | Toggle Invert Colors |
| `Ctrl+Alt+0` | Clear all toggles |

### Chord Keys (Momentary)

Hold these while pressing paste hotkey:

| Key | Processor |
|-----|-----------|
| U | Uppercase |
| L | Lowercase |
| S | Strip Formatting |
| T | Trim |
| N | Remove Newlines |
| R | Reverse |
| G | Grayscale |
| I | Invert Colors |

---

## MIDI Integration

### Toggle Mode

MIDI buttons can toggle processors on/off. LED state reflects toggle state.

```csharp
public class MidiControlScheme
{
    // Existing
    public Dictionary<string, MidiTrigger> Mappings { get; init; }
    public MidiTrigger? CopyModifier { get; init; }

    // New: processor toggles
    public Dictionary<string, MidiTrigger> ProcessorToggles { get; init; }
}
```

### Chord Mode

Hold pad(s) + tap slot pad = paste with those processors active.

The MIDI manager tracks which "processor pads" are held and adds them to the active set for the duration of the paste.

---

## Architecture

### Processor Priority in Registry

Extend processor registration to include priority:

```csharp
public record ProcessorDefinition(
    string Name,
    string DisplayName,
    int Priority,
    ContentType SupportedTypes,
    char? ChordKey,           // Keyboard chord key
    Func<IClipboardContent, IClipboardContent?> Transform
);

public class ProcessorRegistry
{
    private readonly Dictionary<string, ProcessorDefinition> _processors = new();

    public void Register(ProcessorDefinition processor) { ... }

    // Get processors sorted by priority
    public IEnumerable<ProcessorDefinition> GetByPriority() =>
        _processors.Values.OrderBy(p => p.Priority);

    // Execute active set in priority order
    public IClipboardContent? ExecuteActiveSet(
        IClipboardContent content,
        IReadOnlySet<string> activeProcessors)
    {
        var current = content;
        foreach (var proc in GetByPriority())
        {
            if (!activeProcessors.Contains(proc.Name)) continue;
            if (!proc.SupportedTypes.HasFlag(content.Type)) continue;

            current = proc.Transform(current);
            if (current == null) return null;
        }
        return current;
    }
}
```

### Toggle State

```csharp
public class ProcessorToggleState
{
    private readonly HashSet<string> _toggledOn = new();

    public event EventHandler? StateChanged;

    public IReadOnlySet<string> ToggledProcessors => _toggledOn;

    public bool IsToggled(string processorName) => _toggledOn.Contains(processorName);

    public void Toggle(string processorName)
    {
        if (!_toggledOn.Remove(processorName))
            _toggledOn.Add(processorName);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetToggle(string processorName, bool on)
    {
        if (on) _toggledOn.Add(processorName);
        else _toggledOn.Remove(processorName);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearAll()
    {
        _toggledOn.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
```

### Chord Key Tracking

```csharp
public class ChordKeyTracker
{
    private readonly HashSet<char> _heldKeys = new();
    private readonly Dictionary<char, string> _keyToProcessor;

    public ChordKeyTracker(ProcessorRegistry registry)
    {
        _keyToProcessor = registry.GetByPriority()
            .Where(p => p.ChordKey.HasValue)
            .ToDictionary(p => p.ChordKey!.Value, p => p.Name);
    }

    public IReadOnlySet<string> HeldProcessors =>
        _heldKeys
            .Where(k => _keyToProcessor.ContainsKey(k))
            .Select(k => _keyToProcessor[k])
            .ToHashSet();

    public void KeyDown(char key) => _heldKeys.Add(char.ToUpper(key));
    public void KeyUp(char key) => _heldKeys.Remove(char.ToUpper(key));
    public void Reset() => _heldKeys.Clear();
}
```

### Active Set Computation

```csharp
public class ProcessorActivation
{
    private readonly ProcessorToggleState _toggleState;
    private readonly ChordKeyTracker _chordTracker;

    public IReadOnlySet<string> GetActiveSet()
    {
        var active = new HashSet<string>(_toggleState.ToggledProcessors);
        active.UnionWith(_chordTracker.HeldProcessors);
        return active;
    }
}
```

### Integration with Paste

Modify paste command to apply active processors:

```csharp
public class PasteSlotCommand : ICommand
{
    public void Execute(...)
    {
        var content = _slotManager.GetSlot(slotIndex)?.Content;
        if (content == null) return;

        var activeSet = _processorActivation.GetActiveSet();
        if (activeSet.Count > 0)
        {
            content = _processorRegistry.ExecuteActiveSet(content, activeSet);
            if (content == null) return; // Transform failed
        }

        _clipboardService.SetAndPaste(content);
    }
}
```

---

## Implementation Phases

### Phase 1: Processor Priorities

**Goal**: Add priority to processor definitions.

**Tasks**:
1. Create `ProcessorDefinition` record with Priority and ChordKey
2. Update `ProcessorRegistry` to store definitions, sort by priority
3. Add `ExecuteActiveSet()` method
4. Update existing processor registrations with priorities
5. Unit tests for priority ordering

---

### Phase 2: Toggle State

**Goal**: Persistent processor toggle state with hotkeys.

**Tasks**:
1. Create `ProcessorToggleState` class
2. Add toggle commands to `CommandRegistry`
3. Register toggle hotkeys (Ctrl+Alt+letter)
4. Wire into App.xaml.cs composition
5. Persist toggle state in `AppSettings.PersistedProcessorToggles` (survives app restart)

---

### Phase 3: HUD Armed Indicator

**Goal**: Show toggled processors in HUD.

**Tasks**:
1. Update `HudRenderer` to show armed processor badges
2. Subscribe to `ProcessorToggleState.StateChanged`
3. Render badges in header area
4. Style: compact letter badges [U][T][S]

---

### Phase 4: Chord Key Tracking

**Goal**: Track held keys for chord transforms.

**Tasks**:
1. Create `ChordKeyTracker` class
2. Hook keyboard events in HudWindow (or global hook)
3. Create `ProcessorActivation` to combine toggle + chord
4. Update paste command to use active set
5. Test chord + paste workflow

---

### Phase 5: Chord Visual Feedback

**Goal**: Show chord-held processors distinctly in HUD.

**Tasks**:
1. Update `HudRenderer` to distinguish toggle vs chord
2. Toggled = solid badge, Chord = outlined badge (or color difference)
3. Show combined active set when chords held

---

### Phase 6: MIDI Integration

**Goal**: MIDI buttons toggle processors, pads act as chords.

**Tasks**:
1. Add `ProcessorToggles` to `MidiControlScheme`
2. Update `MidiManager` to fire toggle commands
3. Track held pads as chord equivalents
4. LED feedback for toggle state

---

## Files to Create/Modify

| File | Action | Phase |
|------|--------|-------|
| `Processing/ProcessorDefinition.cs` | NEW | 1 |
| `Processing/ProcessorRegistry.cs` | MODIFY | 1 |
| `Processing/ProcessorToggleState.cs` | NEW | 2 |
| `Processing/ChordKeyTracker.cs` | NEW | 4 |
| `Processing/ProcessorActivation.cs` | NEW | 4 |
| `Commands/ToggleProcessorCommand.cs` | NEW | 2 |
| `Commands/CommandRegistry.cs` | MODIFY | 2 |
| `Commands/PasteSlotCommand.cs` | MODIFY | 4 |
| `UI/HudRenderer.cs` | MODIFY | 3, 5 |
| `Models/MidiControlScheme.cs` | MODIFY | 6 |
| `Services/MidiManager.cs` | MODIFY | 6 |
| `App.xaml.cs` | MODIFY | 2, 4 |

---

## Configuration

### Settings

```csharp
public class AppSettings
{
    // Existing...

    // Processor toggle state persists across sessions
    public HashSet<string> PersistedProcessorToggles { get; set; } = new();

    // Custom processor priorities (override defaults) - future
    public Dictionary<string, int> ProcessorPriorityOverrides { get; set; } = new();
}
```

Toggle state loads on startup and saves on every toggle change (debounced with existing settings persistence).

---

## Example Workflows

### Workflow 1: Repetitive Formatting
```
User toggles Uppercase + Trim ON
Pastes 20 code snippets from various slots
All paste as UPPERCASE, TRIMMED
Toggles both OFF when done
```

### Workflow 2: One-Off Transform
```
User holds L key
Pastes from slot 3
Content pastes as lowercase
Releases L - back to normal
```

### Workflow 3: Combined
```
StripFormatting toggled ON (always want plain text)
Hold U + paste slot 1 → plain text, uppercase
Hold L + paste slot 2 → plain text, lowercase
Regular paste slot 3 → plain text only
```

### Workflow 4: MIDI Performance
```
User has Trim + StripFormat toggled ON (LEDs lit)
Holds pad 1 (Uppercase) + taps slot 5 pad
→ Pastes slot 5: Strip → Trim → Upper
Releases pad 1
Taps slot 5 pad again
→ Pastes slot 5: Strip → Trim only
```

---

## What This Design Avoids

1. **No modal picker** - Processors activate inline, no UI interruption
2. **No complex pipeline building** - Priority handles ordering automatically
3. **No invert-on-hold** - Chord always adds, never removes (predictable)
4. **No hidden state** - HUD always shows what's armed
5. **No ordering ambiguity** - Priority is deterministic

---

## Success Criteria

1. User can toggle processors on/off with Ctrl+Alt+letter
2. Toggled processors apply to all pastes until toggled off
3. User can hold chord keys during paste for one-shot transforms
4. Chord + toggle combine additively
5. Processors execute in priority order
6. HUD shows armed processors clearly
7. Ctrl+Alt+0 clears all toggles
8. MIDI buttons can toggle processors
9. MIDI pads work as chord keys when held

---

## Future Enhancements (Deferred)

- Custom pipelines (explicit ordering, saved presets)
- Per-slot processor overrides
- Processor parameters (e.g., "resize to 50%")
- Live preview
- Undo transform
