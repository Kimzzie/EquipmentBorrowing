using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Dtos;

/// <summary>One borrowing with its student and item already joined, ready to display.</summary>
public sealed record BorrowingRecord(
    int Id,
    int StudentId,
    string StudentNumber,
    string StudentName,
    int EquipmentId,
    string EquipmentCode,
    string EquipmentName,
    DateOnly DateBorrowed,
    DateOnly ExpectedReturnDate,
    DateOnly? DateReturned,
    BorrowingStatus Status);