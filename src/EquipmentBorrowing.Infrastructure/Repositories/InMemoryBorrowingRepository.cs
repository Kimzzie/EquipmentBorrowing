using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class InMemoryBorrowingRepository : IBorrowingRepository
{
    private readonly List<Borrowing> _borrowings = new();

    public Task AddAsync(Borrowing borrowing, CancellationToken cancellationToken = default)
    {
        _borrowings.Add(borrowing);
        return Task.CompletedTask;
    }

    public Task<int> CountActiveByStudentIdAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var count = _borrowings.Count(b => b.StudentId == studentId && b.Status == BorrowingStatus.Active);
        return Task.FromResult(count);
    }

    public Task<Borrowing?> GetActiveByEquipmentIdAsync(int equipmentId, CancellationToken cancellationToken = default)
    {
        var borrowing = _borrowings.FirstOrDefault(b => b.EquipmentId == equipmentId && b.Status == BorrowingStatus.Active);
        return Task.FromResult(borrowing);
    }

    public Task UpdateAsync(Borrowing borrowing, CancellationToken cancellationToken = default)
    {
        // In-memory storage keeps a reference to the same object, so status
        // changes made via borrowing.MarkAsReturned() are already reflected.
        // This method exists to satisfy the interface for future real implementations.
        return Task.CompletedTask;
    }
}