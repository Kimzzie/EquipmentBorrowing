using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

public class BorrowResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public Borrowing? Borrowing { get; }

    private BorrowResult(bool isSuccess, string? errorMessage, Borrowing? borrowing)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Borrowing = borrowing;
    }

    public static BorrowResult Success(Borrowing borrowing) =>
        new(true, null, borrowing);

    public static BorrowResult Fail(string errorMessage) =>
        new(false, errorMessage, null);
}