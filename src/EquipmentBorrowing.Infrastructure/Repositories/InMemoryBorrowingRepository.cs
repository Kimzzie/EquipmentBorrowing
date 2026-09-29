using EquipmentBorrowing.Application.Dtos;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class InMemoryBorrowingRepository : IBorrowingRepository
{
    private readonly List<Borrowing> _borrowings = new();
    private readonly IStudentRepository _studentRepository;
    private readonly IEquipmentRepository _equipmentRepository;

    public InMemoryBorrowingRepository(
        IStudentRepository studentRepository,
        IEquipmentRepository equipmentRepository)
    {
        _studentRepository = studentRepository;
        _equipmentRepository = equipmentRepository;
    }

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
        return Task.CompletedTask;
    }

    public Task<Borrowing?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var borrowing = _borrowings.FirstOrDefault(b => b.Id == id);
        return Task.FromResult(borrowing);
    }

    public async Task<IReadOnlyList<ActiveBorrowingSummary>> GetActiveWithDetailsAsync(CancellationToken cancellationToken = default)
    {
        var summaries = new List<ActiveBorrowingSummary>();

        foreach (var borrowing in _borrowings.Where(b => b.Status == BorrowingStatus.Active))
        {
            var student = await _studentRepository.GetByIdAsync(borrowing.StudentId, cancellationToken);
            var equipment = await _equipmentRepository.GetByIdAsync(borrowing.EquipmentId, cancellationToken);

            summaries.Add(new ActiveBorrowingSummary(
                borrowing.Id,
                student?.Name ?? "Unknown student",
                equipment?.Name ?? "Unknown equipment",
                borrowing.DateBorrowed,
                borrowing.ExpectedReturnDate));
        }

        return summaries;
    }
}