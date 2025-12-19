namespace Slipstream.Services.Keyboard;

/// <summary>
/// Represents the state of modifier keys (Ctrl, Shift, Alt).
/// Immutable value type for safe state tracking.
/// </summary>
public readonly record struct ModifierState(bool Ctrl, bool Shift, bool Alt)
{
    /// <summary>
    /// No modifiers pressed.
    /// </summary>
    public static readonly ModifierState None = new(false, false, false);

    /// <summary>
    /// Returns true if any modifier is active.
    /// </summary>
    public bool HasAny => Ctrl || Shift || Alt;

    /// <summary>
    /// Captures the current physical modifier state (what hardware reports).
    /// Use this to determine what the user is actually pressing.
    /// </summary>
    public static ModifierState CapturePhysical(IInputInjector injector)
    {
        return new ModifierState(
            Ctrl: injector.IsKeyPhysicallyDown(VirtualKeys.LeftControl) ||
                  injector.IsKeyPhysicallyDown(VirtualKeys.RightControl) ||
                  injector.IsKeyPhysicallyDown(VirtualKeys.Control),
            Shift: injector.IsKeyPhysicallyDown(VirtualKeys.LeftShift) ||
                   injector.IsKeyPhysicallyDown(VirtualKeys.RightShift) ||
                   injector.IsKeyPhysicallyDown(VirtualKeys.Shift),
            Alt: injector.IsKeyPhysicallyDown(VirtualKeys.LeftAlt) ||
                 injector.IsKeyPhysicallyDown(VirtualKeys.RightAlt) ||
                 injector.IsKeyPhysicallyDown(VirtualKeys.Alt)
        );
    }

    /// <summary>
    /// Captures the current logical modifier state (what Windows believes).
    /// Use this to determine what corrections are needed before sending input.
    /// </summary>
    public static ModifierState CaptureLogical(IInputInjector injector)
    {
        return new ModifierState(
            Ctrl: injector.IsKeyLogicallyDown(VirtualKeys.LeftControl) ||
                  injector.IsKeyLogicallyDown(VirtualKeys.RightControl) ||
                  injector.IsKeyLogicallyDown(VirtualKeys.Control),
            Shift: injector.IsKeyLogicallyDown(VirtualKeys.LeftShift) ||
                   injector.IsKeyLogicallyDown(VirtualKeys.RightShift) ||
                   injector.IsKeyLogicallyDown(VirtualKeys.Shift),
            Alt: injector.IsKeyLogicallyDown(VirtualKeys.LeftAlt) ||
                 injector.IsKeyLogicallyDown(VirtualKeys.RightAlt) ||
                 injector.IsKeyLogicallyDown(VirtualKeys.Alt)
        );
    }

    public override string ToString() =>
        $"[Ctrl={Ctrl}, Shift={Shift}, Alt={Alt}]";
}
