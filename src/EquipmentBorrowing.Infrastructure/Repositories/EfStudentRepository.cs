using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class EfStudentRepository : IStudentRepository
{
    private readonly EquipmentBorrowingDbContext _context;

    public EfStudentRepository(EquipmentBorrowingDbContext context)
    {
        _context = context;
    }

    public Task<Student?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        // Tracked on purpose: update/eligibility services change this instance and
        // SaveChanges writes the difference.
        return DatabaseGuard.ReadAsync(() =>
            _context.Students.FirstOrDefaultAsync(s => s.Id == id, cancellationToken));
    }

    public Task<IReadOnlyList<Student>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return DatabaseGuard.ReadAsync<IReadOnlyList<Student>>(async () =>
            await _context.Students
                .AsNoTracking()
                .OrderBy(s => s.Name).ThenBy(s => s.Id)
                .ToListAsync(cancellationToken));
    }

    public Task<IReadOnlyList<Student>> SearchAsync(string? searchText, CancellationToken cancellationToken = default)
    {
        return DatabaseGuard.ReadAsync<IReadOnlyList<Student>>(async () =>
        {
            var query = _context.Students.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var pattern = $"%{EscapeLike(searchText.Trim())}%";
                query = query.Where(s =>
                    EF.Functions.ILike(s.Name, pattern, "\\") ||
                    EF.Functions.ILike(s.StudentNumber, pattern, "\\"));
            }

            return await query
                .OrderBy(s => s.Name).ThenBy(s => s.Id)   // Id = stable tie-breaker
                .ToListAsync(cancellationToken);
        });
    }

    public Task<bool> StudentNumberExistsAsync(
        string studentNumber,
        int? excludingStudentId,
        CancellationToken cancellationToken = default)
    {
        // Same rule as the database: trimmed + lower-case, compared on the generated column.
        var key = studentNumber.Trim().ToLowerInvariant();

        return DatabaseGuard.ReadAsync(() =>
            _context.Students
                .AsNoTracking()
                .AnyAsync(s =>
                    EF.Property<string>(s, "NormalizedStudentNumber") == key &&
                    (excludingStudentId == null || s.Id != excludingStudentId),
                    cancellationToken));
    }

    public Task AddAsync(Student student, CancellationToken cancellationToken = default)
    {
        // Only marks the student as "to be inserted"; nothing is sent until
        // IUnitOfWork.SaveChangesAsync, so the insert commits as one unit.
        _context.Students.Add(student);
        return Task.CompletedTask;
    }

    // "%" and "_" are wildcards in LIKE. Escape them so typing "50%" searches for a
    // literal "50%" instead of matching everything.
    private static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}