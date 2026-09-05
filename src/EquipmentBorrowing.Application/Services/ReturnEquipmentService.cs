using EquipmentBorrowing.Application.Interfaces;

namespace EquipmentBorrowing.Application.Services;

public class ReturnEquipmentService
{
    private readonly IBorrowingRepository _borrowingRepository;
    private readonly IEquipmentRepository _equipmentRepository;

    public ReturnEquipmentService(
        IBorrowingRepository borrowingRepository,
        IEquipmentRepository equipmentRepository)
    {
        _borrowingRepository = borrowingRepository;
        _equipmentRepository = equipmentRepository;
    }

    public async Task<ReturnResult> ExecuteAsync(
        int borrowingId,
        CancellationToken cancellationToken = default)
    {
        var borrowing = await _borrowingRepository.GetByIdAsync(borrowingId, cancellationToken);
        if (borrowing is null)
            return ReturnResult.Fail("Borrowing record not found.");

        if (borrowing.Status == Domain.BorrowingStatus.Returned)
            return ReturnResult.Fail("Borrowing has already been returned.");

        var equipment = await _equipmentRepository.GetByIdAsync(borrowing.EquipmentId, cancellationToken);
        if (equipment is null)
            return ReturnResult.Fail("Associated equipment was not found.");

        borrowing.MarkAsReturned();
        equipment.MarkAsReturned();

        await _borrowingRepository.UpdateAsync(borrowing, cancellationToken);

        return ReturnResult.Success(borrowing);
    }
}