using System.Data.Common;
using System.Net.Sockets;
using EquipmentBorrowing.Application.Exceptions;
using Npgsql;

namespace EquipmentBorrowing.Infrastructure.Data;

/// <summary>
/// Turns provider exceptions raised while WRITING into exceptions the Application layer
/// understands, and decides whether the outcome is KNOWN (nothing saved) or UNKNOWN (BR-18).
/// </summary>
internal static class DatabaseFailureTranslator
{
    public const string UnknownOutcomeMessage =
        "The connection to PostgreSQL was lost while saving, so it is not known whether the change was saved. " +
        "Nothing was retried. Reconnect, refresh and check the records before trying again.";

    public const string ConcurrentUpdateMessage =
        "Another change was made at the same time, so nothing was saved. " +
        "The data has been refreshed; check it and try again.";

    /// <param name="outcomeMayBeUnknown">
    /// True when the exception happened while a COMMIT may have been in flight. A connection
    /// lost EARLIER (before any COMMIT was sent) is a known failure: the server discards
    /// the uncommitted transaction.
    /// </param>
    public static bool TryTranslate(Exception ex, bool outcomeMayBeUnknown, out Exception translated)
    {
        // The server answered with an error: the transaction was rolled back. KNOWN failure.
        var postgres = FindInChain<PostgresException>(ex);
        if (postgres is not null)
        {
            translated = TranslatePostgres(postgres, ex, outcomeMayBeUnknown);
            return true;
        }

        if (IsConnectionFailure(ex))
        {
            translated = outcomeMayBeUnknown
                ? new CommitOutcomeUnknownException(UnknownOutcomeMessage, ex)
                : new DatabaseAccessException(
                    "The connection to PostgreSQL was lost before the change was committed, so nothing was saved. " +
                    "Reconnect and try again.", ex);
            return true;
        }

        translated = ex;
        return false; // not a database problem (a bug): let it propagate untouched
    }

    private static Exception TranslatePostgres(PostgresException postgres, Exception inner, bool outcomeMayBeUnknown)
    {
        switch (postgres.SqlState)
        {
            case PostgresErrorCodes.UniqueViolation:
                return TranslateUniqueViolation(postgres, inner);

            case PostgresErrorCodes.ForeignKeyViolation:
                return new DataConflictException(
                    DataConflict.ReferenceMissing,
                    "A selected student or item no longer exists. The lists have been refreshed.",
                    inner);

            // Two transactions interfered with each other (Serializable isolation).
            // PostgreSQL aborted ours; nothing was saved.
            case PostgresErrorCodes.SerializationFailure:
            case PostgresErrorCodes.DeadlockDetected:
                return new DataConflictException(DataConflict.ConcurrentUpdate, ConcurrentUpdateMessage, inner);

            default:
                // Class 08 = connection exception, 57P = server shutting down.
                if (postgres.SqlState.StartsWith("08") || postgres.SqlState.StartsWith("57P"))
                {
                    return outcomeMayBeUnknown
                        ? new CommitOutcomeUnknownException(UnknownOutcomeMessage, inner)
                        : new DatabaseAccessException("The database connection was interrupted, so nothing was saved.", inner);
                }

                return new DatabaseAccessException("The database rejected the change, so nothing was saved.", inner);
        }
    }

    private static Exception TranslateUniqueViolation(PostgresException postgres, Exception inner)
    {
        switch (postgres.ConstraintName)
        {
            case "UX_Students_NormalizedStudentNumber":
                return new DataConflictException(
                    DataConflict.DuplicateStudentNumber, "That student number is already registered.", inner);

            case "UX_Equipment_NormalizedEquipmentCode":
                return new DataConflictException(
                    DataConflict.DuplicateEquipmentCode, "That equipment identifier is already registered.", inner);

            case "UX_Borrowings_OneActivePerEquipment":
                return new DataConflictException(
                    DataConflict.EquipmentAlreadyIssued,
                    "An item in this request was just issued to someone else. Nothing was issued; the lists have been refreshed.",
                    inner);

            default:
                return new DatabaseAccessException(
                    "The database rejected the change because it duplicates existing data. Nothing was saved.", inner);
        }
    }

    private static bool IsConnectionFailure(Exception ex)
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