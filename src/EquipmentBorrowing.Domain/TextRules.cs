namespace EquipmentBorrowing.Domain;

internal static class TextRules
{
    // Required text: trim, reject blank.
    public static string Require(string? value, string label)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ArgumentException($"{label} is required.");
        return trimmed;
    }

    // Optional text: trim, blank becomes null (stored as absent).
    public static string? Optional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}