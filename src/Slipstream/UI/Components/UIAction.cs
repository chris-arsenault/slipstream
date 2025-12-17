namespace Slipstream.UI.Components;

/// <summary>
/// Base class for UI actions. Actions are type-safe results from UI interactions.
/// </summary>
public abstract record UIAction;

/// <summary>
/// Action to toggle a boolean setting.
/// </summary>
public record ToggleSettingAction(string SettingKey, bool NewValue) : UIAction;

/// <summary>
/// Action to set a numeric value.
/// </summary>
public record SetValueAction(string SettingKey, int NewValue) : UIAction;

/// <summary>
/// Action to select a string option.
/// </summary>
public record SelectOptionAction(string SettingKey, string SelectedValue) : UIAction;

/// <summary>
/// Action to navigate to a different view/page.
/// </summary>
public record NavigateAction(string Target) : UIAction;

/// <summary>
/// Action to close/dismiss the current view.
/// </summary>
public record CloseAction : UIAction;

/// <summary>
/// Action to select a device (MIDI, etc.).
/// </summary>
public record SelectDeviceAction(string DeviceType, string DeviceName) : UIAction;

/// <summary>
/// Action to add an item to a list.
/// </summary>
public record AddItemAction(string ListKey, string Item) : UIAction;

/// <summary>
/// Action to remove an item from a list.
/// </summary>
public record RemoveItemAction(string ListKey, string Item) : UIAction;

/// <summary>
/// Action to execute a command by name.
/// </summary>
public record ExecuteCommandAction(string CommandName) : UIAction;

/// <summary>
/// Action triggered by a button click with custom payload.
/// </summary>
public record ButtonClickAction(string ButtonId, object? Payload = null) : UIAction;
