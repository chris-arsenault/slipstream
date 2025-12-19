using Slipstream.Services;
using Slipstream.Services.Keyboard;

namespace Slipstream.Tests;

/// <summary>
/// Mock input injector that records all keyboard events for verification.
/// </summary>
public class MockInputInjector : IInputInjector
{
    public List<KeyEvent> Events { get; } = new();

    /// <summary>
    /// Set of keys that are "physically held" for testing purposes.
    /// </summary>
    public HashSet<byte> PhysicallyHeldKeys { get; } = new();

    public void SendBatch(ReadOnlySpan<KeyEvent> events)
    {
        foreach (var evt in events)
        {
            Events.Add(evt);
        }
    }

    public bool IsKeyPhysicallyDown(byte virtualKey)
    {
        return PhysicallyHeldKeys.Contains(virtualKey);
    }

    public bool IsKeyLogicallyDown(byte virtualKey)
    {
        // For testing, treat logical and physical the same
        return PhysicallyHeldKeys.Contains(virtualKey);
    }
}

public class KeyboardSequencerTests
{
    // Use VirtualKeys constants from the production code
    private const byte VK_CONTROL = VirtualKeys.Control;
    private const byte VK_SHIFT = VirtualKeys.Shift;
    private const byte VK_MENU = VirtualKeys.Alt;
    private const byte VK_C = VirtualKeys.C;
    private const byte VK_V = VirtualKeys.V;

    // Left variants for physical key simulation
    private const byte VK_LCONTROL = VirtualKeys.LeftControl;
    private const byte VK_LSHIFT = VirtualKeys.LeftShift;
    private const byte VK_LMENU = VirtualKeys.LeftAlt;

    [Fact]
    public void SendCopyWithModifierRelease_ReleasesAllModifiersFirst()
    {
        // Arrange
        var mock = new MockInputInjector();
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendCopyWithModifierRelease();

        // Assert - First events should be KeyUp for all modifiers (9 total: generic + left + right)
        var keyUpEvents = mock.Events.TakeWhile(e => e.IsKeyUp).ToList();
        Assert.True(keyUpEvents.Count >= 3, $"Expected at least 3 KeyUp events first, got {keyUpEvents.Count}");
    }

    [Fact]
    public void SendCopyWithModifierRelease_SendsCleanCtrlC()
    {
        // Arrange
        var mock = new MockInputInjector();
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendCopyWithModifierRelease();

        // Assert - Should have Ctrl down, C down, C up, Ctrl up in sequence
        var events = mock.Events;

        int cDownIndex = events.FindIndex(e => e == KeyEvent.Down(VK_C));
        int cUpIndex = events.FindIndex(e => e == KeyEvent.Up(VK_C));

        // Find the Ctrl down that happens right before C (the one for the Ctrl+C combo)
        int ctrlDownForCombo = events.FindLastIndex(cDownIndex, e => e == KeyEvent.Down(VK_CONTROL));
        // Find the Ctrl up that happens right after C up (the one ending the Ctrl+C combo)
        int ctrlUpAfterC = events.FindIndex(cUpIndex, e => e == KeyEvent.Up(VK_CONTROL));

        // All should be found
        Assert.True(ctrlDownForCombo >= 0, "Ctrl down for combo not found");
        Assert.True(cDownIndex >= 0, "C down not found");
        Assert.True(cUpIndex >= 0, "C up not found");
        Assert.True(ctrlUpAfterC >= 0, "Ctrl up after C not found");

        // Should be in correct order
        Assert.True(ctrlDownForCombo < cDownIndex, "Ctrl should be pressed before C");
        Assert.True(cDownIndex < cUpIndex, "C down should come before C up");
        Assert.True(cUpIndex < ctrlUpAfterC, "C up should come before Ctrl up");
    }

    [Fact]
    public void SendCopyWithModifierRelease_WhenCtrlAltHeld_RestoresCtrlAltAtEnd()
    {
        // Arrange - Simulate user holding Ctrl+Alt for copy hotkey
        var mock = new MockInputInjector();
        mock.PhysicallyHeldKeys.Add(VK_LCONTROL);
        mock.PhysicallyHeldKeys.Add(VK_LMENU);
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendCopyWithModifierRelease();

        // Assert - Last KeyDown events should be Ctrl and Alt (restored from snapshot)
        var keyDownEvents = mock.Events
            .Where(e => !e.IsKeyUp)
            .ToList();

        var lastTwoKeyDowns = keyDownEvents.TakeLast(2).Select(e => e.VirtualKey).ToList();

        Assert.Contains(VK_CONTROL, lastTwoKeyDowns);
        Assert.Contains(VK_MENU, lastTwoKeyDowns);
    }

