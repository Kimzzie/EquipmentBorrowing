using EquipmentBorrowing.Application.Dtos;
using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;

namespace EquipmentBorrowing.Application.Services;

/// <summary>Loads one borrowing with its student and item already joined.</summary>
public class GetBorrowingService
{
    private readonly IBorrowingQueryRepository _queries;

    public GetBorrowingService(IBorrowingQueryRepository queries)
    {
        _queries = queries;
    }

    public async Task<OperationResult<BorrowingRecord>> ExecuteAsync(
        int borrowingId, CancellationToken cancellationToken = default)
    {
        try
        {
            var record = await _queries.GetByIdAsync(borrowingId, cancellationToken);

            return record is null
                ? OperationResult<BorrowingRecord>.Fail(OperationFailure.NotFound, "No borrowing with this ID exists.")
                : OperationResult<BorrowingRecord>.Success(record);
        }
        catch (Exception ex) when (ex is DataConflictException or DatabaseAccessException)
        {
            return OperationResult<BorrowingRecord>.FromException(ex);
        }
    }
}
