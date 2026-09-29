using EquipmentBorrowing.Application.Interfaces;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class InMemoryUnitOfWork : IUnitOfWork
{
    // Nothing to flush — in-memory repositories write directly to their
    // own lists. This exists only to satisfy the interface until Stage 6.
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}