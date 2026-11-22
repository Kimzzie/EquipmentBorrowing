namespace EquipmentBorrowing.Desktop.Api;

// Mirrors the JSON of the API contract. Deliberately NOT shared with the Api project.
public sealed record StudentDto(int Id, string StudentNumber, string FullName, string? Contact, bool IsEligible);

public sealed record EquipmentDto(int Id, string EquipmentCode, string Name, string? Description, bool IsAvailable);

public sealed record BorrowingDto(
    int Id, int StudentId, string StudentNumber, string StudentName,
    int EquipmentId, string EquipmentCode, string EquipmentName,
    DateOnly BorrowedDate, DateOnly ExpectedReturnDate, DateOnly? ReturnedDate, string Status);

public sealed record CheckoutResponse(IReadOnlyList<BorrowingDto> Borrowings);

public sealed record ApiErrorBody(string Code, string Message);

// ---- Requests (property names become camelCase JSON) ----
public sealed record StudentRequest(string StudentNumber, string FullName, string? Contact);
public sealed record EquipmentRequest(string EquipmentCode, string Name, string? Description);
public sealed record EligibilityRequest(bool IsEligible);
public sealed record BorrowRequest(int StudentId, int EquipmentId, DateOnly ExpectedReturnDate);
public sealed record CheckoutRequest(int StudentId, List<int> EquipmentIds, DateOnly ExpectedReturnDate);
