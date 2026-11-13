using EquipmentBorrowing.Application.Dtos;
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

    public Task AddAsync(Borrowing borrowing, CancellationToken cancellationToken = default)
    {
        // Only marks the borrowing as "to be inserted"; nothing is sent until SaveChanges.
        _context.Borrowings.Add(borrowing);
        return Task.CompletedTask;
    }

    public Task<int> CountActiveByStudentIdAsync(int studentId, CancellationToken cancellationToken = default)
    {
        return DatabaseGuard.ReadAsync(() =>
            _context.Borrowings
                .AsNoTracking()
                .CountAsync(b => b.StudentId == studentId && b.Status == BorrowingStatus.Active, cancellationToken));
    }

    public Task<Borrowing?> GetActiveByEquipmentIdAsync(int equipmentId, CancellationToken cancellationToken = default)
    {
        return DatabaseGuard.ReadAsync(() =>
            _context.Borrowings
                .FirstOrDefaultAsync(b => b.EquipmentId == equipmentId && b.Status == BorrowingStatus.Active, cancellationToken));
    }

    public Task UpdateAsync(Borrowing borrowing, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(borrowing).State == EntityState.Detached)
        {
            _context.Borrowings.Update(borrowing);
        }

        return Task.CompletedTask;
    }

    public Task<Borrowing?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        // Tracked on purpose: ReturnEquipmentService changes this instance.
        return DatabaseGuard.ReadAsync(() =>
            _context.Borrowings.FirstOrDefaultAsync(b => b.Id == id, cancellationToken));
    }

    public Task<IReadOnlyList<ActiveBorrowingSummary>> GetActiveWithDetailsAsync(CancellationToken cancellationToken = default)
    {
        // One query, two JOINs.
        return DatabaseGuard.ReadAsync<IReadOnlyList<ActiveBorrowingSummary>>(async () =>
            await (
                from b in _context.Borrowings.AsNoTracking()
                join s in _context.Students.AsNoTracking() on b.StudentId equals s.Id
                join e in _context.Equipment.AsNoTracking() on b.EquipmentId equals e.Id
                where b.Status == BorrowingStatus.Active
                orderby b.DateBorrowed, b.Id
                select new ActiveBorrowingSummary(
                    b.Id,
                    s.Name,
                    e.Name,
                    b.DateBorrowed,
                    b.ExpectedReturnDate))
                .ToListAsync(cancellationToken));
    }
}