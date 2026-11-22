using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

/// <summary>Loads one student by ID. A missing student is a NotFound result, not an exception.</summary>
public class GetStudentService
{
    private readonly IStudentRepository _students;

    public GetStudentService(IStudentRepository students)
    {
        _students = students;
    }

    public async Task<OperationResult<Student>> ExecuteAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var student = await _students.GetByIdAsync(studentId, cancellationToken);

            return student is null
                ? OperationResult<Student>.Fail(OperationFailure.NotFound, "No student with this ID exists.")
                : OperationResult<Student>.Success(student);
        }
        catch (Exception ex) when (ex is DataConflictException or DatabaseAccessException)
        {
            return OperationResult<Student>.FromException(ex);
        }
    }
}
