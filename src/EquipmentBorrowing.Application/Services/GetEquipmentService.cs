using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

/// <summary>Loads one equipment item by ID. A missing item is a NotFound result.</summary>
public class GetEquipmentService
{
    private readonly IEquipmentRepository _equipment;

    public GetEquipmentService(IEquipmentRepository equipment)
    {
        _equipment = equipment;
    }

    public async Task<OperationResult<Equipment>> ExecuteAsync(
        int equipmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _equipment.GetByIdAsync(equipmentId, cancellationToken);

            return item is null
                ? OperationResult<Equipment>.Fail(OperationFailure.NotFound, "No equipment with this ID exists.")
                : OperationResult<Equipment>.Success(item);
        }
        catch (Exception ex) when (ex is DataConflictException or DatabaseAccessException)
        {
            return OperationResult<Equipment>.FromException(ex);
        }
    }
}
