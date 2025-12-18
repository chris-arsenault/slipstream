using System.Runtime.InteropServices;

namespace Slipstream.Processing;

/// <summary>
/// Tracks held chord keys for momentary processor activation.
/// Uses GetAsyncKeyState to check which processor chord keys are currently held.
/// </summary>
public class ChordKeyTracker
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    // Virtual key codes for chord keys
    private static readonly Dictionary<char, int> ChordKeyToVirtualKey = new()
    {
        ['U'] = 0x55, // VK_U
        ['L'] = 0x4C, // VK_L
        ['S'] = 0x53, // VK_S
        ['T'] = 0x54, // VK_T
        ['N'] = 0x4E, // VK_N
        ['R'] = 0x52, // VK_R
        ['G'] = 0x47, // VK_G
        ['I'] = 0x49, // VK_I
    };

    /// <summary>
    /// Gets the set of processor names for currently held chord keys.
    /// </summary>
    public IReadOnlySet<string> GetHeldProcessors()
    {
        var held = new HashSet<string>();

        foreach (var definition in ProcessorDefinitions.WithChordKeys)
        {
            if (definition.ChordKey.HasValue && IsKeyHeld(definition.ChordKey.Value))
            {
                held.Add(definition.Name);
            }
        }

        return held;
    }

    /// <summary>
    /// Checks if a specific chord key is currently held down.
    /// </summary>
    public bool IsKeyHeld(char key)
    {
        char upper = char.ToUpperInvariant(key);
        if (ChordKeyToVirtualKey.TryGetValue(upper, out int vk))
        {
            // GetAsyncKeyState returns negative (high bit set) if key is pressed
            return (GetAsyncKeyState(vk) & 0x8000) != 0;
        }
        return false;
    }

    /// <summary>
    /// Gets the definitions of currently held chord key processors, sorted by priority.
    /// </summary>
    public IEnumerable<ProcessorDefinition> GetHeldDefinitions()
    {
        return ProcessorDefinitions.All
            .Where(d => d.ChordKey.HasValue && IsKeyHeld(d.ChordKey.Value));
    }
}
