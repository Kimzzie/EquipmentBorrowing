using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Dtos;

public enum BorrowingSortField
{
    DateBorrowed,
    ExpectedReturnDate
}

/// <summary>
/// The criteria for UC-09. All parts are optional and combine with AND.
/// Equal dates are always ordered by Id in the same direction, so the order is stable.
/// </summary>
public sealed record BorrowingQuery(
    string? SearchText = null,
    BorrowingStatus? Status = null,
    BorrowingSortField SortBy = BorrowingSortField.DateBorrowed,
    bool Descending = true);