using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

public class ChangeStudentEligibilityService
{
    private readonly IStudentRepository _students;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeStudentEligibilityService(IStudentRepository students, IUnitOfWork unitOfWork)
    {
        _students = students;
        _unitOfWork = unitOfWork;
    }

    public async Task<OperationResult<Student>> ExecuteAsync(
        int studentId,
        bool allowBorrowing,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var student = await _students.GetByIdAsync(studentId, cancellationToken);
            if (student is null)
                return OperationResult<Student>.Fail(
                    OperationFailure.NotFound,
                    "This student no longer exists. The list has been refreshed.");

            // BR-04: repeating the current choice changes nothing and says so.
            if (!student.SetEligibility(allowBorrowing))
            {
                var current = allowBorrowing ? "permitted to borrow" : "restricted";
                return OperationResult<Student>.Success(
                    student,
                    $"{student.Name} is already {current}. No change was made.");
            }

            // Only the eligibility flag changes. Active borrowings are not touched, and
            // ReturnEquipmentService never checks eligibility, so returns keep working.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return OperationResult<Student>.Success(
                student,
                allowBorrowing
                    ? $"{student.Name} is now permitted to borrow."
                    : $"{student.Name} is now restricted. New borrowing is blocked; existing loans and returns are not affected.");
        }
        catch (Exception ex) when (ex is DataConflictException or DatabaseAccessException)
        {
            return OperationResult<Student>.FromException(ex);
        }
    }
}