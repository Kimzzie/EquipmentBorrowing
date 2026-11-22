namespace EquipmentBorrowing.Api.Contracts;

/// <summary>The one error body every failure uses: { "code": "...", "message": "..." }.</summary>
public sealed record ApiError(string Code, string Message);

/// <summary>Stable, machine-readable codes. The Avalonia client will switch on these.</summary>
public static class ApiErrorCodes
{
    public const string ValidationFailed = "validation_failed";           // 400
    public const string NotFound = "not_found";                           // 404: a record is missing
    public const string RouteNotFound = "route_not_found";                // 404: no such endpoint
    public const string MethodNotAllowed = "method_not_allowed";          // 405
    public const string UnsupportedMediaType = "unsupported_media_type";  // 415: body is not JSON
    public const string Duplicate = "duplicate";                          // 409
    public const string RuleViolation = "rule_violation";                 // 409: eligibility, limit, availability...
    public const string Conflict = "conflict";                            // 409: simultaneous change
    public const string DatabaseError = "database_error";                 // 500: known failure, nothing saved
    public const string CommitOutcomeUnknown = "commit_outcome_unknown";  // 500: BR-18
    public const string InternalError = "internal_error";                 // 500
}
