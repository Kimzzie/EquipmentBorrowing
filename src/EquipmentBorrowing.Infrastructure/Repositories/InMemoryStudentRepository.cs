using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class InMemoryStudentRepository : IStudentRepository
{
    private readonly List<Student> _students = new()
       {
           new Student(id: 1, name: "Juan Dela Cruz", isAllowedToBorrow: true),
           new Student(id: 2, name: "Maria Santos", isAllowedToBorrow: false)
       };

    public Task<Student?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var student = _students.FirstOrDefault(s => s.Id == id);
        return Task.FromResult(student);
    }
}