    [Fact]
    public void SendCopyWithModifierRelease_WhenNoModifiersHeld_DoesNotRestoreAny()
    {
        // Arrange - No physical keys held (e.g., MIDI input)
        var mock = new MockInputInjector();
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendCopyWithModifierRelease();

        // Assert - After Ctrl+C completes, no modifiers should be repressed
        var events = mock.Events;
        int lastCtrlUpIndex = events.FindLastIndex(e => e == KeyEvent.Up(VK_CONTROL));

        var keyDownsAfterCtrlC = events
            .Skip(lastCtrlUpIndex + 1)
            .Where(e => !e.IsKeyUp)
            .ToList();

        Assert.Empty(keyDownsAfterCtrlC);
    }

    [Fact]
    public void SendPasteWithModifierRelease_ReleasesAllModifiersFirst()
    {
        // Arrange
        var mock = new MockInputInjector();
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendPasteWithModifierRelease();

        // Assert - First events should be KeyUp for all modifiers
        var keyUpEvents = mock.Events.TakeWhile(e => e.IsKeyUp).ToList();
        Assert.True(keyUpEvents.Count >= 3, $"Expected at least 3 KeyUp events first, got {keyUpEvents.Count}");
    }

    [Fact]
    public void SendPasteWithModifierRelease_SendsCleanCtrlV()
    {
        // Arrange
        var mock = new MockInputInjector();
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendPasteWithModifierRelease();

        // Assert - Should have Ctrl down, V down, V up, Ctrl up in sequence
        var events = mock.Events;

        int vDownIndex = events.FindIndex(e => e == KeyEvent.Down(VK_V));
        int vUpIndex = events.FindIndex(e => e == KeyEvent.Up(VK_V));

        // Find the Ctrl down that happens right before V (the one for the Ctrl+V combo)
        int ctrlDownForCombo = events.FindLastIndex(vDownIndex, e => e == KeyEvent.Down(VK_CONTROL));
        // Find the Ctrl up that happens right after V up (the one ending the Ctrl+V combo)
        int ctrlUpAfterV = events.FindIndex(vUpIndex, e => e == KeyEvent.Up(VK_CONTROL));

        // All should be found
        Assert.True(ctrlDownForCombo >= 0, "Ctrl down for combo not found");
        Assert.True(vDownIndex >= 0, "V down not found");
        Assert.True(vUpIndex >= 0, "V up not found");
        Assert.True(ctrlUpAfterV >= 0, "Ctrl up after V not found");

        // Should be in correct order
        Assert.True(ctrlDownForCombo < vDownIndex, "Ctrl should be pressed before V");
        Assert.True(vDownIndex < vUpIndex, "V down should come before V up");
        Assert.True(vUpIndex < ctrlUpAfterV, "V up should come before Ctrl up");
    }

    [Fact]
    public void SendPasteWithModifierRelease_WhenCtrlShiftHeld_RestoresCtrlShiftAtEnd()
    {
        // Arrange - Simulate user holding Ctrl+Shift for paste hotkey
        var mock = new MockInputInjector();
        mock.PhysicallyHeldKeys.Add(VK_LCONTROL);
        mock.PhysicallyHeldKeys.Add(VK_LSHIFT);
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendPasteWithModifierRelease();

        // Assert - Last KeyDown events should include Ctrl and Shift being repressed
        var keyDownEvents = mock.Events
            .Where(e => !e.IsKeyUp)
            .ToList();

        var lastTwoKeyDowns = keyDownEvents.TakeLast(2).Select(e => e.VirtualKey).ToList();

        Assert.Contains(VK_CONTROL, lastTwoKeyDowns);
        Assert.Contains(VK_SHIFT, lastTwoKeyDowns);
    }

    [Fact]
    public void SendPasteWithModifierRelease_WhenOnlyCtrlHeld_OnlyRestoresCtrl()
    {
        // Arrange - Simulates numpad paste (Ctrl+Numpad#, no Shift)
        var mock = new MockInputInjector();
        mock.PhysicallyHeldKeys.Add(VK_LCONTROL);
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendPasteWithModifierRelease();

        // Assert - Should have Ctrl restored but NOT Shift
        var events = mock.Events;
        int lastCtrlUpIndex = events.FindLastIndex(e => e == KeyEvent.Up(VK_CONTROL));

        var keyDownsAfterPaste = events
            .Skip(lastCtrlUpIndex + 1)
            .Where(e => !e.IsKeyUp)
            .Select(e => e.VirtualKey)
            .ToList();

        Assert.Contains(VK_CONTROL, keyDownsAfterPaste);
        Assert.DoesNotContain(VK_SHIFT, keyDownsAfterPaste);
    }

