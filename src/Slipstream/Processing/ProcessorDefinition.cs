namespace Slipstream.Processing;

/// <summary>
/// Metadata for a processor including priority and chord key mappings.
/// Lower priority values execute first.
/// </summary>
public record ProcessorDefinition(
    string Name,
    string DisplayName,
    int Priority,
    char? ChordKey = null
)
{
    /// <summary>
    /// Badge text for HUD display (first letter of display name, uppercase).
    /// </summary>
    public string Badge => DisplayName.Length > 0 ? DisplayName[0].ToString().ToUpperInvariant() : "?";
}

/// <summary>
/// Static registry of processor definitions with priorities and chord keys.
/// </summary>
public static class ProcessorDefinitions
{
    // Text processors (10-69)
    public static readonly ProcessorDefinition Uppercase = new("Uppercase", "Upper", 10, 'U');
    public static readonly ProcessorDefinition Lowercase = new("Lowercase", "Lower", 20, 'L');
    public static readonly ProcessorDefinition ReverseText = new("ReverseText", "Reverse", 25, 'R');
    public static readonly ProcessorDefinition StripFormatting = new("StripFormatting", "Strip", 30, 'S');
    public static readonly ProcessorDefinition TrimWhitespace = new("TrimWhitespace", "Trim", 40, 'T');
    public static readonly ProcessorDefinition RemoveLineBreaks = new("RemoveLineBreaks", "NoLines", 45, 'N');
    public static readonly ProcessorDefinition AddNewline = new("AddNewline", "Newline", 50, 'A');

    // Image processors (100-199)
    public static readonly ProcessorDefinition Grayscale = new("Grayscale", "Gray", 100, 'G');
    public static readonly ProcessorDefinition InvertColors = new("InvertColors", "Invert", 110, 'I');
    public static readonly ProcessorDefinition RotateClockwise = new("RotateClockwise", "Rotate", 120);
    public static readonly ProcessorDefinition FlipHorizontal = new("FlipHorizontal", "Flip", 130);

    /// <summary>
    /// All processor definitions, ordered by priority.
    /// </summary>
    public static IReadOnlyList<ProcessorDefinition> All { get; } = new List<ProcessorDefinition>
    {
        Uppercase,
        Lowercase,
        StripFormatting,
        TrimWhitespace,
        AddNewline,
        RemoveLineBreaks,
        ReverseText,
        Grayscale,
        InvertColors,
        RotateClockwise,
        FlipHorizontal
    }.OrderBy(d => d.Priority).ToList();

    /// <summary>
    /// Gets processor definition by name (case-insensitive).
    /// </summary>
    public static ProcessorDefinition? GetByName(string name) =>
        All.FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets processor definition by chord key.
    /// </summary>
    public static ProcessorDefinition? GetByChordKey(char key) =>
        All.FirstOrDefault(d => d.ChordKey.HasValue &&
            char.ToUpperInvariant(d.ChordKey.Value) == char.ToUpperInvariant(key));

    /// <summary>
    /// Gets all definitions that have chord keys assigned.
    /// </summary>
    public static IEnumerable<ProcessorDefinition> WithChordKeys =>
        All.Where(d => d.ChordKey.HasValue);
}
