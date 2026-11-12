using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class InMemoryStudentRepository : IStudentRepository
{
    private readonly List<Student> _students = new()
    {
        new Student(id: 1, studentNumber: "2023-0001", name: "Bryl Lim", isAllowedToBorrow: true),
        new Student(id: 2, studentNumber: "2023-0002", name: "Boss Rod", isAllowedToBorrow: false)
    };

    public Task<Student?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var student = _students.FirstOrDefault(s => s.Id == id);
        return Task.FromResult(student);
    }

    public Task<IReadOnlyList<Student>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Student> all = _students.ToList();
        return Task.FromResult(all);
    }

    public Task<IReadOnlyList<Student>> SearchAsync(string? searchText, CancellationToken cancellationToken = default)
    {
        var text = searchText?.Trim();
        IReadOnlyList<Student> found = string.IsNullOrEmpty(text)
            ? _students.ToList()
            : _students.Where(s =>
                s.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                s.StudentNumber.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
        return Task.FromResult(found);
    }

    public Task<bool> StudentNumberExistsAsync(
        string studentNumber, int? excludingStudentId, CancellationToken cancellationToken = default)
    {
        var exists = _students.Any(s =>
            s.Id != excludingStudentId &&
            string.Equals(s.StudentNumber, studentNumber.Trim(), StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public Task AddAsync(Student student, CancellationToken cancellationToken = default)
    {
        // Student.Id has a private setter (the database assigns it), so this demo
        // repository cannot create real students. Registration needs the PostgreSQL one.
        throw new NotSupportedException("The in-memory repository only holds fixed demo data.");
    }
}