using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Interfaces;

public interface IEquipmentRepository
{
    Task<Equipment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Equipment>> GetAvailableAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Equipment>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Case-insensitive partial match on identifier or name; blank text = everything.
    /// With availableOnly, only items that can be borrowed right now.
    /// </summary>
    Task<IReadOnlyList<Equipment>> SearchAsync(
        string? searchText,
        bool availableOnly,
        CancellationToken cancellationToken = default);

    /// <summary>Is this identifier taken (trim + case-insensitive, same rule as the database)?</summary>
    Task<bool> EquipmentCodeExistsAsync(
        string equipmentCode,
        int? excludingEquipmentId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Equipment equipment, CancellationToken cancellationToken = default);

    Task UpdateAsync(Equipment equipment, CancellationToken cancellationToken = default);
}