using EquipmentBorrowing.Application.Dtos;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Desktop.Models;

/// <summary>Display-ready text for one table row. Formatting lives here, not in the service.</summary>
public sealed class BorrowingRow
{
    public required string StudentLabel { get; init; }
    public required string EquipmentLabel { get; init; }
    public required string DateBorrowedText { get; init; }
    public required string ExpectedReturnText { get; init; }
    public required string ReturnedText { get; init; }
    public required string StatusText { get; init; }

    public static BorrowingRow From(BorrowingRecord record) => new()
    {
        StudentLabel = $"{record.StudentNumber} · {record.StudentName}",
        EquipmentLabel = $"{record.EquipmentCode} · {record.EquipmentName}",
        DateBorrowedText = record.DateBorrowed.ToString("yyyy-MM-dd"),
        ExpectedReturnText = record.ExpectedReturnDate.ToString("yyyy-MM-dd"),
        ReturnedText = record.DateReturned?.ToString("yyyy-MM-dd") ?? "—",
        StatusText = record.Status == BorrowingStatus.Active ? "Active" : "Returned"
    };
}