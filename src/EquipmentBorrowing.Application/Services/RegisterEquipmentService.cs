using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

public class RegisterEquipmentService
{
    private readonly IEquipmentRepository _equipment;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterEquipmentService(IEquipmentRepository equipment, IUnitOfWork unitOfWork)
    {
        _equipment = equipment;
        _unitOfWork = unitOfWork;
    }

    public async Task<OperationResult<Equipment>> ExecuteAsync(
        string? equipmentCode,
        string? name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        // BR-05/06: the constructor trims, rejects blank or too-long values and
        // turns a blank description into "absent".
        Equipment item;
        try
        {
            item = new Equipment(
                id: 0, // the database generates the real ID
                equipmentCode: equipmentCode ?? string.Empty,
                name: name ?? string.Empty,
                description: description,
                isAvailable: true); // BR-06: new items are available, with no active borrowing
        }
        catch (ArgumentException ex)
        {
            return OperationResult<Equipment>.Fail(OperationFailure.Validation, ex.Message);
        }

        try
        {
            // Friendly early check; the unique index remains the real guarantee
            // (it also catches two staff registering the same identifier together).
            if (await _equipment.EquipmentCodeExistsAsync(item.EquipmentCode, null, cancellationToken))
                return OperationResult<Equipment>.Fail(
                    OperationFailure.Duplicate,
                    $"Equipment identifier '{item.EquipmentCode}' is already registered.");

            await _equipment.AddAsync(item, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return OperationResult<Equipment>.Success(item, $"Registered {item.Name} ({item.EquipmentCode}).");
        }
        catch (Exception ex) when (ex is DataConflictException or DatabaseAccessException)
        {
            return OperationResult<Equipment>.FromException(ex);
        }
    }
}