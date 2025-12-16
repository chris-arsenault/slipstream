Below is a **full, copy-pasteable replacement requirements document**, updated to **explicitly mandate WPF + SkiaSharp** and to give Claude **clear, non-ambiguous implementation guidance** for the HUD/widget.

This is written to minimize framework thrash and prevent Claude from defaulting to stock WPF controls.

---

# Project: Slipstream

**High-performance multi-slot clipboard manager for Windows**

---

## 1. Goals / Non-Goals

### Goals

* Deterministic, slot-based clipboard (not history-first)
* Extremely fast paste with zero UI friction
* Fully keyboard-driven
* Windows-native clipboard ownership and hotkeys
* Minimalist, modern, always-on-top HUD
* UI that feels *invisible* during normal use

### Non-Goals (v1)

* Cross-platform support
* Cloud sync
* Collaboration
* Rich text editing
* AI features

---

## 2. Architecture Overview (Mandatory)

### Process Model

* Single Windows-native background process
* No elevated privileges
* Tray application with optional HUD window

### UI Stack (MANDATORY)

* **WPF** for:

    * Windowing
    * DPI awareness
    * Input
    * Focus / z-order
* **SkiaSharp** for:

    * ALL rendering
    * ALL visuals
    * ALL HUD drawing

> **Do not use standard WPF controls** (Button, Menu, ListView, etc.)
> Treat WPF strictly as a host/compositor.

---

## 3. Core Clipboard Model

### Slots

* Fixed slot count (default **10**, configurable 1–20)
* Indexed slots: Slot 1 … Slot N
* Each slot stores:

    * Clipboard payload (text, image, files, HTML)
    * Timestamp (last updated)
    * Type metadata
    * Optional user label
    * Lock flag

### Slot Behavior

* Copy actions can:

    * Auto-fill next slot (round-robin or MRU)
    * Explicitly target slot via hotkey
* Slots persist across restarts
* Locked slots cannot be overwritten without explicit action

---

## 4. Clipboard Ownership & Capture

### Requirements

* Register as a clipboard listener using Windows APIs
* Capture:

    * Text
    * Rich text / HTML
    * Images
    * File lists
* Preserve original formats when possible

### Rules

* Must not block normal clipboard usage
* Capture can be globally paused
* Graceful handling of large payloads

---

## 5. Hotkey System (Critical)

### Global Hotkeys

* Fully configurable
* System-wide (no app focus required)

### Required Actions

* Copy into slot N
* Paste from slot N
* Cycle active slot forward/backward
* Toggle HUD visibility
* Toggle clipboard capture
* Lock/unlock slot
* Clear slot / clear all

### Collision Handling

* Detect conflicts at bind time
* Warn user but allow override
* Support modifier-heavy combos (e.g. Ctrl+Alt+Shift+Number)

---

## 6. Paste Semantics

### Default Paste

* Immediate paste into focused application
* No UI interaction
* Latency indistinguishable from native Ctrl+V

### Optional Transforms (v1 minimal)

* Paste as plain text
* Trim whitespace
* Normalize newlines
* Modifier-key selection (e.g. Shift+Paste)

---

## 7. HUD / Widget UI (Primary UI)

### HUD Characteristics

* Borderless
* Always-on-top
* No taskbar presence
* No menus
* No chrome
* No standard controls
* Minimalist, modern, low-contrast design

### Visual Style Requirements

* Rounded rectangle container
* Semi-transparent background
* Subtle accent color for active slot
* No gradients unless extremely subtle
* No visible borders unless thin and intentional
* No text-heavy UI

Reference style: *lightweight traffic monitor / system HUD overlays*

---

## 8. SkiaSharp Rendering Requirements (MANDATORY)

### Rendering Model

* Single SkiaSharp surface hosted in WPF
* All drawing via Skia (`SKCanvas`, `SKPaint`, `SKPath`)
* No XAML layout for content
* No WPF shapes for visuals

### Drawing Responsibilities

* Slot rows / columns
* Slot index indicators
* Truncated previews
* Active slot highlight
* Locked slot indicator
* Clipboard type glyphs

### Performance Rules

* Redraw only on state change
* No continuous animation loops
* Target <1ms draw time per frame
* No allocation-heavy draw paths

---

## 9. HUD Behavior

### Visibility

* Disabled by default
* Toggle via hotkey
* Can be:

    * Anchored to screen edge
    * Floating

### Interaction

* Keyboard-first
* Optional mouse click-through mode
* No focus stealing unless explicitly interacted with

---

## 10. Tray Application Behavior

### Tray Icon

* Always present while running
* Indicates:

    * Capture enabled/disabled
    * Error state

### Tray Menu (Minimal)

* Enable / Disable capture
* Show / Hide HUD
* Open settings
* Quit

### Startup

* Optional auto-start on login
* Starts minimized to tray

---

## 11. Configuration & Persistence

### Settings

* Slot count
* Hotkey bindings
* Capture rules
* HUD layout & visibility
* Startup behavior

### Persistence

* Local only
* Human-readable config (JSON)
* Slot data persisted separately

---

## 12. Performance Requirements

* Clipboard capture latency: **<5ms**
* Paste latency: **indistinguishable from native**
* Idle CPU: ~0%
* Memory footprint: predictable, low

---

## 13. Reliability & Safety

* No crashes on malformed clipboard data
* No data loss on restart
* Clean shutdown releases:

    * Clipboard listeners
    * Hotkeys
    * Skia surfaces

---

## 14. Explicit Out-of-Scope (v1)

* Clipboard timeline UI
* Search UI
* AI features
* Sync
* Scripting
* Regex transforms

---

## 15. Success Criteria

* User can copy into specific slots without UI
* User can paste from memory without thinking
* Clipboard state is readable at a glance
* Tool feels invisible during normal work
* UI never looks like a traditional Windows app

---

## 16. Guidance for Claude (IMPORTANT)

When implementing:

* Treat WPF as **window plumbing only**
* Treat SkiaSharp as **the UI**
* Avoid default WPF controls entirely
* Favor explicit state → redraw model
* Optimize for responsiveness, not decoration

If unsure between:

* **More features** vs **less UI** → choose less UI
* **Framework convenience** vs **predictable behavior** → choose predictability

---

If you want next:

* A **SkiaSharp + WPF HUD skeleton**
* Exact **NuGet packages**
* A **draw loop pseudocode**
* A **slot-to-render mapping spec**

Say which and I’ll stay concrete.
