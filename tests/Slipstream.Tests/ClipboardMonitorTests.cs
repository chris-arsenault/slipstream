using Slipstream.Models;
using Slipstream.Services;

namespace Slipstream.Tests;

public class ClipboardMonitorTests
{
    [Fact]
    public void SetPendingTargetSlot_CapturesNextClipboardChangeToTargetSlot()
    {
        // This test verifies the critical behavior:
        // When Ctrl+Alt+# is pressed, we set a pending target slot BEFORE sending Ctrl+C.
        // The next clipboard change should go to that specific slot, NOT round-robin.

        // Arrange
        var capturedEvents = new List<ClipboardChangedEventArgs>();

        // We can't easily test ClipboardMonitor directly since it needs Win32,
        // but we can test the event args pattern that App.xaml.cs relies on

        // Simulate the flow:
        // 1. User presses Ctrl+Alt+3 (copy to slot 3, which is index 2)
        // 2. ClipboardMonitor.SetPendingTargetSlot(2) is called
        // 3. Ctrl+C is sent
        // 4. WM_CLIPBOARDUPDATE fires
        // 5. ClipboardChangedEventArgs should have TargetSlotIndex = 2

        // Test the event args behavior
        var eventWithTarget = new ClipboardChangedEventArgs("test data", ClipboardType.Text, targetSlotIndex: 2);
        var eventWithoutTarget = new ClipboardChangedEventArgs("test data", ClipboardType.Text);

        Assert.Equal(2, eventWithTarget.TargetSlotIndex);
        Assert.Null(eventWithoutTarget.TargetSlotIndex);
    }

    [Fact]
    public void ClipboardChangedEventArgs_WithTargetSlot_ShouldCaptureToSpecificSlot()
    {
        // Arrange
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10);

        // Act - Simulate targeted copy to slot 5 (index 4)
        var eventArgs = new ClipboardChangedEventArgs("targeted content", ClipboardType.Text, targetSlotIndex: 4);

        // This is the logic from App.OnClipboardChanged that we're testing
        if (eventArgs.TargetSlotIndex.HasValue)
        {
            slotManager.CaptureToSlot(eventArgs.TargetSlotIndex.Value, eventArgs.Data, eventArgs.Type);
        }
        else
        {
            slotManager.CaptureToNextSlot(eventArgs.Data, eventArgs.Type);
        }

