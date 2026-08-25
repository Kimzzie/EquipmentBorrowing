using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class InMemoryEquipmentRepository : IEquipmentRepository
{
    private readonly List<Equipment> _equipment = new()
       {
           new Equipment(id: 1, name: "Digital Multimeter", isAvailable: true),
           new Equipment(id: 2, name: "Oscilloscope", isAvailable: false)
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
}