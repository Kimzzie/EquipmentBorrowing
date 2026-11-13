using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

/// <summary>UC-03: borrow ONE item. The rules live in CheckoutProcessor.</summary>
public class BorrowEquipmentService
{
    private readonly CheckoutProcessor _processor;

    public BorrowEquipmentService(CheckoutProcessor processor)
    {
        _processor = processor;
    }

    public async Task<OperationResult<Borrowing>> ExecuteAsync(
        int studentId,
        int equipmentId,
        DateOnly expectedReturnDate,
        CancellationToken cancellationToken = default)
    {
        var result = await _processor.ExecuteAsync(
            studentId, new[] { equipmentId }, expectedReturnDate, cancellationToken);

        return result.IsSuccess
            ? OperationResult<Borrowing>.Success(result.Value![0], result.Message)
            : OperationResult<Borrowing>.Fail(result.Failure, result.Message);
    }
}