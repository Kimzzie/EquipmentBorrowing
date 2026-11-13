namespace EquipmentBorrowing.Infrastructure.Data;

/// <summary>Builds a "contains" pattern for ILIKE with the user's wildcards neutralised.</summary>
internal static class LikePattern
{
    public const string EscapeCharacter = "\\";

    public static string Contains(string text) =>
        "%" + text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}