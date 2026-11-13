using EquipmentBorrowing.Domain;
using CheckoutResult = EquipmentBorrowing.Application.Services.OperationResult<
    System.Collections.Generic.IReadOnlyList<EquipmentBorrowing.Domain.Borrowing>>;

namespace EquipmentBorrowing.Application.Services;

/// <summary>UC-06: issue two or more distinct items to one student, all or nothing.</summary>
public class CheckOutMultipleEquipmentService
{
    private readonly CheckoutProcessor _processor;

    public CheckOutMultipleEquipmentService(CheckoutProcessor processor)
    {
        _processor = processor;
    }

    public Task<CheckoutResult> ExecuteAsync(
        int studentId,
        IReadOnlyList<int> equipmentIds,
        DateOnly expectedReturnDate,
        CancellationToken cancellationToken = default)
    {
        // BR-12: at least two items. (Duplicates and everything else are checked by the processor.)
        if (equipmentIds.Count < 2)
            return Task.FromResult(CheckoutResult.Fail(
                OperationFailure.Validation,
                "A multi-item checkout needs at least two different items. For a single item, use Borrow."));

        return _processor.ExecuteAsync(studentId, equipmentIds, expectedReturnDate, cancellationToken);
    }
}