# Processor System Design

## Overview

Extend Slipstream's existing processor system to support interactive processor selection and pipeline composition. The design prioritizes simplicity, discoverability, and alignment with existing patterns.

---

## Design Principles

1. **One mental model** - Processors transform content. That's it.
2. **Progressive disclosure** - Simple single-processor use first, pipelines for power users.
3. **Reuse existing patterns** - Build on ProcessorRegistry, command system, and modifier patterns.
4. **Minimal UI** - Processor picker appears when needed, disappears after use.
5. **Keyboard-first, MIDI-enhanced** - Works fully with keyboard; MIDI adds convenience, not complexity.

---

## User Experience

### Core Concepts

| Concept | Description |
|---------|-------------|
| **Processor** | A single transformation (e.g., Uppercase, Grayscale) |
| **Pipeline** | An ordered list of processors to apply sequentially |
| **Processor Picker** | A temporary UI overlay for selecting processors |
| **Output Mode** | Where the result goes: Replace, Paste, or New Slot |

### The Two Interaction Patterns

**Pattern 1: Quick Apply (Single Processor)**
```
Ctrl+T → Picker opens → Press number/key → Processor applies immediately
```
Fast, single keystroke after trigger. Result replaces slot content by default.

**Pattern 2: Pipeline Build (Multiple Processors)**
```
Ctrl+T → Picker opens → Hold Shift + Press numbers → Pipeline builds
→ Release Shift → Pipeline applies
```
Hold Shift to accumulate processors. Visual feedback shows pipeline building.

---

## Processor Picker UI

When activated, the picker renders as a compact overlay below/beside the active slot:

```
┌──────────────────────────────────────────────────────────────┐
│ SLIPSTREAM                                                ─  │
├──────────────────────────────────────────────────────────────┤
│ T │ I │ Screenshot.png                                    🔓 │
├──────────────────────────────────────────────────────────────┤
│ 1 │ T │ Hello World                      [ACTIVE]         🔓 │
├──────────────────────────────────────────────────────────────┤
│     PROCESSORS ─────────────────────────                     │
│     [1] Uppercase    [4] Reverse     [7] URL Encode          │
│     [2] Lowercase    [5] Trim        [8] Base64              │
│     [3] Strip Format [6] No Newlines [9] JSON Escape         │
│                                                              │
│     Pipeline: (none)                      [Esc] Cancel       │
├──────────────────────────────────────────────────────────────┤
│ 2 │ I │ logo.png                                          🔒 │
└──────────────────────────────────────────────────────────────┘
```

### Picker Behavior

| Action | Keyboard | MIDI | Result |
|--------|----------|------|--------|
| Open picker | `Ctrl+T` | Assigned button | Shows picker for active slot |
| Apply single | `1-9` | Pad tap | Apply processor immediately, close picker |
| Add to pipeline | `Shift+1-9` | Shift+Pad | Add to pipeline, stay open |
| Remove last | `Backspace` | - | Remove last pipeline step |
| Execute pipeline | `Enter` | - | Apply pipeline, close picker |
| Cancel | `Escape` | Same button | Close picker, no changes |
| Cycle output mode | `Tab` | Knob | Toggle Replace/Paste/NewSlot |

### Output Modes

| Mode | Indicator | Behavior |
|------|-----------|----------|
| **Replace** (default) | `→ Slot` | Result overwrites active slot |
| **Paste** | `→ Paste` | Result pastes to active window |
| **New Slot** | `→ New` | Result goes to next available slot |

The output mode indicator shows in the picker. Tab cycles through modes.

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+T` | Open processor picker for active slot |
| `Ctrl+Shift+T` | Open picker in Paste mode (apply & paste immediately) |
| `1-9` (in picker) | Apply processor N |
| `Shift+1-9` (in picker) | Add processor N to pipeline |
| `Enter` (in picker) | Execute pipeline |
| `Backspace` (in picker) | Remove last from pipeline |
| `Tab` (in picker) | Cycle output mode |
| `Escape` (in picker) | Cancel and close |

---

## MIDI Integration

Extends `MidiControlScheme` minimally:

```csharp
public class MidiControlScheme
{
    // Existing
    public Dictionary<string, MidiTrigger> Mappings { get; init; }
    public MidiTrigger? CopyModifier { get; init; }

    // New
    public MidiTrigger? ProcessorPickerToggle { get; init; }  // Opens/closes picker
}
```

### MIDI Workflow

1. **Open Picker**: Press assigned button → Picker shows
2. **Select Processor**: Press pad 1-9 → Applies immediately (or adds to pipeline if building)
3. **Close Picker**: Press same button again → Closes

Direct processor mappings already exist via `ProcessActive{Name}` commands. The picker adds discoverability but doesn't replace direct bindings.

---

## Pipelines

### Building Pipelines

```
User holds Shift → enters "pipeline build mode"
While Shift held:
  - Number keys add processors to pipeline
  - HUD shows: "Pipeline: Trim → Upper → StripFormat"
  - Each addition shows visual feedback
