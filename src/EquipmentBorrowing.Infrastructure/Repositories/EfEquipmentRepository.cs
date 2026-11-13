using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class EfEquipmentRepository : IEquipmentRepository
{
    private readonly EquipmentBorrowingDbContext _context;

    public EfEquipmentRepository(EquipmentBorrowingDbContext context)
    {
        _context = context;
    }

    public Task<Equipment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        // Tracked on purpose: services mutate this instance (MarkAsBorrowed, UpdateDetails)
        // and the change tracker must see it when SaveChangesAsync runs.
        return DatabaseGuard.ReadAsync(() =>
            _context.Equipment.FirstOrDefaultAsync(e => e.Id == id, cancellationToken));
    }

    public Task<IReadOnlyList<Equipment>> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        return DatabaseGuard.ReadAsync<IReadOnlyList<Equipment>>(async () =>
            await _context.Equipment
                .AsNoTracking()
                .Where(e => e.IsAvailable)
                .OrderBy(e => e.Name).ThenBy(e => e.Id)
                .ToListAsync(cancellationToken));
    }

    public Task<IReadOnlyList<Equipment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return DatabaseGuard.ReadAsync<IReadOnlyList<Equipment>>(async () =>
            await _context.Equipment
                .AsNoTracking()
                .OrderBy(e => e.Name).ThenBy(e => e.Id)
                .ToListAsync(cancellationToken));
    }

    public Task<IReadOnlyList<Equipment>> SearchAsync(
        string? searchText,
        bool availableOnly,
        CancellationToken cancellationToken = default)
    {
        return DatabaseGuard.ReadAsync<IReadOnlyList<Equipment>>(async () =>
        {
            var query = _context.Equipment.AsNoTracking();

            if (availableOnly)
                query = query.Where(e => e.IsAvailable);

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var pattern = $"%{EscapeLike(searchText.Trim())}%";
                query = query.Where(e =>
                    EF.Functions.ILike(e.Name, pattern, "\\") ||
                    EF.Functions.ILike(e.EquipmentCode, pattern, "\\"));
            }

            return await query
                .OrderBy(e => e.Name).ThenBy(e => e.Id)   // Id = stable tie-breaker
                .ToListAsync(cancellationToken);
        });
    }

    public Task<bool> EquipmentCodeExistsAsync(
        string equipmentCode,
        int? excludingEquipmentId,
        CancellationToken cancellationToken = default)
    {
        // Same rule as the database: trimmed + lower-case, compared on the generated column.
        var key = equipmentCode.Trim().ToLowerInvariant();

        return DatabaseGuard.ReadAsync(() =>
            _context.Equipment
                .AsNoTracking()
                .AnyAsync(e =>
                    EF.Property<string>(e, "NormalizedEquipmentCode") == key &&
                    (excludingEquipmentId == null || e.Id != excludingEquipmentId),
                    cancellationToken));
    }

    public Task AddAsync(Equipment equipment, CancellationToken cancellationToken = default)
    {
        // Only marks the item as "to be inserted"; nothing is sent until SaveChangesAsync.
        _context.Equipment.Add(equipment);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Equipment equipment, CancellationToken cancellationToken = default)
    {
        // If this instance came from GetByIdAsync it is already tracked and dirty.
        // This only matters for an instance that came from a no-tracking query.
        if (_context.Entry(equipment).State == EntityState.Detached)
        {
            _context.Equipment.Update(equipment);
        }

        return Task.CompletedTask;
    }

    // "%" and "_" are LIKE wildcards. Escape them so typing "50%" searches for a literal "50%".
    private static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}