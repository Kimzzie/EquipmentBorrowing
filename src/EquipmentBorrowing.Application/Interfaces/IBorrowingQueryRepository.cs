using EquipmentBorrowing.Application.Dtos;

namespace EquipmentBorrowing.Application.Interfaces;

/// <summary>
/// Read-only queries that return display rows. Kept apart from IBorrowingRepository,
/// which loads tracked entities for the write operations (checkout, return).
/// </summary>
public interface IBorrowingQueryRepository
{
    Task<IReadOnlyList<BorrowingRecord>> GetByStudentAsync(int studentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BorrowingRecord>> GetByEquipmentAsync(int equipmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BorrowingRecord>> SearchAsync(BorrowingQuery query, CancellationToken cancellationToken = default);
}