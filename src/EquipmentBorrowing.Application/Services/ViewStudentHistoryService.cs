using EquipmentBorrowing.Application.Dtos;
using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;

namespace EquipmentBorrowing.Application.Services;

public class ViewStudentHistoryService
{
    private readonly IStudentRepository _students;
    private readonly IBorrowingQueryRepository _queries;

    public ViewStudentHistoryService(IStudentRepository students, IBorrowingQueryRepository queries)
    {
        _students = students;
        _queries = queries;
    }

    public async Task<OperationResult<BorrowingHistory>> ExecuteAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Exception flow 2e: the selected student may be gone (stale selection).
            var student = await _students.GetByIdAsync(studentId, cancellationToken);
            if (student is null)
                return OperationResult<BorrowingHistory>.Fail(
                    OperationFailure.NotFound,
                    "This student no longer exists. Please reselect a student from the refreshed list.");

            var records = await _queries.GetByStudentAsync(studentId, cancellationToken);

            return OperationResult<BorrowingHistory>.Success(
                new BorrowingHistory(student.Name, student.StudentNumber, records));
        }
        catch (Exception ex) when (ex is DataConflictException or DatabaseAccessException)
        {
            return OperationResult<BorrowingHistory>.FromException(ex);
        }
    }
}