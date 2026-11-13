using EquipmentBorrowing.Application.Dtos;
using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;

namespace EquipmentBorrowing.Application.Services;

public class SearchBorrowingsService
{
    public const int MaxSearchTextLength = 100;

    private readonly IBorrowingQueryRepository _queries;

    public SearchBorrowingsService(IBorrowingQueryRepository queries)
    {
        _queries = queries;
    }

    public async Task<OperationResult<IReadOnlyList<BorrowingRecord>>> ExecuteAsync(
        BorrowingQuery query, CancellationToken cancellationToken = default)
    {
        // Blank search text means "no text filter", not "match nothing".
        var text = string.IsNullOrWhiteSpace(query.SearchText) ? null : query.SearchText.Trim();

        if (text is not null && text.Length > MaxSearchTextLength)
            return OperationResult<IReadOnlyList<BorrowingRecord>>.Fail(
                OperationFailure.Validation,
                $"Search text can be at most {MaxSearchTextLength} characters.");

        try
        {
            var records = await _queries.SearchAsync(query with { SearchText = text }, cancellationToken);
            return OperationResult<IReadOnlyList<BorrowingRecord>>.Success(records);
        }
        catch (Exception ex) when (ex is DataConflictException or DatabaseAccessException)
        {
            return OperationResult<IReadOnlyList<BorrowingRecord>>.FromException(ex);
        }
    }
}