using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class EfBorrowingRepository : IBorrowingRepository
{
    private readonly EquipmentBorrowingDbContext _context;

    public EfBorrowingRepository(EquipmentBorrowingDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Borrowing borrowing, CancellationToken cancellationToken = default)
    {
        // Only staged here — the actual INSERT happens on SaveChangesAsync.
        await _context.Borrowings.AddAsync(borrowing, cancellationToken);
    }

    public async Task<int> CountActiveByStudentIdAsync(int studentId, CancellationToken cancellationToken = default)
    {
        return await _context.Borrowings
            .AsNoTracking()
            .CountAsync(b => b.StudentId == studentId && b.Status == BorrowingStatus.Active, cancellationToken);
    }

    public async Task<Borrowing?> GetActiveByEquipmentIdAsync(int equipmentId, CancellationToken cancellationToken = default)
    {
        return await _context.Borrowings
            .FirstOrDefaultAsync(b => b.EquipmentId == equipmentId && b.Status == BorrowingStatus.Active, cancellationToken);
    }

    public Task UpdateAsync(Borrowing borrowing, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(borrowing).State == EntityState.Detached)
        {
            _context.Borrowings.Update(borrowing);
        }

        return Task.CompletedTask;
    }

    public async Task<Borrowing?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        // Tracked: ReturnEquipmentService mutates this via MarkAsReturned().
        return await _context.Borrowings
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Borrowing>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Borrowings
            .AsNoTracking()
            .Where(b => b.Status == BorrowingStatus.Active)
            .ToListAsync(cancellationToken);
    }
}