using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Interfaces;

public interface IBorrowingRepository
{
    Task AddAsync(Borrowing borrowing, CancellationToken cancellationToken = default);

    Task<int> CountActiveByStudentIdAsync(int studentId, CancellationToken cancellationToken = default);

    Task<Borrowing?> GetActiveByEquipmentIdAsync(int equipmentId, CancellationToken cancellationToken = default);

    Task UpdateAsync(Borrowing borrowing, CancellationToken cancellationToken = default);
}