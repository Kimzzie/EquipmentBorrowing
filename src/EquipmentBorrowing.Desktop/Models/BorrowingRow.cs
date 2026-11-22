using EquipmentBorrowing.Desktop.Api;

namespace EquipmentBorrowing.Desktop.Models;

/// <summary>Display-ready text for one table row. Formatting lives here, not in the API.</summary>
public sealed class BorrowingRow
{
    public required string StudentLabel { get; init; }
    public required string EquipmentLabel { get; init; }
    public required string DateBorrowedText { get; init; }
    public required string ExpectedReturnText { get; init; }
    public required string ReturnedText { get; init; }
    public required string StatusText { get; init; }

    public static BorrowingRow From(BorrowingDto b) => new()
    {
        StudentLabel = $"{b.StudentNumber} · {b.StudentName}",
        EquipmentLabel = $"{b.EquipmentCode} · {b.EquipmentName}",
        DateBorrowedText = b.BorrowedDate.ToString("yyyy-MM-dd"),
        ExpectedReturnText = b.ExpectedReturnDate.ToString("yyyy-MM-dd"),
        ReturnedText = b.ReturnedDate?.ToString("yyyy-MM-dd") ?? "—",
        StatusText = string.Equals(b.Status, "Active", StringComparison.OrdinalIgnoreCase) ? "Active" : "Returned"
    };
}
