using EquipmentBorrowing.Api.Contracts;
using EquipmentBorrowing.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EquipmentBorrowing.Api.Support;

public static class ApiResults
{
    public static ObjectResult Error(int status, string code, string message) =>
        new(new ApiError(code, message)) { StatusCode = status };

    /// <summary>
    /// Your services already return OperationResult with a failure kind. Mapping it here,
    /// once, means no controller repeats status-code logic.
    /// </summary>
    public static ObjectResult Failure<T>(OperationResult<T> result) => result.Failure switch
    {
        OperationFailure.Validation => Error(400, ApiErrorCodes.ValidationFailed, result.Message),
        OperationFailure.NotFound => Error(404, ApiErrorCodes.NotFound, result.Message),
        OperationFailure.Duplicate => Error(409, ApiErrorCodes.Duplicate, result.Message),
        OperationFailure.Rejected => Error(409, ApiErrorCodes.RuleViolation, result.Message),
        OperationFailure.Conflict => Error(409, ApiErrorCodes.Conflict, result.Message),
        OperationFailure.DatabaseError => Error(500, ApiErrorCodes.DatabaseError, result.Message),
        OperationFailure.UnknownOutcome => Error(500, ApiErrorCodes.CommitOutcomeUnknown, result.Message),
        _ => throw new InvalidOperationException("A successful result was passed to Failure().")
    };

    /// <summary>Replaces ASP.NET's default ProblemDetails 400 with our { code, message } body.</summary>
    public static IActionResult FromModelState(ActionContext context)
    {
        var messages = new List<string>();

        foreach (var (key, entry) in context.ModelState)
        {
            if (entry is null)
                continue;

            foreach (var error in entry.Errors)
            {
                var text = Describe(key, error);
                if (!messages.Contains(text))
                    messages.Add(text);
            }
        }

        var message = messages.Count > 0
            ? "The request is not valid: " + string.Join(" ", messages.Take(3))
            : "The request is not valid.";

        return Error(400, ApiErrorCodes.ValidationFailed, message);
    }

    private static string Describe(string key, ModelError error)
    {
        if (!string.IsNullOrWhiteSpace(error.ErrorMessage))
            return error.ErrorMessage;

        // Body-parsing errors (unknown field, wrong type, bad date) keep their detail in an
        // exception whose text mentions .NET type names. Name only the field instead.
        var field = key.TrimStart('$', '.');
        return field.Length == 0
            ? "The request body could not be read."
            : $"The field '{field}' is missing, unknown or has the wrong type.";
    }
}
