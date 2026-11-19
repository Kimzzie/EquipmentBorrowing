using System.Data.Common;
using System.Net.Sockets;
using EquipmentBorrowing.Application.Exceptions;
using Npgsql;

namespace EquipmentBorrowing.Infrastructure.Data;

/// <summary>
/// Wraps READ queries so a connection problem reaches the ViewModel as a
/// DatabaseAccessException with a safe message, instead of a raw provider exception.
/// That lets the UI show "database error + retry" and keep it distinct from "no results".
/// </summary>
internal static class DatabaseGuard
{
    public static async Task<T> ReadAsync<T>(Func<Task<T>> read)
    {
        try
        {
            return await read();
        }
        // The provider may wrap the real error (for example in an InvalidOperationException
        // saying "likely due to a transient failure"), so we look through the whole chain.
        // Cancellation is NOT a database problem: a newer selection cancels an older request
        // on purpose, and the ViewModels handle that case themselves.
        catch (Exception ex) when (ex is not OperationCanceledException
            && FindInChain<PostgresException>(ex) is
            { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected })
        {
            // Can happen to a READ inside a Serializable checkout transaction.
            throw new DataConflictException(
                DataConflict.ConcurrentUpdate, DatabaseFailureTranslator.ConcurrentUpdateMessage, ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && IsDatabaseProblem(ex))
        {
            throw new DatabaseAccessException(DatabaseErrorTranslator.ToUserMessage(ex), ex);
        }
    }

    private static bool IsDatabaseProblem(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is DbException or SocketException or IOException or TimeoutException)
                return true;
        }

        return false;
    }

    private static T? FindInChain<T>(Exception ex) where T : Exception
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is T match)
                return match;
        }

        return null;
    }
}