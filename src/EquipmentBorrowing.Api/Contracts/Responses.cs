namespace EquipmentBorrowing.Api.Contracts;

public sealed record StudentDto(
    int Id,
    string StudentNumber,
    string FullName,
    string? Contact,
    bool IsEligible);

public sealed record EquipmentDto(
    int Id,
    string EquipmentCode,
    string Name,
    string? Description,
    bool IsAvailable);

public sealed record BorrowingDto(
    int Id,
    int StudentId,
    string StudentNumber,
    string StudentName,
    int EquipmentId,
    string EquipmentCode,
    string EquipmentName,
    DateOnly BorrowedDate,
    DateOnly ExpectedReturnDate,
    DateOnly? ReturnedDate,   // null while active
    string Status);           // "Active" or "Returned"

public sealed record CheckoutResponse(IReadOnlyList<BorrowingDto> Borrowings);
