using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;
using CheckoutResult = EquipmentBorrowing.Application.Services.OperationResult<
    System.Collections.Generic.IReadOnlyList<EquipmentBorrowing.Domain.Borrowing>>;

namespace EquipmentBorrowing.Application.Services;

/// <summary>
/// The one place that knows how to issue items to a student: BR-08 to BR-12 and BR-15.
/// Single borrow (UC-03) and multi-item checkout (UC-06) both go through here.
/// </summary>
public class CheckoutProcessor
{
    public const int MaxActiveBorrowings = 3;

    private readonly IStudentRepository _students;
    private readonly IEquipmentRepository _equipment;
    private readonly IBorrowingRepository _borrowings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CheckoutProcessor(
        IStudentRepository students,
        IEquipmentRepository equipment,
        IBorrowingRepository borrowings,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _students = students;
        _equipment = equipment;
        _borrowings = borrowings;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<CheckoutResult> ExecuteAsync(
        int studentId,
        IReadOnlyList<int> equipmentIds,
        DateOnly expectedReturnDate,
        CancellationToken cancellationToken = default)
    {
        // ---- Checks that need no database ----
        if (equipmentIds.Count == 0)
            return CheckoutResult.Fail(OperationFailure.Validation, "Select at least one item.");

        // BR-12: reject duplicates; never silently count or drop them.
        if (equipmentIds.Distinct().Count() != equipmentIds.Count)
            return CheckoutResult.Fail(
                OperationFailure.Validation,
                "The same item was selected more than once. Remove the duplicate and try again.");

        var today = _timeProvider.GetLocalToday();
        if (expectedReturnDate <= today)
            return CheckoutResult.Fail(OperationFailure.Validation, "Expected return date must be later than today.");

        try
        {
            // BR-15: everything below happens INSIDE one transaction. We re-read the CURRENT
            // student, items and active count here, so a stale screen cannot sneak through.
            // Leaving this block without CommitAsync (any 'return' below) rolls back.
            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var student = await _students.GetByIdAsync(studentId, cancellationToken);
            if (student is null)
                return CheckoutResult.Fail(
                    OperationFailure.NotFound, "This student no longer exists. The lists have been refreshed.");

            if (!student.IsAllowedToBorrow)
                return CheckoutResult.Fail(OperationFailure.Rejected, $"{student.Name} is not allowed to borrow.");

            var items = new List<Equipment>();
            var missingCount = 0;
            var unavailableNames = new List<string>();

            foreach (var equipmentId in equipmentIds)
            {
                var item = await _equipment.GetByIdAsync(equipmentId, cancellationToken);
                if (item is null)
                {
                    missingCount++;
                    continue;
                }

                // BR-10: borrowable only if available AND without an active borrowing.
                var activeBorrowing = await _borrowings.GetActiveByEquipmentIdAsync(equipmentId, cancellationToken);
                if (!item.IsAvailable || activeBorrowing is not null)
                    unavailableNames.Add(item.Name);

                items.Add(item);
            }

            if (missingCount > 0)
                return CheckoutResult.Fail(
                    OperationFailure.NotFound,
                    "A selected item no longer exists. Nothing was issued; the lists have been refreshed.");

            if (unavailableNames.Count > 0)
                return CheckoutResult.Fail(
                    OperationFailure.Rejected,
                    $"Not available: {string.Join(", ", unavailableNames)}. Nothing was issued.");

            // BR-09: existing active records PLUS the requested distinct items must not exceed the limit.
            var activeCount = await _borrowings.CountActiveByStudentIdAsync(studentId, cancellationToken);
            if (activeCount + items.Count > MaxActiveBorrowings)
            {
                var remaining = Math.Max(0, MaxActiveBorrowings - activeCount);
                return CheckoutResult.Fail(
                    OperationFailure.Rejected,
                    $"{student.Name} has {activeCount} active borrowing(s) and the limit is {MaxActiveBorrowings}, " +
                    $"so at most {remaining} more item(s) can be issued ({items.Count} requested). Nothing was issued.");
            }

            // ---- All checks passed: change the tracked objects ----
            var created = new List<Borrowing>();
            foreach (var item in items)
            {
                item.MarkAsBorrowed();

                var borrowing = new Borrowing(
                    id: 0, // the database generates the real ID
                    studentId: studentId,
                    equipmentId: item.Id,
                    dateBorrowed: today,
                    expectedReturnDate: expectedReturnDate);

                await _equipment.UpdateAsync(item, cancellationToken);
                await _borrowings.AddAsync(borrowing, cancellationToken);
                created.Add(borrowing);
            }

            // Writes are sent now (inside the transaction), but nothing is final until commit.
            await transaction.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var message = items.Count == 1
                ? $"Borrowed '{items[0].Name}' successfully."
                : $"Issued {items.Count} items to {student.Name}: {string.Join(", ", items.Select(i => i.Name))}.";

            return CheckoutResult.Success(created, message);
        }
        catch (Exception ex) when (ex is DataConflictException or DatabaseAccessException)
        {
            return CheckoutResult.FromException(ex);
        }
    }
}