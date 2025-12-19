using Slipstream.Services.Keyboard;

namespace Slipstream.Services;

/// <summary>
/// Generates keyboard sequences for copy/paste operations.
/// Uses atomic SendInput batching for reliable modifier handling.
///
/// Design:
/// 1. Capture both logical state (what OS thinks) and physical state (what user holds)
/// 2. Build a complete atomic sequence that handles state transition
/// 3. Send everything in one SendInput call - no interleaving possible
/// 4. Use operation lifecycle for cleanup instead of timestamp tracking
/// </summary>
public class KeyboardSequencer
{
    private readonly IInputInjector _injector;
    private readonly KeyboardOperationBuilder _builder = new();
    private readonly object _operationLock = new();

    // Operation lifecycle tracking for safety net
    private DateTime? _operationStartTime;

    // Safety net threshold - if operation started but didn't complete within this time, cleanup
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Creates a KeyboardSequencer with the production AtomicInputInjector.
    /// </summary>
    public KeyboardSequencer() : this(new AtomicInputInjector())
    {
    }

    /// <summary>
    /// Creates a KeyboardSequencer with a custom injector (for testing).
    /// </summary>
    public KeyboardSequencer(IInputInjector injector)
    {
        _injector = injector;
    }

    /// <summary>
    /// Sends Ctrl+C atomically with proper modifier state management.
    /// </summary>
    public void SendCopyWithModifierRelease()
    {
        SendKeyComboWithModifierRelease(VirtualKeys.C);
    }

    /// <summary>
    /// Sends Ctrl+V atomically with proper modifier state management.
    /// </summary>
    public void SendPasteWithModifierRelease()
    {
        SendKeyComboWithModifierRelease(VirtualKeys.V);
    }

    /// <summary>
    /// Sends Ctrl+[key] atomically with proper modifier state management.
    ///
    /// The sequence:
    /// 1. Capture physical state (what user is holding) - this is what we restore to
    /// 2. Capture logical state (what OS thinks) - this is what we need to correct
    /// 3. Build atomic sequence: release all → Ctrl+Key → restore to physical
    /// 4. Send everything in one call
    /// </summary>
    private void SendKeyComboWithModifierRelease(byte keyCode)
    {
        lock (_operationLock)
        {
            // Capture states BEFORE we do anything
            var physicalState = ModifierState.CapturePhysical(_injector);
            var logicalState = ModifierState.CaptureLogical(_injector);

            Console.WriteLine($"[KeyboardSequencer] Sending Ctrl+0x{keyCode:X2} - Physical: {physicalState}, Logical: {logicalState}");

            // Mark operation start for safety net
            _operationStartTime = DateTime.UtcNow;

            try
            {
                _builder.Clear();

                // Step 1: Release ALL modifiers (both generic and left/right variants)
                // This ensures we start from a known clean state regardless of logical state
                _builder.ReleaseAllModifiers();

                // Step 2: Press Ctrl, press key, release key, release Ctrl
                _builder.KeyDown(VirtualKeys.Control);
                _builder.KeyPress(keyCode);
                _builder.KeyUp(VirtualKeys.Control);

                // Step 3: Restore modifiers to match what user is physically holding
                // We transition from None (after our Ctrl+Key) to physical state
                _builder.TransitionModifiers(ModifierState.None, physicalState);

                // Step 4: Send atomically - NO INTERLEAVING POSSIBLE
                var events = _builder.Build();
                Console.WriteLine($"[KeyboardSequencer] Sending {events.Length} events atomically");
                _injector.SendBatch(events);

                // Operation complete
                _operationStartTime = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KeyboardSequencer] Operation failed: {ex.Message}");
                // Leave operation time set so safety net can clean up
                throw;
            }
        }
    }

    /// <summary>
    /// Releases all modifier keys unconditionally.
    /// Call this on app shutdown.
    /// </summary>
    public void ReleaseAllModifiers()
    {
        lock (_operationLock)
        {
            _builder.Clear();
            _builder.ReleaseAllModifiers();
            _injector.SendBatch(_builder.Build());
            _operationStartTime = null;
        }
    }

    /// <summary>
    /// Safety net: Checks if an operation timed out and performs recovery.
    ///
    /// This is a TRUE safety net - it only activates if:
    /// 1. An operation started but never completed (exception during SendInput?)
    /// 2. Sufficient time has passed (not just slow execution)
    /// 3. Logical state differs from physical state (something is actually stuck)
    ///
    /// Safe to call periodically. Does nothing if everything is normal.
    /// </summary>
    public void CleanupStuckModifiers()
    {
        lock (_operationLock)
        {
            // Check if we have a potentially stuck operation
            if (_operationStartTime == null)
                return; // No operation in flight

            if (DateTime.UtcNow - _operationStartTime < OperationTimeout)
                return; // Give it time

            Console.WriteLine("[KeyboardSequencer] Operation timeout detected, checking for stuck modifiers");

            // Operation appears to have failed mid-flight
            var logical = ModifierState.CaptureLogical(_injector);
            var physical = ModifierState.CapturePhysical(_injector);

            // Only act if logical differs from physical (something is stuck)
            if (logical == physical)
            {
                Console.WriteLine("[KeyboardSequencer] States match, no cleanup needed");
                _operationStartTime = null;
                return;
            }

            Console.WriteLine($"[KeyboardSequencer] State mismatch - Logical: {logical}, Physical: {physical}");

            // Reconcile: release logically-down keys that aren't physically held
            _builder.Clear();

            if (logical.Ctrl && !physical.Ctrl)
            {
                Console.WriteLine("[KeyboardSequencer] Releasing stuck Ctrl");
                _builder.KeyUp(VirtualKeys.Control);
                _builder.KeyUp(VirtualKeys.LeftControl);
                _builder.KeyUp(VirtualKeys.RightControl);
            }

            if (logical.Shift && !physical.Shift)
            {
                Console.WriteLine("[KeyboardSequencer] Releasing stuck Shift");
                _builder.KeyUp(VirtualKeys.Shift);
                _builder.KeyUp(VirtualKeys.LeftShift);
                _builder.KeyUp(VirtualKeys.RightShift);
            }

            if (logical.Alt && !physical.Alt)
            {
                Console.WriteLine("[KeyboardSequencer] Releasing stuck Alt");
                _builder.KeyUp(VirtualKeys.Alt);
                _builder.KeyUp(VirtualKeys.LeftAlt);
                _builder.KeyUp(VirtualKeys.RightAlt);
            }

            if (_builder.Count > 0)
            {
                _injector.SendBatch(_builder.Build());
            }

            _operationStartTime = null;
        }
    }
}
