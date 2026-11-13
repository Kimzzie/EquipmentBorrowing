using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

public class ReturnEquipmentService
{
    private readonly IBorrowingRepository _borrowings;
    private readonly IEquipmentRepository _equipment;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ReturnEquipmentService(
        IBorrowingRepository borrowings,
        IEquipmentRepository equipment,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _borrowings = borrowings;
        _equipment = equipment;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<OperationResult<Borrowing>> ExecuteAsync(
        int borrowingId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Same pattern as checkout: recheck the CURRENT status inside the transaction.
            // Note there is deliberately NO eligibility check: a restricted student may
            // still return items (BR-04).
            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var borrowing = await _borrowings.GetByIdAsync(borrowingId, cancellationToken);
            if (borrowing is null)
                return OperationResult<Borrowing>.Fail(
                    OperationFailure.NotFound, "This borrowing no longer exists. The list has been refreshed.");

            // BR-13: only an active borrowing can be returned; a repeat changes nothing.
            if (borrowing.Status == BorrowingStatus.Returned)
                return OperationResult<Borrowing>.Fail(
                    OperationFailure.Rejected,
                    "This borrowing was already returned. No change was made. The list has been refreshed.");

            var today = _timeProvider.GetLocalToday();
            if (today < borrowing.DateBorrowed)
                return OperationResult<Borrowing>.Fail(
                    OperationFailure.Rejected, "The return date cannot be earlier than the checkout date.");

            var equipment = await _equipment.GetByIdAsync(borrowing.EquipmentId, cancellationToken);
            if (equipment is null)
                return OperationResult<Borrowing>.Fail(
                    OperationFailure.NotFound, "The item for this borrowing was not found.");

            borrowing.MarkAsReturned(today);
            equipment.MarkAsReturned();

            await _borrowings.UpdateAsync(borrowing, cancellationToken);
            await _equipment.UpdateAsync(equipment, cancellationToken);

            await transaction.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return OperationResult<Borrowing>.Success(borrowing, $"Returned '{equipment.Name}' successfully.");
        }
        catch (Exception ex) when (ex is DataConflictException or DatabaseAccessException)
        {
            return OperationResult<Borrowing>.FromException(ex);
        }
    }
}