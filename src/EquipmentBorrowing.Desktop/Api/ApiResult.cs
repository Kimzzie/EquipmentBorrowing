namespace EquipmentBorrowing.Desktop.Api;

public enum ApiFailure
{
    None,
    Unreachable,   // no HTTP response at all
    Validation,    // 400
    NotFound,      // 404
    Duplicate,     // 409 duplicate
    Rejected,      // 409 rule_violation
    Conflict,      // 409 conflict
    Server,        // 5xx: the API answered, but it failed (database_error, commit_outcome_unknown...)
    Unexpected     // anything else (bad JSON, 415, ...)
}

public sealed class ApiResult<T>
{
    private ApiResult(T? value, ApiFailure failure, string? code, string message)
    {
        Value = value; Failure = failure; Code = code; Message = message;
    }

    public bool IsSuccess => Failure == ApiFailure.None;
    public T? Value { get; }
    public ApiFailure Failure { get; }
    public string? Code { get; }
    public string Message { get; }

    public static ApiResult<T> Ok(T value) => new(value, ApiFailure.None, null, "");
    public static ApiResult<T> Fail(ApiFailure failure, string? code, string message) => new(default, failure, code, message);
}
