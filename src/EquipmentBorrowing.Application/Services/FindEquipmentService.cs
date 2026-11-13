using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

public class FindEquipmentService
{
    private readonly IEquipmentRepository _equipment;

    public FindEquipmentService(IEquipmentRepository equipment)
    {
        _equipment = equipment;
    }

    /// <summary>Throws DatabaseAccessException if the database cannot be reached.</summary>
    public Task<IReadOnlyList<Equipment>> ExecuteAsync(
        string? searchText,
        bool availableOnly = false,
        CancellationToken cancellationToken = default)
    {
        return _equipment.SearchAsync(searchText?.Trim(), availableOnly, cancellationToken);
    }
}