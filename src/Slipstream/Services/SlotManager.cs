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

    public ClipboardSlot? GetSlot(int index)
    {
        if (index < 0 || index >= _slots.Count)
            return null;
        return _slots[index];
    }

    public List<ClipboardSlot> GetAllSlots() => _slots.ToList();

    /// <summary>
    /// Captures content to the temp slot. Used for normal Ctrl+C and external clipboard changes.
    /// </summary>
    public void CaptureToTempSlot(object data, ClipboardType type)
    {
        SetSlotContent(_tempSlot, data, type);
        OnSlotChanged(TempSlotIndex, SlotChangeType.ContentUpdated);
    }

    /// <summary>
    /// Promotes temp slot content to a numbered slot based on current SlotBehavior.
    /// RoundRobin: cycles through slots sequentially.
    /// Fixed: always targets the active slot (changed only via Ctrl+Alt+Up/Down).
    /// Returns the slot index it was promoted to, or -1 if target slot is locked or all slots are locked.
    /// </summary>
    public int PromoteTempSlot()
    {
        if (!_tempSlot.HasContent)
            return -1;

        if (_slotBehavior == SlotBehavior.Fixed)
        {
            // Fixed mode: always use active slot
            var activeSlot = _slots[_activeSlotIndex];
            if (activeSlot.IsLocked)
                return -1;

            CopySlotContent(_tempSlot, activeSlot);
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
                // Copy temp slot content to the target slot
                CopySlotContent(_tempSlot, slot);
                int promotedIndex = _nextRoundRobinIndex;
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
    /// Promotes temp slot content to a specific numbered slot.
    /// Returns true if successful, false if slot is locked.
    /// </summary>
    public bool PromoteTempSlotTo(int targetIndex)
    {
        if (!_tempSlot.HasContent)
            return false;

        if (targetIndex < 0 || targetIndex >= _slots.Count)
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
        if (index < 0 || index >= _slots.Count)
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
        target.Clear();
        switch (source.Type)
        {
            case ClipboardType.Text:
                target.SetText(source.TextContent ?? "");
                break;
            case ClipboardType.RichText:
                target.SetRichText(source.TextContent ?? "", source.RichTextContent);
                break;
            case ClipboardType.Html:
                target.SetHtml(source.TextContent ?? "", source.HtmlContent);
                break;
            case ClipboardType.Image:
                if (source.ImageContent != null)
                    target.SetImage(source.ImageContent);
                break;
            case ClipboardType.FileList:
                if (source.FileListContent != null)
                    target.SetFileList(source.FileListContent);
                break;
        }
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
        if (index < 0 || index >= _slots.Count)
            return;

        if (_activeSlotIndex != index)
        {
            _activeSlotIndex = index;
            OnSlotChanged(index, SlotChangeType.ActiveChanged);
        }
    }

    public void ToggleLock(int index)
    {
        if (index < 0 || index >= _slots.Count)
            return;

        _slots[index].IsLocked = !_slots[index].IsLocked;
        OnSlotChanged(index, SlotChangeType.LockToggled);
    }

    public void ClearSlot(int index)
    {
        if (index < 0 || index >= _slots.Count)
            return;

        var slot = _slots[index];
        if (slot.IsLocked)
            return;

        slot.Clear();
        OnSlotChanged(index, SlotChangeType.Cleared);
    }

    public void ClearAllUnlocked()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (!_slots[i].IsLocked)
            {
                _slots[i].Clear();
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