    [Fact]
    public void SendPasteWithModifierRelease_WhenNoModifiersHeld_DoesNotRestoreAny()
    {
        // Arrange - No physical keys held (e.g., MIDI input)
        var mock = new MockInputInjector();
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendPasteWithModifierRelease();

        // Assert - After Ctrl+V completes, no modifiers should be repressed
        var events = mock.Events;
        int lastCtrlUpIndex = events.FindLastIndex(e => e == KeyEvent.Up(VK_CONTROL));

        var keyDownsAfterPaste = events
            .Skip(lastCtrlUpIndex + 1)
            .Where(e => !e.IsKeyUp)
            .ToList();

        Assert.Empty(keyDownsAfterPaste);
    }

    [Fact]
    public void SendCopyWithModifierRelease_SendsEventsAtomically()
    {
        // Arrange
        var mock = new MockInputInjector();
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendCopyWithModifierRelease();

        // Assert - Verify the sequence is correct: release modifiers, Ctrl+C, restore modifiers
        var events = mock.Events;

        // Should have modifier releases first
        var firstKeyUp = events.FindIndex(e => e.IsKeyUp);
        Assert.True(firstKeyUp >= 0, "Should have KeyUp events for releasing modifiers");

        // Then Ctrl+C
        int cDownIndex = events.FindIndex(e => e == KeyEvent.Down(VK_C));
        Assert.True(cDownIndex > firstKeyUp, "Ctrl+C should come after modifier release");
    }

    [Fact]
    public void SendPasteWithModifierRelease_SendsEventsAtomically()
    {
        // Arrange
        var mock = new MockInputInjector();
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendPasteWithModifierRelease();

        // Assert - Verify the sequence is correct: release modifiers, Ctrl+V, restore modifiers
        var events = mock.Events;

        // Should have modifier releases first
        var firstKeyUp = events.FindIndex(e => e.IsKeyUp);
        Assert.True(firstKeyUp >= 0, "Should have KeyUp events for releasing modifiers");

        // Then Ctrl+V
        int vDownIndex = events.FindIndex(e => e == KeyEvent.Down(VK_V));
        Assert.True(vDownIndex > firstKeyUp, "Ctrl+V should come after modifier release");
    }

    [Fact]
    public void SendCopyWithModifierRelease_ModifierReleaseHappensBeforeCtrlC()
    {
        // Arrange
        var mock = new MockInputInjector();
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendCopyWithModifierRelease();

        // Assert - All modifier KeyUp events should happen before the Ctrl+C KeyDown
        var events = mock.Events;
        int altUpIndex = events.FindIndex(e => e == KeyEvent.Up(VK_MENU));
        int ctrlCDownIndex = events.FindIndex(e => e == KeyEvent.Down(VK_C));

        Assert.True(altUpIndex < ctrlCDownIndex,
            "Alt should be released before sending C keystroke");
    }

    [Fact]
    public void SendPasteWithModifierRelease_ModifierReleaseHappensBeforeCtrlV()
    {
        // Arrange
        var mock = new MockInputInjector();
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendPasteWithModifierRelease();

        // Assert - All modifier KeyUp events should happen before the Ctrl+V KeyDown
        var events = mock.Events;
        int shiftUpIndex = events.FindIndex(e => e == KeyEvent.Up(VK_SHIFT));
        int ctrlVDownIndex = events.FindIndex(e => e == KeyEvent.Down(VK_V));

        Assert.True(shiftUpIndex < ctrlVDownIndex,
            "Shift should be released before sending V keystroke");
    }

    [Fact]
    public void ChainedPaste_WithCtrlShiftHeld_ModifiersRestoredAfterEachPaste()
    {
        // Arrange - Simulate chained paste (e.g., Ctrl+Shift+1 then Ctrl+Shift+2)
        var mock = new MockInputInjector();
        mock.PhysicallyHeldKeys.Add(VK_LCONTROL);
        mock.PhysicallyHeldKeys.Add(VK_LSHIFT);
        var sequencer = new KeyboardSequencer(mock);

        // Act - Simulate two consecutive paste operations with Ctrl+Shift held
        sequencer.SendPasteWithModifierRelease();

        // Clear events for second paste
        var firstPasteEvents = mock.Events.ToList();
        mock.Events.Clear();

        sequencer.SendPasteWithModifierRelease();
        var secondPasteEvents = mock.Events.ToList();

        // Assert - First paste should end with Ctrl and Shift being repressed
        var firstPasteLastKeyDowns = firstPasteEvents
            .Where(e => !e.IsKeyUp)
            .TakeLast(2)
            .Select(e => e.VirtualKey)
            .ToList();
        Assert.Contains(VK_CONTROL, firstPasteLastKeyDowns);
        Assert.Contains(VK_SHIFT, firstPasteLastKeyDowns);

        // Assert - Second paste should also end with Ctrl and Shift being repressed
        var secondPasteLastKeyDowns = secondPasteEvents
            .Where(e => !e.IsKeyUp)
            .TakeLast(2)
            .Select(e => e.VirtualKey)
            .ToList();
        Assert.Contains(VK_CONTROL, secondPasteLastKeyDowns);
        Assert.Contains(VK_SHIFT, secondPasteLastKeyDowns);
    }

