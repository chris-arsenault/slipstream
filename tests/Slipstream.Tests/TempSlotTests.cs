using Slipstream.Models;
using Slipstream.Services;

namespace Slipstream.Tests;

public class TempSlotTests
{
    [Fact]
    public void TempSlot_ExistsByDefault()
    {
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10);

        Assert.NotNull(slotManager.TempSlot);
        Assert.Equal(SlotManager.TempSlotIndex, slotManager.TempSlot.Index);
    }

    [Fact]
    public void CaptureToTempSlot_StoresContent()
    {
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10);

        slotManager.CaptureToTempSlot("test content", ClipboardType.Text);

        Assert.True(slotManager.TempSlot.HasContent);
        Assert.Equal("test content", slotManager.TempSlot.TextContent);
    }

    [Fact]
    public void CaptureToTempSlot_OverwritesPreviousContent()
    {
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10);

        slotManager.CaptureToTempSlot("first", ClipboardType.Text);
        slotManager.CaptureToTempSlot("second", ClipboardType.Text);

        Assert.Equal("second", slotManager.TempSlot.TextContent);
    }

    [Fact]
    public void CaptureToTempSlot_DoesNotAffectNumberedSlots()
    {
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10);

        slotManager.CaptureToTempSlot("temp content", ClipboardType.Text);

        // All numbered slots should still be empty
        foreach (var slot in slots)
        {
            Assert.False(slot.HasContent, $"Slot {slot.Index} should be empty");
        }
    }

    [Fact]
    public void PromoteTempSlot_CopiesToNextAvailableSlot()
    {
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10);

        slotManager.CaptureToTempSlot("promote me", ClipboardType.Text);
        int promotedIndex = slotManager.PromoteTempSlot();

        Assert.Equal(0, promotedIndex); // First slot (round-robin starts at 0)
        Assert.Equal("promote me", slots[0].TextContent);
    }

    [Fact]
    public void PromoteTempSlot_AdvancesRoundRobinIndex()
    {
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10);

        slotManager.CaptureToTempSlot("first", ClipboardType.Text);
        slotManager.PromoteTempSlot(); // Goes to slot 0

        slotManager.CaptureToTempSlot("second", ClipboardType.Text);
        int secondIndex = slotManager.PromoteTempSlot(); // Should go to slot 1

        Assert.Equal(1, secondIndex);
        Assert.Equal("second", slots[1].TextContent);
    }

    [Fact]
    public void PromoteTempSlot_SkipsLockedSlots()
    {
        var slots = CreateTestSlots(10);
        slots[0].IsLocked = true;
        var slotManager = new SlotManager(slots, 10);

        slotManager.CaptureToTempSlot("content", ClipboardType.Text);
        int promotedIndex = slotManager.PromoteTempSlot();

        Assert.Equal(1, promotedIndex); // Skipped slot 0
        Assert.Equal("content", slots[1].TextContent);
        Assert.False(slots[0].HasContent); // Locked slot unchanged
    }

    [Fact]
    public void PromoteTempSlot_ReturnsMinusOne_WhenAllSlotsLocked()
    {
        var slots = CreateTestSlots(3);
        foreach (var slot in slots)
        {
            slot.IsLocked = true;
        }
        var slotManager = new SlotManager(slots, 3);

        slotManager.CaptureToTempSlot("content", ClipboardType.Text);
        int result = slotManager.PromoteTempSlot();

        Assert.Equal(-1, result);
    }

    [Fact]
    public void PromoteTempSlot_ReturnsMinusOne_WhenTempSlotEmpty()
    {
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10);

        int result = slotManager.PromoteTempSlot();

        Assert.Equal(-1, result);
    }

    [Fact]
    public void PromoteTempSlotTo_CopiesToSpecificSlot()
    {
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10);

        slotManager.CaptureToTempSlot("targeted", ClipboardType.Text);
        bool success = slotManager.PromoteTempSlotTo(5);

        Assert.True(success);
        Assert.Equal("targeted", slots[5].TextContent);
    }

    [Fact]
    public void PromoteTempSlotTo_FailsOnLockedSlot()
    {
        var slots = CreateTestSlots(10);
        slots[5].IsLocked = true;
        var slotManager = new SlotManager(slots, 10);

        slotManager.CaptureToTempSlot("content", ClipboardType.Text);
        bool success = slotManager.PromoteTempSlotTo(5);

        Assert.False(success);
        Assert.False(slots[5].HasContent);
    }

    [Fact]
    public void PromoteTempSlotTo_FailsWithInvalidIndex()
    {
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10);

        slotManager.CaptureToTempSlot("content", ClipboardType.Text);

        Assert.False(slotManager.PromoteTempSlotTo(-1));
        Assert.False(slotManager.PromoteTempSlotTo(10));
        Assert.False(slotManager.PromoteTempSlotTo(100));
    }

    [Fact]
    public void TempSlot_PreservesContentAfterPromotion()
    {
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10);

        slotManager.CaptureToTempSlot("keep me", ClipboardType.Text);
        slotManager.PromoteTempSlot();

        // Temp slot should still have its content (copy, not move)
        Assert.True(slotManager.TempSlot.HasContent);
        Assert.Equal("keep me", slotManager.TempSlot.TextContent);
    }

    [Fact]
    public void NormalClipboardChange_GoesToTempSlot_NotRoundRobin()
    {
        // This test verifies the new behavior:
        // Normal Ctrl+C (no target) should go to temp slot, not numbered slots

        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10);

        // Simulate what App.OnClipboardChanged does for normal clipboard change
        var eventArgs = new ClipboardChangedEventArgs("clipboard content", ClipboardType.Text);

        if (eventArgs.TargetSlotIndex.HasValue)
        {
            slotManager.CaptureToSlot(eventArgs.TargetSlotIndex.Value, eventArgs.Data, eventArgs.Type);
        }
        else
        {
            // NEW BEHAVIOR: goes to temp slot
            slotManager.CaptureToTempSlot(eventArgs.Data, eventArgs.Type);
        }

        // Temp slot should have the content
        Assert.Equal("clipboard content", slotManager.TempSlot.TextContent);

        // Numbered slots should be empty
        foreach (var slot in slots)
        {
            Assert.False(slot.HasContent, $"Slot {slot.Index} should be empty");
        }
    }

    [Fact]
    public void TargetedCopy_StillGoesToNumberedSlot()
    {
        // Verify Ctrl+Alt+# still works correctly

        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10);

        // Simulate what App.OnClipboardChanged does for targeted copy
        var eventArgs = new ClipboardChangedEventArgs("targeted content", ClipboardType.Text, targetSlotIndex: 3);

        if (eventArgs.TargetSlotIndex.HasValue)
        {
            slotManager.CaptureToSlot(eventArgs.TargetSlotIndex.Value, eventArgs.Data, eventArgs.Type);
        }
        else
        {
            slotManager.CaptureToTempSlot(eventArgs.Data, eventArgs.Type);
        }

        // Content should be in slot 4 (index 3)
        Assert.Equal("targeted content", slots[3].TextContent);

        // Temp slot should be empty
        Assert.False(slotManager.TempSlot.HasContent);
    }

    [Fact]
    public void CaptureToTempSlot_FiresSlotChangedEvent()
    {
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10);

        SlotChangedEventArgs? receivedArgs = null;
        slotManager.SlotChanged += (_, args) => receivedArgs = args;

        slotManager.CaptureToTempSlot("content", ClipboardType.Text);

        Assert.NotNull(receivedArgs);
        Assert.Equal(SlotManager.TempSlotIndex, receivedArgs.SlotIndex);
        Assert.Equal(SlotChangeType.ContentUpdated, receivedArgs.ChangeType);
    }

    // === SlotBehavior.Fixed Tests ===

    [Fact]
    public void PromoteTempSlot_FixedMode_AlwaysUsesActiveSlot()
    {
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10, SlotBehavior.Fixed);

        // Set active slot to 5
        slotManager.SetActiveSlot(5);

        slotManager.CaptureToTempSlot("fixed content", ClipboardType.Text);
        int promotedIndex = slotManager.PromoteTempSlot();

        Assert.Equal(5, promotedIndex);
        Assert.Equal("fixed content", slots[5].TextContent);
    }

    [Fact]
    public void PromoteTempSlot_FixedMode_RepeatedPromotesOverwriteSameSlot()
    {
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10, SlotBehavior.Fixed);

        slotManager.SetActiveSlot(3);

        // First promote
        slotManager.CaptureToTempSlot("first", ClipboardType.Text);
        slotManager.PromoteTempSlot();

        // Second promote - should overwrite same slot, not advance
        slotManager.CaptureToTempSlot("second", ClipboardType.Text);
        int secondIndex = slotManager.PromoteTempSlot();

        Assert.Equal(3, secondIndex); // Same slot
        Assert.Equal("second", slots[3].TextContent);

        // Other slots should be empty
        Assert.False(slots[0].HasContent);
        Assert.False(slots[4].HasContent);
    }

    [Fact]
    public void PromoteTempSlot_FixedMode_FailsIfActiveSlotLocked()
    {
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10, SlotBehavior.Fixed);

        slotManager.SetActiveSlot(5);
        slots[5].IsLocked = true;

        slotManager.CaptureToTempSlot("content", ClipboardType.Text);
        int result = slotManager.PromoteTempSlot();

        Assert.Equal(-1, result);
        Assert.False(slots[5].HasContent);
    }

    [Fact]
    public void PromoteTempSlot_FixedMode_ChangesWithCycleActiveSlot()
    {
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10, SlotBehavior.Fixed);

        // Start at slot 0, promote
        slotManager.CaptureToTempSlot("at slot 0", ClipboardType.Text);
        slotManager.PromoteTempSlot();
        Assert.Equal("at slot 0", slots[0].TextContent);

        // Cycle forward to slot 1
        slotManager.CycleActiveSlot(1);

        // Promote again - should go to slot 1 now
        slotManager.CaptureToTempSlot("at slot 1", ClipboardType.Text);
        int index = slotManager.PromoteTempSlot();

        Assert.Equal(1, index);
        Assert.Equal("at slot 1", slots[1].TextContent);
    }

    [Fact]
    public void SetSlotBehavior_CanSwitchModes()
    {
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10, SlotBehavior.RoundRobin);

        Assert.Equal(SlotBehavior.RoundRobin, slotManager.SlotBehavior);

        slotManager.SetSlotBehavior(SlotBehavior.Fixed);

        Assert.Equal(SlotBehavior.Fixed, slotManager.SlotBehavior);
    }

    [Fact]
    public void PromoteTempSlot_RoundRobinMode_AdvancesIndependentOfActiveSlot()
    {
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10, SlotBehavior.RoundRobin);

        // Set active slot to 5, but round-robin should start at 0
        slotManager.SetActiveSlot(5);

        slotManager.CaptureToTempSlot("first", ClipboardType.Text);
        int firstIndex = slotManager.PromoteTempSlot();

        slotManager.CaptureToTempSlot("second", ClipboardType.Text);
        int secondIndex = slotManager.PromoteTempSlot();

        // Round-robin advances regardless of active slot
        Assert.Equal(0, firstIndex);
        Assert.Equal(1, secondIndex);
        Assert.Equal("first", slots[0].TextContent);
        Assert.Equal("second", slots[1].TextContent);
    }

    // === SetActiveSlot Tests (for HUD click functionality) ===

    [Fact]
    public void SetActiveSlot_ChangesActiveSlotIndex()
    {
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10);

        Assert.Equal(0, slotManager.ActiveSlotIndex); // Default

        slotManager.SetActiveSlot(5);

        Assert.Equal(5, slotManager.ActiveSlotIndex);
    }

    [Fact]
    public void SetActiveSlot_FiresSlotChangedEvent()
    {
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10);

        SlotChangedEventArgs? receivedArgs = null;
        slotManager.SlotChanged += (_, args) => receivedArgs = args;

        slotManager.SetActiveSlot(3);

        Assert.NotNull(receivedArgs);
        Assert.Equal(3, receivedArgs.SlotIndex);
        Assert.Equal(SlotChangeType.ActiveChanged, receivedArgs.ChangeType);
    }

    [Fact]
    public void SetActiveSlot_DoesNotFireEvent_WhenSameSlot()
    {
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10);

        slotManager.SetActiveSlot(3);

        int eventCount = 0;
        slotManager.SlotChanged += (_, _) => eventCount++;

        slotManager.SetActiveSlot(3); // Same slot

        Assert.Equal(0, eventCount); // No event fired
    }

    [Fact]
    public void SetActiveSlot_IgnoresInvalidIndex()
    {
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10);

        slotManager.SetActiveSlot(5);

        slotManager.SetActiveSlot(-1); // Invalid
        Assert.Equal(5, slotManager.ActiveSlotIndex); // Unchanged

        slotManager.SetActiveSlot(10); // Out of range
        Assert.Equal(5, slotManager.ActiveSlotIndex); // Unchanged

        slotManager.SetActiveSlot(100); // Way out of range
        Assert.Equal(5, slotManager.ActiveSlotIndex); // Unchanged
    }

    [Fact]
    public void SetActiveSlot_AffectsFixedModePromote()
    {
        // This tests the full workflow: click slot in HUD -> Ctrl+Alt+V pastes from that slot
        var slots = CreateTestSlots(10);
        var slotManager = new SlotManager(slots, 10, SlotBehavior.Fixed);

        // Put content in slots 2 and 7
        slotManager.CaptureToSlot(2, "slot 2 content", ClipboardType.Text);
        slotManager.CaptureToSlot(7, "slot 7 content", ClipboardType.Text);

        // Click on slot 2 in HUD (simulated)
        slotManager.SetActiveSlot(2);

        // GetSlot with active index should return slot 2
        var activeSlot = slotManager.GetSlot(slotManager.ActiveSlotIndex);
        Assert.Equal("slot 2 content", activeSlot?.TextContent);

        // Click on slot 7 in HUD (simulated)
        slotManager.SetActiveSlot(7);

        // GetSlot with active index should return slot 7
        activeSlot = slotManager.GetSlot(slotManager.ActiveSlotIndex);
        Assert.Equal("slot 7 content", activeSlot?.TextContent);
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
