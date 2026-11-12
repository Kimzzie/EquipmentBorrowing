using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

public class FindStudentsService
{
    private readonly IStudentRepository _students;

    public FindStudentsService(IStudentRepository students)
    {
        _students = students;
    }

    /// <summary>Throws DatabaseAccessException if the database cannot be reached.</summary>
    public Task<IReadOnlyList<Student>> ExecuteAsync(
        string? searchText,
        CancellationToken cancellationToken = default)
    {
        return _students.SearchAsync(searchText?.Trim(), cancellationToken);
    }
}