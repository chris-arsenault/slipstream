using Slipstream.Services;

namespace Slipstream.Tests;

/// <summary>
/// Mock keyboard simulator that records all keyboard events for verification.
/// </summary>
public class MockKeyboardSimulator : IKeyboardSimulator
{
    public List<(string Action, byte? Key)> Events { get; } = new();

    public void KeyDown(byte virtualKey)
    {
        Events.Add(("KeyDown", virtualKey));
    }

    public void KeyUp(byte virtualKey)
    {
        Events.Add(("KeyUp", virtualKey));
    }

    public void Sleep(int milliseconds)
    {
        Events.Add(("Sleep", null));
    }
}

public class KeyboardSequencerTests
{
    private const byte VK_CONTROL = 0x11;
    private const byte VK_SHIFT = 0x10;
    private const byte VK_MENU = 0x12; // Alt
    private const byte VK_C = 0x43;
    private const byte VK_V = 0x56;

    [Fact]
    public void SendCopyWithModifierRelease_ReleasesAllModifiersFirst()
    {
        // Arrange
        var mock = new MockKeyboardSimulator();
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendCopyWithModifierRelease();

        // Assert - First three events should be KeyUp for all modifiers
        var firstThreeEvents = mock.Events.Take(3).ToList();

        Assert.Contains(("KeyUp", VK_CONTROL), firstThreeEvents);
        Assert.Contains(("KeyUp", VK_MENU), firstThreeEvents);
        Assert.Contains(("KeyUp", VK_SHIFT), firstThreeEvents);
    }