When Shift released:
  - Pipeline executes
  - Picker closes
```

### Saved Pipelines (Future)

Not in MVP. When added:
- Store in `AppSettings.SavedPipelines`
- Assign hotkeys/MIDI triggers
- Show in picker as "quick presets" section

---

## Architecture

### Extend Existing Components (Don't Create New Ones)

The codebase already has the right abstractions. We extend them:

| Component | Extends | Change |
|-----------|---------|--------|
| `ProcessorRegistry` | Add `ExecutePipeline()` | Execute ordered list of processors |
| `SlotManager` | Add picker state | Track if picker is open, for which slot |
| `HudRenderer` | Add `RenderProcessorPicker()` | Draw picker overlay |
| `CommandRegistry` | Add picker commands | `OpenProcessorPicker`, `ApplyProcessorN` |

### New Files

| File | Purpose |
|------|---------|
| `Processing/ProcessorPipeline.cs` | Pipeline execution and state |
| `Commands/ProcessorPickerCommands.cs` | Commands for picker interaction |
| `UI/ProcessorPickerRenderer.cs` | SkiaSharp rendering for picker |

### Data Models

```csharp
// Processing/ProcessorPipeline.cs
public class ProcessorPipeline
{
    public List<string> ProcessorNames { get; } = new();

    public void Add(string processorName) => ProcessorNames.Add(processorName);
    public void RemoveLast() { if (ProcessorNames.Count > 0) ProcessorNames.RemoveAt(ProcessorNames.Count - 1); }
    public void Clear() => ProcessorNames.Clear();

    public IClipboardContent? Execute(ProcessorRegistry registry, IClipboardContent content)
    {
        var current = content;
        foreach (var name in ProcessorNames)
        {
            current = registry.Process(name, current);
            if (current == null) return null;
        }
        return current;
    }
}

// Output mode enum
public enum ProcessorOutputMode
{
    Replace,   // Overwrite source slot
    Paste,     // Paste to active window
    NewSlot    // Store in next available slot
}
```

### State Management

Add to `SlotManager` or create `ProcessorPickerState`:

```csharp
public class ProcessorPickerState
{
    public bool IsOpen { get; set; }
    public int TargetSlotIndex { get; set; } = -1;
    public ProcessorPipeline Pipeline { get; } = new();
    public ProcessorOutputMode OutputMode { get; set; } = ProcessorOutputMode.Replace;
    public bool IsBuildingPipeline { get; set; }  // Shift held

    public event EventHandler? StateChanged;

