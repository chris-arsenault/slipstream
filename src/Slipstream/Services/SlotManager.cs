using Slipstream.Content;
using Slipstream.Models;

namespace Slipstream.Services;

public class SlotManager
{
    private readonly List<ClipboardSlot> _slots;
    private readonly ClipboardSlot _tempSlot;
    private int _activeSlotIndex;
    private int _nextRoundRobinIndex;
    private readonly int _maxSlots;
    private SlotBehavior _slotBehavior;

    // Sticky app tracking: maps process name -> slot index that the app has claimed
    private readonly Dictionary<string, int> _stickyAppBindings = new(StringComparer.OrdinalIgnoreCase);
    // Reverse mapping: slot index -> process name that owns it (for clearing bindings)
    private readonly Dictionary<int, string> _slotOwners = new();
    // The source process for the current temp slot content
    private string? _tempSlotSourceProcess;

    /// <summary>
    /// Special index used to identify the temp slot in events and operations.
    /// </summary>
    public const int TempSlotIndex = -1;

    public event EventHandler<SlotChangedEventArgs>? SlotChanged;

    public int ActiveSlotIndex => _activeSlotIndex;
    public int SlotCount => _slots.Count;
    public ClipboardSlot TempSlot => _tempSlot;
    public SlotBehavior SlotBehavior => _slotBehavior;
    public int NextRoundRobinIndex => _nextRoundRobinIndex;
    public string? TempSlotSourceProcess => _tempSlotSourceProcess;

    public SlotManager(List<ClipboardSlot> slots, int maxSlots, SlotBehavior slotBehavior = SlotBehavior.RoundRobin)
    {
        _slots = slots;
        _maxSlots = maxSlots;
        _activeSlotIndex = 0;
        _nextRoundRobinIndex = 0;
        _tempSlot = new ClipboardSlot { Index = TempSlotIndex };
        _slotBehavior = slotBehavior;
    }

    public void SetSlotBehavior(SlotBehavior behavior)
    {
        _slotBehavior = behavior;
    }

    /// <summary>
    /// Checks if an index is within valid slot range.
    /// </summary>
    private bool IsValidIndex(int index) => index >= 0 && index < _slots.Count;

    public ClipboardSlot? GetSlot(int index)
    {
        if (!IsValidIndex(index))
            return null;
        return _slots[index];
    }

    public List<ClipboardSlot> GetAllSlots() => _slots.ToList();

    /// <summary>
    /// Captures content to the temp slot. Used for normal Ctrl+C and external clipboard changes.
    /// </summary>
    public void CaptureToTempSlot(object data, ClipboardType type, string? sourceProcess = null)
    {
        SetSlotContent(_tempSlot, data, type);
        _tempSlotSourceProcess = sourceProcess;

        if (sourceProcess != null)
        {
            Console.WriteLine($"[SlotManager] Captured from process: {sourceProcess}");
        }

        OnSlotChanged(TempSlotIndex, SlotChangeType.ContentUpdated);
    }

