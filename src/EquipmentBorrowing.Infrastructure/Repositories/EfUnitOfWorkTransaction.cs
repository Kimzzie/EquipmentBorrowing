using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EquipmentBorrowing.Infrastructure.Repositories;

internal sealed class EfUnitOfWorkTransaction : IUnitOfWorkTransaction
{
    private readonly EquipmentBorrowingDbContext _context;
    private readonly IDbContextTransaction _transaction;

    public EfUnitOfWorkTransaction(EquipmentBorrowingDbContext context, IDbContextTransaction transaction)
    {
        _context = context;
        _transaction = transaction;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Sends the INSERT/UPDATE statements INSIDE the open transaction. Not final yet.
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (DatabaseFailureTranslator.TryTranslate(ex, outcomeMayBeUnknown: false, out var translated))
        {
            // No COMMIT was sent yet, so whatever went wrong, the outcome is KNOWN: not saved.
            throw translated;
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        // Demo seam (see FaultInjection). All writes were already sent at this point.
        FaultInjection.GuardBeforeCommit();
        FaultInjection.TriggerBeforeCommit();

        try
        {
            await _transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex) when (DatabaseFailureTranslator.TryTranslate(ex, outcomeMayBeUnknown: true, out var translated))
        {
            // A lost connection DURING the commit is the one case where we cannot know (BR-18).
            throw translated;
        }

        // Demo seam: the commit really happened; pretend the acknowledgment was lost.
        FaultInjection.TriggerAfterCommit();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            // If CommitAsync was never reached, disposing rolls the transaction back.
            await _transaction.DisposeAsync();
        }
        catch
        {
            // A broken connection already discarded the transaction on the server.
        }
    }
}