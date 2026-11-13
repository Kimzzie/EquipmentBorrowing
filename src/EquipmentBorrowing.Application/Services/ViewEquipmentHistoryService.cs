using EquipmentBorrowing.Application.Dtos;
using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;

namespace EquipmentBorrowing.Application.Services;

public class ViewEquipmentHistoryService
{
    private readonly IEquipmentRepository _equipment;
    private readonly IBorrowingQueryRepository _queries;

    public ViewEquipmentHistoryService(IEquipmentRepository equipment, IBorrowingQueryRepository queries)
    {
        _equipment = equipment;
        _queries = queries;
    }

    public async Task<OperationResult<BorrowingHistory>> ExecuteAsync(
        int equipmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _equipment.GetByIdAsync(equipmentId, cancellationToken);
            if (item is null)
                return OperationResult<BorrowingHistory>.Fail(
                    OperationFailure.NotFound,
                    "This item no longer exists. Please reselect an item from the refreshed list.");

            var records = await _queries.GetByEquipmentAsync(equipmentId, cancellationToken);

            var state = item.IsAvailable ? "Available" : "Currently issued";
            return OperationResult<BorrowingHistory>.Success(
                new BorrowingHistory(item.Name, $"{item.EquipmentCode} · {state}", records));
        }
        catch (Exception ex) when (ex is DataConflictException or DatabaseAccessException)
        {
            return OperationResult<BorrowingHistory>.FromException(ex);
        }
    }
}