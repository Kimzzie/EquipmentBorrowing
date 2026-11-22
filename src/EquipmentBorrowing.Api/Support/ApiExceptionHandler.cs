using System.Data.Common;
using EquipmentBorrowing.Api.Contracts;
using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Infrastructure.Data;
using Microsoft.AspNetCore.Diagnostics;

namespace EquipmentBorrowing.Api.Support;

/// <summary>
/// Anything a controller does not catch ends up here. The client gets a safe
/// { code, message }; the full exception goes only to the server log.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // The caller hung up; nobody is left to answer.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            return true;

        _logger.LogError(exception, "Request {Method} {Path} failed with {ExceptionType}",
            httpContext.Request.Method, httpContext.Request.Path, exception.GetType().Name);

        // Order matters: the specific types derive from DatabaseAccessException.
        var (status, error) = exception switch
        {
            CommitOutcomeUnknownException unknown =>
                (500, new ApiError(ApiErrorCodes.CommitOutcomeUnknown, unknown.Message)),

            // Its own message names files and env variables: useful in the server log,
            // not for API clients.
            DatabaseConfigurationException =>
                (500, new ApiError(ApiErrorCodes.DatabaseError,
                    "The server's database connection is not configured correctly.")),

            DatabaseAccessException database =>
                (500, new ApiError(ApiErrorCodes.DatabaseError, database.Message)),

            DataConflictException conflict =>
                (409, new ApiError(ApiErrorCodes.Conflict, conflict.Message)),

            DbException =>
                (500, new ApiError(ApiErrorCodes.DatabaseError, "The database could not complete the request.")),

            _ => (500, new ApiError(ApiErrorCodes.InternalError, "An unexpected server error occurred."))
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(error, cancellationToken);
        return true;
    }
}
