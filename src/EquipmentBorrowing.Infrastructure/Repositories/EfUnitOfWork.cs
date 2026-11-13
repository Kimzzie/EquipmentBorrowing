using System.Data;
using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class EfUnitOfWork : IUnitOfWork
{
    private readonly EquipmentBorrowingDbContext _context;

    public EfUnitOfWork(EquipmentBorrowingDbContext context)
    {
        _context = context;
    }

    // One-shot save for simple single-row operations.
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Reach the server BEFORE writing anything. If this fails, nothing was sent:
        // a KNOWN failure.
        try
        {
            await _context.Database.OpenConnectionAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new DatabaseAccessException(
                $"{DatabaseErrorTranslator.ToUserMessage(ex)} Nothing was saved.", ex);
        }

        try
        {
            // EF wraps all pending changes in ONE implicit transaction. If the connection
            // breaks here, the COMMIT may or may not have happened: outcome may be unknown.
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (DatabaseFailureTranslator.TryTranslate(ex, outcomeMayBeUnknown: true, out var translated))
        {
            throw translated;
        }
        finally
        {
            await CloseQuietlyAsync();
        }
    }

    // Explicit transaction for checkout and return (BR-15).
    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Serializable: PostgreSQL detects two transactions whose combined effect could
            // not have happened one after the other (e.g. the same student passing the
            // three-item limit twice) and aborts one of them.
            var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);

            return new EfUnitOfWorkTransaction(_context, transaction);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new DatabaseAccessException(
                $"{DatabaseErrorTranslator.ToUserMessage(ex)} Nothing was saved.", ex);
        }
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