using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

public class UpdateStudentService
{
    private readonly IStudentRepository _students;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateStudentService(IStudentRepository students, IUnitOfWork unitOfWork)
    {
        _students = students;
        _unitOfWork = unitOfWork;
    }

    public async Task<OperationResult<Student>> ExecuteAsync(
        int studentId,
        string? studentNumber,
        string? fullName,
        string? contact,
        CancellationToken cancellationToken = default)
    {
        string number;
        string name;
        string? normalizedContact;
        try
        {
            number = Student.NormalizeStudentNumber(studentNumber);
            name = Student.NormalizeName(fullName);
            normalizedContact = Student.NormalizeContact(contact); // blank clears it (BR-03)
        }
        catch (ArgumentException ex)
        {
            return OperationResult<Student>.Fail(OperationFailure.Validation, ex.Message);
        }

        try
        {
            var student = await _students.GetByIdAsync(studentId, cancellationToken);
            if (student is null)
                return OperationResult<Student>.Fail(
                    OperationFailure.NotFound,
                    "This student no longer exists. The list has been refreshed.");

            // Exclude this student's own row, so changing only the letter case of
            // their own number is allowed.
            if (await _students.StudentNumberExistsAsync(number, studentId, cancellationToken))
                return OperationResult<Student>.Fail(
                    OperationFailure.Duplicate,
                    $"Student number '{number}' belongs to another student.");

            if (student.StudentNumber == number && student.Name == name && student.Contact == normalizedContact)
                return OperationResult<Student>.Success(student, "No changes to save.");

            // Same Id, same tracked row: borrowing history keeps pointing at this student (BR-03/14).
            student.UpdateDetails(number, name, normalizedContact);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return OperationResult<Student>.Success(student, $"Updated {student.Name}.");
        }
        catch (Exception ex) when (ex is DataConflictException or DatabaseAccessException)
        {
            return OperationResult<Student>.FromException(ex);
        }
    }
}