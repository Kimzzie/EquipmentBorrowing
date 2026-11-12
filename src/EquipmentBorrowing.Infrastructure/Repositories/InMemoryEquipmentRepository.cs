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

    public Task UpdateAsync(Equipment equipment, CancellationToken cancellationToken = default)
    {
        // The shared reference already reflects the change.
        return Task.CompletedTask;
    }
}