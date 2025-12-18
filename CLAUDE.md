# Slipstream - Development Guide

## Project Overview
High-performance multi-slot clipboard manager for Windows.

## Tech Stack (MANDATORY)
- .NET 8 WPF (windowing/input ONLY)
- SkiaSharp for ALL rendering - HUD, settings, everything
- NO standard WPF controls (Button, TextBox, ListView, Menu, etc.)
- Treat WPF as window plumbing, SkiaSharp as the UI

## Build & Run
```bash
dotnet build src/Slipstream/Slipstream.csproj
dotnet run --project src/Slipstream/Slipstream.csproj
```

## Architecture Rules
1. **WPF = plumbing** - Window creation, DPI awareness, input events, focus/z-order
2. **SkiaSharp = UI** - Every visible pixel rendered via SKCanvas
3. **State → Redraw model** - Explicit state changes trigger redraws, no animation loops
4. **Performance targets**: <5ms clipboard capture, <1ms HUD draw, native-feel paste

## Code Conventions
- File-scoped namespaces
- Primary constructors where appropriate
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Records for immutable data models
- Async/await for I/O operations

## Project Structure
```
src/Slipstream/
├── Models/       # Data models (ClipboardSlot, AppSettings, etc.)
├── Services/     # Business logic (SlotManager, ClipboardMonitor, etc.)
├── UI/           # Windows and renderers
├── Native/       # Win32 P/Invoke declarations
└── App.xaml.cs   # Composition root
```

## Key Patterns

### Service Registration
Services are created in App.xaml.cs and passed via constructor injection.

### Windows API Access
All P/Invoke declarations go in `Native/Win32.cs`. Use:
- `RegisterHotKey`/`UnregisterHotKey` for global hotkeys
- `AddClipboardFormatListener`/`RemoveClipboardFormatListener` for clipboard monitoring
- Window message handling via HwndSource

### Persistence
- Config location: `%AppData%\Slipstream\`
- Files: `config.json` (settings), `slots.json` (slot data)
- Use System.Text.Json with source generators for performance

### SkiaSharp Rendering Pattern
```csharp
// In renderer class
public void Render(SKCanvas canvas, SKSize size, AppState state)
{
    canvas.Clear(SKColors.Transparent);
    // Draw based on state - no side effects
}

// Trigger redraw on state change
skElement.InvalidateVisual();
```

## Performance Guidelines
- Redraw only on state change
- No continuous animation loops
- Avoid allocations in draw paths
- Use SKPaint object pooling
- Debounce persistence writes

## What NOT To Do
- Don't use WPF controls (Button, TextBox, etc.)
- Don't use XAML for layout/styling of HUD content
- Don't create animation storyboards
- Don't use data binding for rendered content
- Don't block UI thread with clipboard operations
- Don't use `AllowUnsafeBlocks` - use traditional `DllImport` instead of `LibraryImport`
- Don't use `GlobalAlloc`, `GlobalLock`, `GlobalUnlock`, or `GlobalFree` from kernel32.dll
- Don't use `OpenClipboard`, `CloseClipboard`, `SetClipboardData`, `GetClipboardData` directly
- **Use WPF `Clipboard` class** for all clipboard read/write operations - it handles formats, lifetime, and thread safety correctly

## Test Integrity (CRITICAL)
**NEVER modify tests to make them pass.** Tests exist to enforce specific behavior contracts:
- If a test fails, the implementation is wrong - fix the implementation, not the test
- Tests document expected behavior (e.g., modifier keys must be restored after paste operations)
- Changing test expectations to match broken code defeats the purpose of testing
- When debugging a regression, the failing test tells you what behavior broke - investigate why

Example: If `SendPasteWithModifierRelease_WhenCtrlShiftHeld_RestoresCtrlShiftAtEnd` fails, it means:
1. The modifier restoration logic is broken
2. Find what change broke it
3. Fix the implementation to restore modifiers correctly
4. Do NOT rename the test or change its assertions

## Default Hotkeys
- Copy to slot N: `Ctrl+Alt+[1-0]`
- Paste from slot N: `Ctrl+Shift+[1-0]`
- Cycle slots: `Ctrl+Alt+Up/Down`
- Toggle HUD: `Ctrl+Alt+H`
- Toggle capture: `Ctrl+Alt+P`

## Clipboard Slot Model
- Fixed slot count (default 10, configurable 1-20)
- Each slot stores: payload, timestamp, type metadata, optional label, lock flag
- Supported types: Text, RichText/HTML, Image, FileList
- Locked slots cannot be overwritten without explicit action

## HUD Visual Requirements
- Borderless, always-on-top, no taskbar presence
- Rounded rectangle container, semi-transparent background
- Subtle accent color for active slot
- Minimal, low-contrast design
- No text-heavy UI
