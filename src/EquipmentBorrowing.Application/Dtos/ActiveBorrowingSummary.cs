namespace EquipmentBorrowing.Application.Dtos;

public record ActiveBorrowingSummary(
    int Id,
    string StudentName,
    string EquipmentName,
    DateTime DateBorrowed,
    DateTime ExpectedReturnDate);