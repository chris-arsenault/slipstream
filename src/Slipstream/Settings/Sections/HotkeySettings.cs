namespace Slipstream.Settings.Sections;

using Slipstream.Models;

/// <summary>
/// Hotkey binding settings. Generates default bindings programmatically instead of hardcoding.
/// </summary>
public class HotkeySettings : ISettingsSection
{
    public string SectionName => "Hotkeys";

    /// <summary>
    /// Map of action names to their hotkey bindings.
    /// </summary>
    public Dictionary<string, HotkeyBinding> Bindings { get; set; } = new();

    public HotkeySettings()
    {
        ApplyDefaults();
    }

    public void ApplyDefaults()
    {
        Bindings = GenerateDefaultBindings();
    }

    /// <summary>
    /// Generates the default hotkey bindings programmatically.
    /// </summary>
    private static Dictionary<string, HotkeyBinding> GenerateDefaultBindings()
    {
        var bindings = new Dictionary<string, HotkeyBinding>();

        // Generate slot bindings for slots 1-10
        for (int i = 1; i <= 10; i++)
        {
            // Number row keys (D1-D9, D0 for slot 10)
            var numberKey = i == 10 ? VirtualKey.D0 : (VirtualKey)(VirtualKey.D1 + i - 1);

            // Copy to slot: Ctrl+Alt+Number
            bindings[$"CopyToSlot{i}"] = new HotkeyBinding(
                ModifierKeys.Control | ModifierKeys.Alt, numberKey);

            // Paste from slot: Ctrl+Shift+Number
            bindings[$"PasteFromSlot{i}"] = new HotkeyBinding(
                ModifierKeys.Control | ModifierKeys.Shift, numberKey);

            // Numpad keys
            var numpadKey = i == 10 ? VirtualKey.NumPad0 : (VirtualKey)(VirtualKey.NumPad1 + i - 1);

            // Copy to slot via numpad: Ctrl+Alt+Numpad
            bindings[$"CopyToSlotNumpad{i}"] = new HotkeyBinding(
                ModifierKeys.Control | ModifierKeys.Alt, numpadKey);

            // Paste from slot via numpad: Ctrl+Numpad
            bindings[$"PasteFromSlotNumpad{i}"] = new HotkeyBinding(
                ModifierKeys.Control, numpadKey);
        }

        // Control hotkeys
        bindings["ToggleHud"] = new HotkeyBinding(
            ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.H);

        bindings["CycleForward"] = new HotkeyBinding(
            ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.Down);

        bindings["CycleBackward"] = new HotkeyBinding(
            ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.Up);

        // Temp slot operations
        bindings["PromoteTempSlot"] = new HotkeyBinding(
            ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.C);

        bindings["PasteFromActiveSlot"] = new HotkeyBinding(
            ModifierKeys.Control | ModifierKeys.Alt, VirtualKey.V);

        return bindings;
    }

    /// <summary>
    /// Gets a binding for an action, or null if not found.
    /// </summary>
    public HotkeyBinding? GetBinding(string actionName)
    {
        return Bindings.TryGetValue(actionName, out var binding) ? binding : null;
    }

    /// <summary>
    /// Sets or updates a binding for an action.
    /// </summary>
    public void SetBinding(string actionName, HotkeyBinding binding)
    {
        Bindings[actionName] = binding;
    }

    /// <summary>
    /// Removes a binding for an action.
    /// </summary>
    public bool RemoveBinding(string actionName)
    {
        return Bindings.Remove(actionName);
    }

    /// <summary>
    /// Gets all action names that have bindings.
    /// </summary>
    public IEnumerable<string> GetBoundActions() => Bindings.Keys;
}
