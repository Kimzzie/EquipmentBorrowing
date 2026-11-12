using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Interfaces;

public interface IStudentRepository
{
    Task<Student?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Student>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Case-insensitive partial match on name or student number. Blank = everyone.</summary>
    Task<IReadOnlyList<Student>> SearchAsync(string? searchText, CancellationToken cancellationToken = default);

    /// <summary>Is this student number taken (trim + case-insensitive, same rule as the database)?</summary>
    Task<bool> StudentNumberExistsAsync(
        string studentNumber,
        int? excludingStudentId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Student student, CancellationToken cancellationToken = default);
}