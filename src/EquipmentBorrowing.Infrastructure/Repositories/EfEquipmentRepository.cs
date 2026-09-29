using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class EfEquipmentRepository : IEquipmentRepository
{
    private readonly EquipmentBorrowingDbContext _context;

    public EfEquipmentRepository(EquipmentBorrowingDbContext context)
    {
        _context = context;
    }

    public async Task<Equipment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        // Tracked on purpose: BorrowEquipmentService mutates this instance
        // via MarkAsBorrowed(), and the change tracker needs to see it
        // when SaveChangesAsync runs later.
        return await _context.Equipment
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Equipment>> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Equipment
            .AsNoTracking()
            .Where(e => e.IsAvailable)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Equipment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Equipment
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task UpdateAsync(Equipment equipment, CancellationToken cancellationToken = default)
    {
        // If this instance came from GetByIdAsync above, it's already
        // tracked and already dirty — nothing more to do. This handles
        // the case where it isn't (e.g. came from a no-tracking query).
        if (_context.Entry(equipment).State == EntityState.Detached)
        {
            _context.Equipment.Update(equipment);
        }

        return Task.CompletedTask;
    }
}