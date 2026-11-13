using EquipmentBorrowing.Application.Dtos;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class EfBorrowingQueryRepository : IBorrowingQueryRepository
{
    private readonly EquipmentBorrowingDbContext _context;

    public EfBorrowingQueryRepository(EquipmentBorrowingDbContext context)
    {
        _context = context;
    }

    // Holds the three joined entities so we can filter and sort on them BEFORE projecting.
    private sealed class Joined
    {
        public Borrowing B { get; set; } = null!;
        public Student S { get; set; } = null!;
        public Equipment E { get; set; } = null!;
    }

    private IQueryable<Joined> JoinedRows() =>
        from b in _context.Borrowings.AsNoTracking()
        join s in _context.Students.AsNoTracking() on b.StudentId equals s.Id
        join e in _context.Equipment.AsNoTracking() on b.EquipmentId equals e.Id
        select new Joined { B = b, S = s, E = e };

    private static IQueryable<BorrowingRecord> ToRecords(IQueryable<Joined> query) =>
        query.Select(x => new BorrowingRecord(
            x.B.Id,
            x.S.Id,
            x.S.StudentNumber,
            x.S.Name,
            x.E.Id,
            x.E.EquipmentCode,
            x.E.Name,
            x.B.DateBorrowed,
            x.B.ExpectedReturnDate,
            x.B.DateReturned,
            x.B.Status));

    public Task<IReadOnlyList<BorrowingRecord>> GetByStudentAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        return DatabaseGuard.ReadAsync<IReadOnlyList<BorrowingRecord>>(async () =>
            await ToRecords(
                JoinedRows()
                    .Where(x => x.B.StudentId == studentId)
                    .OrderByDescending(x => x.B.DateBorrowed)
                    .ThenByDescending(x => x.B.Id))
                .ToListAsync(cancellationToken));
    }

    public Task<IReadOnlyList<BorrowingRecord>> GetByEquipmentAsync(
        int equipmentId, CancellationToken cancellationToken = default)
    {
        return DatabaseGuard.ReadAsync<IReadOnlyList<BorrowingRecord>>(async () =>
            await ToRecords(
                JoinedRows()
                    .Where(x => x.B.EquipmentId == equipmentId)
                    .OrderByDescending(x => x.B.DateBorrowed)
                    .ThenByDescending(x => x.B.Id))
                .ToListAsync(cancellationToken));
    }

    public Task<IReadOnlyList<BorrowingRecord>> SearchAsync(
        BorrowingQuery query, CancellationToken cancellationToken = default)
    {
        return DatabaseGuard.ReadAsync<IReadOnlyList<BorrowingRecord>>(async () =>
        {
            var rows = JoinedRows();

            // Text: partial, case-insensitive, over student name, student number, item name.
            if (!string.IsNullOrWhiteSpace(query.SearchText))
            {
                var pattern = LikePattern.Contains(query.SearchText);
                rows = rows.Where(x =>
                    EF.Functions.ILike(x.S.Name, pattern, LikePattern.EscapeCharacter) ||
                    EF.Functions.ILike(x.S.StudentNumber, pattern, LikePattern.EscapeCharacter) ||
                    EF.Functions.ILike(x.E.Name, pattern, LikePattern.EscapeCharacter));
            }

            if (query.Status is { } status)
                rows = rows.Where(x => x.B.Status == status);

            // Chosen field and direction first, Id as the tie-breaker so equal dates keep a stable order.
            IOrderedQueryable<Joined> ordered = query.SortBy == BorrowingSortField.ExpectedReturnDate
                ? (query.Descending
                    ? rows.OrderByDescending(x => x.B.ExpectedReturnDate).ThenByDescending(x => x.B.Id)
                    : rows.OrderBy(x => x.B.ExpectedReturnDate).ThenBy(x => x.B.Id))
                : (query.Descending
                    ? rows.OrderByDescending(x => x.B.DateBorrowed).ThenByDescending(x => x.B.Id)
                    : rows.OrderBy(x => x.B.DateBorrowed).ThenBy(x => x.B.Id));

            return await ToRecords(ordered).ToListAsync(cancellationToken);
        });
    }
}