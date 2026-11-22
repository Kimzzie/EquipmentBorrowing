namespace EquipmentBorrowing.Api.Contracts;

// Request bodies. Mutations never accept id, availability or status (contract Section 2).
// Text fields are nullable on purpose: a missing or blank value reaches the Application
// service, which gives the friendly BR-01/05 message. Value types use "required",
// so a missing studentId is a 400 instead of a silent 0.

public sealed record StudentRequest
{
    public string? StudentNumber { get; init; }
    public string? FullName { get; init; }
    public string? Contact { get; init; }
}

public sealed record EquipmentRequest
{
    public string? EquipmentCode { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
}

public sealed record EligibilityRequest
{
    public required bool IsEligible { get; init; }
}

public sealed record BorrowRequest
{
    public required int StudentId { get; init; }
    public required int EquipmentId { get; init; }
    public required DateOnly ExpectedReturnDate { get; init; }
}

public sealed record CheckoutRequest
{
    public required int StudentId { get; init; }
    public required List<int> EquipmentIds { get; init; }
    public required DateOnly ExpectedReturnDate { get; init; }
}

/// <summary>
/// API-12's body is "{}". It has no properties ON PURPOSE: because unknown JSON fields are
/// rejected, "returnedDate" or "status" in the body is a 400. The server decides the date.
/// </summary>
public sealed record ReturnRequest;
