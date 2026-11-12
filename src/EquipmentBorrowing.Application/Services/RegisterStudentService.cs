using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

public class RegisterStudentService
{
    private readonly IStudentRepository _students;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterStudentService(IStudentRepository students, IUnitOfWork unitOfWork)
    {
        _students = students;
        _unitOfWork = unitOfWork;
    }

    public async Task<OperationResult<Student>> ExecuteAsync(
        string? studentNumber,
        string? fullName,
        string? contact,
        CancellationToken cancellationToken = default)
    {
        // BR-01/02: the Student constructor trims and rejects blank or too-long values.
        Student student;
        try
        {
            student = new Student(
                id: 0, // the database generates the real ID
                studentNumber: studentNumber ?? string.Empty,
                name: fullName ?? string.Empty,
                isAllowedToBorrow: true, // BR-02: new students are eligible
                contact: contact);
        }
        catch (ArgumentException ex)
        {
            return OperationResult<Student>.Fail(OperationFailure.Validation, ex.Message);
        }

        try
        {
            // Friendly early check. The unique index is still the real guarantee: it also
            // catches two staff registering the same number at the same moment.
            if (await _students.StudentNumberExistsAsync(student.StudentNumber, null, cancellationToken))
                return OperationResult<Student>.Fail(
                    OperationFailure.Duplicate,
                    $"Student number '{student.StudentNumber}' is already registered.");

            await _students.AddAsync(student, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return OperationResult<Student>.Success(student, $"Registered {student.Name} ({student.StudentNumber}).");
        }
        catch (Exception ex) when (ex is DataConflictException or DatabaseAccessException)
        {
            return OperationResult<Student>.FromException(ex);
        }
    }
}