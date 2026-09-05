using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

public class ReturnResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public Borrowing? Borrowing { get; }

    private ReturnResult(bool isSuccess, string? errorMessage, Borrowing? borrowing)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Borrowing = borrowing;
    }

    public static ReturnResult Success(Borrowing borrowing) =>
        new(true, null, borrowing);

    public static ReturnResult Fail(string errorMessage) =>
        new(false, errorMessage, null);
}