    [Fact]
    public void SendCopyWithModifierRelease_SendsCleanCtrlC()
    {
        // Arrange
        var mock = new MockKeyboardSimulator();
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendCopyWithModifierRelease();

        // Assert - Should have Ctrl down, C down, C up, Ctrl up in sequence
        // Note: There are multiple Ctrl events (release at start, press for Ctrl+C, release after C, press at end)
        // We need to find the Ctrl+C sequence specifically
        var events = mock.Events;

        int cDownIndex = events.FindIndex(e => e == ("KeyDown", VK_C));
        int cUpIndex = events.FindIndex(e => e == ("KeyUp", VK_C));

        // Find the Ctrl down that happens right before C (the one for the Ctrl+C combo)
        int ctrlDownForCombo = events.FindLastIndex(cDownIndex, e => e == ("KeyDown", VK_CONTROL));
        // Find the Ctrl up that happens right after C up (the one ending the Ctrl+C combo)
        int ctrlUpAfterC = events.FindIndex(cUpIndex, e => e == ("KeyUp", VK_CONTROL));

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
    public void SendCopyWithModifierRelease_RepressesCtrlAltAtEnd()
    {
        // Arrange
        var mock = new MockKeyboardSimulator();
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendCopyWithModifierRelease();

        // Assert - Last two KeyDown events should be Ctrl and Alt
        var lastTwoKeyDowns = mock.Events
            .Where(e => e.Action == "KeyDown")
            .TakeLast(2)
            .Select(e => e.Key)
            .ToList();

        Assert.Contains(VK_CONTROL, lastTwoKeyDowns);
        Assert.Contains(VK_MENU, lastTwoKeyDowns);
    }

    [Fact]
    public void SendPasteWithModifierRelease_ReleasesAllModifiersFirst()
    {
        // Arrange
        var mock = new MockKeyboardSimulator();
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendPasteWithModifierRelease();

        // Assert - First three events should be KeyUp for all modifiers
        var firstThreeEvents = mock.Events.Take(3).ToList();

        Assert.Contains(("KeyUp", VK_CONTROL), firstThreeEvents);
        Assert.Contains(("KeyUp", VK_SHIFT), firstThreeEvents);
        Assert.Contains(("KeyUp", VK_MENU), firstThreeEvents);
    }

    [Fact]
    public void SendPasteWithModifierRelease_SendsCleanCtrlV()
    {
        // Arrange
        var mock = new MockKeyboardSimulator();
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendPasteWithModifierRelease();

        // Assert - Should have Ctrl down, V down, V up, Ctrl up in sequence
        // Note: There are multiple Ctrl events (release at start, press for Ctrl+V, release after V, press at end)
        // We need to find the Ctrl+V sequence specifically
        var events = mock.Events;

        int vDownIndex = events.FindIndex(e => e == ("KeyDown", VK_V));
        int vUpIndex = events.FindIndex(e => e == ("KeyUp", VK_V));

        // Find the Ctrl down that happens right before V (the one for the Ctrl+V combo)
        int ctrlDownForCombo = events.FindLastIndex(vDownIndex, e => e == ("KeyDown", VK_CONTROL));
        // Find the Ctrl up that happens right after V up (the one ending the Ctrl+V combo)
        int ctrlUpAfterV = events.FindIndex(vUpIndex, e => e == ("KeyUp", VK_CONTROL));

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
    public void SendPasteWithModifierRelease_RepressesCtrlShiftAtEnd()
    {
        // Arrange
        var mock = new MockKeyboardSimulator();
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendPasteWithModifierRelease();

        // Assert - Last two KeyDown events should be Ctrl and Shift
        var lastTwoKeyDowns = mock.Events
            .Where(e => e.Action == "KeyDown")
            .TakeLast(2)
            .Select(e => e.Key)
            .ToList();

        Assert.Contains(VK_CONTROL, lastTwoKeyDowns);
        Assert.Contains(VK_SHIFT, lastTwoKeyDowns);
    }

    [Fact]
    public void SendCopyWithModifierRelease_IncludesDelaysBetweenPhases()
    {
        // Arrange
        var mock = new MockKeyboardSimulator();
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendCopyWithModifierRelease();

        // Assert - Should have Sleep calls between phases
        var sleepCount = mock.Events.Count(e => e.Action == "Sleep");
        Assert.True(sleepCount >= 2, $"Expected at least 2 sleep calls, got {sleepCount}");
    }

    [Fact]
    public void SendPasteWithModifierRelease_IncludesDelaysBetweenPhases()
    {
        // Arrange
        var mock = new MockKeyboardSimulator();
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendPasteWithModifierRelease();

        // Assert - Should have Sleep calls between phases
        var sleepCount = mock.Events.Count(e => e.Action == "Sleep");
        Assert.True(sleepCount >= 2, $"Expected at least 2 sleep calls, got {sleepCount}");
    }

    [Fact]
    public void SendCopyWithModifierRelease_ModifierReleaseHappensBeforeCtrlC()
    {
        // Arrange
        var mock = new MockKeyboardSimulator();
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendCopyWithModifierRelease();

        // Assert - All modifier KeyUp events should happen before the Ctrl+C KeyDown
        var events = mock.Events;
        int altUpIndex = events.FindIndex(e => e == ("KeyUp", VK_MENU));
        int ctrlCDownIndex = events.FindIndex(e => e == ("KeyDown", VK_C));

        Assert.True(altUpIndex < ctrlCDownIndex,
            "Alt should be released before sending C keystroke");
    }

    [Fact]
    public void SendPasteWithModifierRelease_ModifierReleaseHappensBeforeCtrlV()
    {
        // Arrange
        var mock = new MockKeyboardSimulator();
        var sequencer = new KeyboardSequencer(mock);

        // Act
        sequencer.SendPasteWithModifierRelease();

        // Assert - All modifier KeyUp events should happen before the Ctrl+V KeyDown
        var events = mock.Events;
        int shiftUpIndex = events.FindIndex(e => e == ("KeyUp", VK_SHIFT));
        int ctrlVDownIndex = events.FindIndex(e => e == ("KeyDown", VK_V));

        Assert.True(shiftUpIndex < ctrlVDownIndex,
            "Shift should be released before sending V keystroke");
    }
}