    [Fact]
    public void ChainedPaste_FromMidi_NoModifiersRestored()
    {
        // Arrange - MIDI input has no physical modifiers held
        var mock = new MockInputInjector();
        // No keys in PhysicallyHeldKeys - simulates MIDI
        var sequencer = new KeyboardSequencer(mock);

        // Act - Simulate two consecutive MIDI-triggered paste operations
        sequencer.SendPasteWithModifierRelease();
        var firstPasteEvents = mock.Events.ToList();
        mock.Events.Clear();

        sequencer.SendPasteWithModifierRelease();
        var secondPasteEvents = mock.Events.ToList();

        // Assert - Neither paste should have any modifiers repressed at the end
        // (no phantom stuck modifiers from MIDI)
        var firstLastCtrlUp = firstPasteEvents.FindLastIndex(e => e == KeyEvent.Up(VK_CONTROL));
        var firstKeyDownsAfter = firstPasteEvents
            .Skip(firstLastCtrlUp + 1)
            .Where(e => !e.IsKeyUp)
            .ToList();
        Assert.Empty(firstKeyDownsAfter);

        var secondLastCtrlUp = secondPasteEvents.FindLastIndex(e => e == KeyEvent.Up(VK_CONTROL));
        var secondKeyDownsAfter = secondPasteEvents
            .Skip(secondLastCtrlUp + 1)
            .Where(e => !e.IsKeyUp)
            .ToList();
        Assert.Empty(secondKeyDownsAfter);
    }

    [Fact]
    public void ChainedCopy_WithCtrlAltHeld_ModifiersRestoredAfterEachCopy()
    {
        // Arrange - Simulate chained copy (e.g., Ctrl+Alt+1 then Ctrl+Alt+2)
        var mock = new MockInputInjector();
        mock.PhysicallyHeldKeys.Add(VK_LCONTROL);
        mock.PhysicallyHeldKeys.Add(VK_LMENU);
        var sequencer = new KeyboardSequencer(mock);

        // Act - Simulate two consecutive copy operations
        sequencer.SendCopyWithModifierRelease();

        // Clear events for second copy
        var firstCopyEvents = mock.Events.ToList();
        mock.Events.Clear();

        sequencer.SendCopyWithModifierRelease();
        var secondCopyEvents = mock.Events.ToList();

        // Assert - First copy should end with Ctrl and Alt being repressed
        var firstCopyLastKeyDowns = firstCopyEvents
            .Where(e => !e.IsKeyUp)
            .TakeLast(2)
            .Select(e => e.VirtualKey)
            .ToList();
        Assert.Contains(VK_CONTROL, firstCopyLastKeyDowns);
        Assert.Contains(VK_MENU, firstCopyLastKeyDowns);

        // Assert - Second copy should also end with Ctrl and Alt being repressed
        var secondCopyLastKeyDowns = secondCopyEvents
            .Where(e => !e.IsKeyUp)
            .TakeLast(2)
            .Select(e => e.VirtualKey)
            .ToList();
        Assert.Contains(VK_CONTROL, secondCopyLastKeyDowns);
        Assert.Contains(VK_MENU, secondCopyLastKeyDowns);
    }

    [Fact]
    public void ChainedCopy_FromMidi_NoModifiersRestored()
    {
        // Arrange - MIDI input has no physical modifiers held
        var mock = new MockInputInjector();
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendCopyWithModifierRelease();

        // Assert - No modifiers repressed at end
        var events = mock.Events;
        int lastCtrlUpIndex = events.FindLastIndex(e => e == KeyEvent.Up(VK_CONTROL));

        var keyDownsAfterCopy = events
            .Skip(lastCtrlUpIndex + 1)
            .Where(e => !e.IsKeyUp)
            .ToList();

        Assert.Empty(keyDownsAfterCopy);
    }
}