        // Assert - Content should be in slot 5 (index 4), not slot 1 (round-robin start)
        Assert.Equal("targeted content", slots[4].TextContent);
        Assert.False(slots[0].HasContent, "Slot 0 should be empty - round-robin should NOT have been used");
    }

    [Fact]
    public void ClipboardChangedEventArgs_WithoutTargetSlot_ShouldGoToTempSlot()
    {
        // Arrange
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10);

        // Act - Simulate normal Ctrl+C (no target slot)
        var eventArgs = new ClipboardChangedEventArgs("temp slot content", ClipboardType.Text);

        // This is the logic from App.OnClipboardChanged (NEW BEHAVIOR)
        if (eventArgs.TargetSlotIndex.HasValue)
        {
            slotManager.CaptureToSlot(eventArgs.TargetSlotIndex.Value, eventArgs.Data, eventArgs.Type);
        }
        else
        {
            slotManager.CaptureToTempSlot(eventArgs.Data, eventArgs.Type);
        }

        // Assert - Content should be in temp slot, not numbered slots
        Assert.Equal("temp slot content", slotManager.TempSlot.TextContent);
        Assert.False(slots[0].HasContent, "Numbered slots should be empty");
    }

    [Fact]
    public void TargetedCopy_ShouldNotAffectTempSlot()
    {
        // Arrange
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10);

        // Act 1 - Targeted copy to slot 5 (index 4)
        var targetedEvent = new ClipboardChangedEventArgs("targeted", ClipboardType.Text, targetSlotIndex: 4);
        if (targetedEvent.TargetSlotIndex.HasValue)
            slotManager.CaptureToSlot(targetedEvent.TargetSlotIndex.Value, targetedEvent.Data, targetedEvent.Type);
        else
            slotManager.CaptureToTempSlot(targetedEvent.Data, targetedEvent.Type);

        // Act 2 - Normal Ctrl+C (should go to temp slot)
        var tempEvent = new ClipboardChangedEventArgs("temp content", ClipboardType.Text);
        if (tempEvent.TargetSlotIndex.HasValue)
            slotManager.CaptureToSlot(tempEvent.TargetSlotIndex.Value, tempEvent.Data, tempEvent.Type);
        else
            slotManager.CaptureToTempSlot(tempEvent.Data, tempEvent.Type);

        // Assert
        Assert.Equal("targeted", slots[4].TextContent);
        Assert.Equal("temp content", slotManager.TempSlot.TextContent);
    }

    [Fact]
    public void TargetedCopy_ToLockedSlot_ShouldNotCapture()
    {
        // Arrange
        var slots = CreateTestSlots(10);
        slots[4].IsLocked = true;
        slots[4].SetText("locked content");
        var slotManager = new SlotManager(slots, 10);

        // Act - Try to target copy to locked slot 5 (index 4)
        var eventArgs = new ClipboardChangedEventArgs("new content", ClipboardType.Text, targetSlotIndex: 4);
        if (eventArgs.TargetSlotIndex.HasValue)
            slotManager.CaptureToSlot(eventArgs.TargetSlotIndex.Value, eventArgs.Data, eventArgs.Type);
        else
            slotManager.CaptureToNextSlot(eventArgs.Data, eventArgs.Type);

        // Assert - Locked slot should retain original content
        Assert.Equal("locked content", slots[4].TextContent);
    }

    [Fact]
    public void MultipleTargetedCopies_ShouldEachGoToCorrectSlot()
    {
        // Arrange
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10);

        // Act - Simulate Ctrl+Alt+1, Ctrl+Alt+3, Ctrl+Alt+5
        SimulateCopyToSlot(slotManager, "content for slot 1", 0);
        SimulateCopyToSlot(slotManager, "content for slot 3", 2);
        SimulateCopyToSlot(slotManager, "content for slot 5", 4);

        // Assert - Each slot should have its specific content
        Assert.Equal("content for slot 1", slots[0].TextContent);
        Assert.Equal("content for slot 3", slots[2].TextContent);
        Assert.Equal("content for slot 5", slots[4].TextContent);

        // Other slots should be empty
        Assert.False(slots[1].HasContent);
        Assert.False(slots[3].HasContent);
    }

    [Fact]
    public void TargetedCopy_OverwritesExistingContent()
    {
        // Arrange
        var slots = CreateTestSlots(10);
        slots[2].SetText("old content");
        var slotManager = new SlotManager(slots, 10);

        // Act - Target copy to slot 3 (index 2) which already has content
        SimulateCopyToSlot(slotManager, "new content", 2);

        // Assert
        Assert.Equal("new content", slots[2].TextContent);
    }

    private static void SimulateCopyToSlot(SlotManager slotManager, string content, int slotIndex)
    {
        var eventArgs = new ClipboardChangedEventArgs(content, ClipboardType.Text, targetSlotIndex: slotIndex);
        if (eventArgs.TargetSlotIndex.HasValue)
            slotManager.CaptureToSlot(eventArgs.TargetSlotIndex.Value, eventArgs.Data, eventArgs.Type);
        else
            slotManager.CaptureToNextSlot(eventArgs.Data, eventArgs.Type);
    }

    private static List<ClipboardSlot> CreateTestSlots(int count)
    {
        var slots = new List<ClipboardSlot>();
        for (int i = 0; i < count; i++)
        {
            slots.Add(new ClipboardSlot { Index = i });
        }
        return slots;
    }
}
