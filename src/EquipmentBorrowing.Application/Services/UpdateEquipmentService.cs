using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

public class UpdateEquipmentService
{
    private readonly IEquipmentRepository _equipment;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateEquipmentService(IEquipmentRepository equipment, IUnitOfWork unitOfWork)
    {
        _equipment = equipment;
        _unitOfWork = unitOfWork;
    }

    // Note what is NOT a parameter: availability. A descriptive edit has no way
    // to change it (BR-07, UC-16 flow 2b).
    public async Task<OperationResult<Equipment>> ExecuteAsync(
        int equipmentId,
        string? equipmentCode,
        string? name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        string code;
        string itemName;
        string? normalizedDescription;
        try
        {
            code = Equipment.NormalizeEquipmentCode(equipmentCode);
            itemName = Equipment.NormalizeName(name);
            normalizedDescription = Equipment.NormalizeDescription(description); // blank clears it
        }
        catch (ArgumentException ex)
        {
            return OperationResult<Equipment>.Fail(OperationFailure.Validation, ex.Message);
        }

        try
        {
            var item = await _equipment.GetByIdAsync(equipmentId, cancellationToken);
            if (item is null)
                return OperationResult<Equipment>.Fail(
                    OperationFailure.NotFound,
                    "This item no longer exists. The list has been refreshed.");

            // Exclude this item's own row, so a case-only change of its own identifier is allowed.
            if (await _equipment.EquipmentCodeExistsAsync(code, equipmentId, cancellationToken))
                return OperationResult<Equipment>.Fail(
                    OperationFailure.Duplicate,
                    $"Equipment identifier '{code}' belongs to another item.");

            if (item.EquipmentCode == code && item.Name == itemName && item.Description == normalizedDescription)
                return OperationResult<Equipment>.Success(item, "No changes to save.");

            // Same Id, same row: borrowing history keeps pointing at this item (BR-07/14).
            item.UpdateDetails(code, itemName, normalizedDescription);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return OperationResult<Equipment>.Success(item, $"Updated {item.Name}.");
        }
        catch (Exception ex) when (ex is DataConflictException or DatabaseAccessException)
        {
            return OperationResult<Equipment>.FromException(ex);
        }
    }
}