    public void Open(int slotIndex)
    {
        IsOpen = true;
        TargetSlotIndex = slotIndex;
        Pipeline.Clear();
        IsBuildingPipeline = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Close()
    {
        IsOpen = false;
        TargetSlotIndex = -1;
        Pipeline.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
```

### Command Flow

```
User presses Ctrl+T
    ↓
HotkeyManager fires "OpenProcessorPicker"
    ↓
CommandRegistry.Execute("OpenProcessorPicker")
    ↓
OpenProcessorPickerCommand.Execute()
    - Gets active slot index from SlotManager
    - Gets available processors for slot content type
    - Opens ProcessorPickerState
    ↓
ProcessorPickerState.StateChanged event fires
    ↓
HudWindow.Refresh() → HudRenderer renders picker overlay
    ↓
User presses "2" (in picker)
    ↓
CommandRegistry.Execute("ApplyProcessor2")
    ↓
ApplyProcessorCommand.Execute()
    - If building pipeline: Add to pipeline, stay open
    - If not: Apply immediately, close picker
    ↓
ProcessorPickerState.StateChanged / SlotManager.SlotChanged events
    ↓
HudWindow.Refresh()
```

---

## Implementation Phases

### Phase 1: Pipeline Execution (Foundation)

**Goal**: Enable executing multiple processors in sequence via code.

**Files**:
- `Processing/ProcessorPipeline.cs` (new)

**Tasks**:
1. Create `ProcessorPipeline` class with `Add()`, `RemoveLast()`, `Execute()`
2. Add `ExecutePipeline()` method to `ProcessorRegistry` that delegates to pipeline
3. Create `ProcessorOutputMode` enum
4. Write unit tests for pipeline execution

**Verification**: Can execute a pipeline programmatically from test code.

---

### Phase 2: Picker State & Commands

**Goal**: Open/close picker via keyboard, track state.

**Files**:
- `Processing/ProcessorPickerState.cs` (new)
- `Commands/ProcessorPickerCommands.cs` (new)
- `Commands/CommandRegistry.cs` (modify)
- `App.xaml.cs` (modify - wire up state)

**Tasks**:
1. Create `ProcessorPickerState` with `Open()`, `Close()`, events
2. Create `OpenProcessorPickerCommand` - opens picker for active slot
3. Create `CloseProcessorPickerCommand` - closes picker
4. Create `ApplyProcessorCommand` - applies processor N (1-9)
5. Register commands in `CommandRegistry`
6. Add `Ctrl+T` hotkey binding
7. Wire `ProcessorPickerState` into app composition

**Verification**: Ctrl+T logs "picker opened", Escape logs "picker closed".

---

### Phase 3: Picker UI Rendering

**Goal**: Visual picker overlay in HUD.

**Files**:
- `UI/ProcessorPickerRenderer.cs` (new)
- `UI/HudRenderer.cs` (modify)
- `UI/HudWindow.xaml.cs` (modify)

**Tasks**:
1. Create `ProcessorPickerRenderer` extending `BaseRenderer`
2. Render processor list filtered by content type
3. Show numbered options (1-9)
4. Show current pipeline state
5. Show output mode indicator
6. Integrate into `HudRenderer.Render()` when picker is open
7. Subscribe HudWindow to `ProcessorPickerState.StateChanged`

**Verification**: Ctrl+T shows visual picker, Escape hides it.

---

### Phase 4: Single Processor Apply

**Goal**: Press number to apply processor immediately.

**Files**:
- `Commands/ProcessorPickerCommands.cs` (modify)
- `Processing/ProcessorPickerState.cs` (modify)

**Tasks**:
1. Implement `ApplyProcessorCommand.Execute()`:
   - Get processor at index
   - Execute on target slot content
   - Handle output mode (Replace/Paste/NewSlot)
   - Close picker
2. Register number keys 1-9 as picker-context commands
3. Add `CycleOutputModeCommand` (Tab key)

**Verification**: Open picker, press 2, processor applies, picker closes.

---

### Phase 5: Pipeline Building

**Goal**: Hold Shift to build multi-processor pipeline.

**Files**:
- `Commands/ProcessorPickerCommands.cs` (modify)
- `UI/ProcessorPickerRenderer.cs` (modify)

**Tasks**:
1. Track Shift key state in picker commands
2. When Shift held: add to pipeline instead of immediate apply
3. Update renderer to show pipeline steps
4. Implement `Backspace` to remove last step
5. `Enter` executes pipeline and closes
6. `Shift release` executes pipeline (optional alternative flow)

**Verification**: Shift+1, Shift+2, Enter → applies "Processor1 → Processor2".

---

### Phase 6: MIDI Integration

**Goal**: MIDI support for processor picker.

**Files**:
- `Models/MidiControlScheme.cs` (modify)
- `Services/MidiManager.cs` (modify)

**Tasks**:
1. Add `ProcessorPickerToggle` to `MidiControlScheme`
2. Update `MidiManager` to dispatch picker commands
3. Map pads to `ApplyProcessor1-9` when picker open
4. Update default MIDI presets

**Verification**: MIDI button opens picker, pads apply processors.

---

## Files to Create/Modify Summary

| File | Action | Phase |
|------|--------|-------|
| `Processing/ProcessorPipeline.cs` | NEW | 1 |
| `Processing/ProcessorPickerState.cs` | NEW | 2 |
| `Commands/ProcessorPickerCommands.cs` | NEW | 2, 4, 5 |
| `Commands/CommandRegistry.cs` | MODIFY | 2 |
| `UI/ProcessorPickerRenderer.cs` | NEW | 3 |
| `UI/HudRenderer.cs` | MODIFY | 3 |
| `UI/HudWindow.xaml.cs` | MODIFY | 3 |
| `Models/MidiControlScheme.cs` | MODIFY | 6 |
| `Services/MidiManager.cs` | MODIFY | 6 |
| `App.xaml.cs` | MODIFY | 2 |
| `Models/AppSettings.cs` | MODIFY | (future: saved pipelines) |

---

## Example Processors to Add

Current processors are sufficient for MVP. Future additions:

**Text**:
- `AddNewline` - Add trailing newline
- `WrapInQuotes` - Surround with double quotes
- `UrlEncode` / `UrlDecode`
- `Base64Encode` / `Base64Decode`
- `JsonEscape`

**Image**:
- `Resize` (with options)
- `Crop` (with options)
- `AdjustBrightness` / `AdjustContrast`

---

## What This Design Avoids

1. **No separate "transform modes"** - One picker, one mental model
2. **No modifier overload** - Only Shift for pipeline building, Tab for output mode
3. **No new manager classes** - Extend `ProcessorRegistry`, don't duplicate
4. **No complex preset system** - Direct hotkeys work; presets are future enhancement
5. **No "compose mode" or "transform modifier"** - Simplicity over power-user complexity
6. **No live preview** - Deferred to post-MVP (requires significant rendering work)

---

## Success Criteria

1. User can open picker with Ctrl+T
2. User can apply single processor by pressing number key
3. User can build pipeline with Shift+numbers, execute with Enter
4. User can cancel with Escape
5. Output mode can be cycled with Tab
6. Picker shows only processors valid for content type
7. MIDI button can toggle picker
8. All actions feel instantaneous (<50ms response)

---

## Deferred (Post-MVP)

- Live preview of processor output
- Saved pipeline presets with hotkey/MIDI triggers
- Preset editor UI
- Undo/redo for processor applications
- Processor parameter configuration UI
- Arrow key navigation in picker
- Search/filter in picker
