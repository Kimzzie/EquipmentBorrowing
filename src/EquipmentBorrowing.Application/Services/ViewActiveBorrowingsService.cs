using EquipmentBorrowing.Application.Dtos;
using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;

namespace EquipmentBorrowing.Application.Services;

/// <summary>UC-04. Exists so the ViewModel never talks to a repository directly.</summary>
public class ViewActiveBorrowingsService
{
    private readonly IBorrowingRepository _borrowings;

    public ViewActiveBorrowingsService(IBorrowingRepository borrowings)
    {
        _borrowings = borrowings;
    }

    public async Task<OperationResult<IReadOnlyList<ActiveBorrowingSummary>>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var active = await _borrowings.GetActiveWithDetailsAsync(cancellationToken);
            return OperationResult<IReadOnlyList<ActiveBorrowingSummary>>.Success(active);
        }
        catch (Exception ex) when (ex is DataConflictException or DatabaseAccessException)
        {
            return OperationResult<IReadOnlyList<ActiveBorrowingSummary>>.FromException(ex);
        }
    }
}