    /// <summary>
    /// Promotes temp slot content to a numbered slot based on current SlotBehavior.
    /// RoundRobin: cycles through slots sequentially.
    /// Fixed: always targets the active slot (changed only via Ctrl+Alt+Up/Down).
    /// Sticky apps: if the source process is a sticky app, reuse its claimed slot.
    /// Returns the slot index it was promoted to, or -1 if target slot is locked or all slots are locked.
    /// </summary>
    public int PromoteTempSlot(HashSet<string>? stickyApps = null)
    {
        if (!_tempSlot.HasContent)
            return -1;

        // Check if this is from a sticky app that already has a claimed slot
        if (_tempSlotSourceProcess != null && stickyApps != null && stickyApps.Contains(_tempSlotSourceProcess))
        {
            if (_stickyAppBindings.TryGetValue(_tempSlotSourceProcess, out int claimedSlot))
            {
                // Sticky app has a claimed slot - reuse it (even if content is different)
                var slot = _slots[claimedSlot];
                if (!slot.IsLocked)
                {
                    Console.WriteLine($"[SlotManager] Sticky app '{_tempSlotSourceProcess}' reusing claimed slot {claimedSlot}");
                    CopySlotContent(_tempSlot, slot);
                    OnSlotChanged(claimedSlot, SlotChangeType.ContentUpdated);
                    return claimedSlot;
                }
                else
                {
                    // Claimed slot is now locked - clear the binding and fall through to normal behavior
                    Console.WriteLine($"[SlotManager] Sticky app '{_tempSlotSourceProcess}' claimed slot {claimedSlot} is locked, clearing binding");
                    ClearStickyBinding(_tempSlotSourceProcess);
                }
            }
            // Sticky app doesn't have a claimed slot yet - will claim one via normal promotion
        }

        if (_slotBehavior == SlotBehavior.Fixed)
        {
            // Fixed mode: always use active slot
            var activeSlot = _slots[_activeSlotIndex];
            if (activeSlot.IsLocked)
                return -1;

            CopySlotContent(_tempSlot, activeSlot);
            ClaimSlotForStickyApp(_activeSlotIndex, stickyApps);
            OnSlotChanged(_activeSlotIndex, SlotChangeType.ContentUpdated);
            return _activeSlotIndex;
        }

        // RoundRobin mode: find next available slot
        int attempts = 0;
        while (attempts < _slots.Count)
        {
            var slot = _slots[_nextRoundRobinIndex];
            if (!slot.IsLocked)
            {
                // Skip if content is identical to what's already in the target slot
                // This prevents duplicate entries from tools like Snipping Tool that
                // write to clipboard multiple times for a single screenshot
                if (_tempSlot.HasSameContent(slot))
                {
                    Console.WriteLine($"[SlotManager] Skipping duplicate content in slot {_nextRoundRobinIndex}");
                    return _nextRoundRobinIndex; // Return the slot but don't advance or copy
                }

                // Copy temp slot content to the target slot
                CopySlotContent(_tempSlot, slot);
                int promotedIndex = _nextRoundRobinIndex;

                // If this slot was owned by a different sticky app, clear that binding
                ClearSlotOwnership(promotedIndex);

                // If this is a sticky app, claim this slot
                ClaimSlotForStickyApp(promotedIndex, stickyApps);

                _nextRoundRobinIndex = (_nextRoundRobinIndex + 1) % _slots.Count;
                OnSlotChanged(promotedIndex, SlotChangeType.ContentUpdated);
                return promotedIndex;
            }

            _nextRoundRobinIndex = (_nextRoundRobinIndex + 1) % _slots.Count;
            attempts++;
        }

        // All slots are locked
        return -1;
    }

    /// <summary>
    /// Claims a slot for the current temp slot's source process if it's a sticky app.
    /// </summary>
    private void ClaimSlotForStickyApp(int slotIndex, HashSet<string>? stickyApps)
    {
        if (_tempSlotSourceProcess == null || stickyApps == null)
            return;

        if (!stickyApps.Contains(_tempSlotSourceProcess))
            return;

        // Clear any previous binding for this app
        if (_stickyAppBindings.TryGetValue(_tempSlotSourceProcess, out int oldSlot))
        {
            _slotOwners.Remove(oldSlot);
        }

        // Create new binding
        _stickyAppBindings[_tempSlotSourceProcess] = slotIndex;
        _slotOwners[slotIndex] = _tempSlotSourceProcess;
        Console.WriteLine($"[SlotManager] Sticky app '{_tempSlotSourceProcess}' claimed slot {slotIndex}");
    }

    /// <summary>
    /// Clears the sticky binding for a specific app.
    /// </summary>
    private void ClearStickyBinding(string processName)
    {
        if (_stickyAppBindings.TryGetValue(processName, out int slotIndex))
        {
            _stickyAppBindings.Remove(processName);
            _slotOwners.Remove(slotIndex);
        }
    }

    /// <summary>
    /// Clears any sticky app ownership of a slot (called when slot is overwritten by round-robin or direct capture).
    /// </summary>
    private void ClearSlotOwnership(int slotIndex)
    {
        if (_slotOwners.TryGetValue(slotIndex, out string? processName))
        {
            Console.WriteLine($"[SlotManager] Clearing sticky binding for '{processName}' (slot {slotIndex} overwritten)");
            _stickyAppBindings.Remove(processName);
            _slotOwners.Remove(slotIndex);
        }
    }

    /// <summary>
    /// Promotes temp slot content to a specific numbered slot.
    /// Returns true if successful, false if slot is locked.
    /// </summary>
    public bool PromoteTempSlotTo(int targetIndex)
    {
        if (!_tempSlot.HasContent)
            return false;

        if (!IsValidIndex(targetIndex))
            return false;

        var slot = _slots[targetIndex];
        if (slot.IsLocked)
            return false;

        CopySlotContent(_tempSlot, slot);
        OnSlotChanged(targetIndex, SlotChangeType.ContentUpdated);
        return true;
    }

