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

    public async Task AddAsync(Borrowing borrowing, CancellationToken cancellationToken = default)
    {
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
        return await _context.Borrowings
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ActiveBorrowingSummary>> GetActiveWithDetailsAsync(CancellationToken cancellationToken = default)
    {
        // One query, two JOINs — replaces what used to be one query per
        // borrowing plus a separate student lookup and equipment lookup.
        return await (
            from b in _context.Borrowings.AsNoTracking()
            join s in _context.Students.AsNoTracking() on b.StudentId equals s.Id
            join e in _context.Equipment.AsNoTracking() on b.EquipmentId equals e.Id
            where b.Status == BorrowingStatus.Active
            select new ActiveBorrowingSummary(
                b.Id,
                s.Name,
                e.Name,
                b.DateBorrowed,
                b.ExpectedReturnDate))
            .ToListAsync(cancellationToken);
    }
}