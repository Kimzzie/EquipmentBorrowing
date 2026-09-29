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
}