    public void CaptureToNextSlot(object data, ClipboardType type)
    {
        // Find next available slot using round-robin
        int attempts = 0;

        while (attempts < _slots.Count)
        {
            var slot = _slots[_nextRoundRobinIndex];
            if (!slot.IsLocked)
            {
                CaptureToSlot(_nextRoundRobinIndex, data, type);
                _nextRoundRobinIndex = (_nextRoundRobinIndex + 1) % _slots.Count;
                return;
            }

            _nextRoundRobinIndex = (_nextRoundRobinIndex + 1) % _slots.Count;
            attempts++;
        }

        // All slots are locked - do nothing
    }

    public void CaptureToSlot(int index, object data, ClipboardType type)
    {
        if (!IsValidIndex(index))
            return;

        var slot = _slots[index];
        if (slot.IsLocked)
            return;

        SetSlotContent(slot, data, type);
        OnSlotChanged(index, SlotChangeType.ContentUpdated);
    }

    private static void SetSlotContent(ClipboardSlot slot, object data, ClipboardType type)
    {
        switch (type)
        {
            case ClipboardType.Text when data is string text:
                slot.SetText(text);
                break;

            case ClipboardType.RichText when data is ValueTuple<string, string?> richData:
                slot.SetRichText(richData.Item1, richData.Item2);
                break;

            case ClipboardType.Html when data is ValueTuple<string, string?> htmlData:
                slot.SetHtml(htmlData.Item1, htmlData.Item2);
                break;

            case ClipboardType.Image when data is byte[] imageData:
                slot.SetImage(imageData);
                break;

            case ClipboardType.FileList when data is string[] files:
                slot.SetFileList(files);
                break;
        }
    }

    private static void CopySlotContent(ClipboardSlot source, ClipboardSlot target)
    {
        var content = source.GetContent();
        if (content != null)
            target.SetContent(content);
        else
            target.Clear();
    }

    public void CycleActiveSlot(int direction)
    {
        int newIndex = _activeSlotIndex + direction;

        if (newIndex < 0)
            newIndex = _slots.Count - 1;
        else if (newIndex >= _slots.Count)
            newIndex = 0;

        SetActiveSlot(newIndex);
    }

    public void SetActiveSlot(int index)
    {
        if (!IsValidIndex(index))
            return;

        if (_activeSlotIndex != index)
        {
            _activeSlotIndex = index;
            OnSlotChanged(index, SlotChangeType.ActiveChanged);
        }
    }

    public void ToggleLock(int index)
    {
        if (!IsValidIndex(index))
            return;

        _slots[index].IsLocked = !_slots[index].IsLocked;
        OnSlotChanged(index, SlotChangeType.LockToggled);
    }

    public void ClearSlot(int index)
    {
        if (!IsValidIndex(index))
            return;

        var slot = _slots[index];
        if (slot.IsLocked)
            return;

        slot.Clear();
        ClearSlotOwnership(index); // Release any sticky app binding
        OnSlotChanged(index, SlotChangeType.Cleared);
    }

    public void ClearAllUnlocked()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (!_slots[i].IsLocked)
            {
                _slots[i].Clear();
                ClearSlotOwnership(i); // Release any sticky app binding
            }
        }
        OnSlotChanged(-1, SlotChangeType.AllCleared);
    }

    public void ClearAllSlots()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            _slots[i].Clear();
            _slots[i].IsLocked = false;
        }
        _nextRoundRobinIndex = 0;
        // Clear all sticky app bindings
        _stickyAppBindings.Clear();
        _slotOwners.Clear();
        OnSlotChanged(-1, SlotChangeType.AllCleared);
    }

    public void SetSlotCount(int count)
    {
        if (count < 1 || count > 20)
            return;

        while (_slots.Count < count)
        {
            _slots.Add(new ClipboardSlot { Index = _slots.Count });
        }

        while (_slots.Count > count)
        {
            var lastSlot = _slots[^1];
            if (!lastSlot.IsLocked)
            {
                _slots.RemoveAt(_slots.Count - 1);
            }
            else
            {
                break;
            }
        }

        // Ensure active index is valid
        if (_activeSlotIndex >= _slots.Count)
        {
            _activeSlotIndex = _slots.Count - 1;
        }

        OnSlotChanged(-1, SlotChangeType.CountChanged);
    }

    private void OnSlotChanged(int index, SlotChangeType changeType)
    {
        SlotChanged?.Invoke(this, new SlotChangedEventArgs(index, changeType));
    }
}

public class SlotChangedEventArgs : EventArgs
{
    public int SlotIndex { get; }
    public SlotChangeType ChangeType { get; }

    public SlotChangedEventArgs(int slotIndex, SlotChangeType changeType)
    {
        SlotIndex = slotIndex;
        ChangeType = changeType;
    }
}

public enum SlotChangeType
{
    ContentUpdated,
    ActiveChanged,
    LockToggled,
    Cleared,
    AllCleared,
    CountChanged
}
