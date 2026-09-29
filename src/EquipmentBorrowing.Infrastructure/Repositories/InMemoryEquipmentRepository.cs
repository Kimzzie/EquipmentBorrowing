using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class InMemoryEquipmentRepository : IEquipmentRepository
{
    private readonly List<Equipment> _equipment = new()
       {
           new Equipment(id: 1, name: "Crimping Tool", type: "Networking Tool", isAvailable: true),
           new Equipment(id: 2, name: "LAN Tester", type: "Networking Tool", isAvailable: false),
           new Equipment(id: 3, name: "Aircon Remote", type: "Remote Control", isAvailable: true)
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
        // Same reasoning as InMemoryBorrowingRepository.UpdateAsync:
        // the shared reference already reflects the change.
        return Task.CompletedTask;
    }
}