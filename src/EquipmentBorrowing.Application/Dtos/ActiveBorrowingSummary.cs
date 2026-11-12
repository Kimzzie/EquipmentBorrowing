namespace EquipmentBorrowing.Application.Dtos;

public record ActiveBorrowingSummary(
    int Id,
    string StudentName,
    string EquipmentName,
    DateOnly DateBorrowed,
    DateOnly ExpectedReturnDate);