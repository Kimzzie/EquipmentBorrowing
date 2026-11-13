using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class InMemoryEquipmentRepository : IEquipmentRepository
{
    private readonly List<Equipment> _equipment = new()
    {
        new Equipment(id: 1, equipmentCode: "EQ-001", name: "Crimping Tool", description: "Networking Tool"),
        new Equipment(id: 2, equipmentCode: "EQ-002", name: "LAN Tester", description: "Networking Tool", isAvailable: false),
        new Equipment(id: 3, equipmentCode: "EQ-003", name: "Aircon Remote", description: "Remote Control")
    };

    public Task<Equipment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var equipment = _equipment.FirstOrDefault(e => e.Id == id);
        return Task.FromResult(equipment);
    }

    public Task<IReadOnlyList<Equipment>> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Equipment> available = _equipment.Where(e => e.IsAvailable).ToList();
        return Task.FromResult(available);
    }

    public Task<IReadOnlyList<Equipment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Equipment> all = _equipment.ToList();
        return Task.FromResult(all);
    }

    public Task<IReadOnlyList<Equipment>> SearchAsync(
        string? searchText, bool availableOnly, CancellationToken cancellationToken = default)
    {
        var text = searchText?.Trim();
        IReadOnlyList<Equipment> found = _equipment
            .Where(e => !availableOnly || e.IsAvailable)
            .Where(e => string.IsNullOrEmpty(text) ||
                        e.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                        e.EquipmentCode.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult(found);
    }

    public Task<bool> EquipmentCodeExistsAsync(
        string equipmentCode, int? excludingEquipmentId, CancellationToken cancellationToken = default)
    {
        var exists = _equipment.Any(e =>
            e.Id != excludingEquipmentId &&
            string.Equals(e.EquipmentCode, equipmentCode.Trim(), StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public Task AddAsync(Equipment equipment, CancellationToken cancellationToken = default)
    {
        // Equipment.Id has a private setter (the database assigns it), so this demo
        // repository cannot create real items. Registration needs the PostgreSQL one.
        throw new NotSupportedException("The in-memory repository only holds fixed demo data.");
    }

    public Task UpdateAsync(Equipment equipment, CancellationToken cancellationToken = default)
    {
        // The shared reference already reflects the change.
        return Task.CompletedTask;
    }
}