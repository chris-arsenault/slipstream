using Slipstream.Models;

namespace Slipstream.Services;

/// <summary>
/// Hardcoded default MIDI presets used to generate initial JSON config files.
/// These are only used on first boot to create the preset files - after that,
/// presets are loaded from JSON files in the config directory.
/// </summary>
internal static class MidiPresetDefaults
{
    /// <summary>
    /// Gets all default presets for initial file creation
    /// </summary>
    public static IReadOnlyList<MidiControlScheme> GetAllDefaults()
    {
        return new[]
        {
            CreatePiano10SlotPreset(),
            CreateLaunchkey25MiniPreset(),
            CreateLaunchpadPreset(),
            CreateNanoKontrolPreset()
        };
    }

    /// <summary>
    /// Generic piano keyboard: C3-A3 (notes 48-57) for slots 1-10 paste
    /// Hold C2 (note 36) as copy modifier
    /// </summary>
    private static MidiControlScheme CreatePiano10SlotPreset()
    {
        var mappings = new Dictionary<string, MidiTrigger>();

        // Slots 1-10 mapped to notes 48-57 (C3 to A3)
        for (int i = 1; i <= 10; i++)
        {
            mappings[$"PasteFromSlot{i}"] = MidiTrigger.NoteOn(47 + i); // 48-57
        }

        // Control actions on higher octave
        mappings["ToggleHud"] = MidiTrigger.NoteOn(60);        // C4
        mappings["CycleForward"] = MidiTrigger.NoteOn(62);     // D4
        mappings["CycleBackward"] = MidiTrigger.NoteOn(64);    // E4
        mappings["PromoteTempSlot"] = MidiTrigger.NoteOn(65);  // F4
        mappings["PasteFromActiveSlot"] = MidiTrigger.NoteOn(67); // G4

        return new MidiControlScheme
        {
            Name = "Piano10Slot",
            Description = "Piano keyboard: C3-A3 for slots 1-10, hold C2 for copy mode",
            DeviceHint = "Piano",
            Mappings = mappings,
            CopyModifier = MidiTrigger.NoteOn(36) // C2
        };
    }

    /// <summary>
    /// Novation Launchkey 25 Mini MK4
    /// </summary>
    private static MidiControlScheme CreateLaunchkey25MiniPreset()
    {
        var mappings = new Dictionary<string, MidiTrigger>();

        // Main paste zone: C2-A2 (notes 36-45) for slots 1-10
        int[] pasteNotes = { 36, 37, 38, 39, 40, 41, 42, 43, 44, 45 };
        for (int i = 0; i < pasteNotes.Length; i++)
        {
            mappings[$"PasteFromSlot{i + 1}"] = MidiTrigger.NoteOn(pasteNotes[i]);
        }

        // Control zone: C3 and above (notes 48+)
        mappings["ToggleHud"] = MidiTrigger.NoteOn(48);           // C3
        mappings["CycleForward"] = MidiTrigger.NoteOn(50);        // D3
        mappings["CycleBackward"] = MidiTrigger.NoteOn(52);       // E3
        mappings["PromoteTempSlot"] = MidiTrigger.NoteOn(53);     // F3
        mappings["PasteFromActiveSlot"] = MidiTrigger.NoteOn(55); // G3
        mappings["ClearAll"] = MidiTrigger.NoteOn(57);            // A3

        // Pads for Copy (notes 36-43 on drum channel 10)
        for (int i = 1; i <= 8; i++)
        {
            mappings[$"CopyToSlot{i}"] = new MidiTrigger
            {
                Type = MidiTriggerType.NoteOn,
                Number = 35 + i,
                Channel = 9,
                Threshold = 1
            };
        }

        return new MidiControlScheme
        {
            Name = "Launchkey25Mini",
            Description = "Launchkey 25 Mini: Keys=paste (hold B1 for copy), Pads=copy",
            DeviceHint = "Launchkey",
            Mappings = mappings,
            CopyModifier = MidiTrigger.NoteOn(35) // B1
        };
    }

