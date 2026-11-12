using System.Data.Common;
using System.Net.Sockets;
using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class EfUnitOfWork : IUnitOfWork
{
    private const string UnknownOutcomeMessage =
        "The connection to PostgreSQL was lost while saving, so it is not known whether the change was saved. " +
        "Nothing was retried. Reconnect, refresh and check the record before trying again.";

    private readonly EquipmentBorrowingDbContext _context;

    public EfUnitOfWork(EquipmentBorrowingDbContext context)
    {
        _context = context;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Step 1: reach the server BEFORE writing anything. If this fails, nothing was
        // sent, so the outcome is KNOWN: not saved.
        try
        {
            await _context.Database.OpenConnectionAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new DatabaseAccessException(
                $"{DatabaseErrorTranslator.ToUserMessage(ex)} Nothing was saved.", ex);
        }

        // Step 2: the actual save. EF wraps all pending changes in ONE transaction.
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (TryTranslate(ex, out var translated))
        {
            throw translated;
        }
        finally
        {
            await CloseQuietlyAsync();
        }
    }

    private static bool TryTranslate(Exception ex, out Exception translated)
    {
        // The server answered with an error: the transaction was rolled back. KNOWN failure.
        var postgres = FindInChain<PostgresException>(ex);
        if (postgres is not null)
        {
            translated = TranslatePostgres(postgres, ex);
            return true;
        }

        // The connection broke after we started writing: the COMMIT may or may not have
        // reached the server. UNKNOWN outcome (BR-18). Never claim a rollback or retry.
        if (IsConnectionFailure(ex))
        {
            translated = new CommitOutcomeUnknownException(UnknownOutcomeMessage, ex);
            return true;
        }

        translated = ex;
        return false; // not a database problem (a bug): let it propagate untouched
    }

    private static Exception TranslatePostgres(PostgresException postgres, Exception inner)
    {
        switch (postgres.SqlState)
        {
            case PostgresErrorCodes.UniqueViolation:
                return TranslateUniqueViolation(postgres, inner);

            case PostgresErrorCodes.ForeignKeyViolation:
                return new DataConflictException(
                    DataConflict.ReferenceMissing,
                    "A selected student or item no longer exists. The list will be refreshed.",
                    inner);

            default:
                // Class 08 = connection exception, 57P = server shutting down. Treat as unknown.
                if (postgres.SqlState.StartsWith("08") || postgres.SqlState.StartsWith("57P"))
                    return new CommitOutcomeUnknownException(UnknownOutcomeMessage, inner);

                return new DatabaseAccessException(
                    "The database rejected the change, so nothing was saved.", inner);
        }
    }

    private static Exception TranslateUniqueViolation(PostgresException postgres, Exception inner)
    {
        switch (postgres.ConstraintName)
        {
            case "UX_Students_NormalizedStudentNumber":
                return new DataConflictException(
                    DataConflict.DuplicateStudentNumber,
                    "That student number is already registered.", inner);

            case "UX_Equipment_NormalizedEquipmentCode":
                return new DataConflictException(
                    DataConflict.DuplicateEquipmentCode,
                    "That equipment identifier is already registered.", inner);

            case "UX_Borrowings_OneActivePerEquipment":
                return new DataConflictException(
                    DataConflict.EquipmentAlreadyIssued,
                    "An item in this request was just issued to someone else. The list will be refreshed.", inner);

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

    private async Task CloseQuietlyAsync()
    {
        try
        {
            await _context.Database.CloseConnectionAsync();
        }
        catch
        {
            // The connection may already be broken; the scope disposes it anyway.
            // A close problem must never hide the real result of the save.
        }
    }
}