    /// <summary>
    /// Novation Launchpad Mini MK3
    /// </summary>
    private static MidiControlScheme CreateLaunchpadPreset()
    {
        var mappings = new Dictionary<string, MidiTrigger>();

        // Bottom row: notes 36-43 = Paste slots 1-8
        for (int i = 1; i <= 8; i++)
        {
            mappings[$"PasteFromSlot{i}"] = MidiTrigger.NoteOn(35 + i);
        }

        // Second row: notes 51-58 = Copy slots 1-8
        for (int i = 1; i <= 8; i++)
        {
            mappings[$"CopyToSlot{i}"] = MidiTrigger.NoteOn(50 + i);
        }

        // Third row: control actions
        mappings["ToggleHud"] = MidiTrigger.NoteOn(61);
        mappings["CycleForward"] = MidiTrigger.NoteOn(62);
        mappings["CycleBackward"] = MidiTrigger.NoteOn(63);
        mappings["PromoteTempSlot"] = MidiTrigger.NoteOn(64);
        mappings["PasteFromActiveSlot"] = MidiTrigger.NoteOn(65);
        mappings["ClearAll"] = MidiTrigger.NoteOn(66);

        // Fourth row: slots 9-10
        mappings["PasteFromSlot9"] = MidiTrigger.NoteOn(71);
        mappings["PasteFromSlot10"] = MidiTrigger.NoteOn(72);
        mappings["CopyToSlot9"] = MidiTrigger.NoteOn(73);
        mappings["CopyToSlot10"] = MidiTrigger.NoteOn(74);

        return new MidiControlScheme
        {
            Name = "Launchpad",
            Description = "Launchpad MK3: Bottom row=paste, second row=copy",
            DeviceHint = "Launchpad",
            Mappings = mappings,
            CopyModifier = null
        };
    }

    /// <summary>
    /// Korg nanoKONTROL2
    /// </summary>
    private static MidiControlScheme CreateNanoKontrolPreset()
    {
        var mappings = new Dictionary<string, MidiTrigger>();

        // S buttons (CC 32-39) = Copy
        for (int i = 1; i <= 8; i++)
        {
            mappings[$"CopyToSlot{i}"] = MidiTrigger.CC(31 + i, threshold: 64);
        }

        // M buttons (CC 48-55) = Paste
        for (int i = 1; i <= 8; i++)
        {
            mappings[$"PasteFromSlot{i}"] = MidiTrigger.CC(47 + i, threshold: 64);
        }

        // R buttons (CC 64-71) = Lock
        for (int i = 1; i <= 8; i++)
        {
            mappings[$"LockSlot{i}"] = MidiTrigger.CC(63 + i, threshold: 64);
        }

        // Transport
        mappings["CycleBackward"] = MidiTrigger.CC(43, threshold: 64);
        mappings["CycleForward"] = MidiTrigger.CC(44, threshold: 64);
        mappings["ToggleHud"] = MidiTrigger.CC(42, threshold: 64);
        mappings["PromoteTempSlot"] = MidiTrigger.CC(41, threshold: 64);
        mappings["PasteFromActiveSlot"] = MidiTrigger.CC(45, threshold: 64);

        // Slots 9-10
        mappings["CopyToSlot9"] = MidiTrigger.CC(58, threshold: 64);
        mappings["CopyToSlot10"] = MidiTrigger.CC(59, threshold: 64);
        mappings["PasteFromSlot9"] = MidiTrigger.CC(46, threshold: 64);
        mappings["PasteFromSlot10"] = MidiTrigger.CC(60, threshold: 64);

        return new MidiControlScheme
        {
            Name = "nanoKONTROL2",
            Description = "Korg nanoKONTROL2: S=copy, M=paste, R=lock, transport=control",
            DeviceHint = "nanoKONTROL",
            Mappings = mappings,
            CopyModifier = null
        };
    }
}
