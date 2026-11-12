using EquipmentBorrowing.Application.Exceptions;

namespace EquipmentBorrowing.Application.Services;

public enum OperationFailure
{
    None,
    Validation,      // staff can correct the input
    Duplicate,       // number/identifier already used
    NotFound,        // the record disappeared (stale selection)
    Conflict,        // another change got there first
    DatabaseError,   // known failure: nothing was saved
    UnknownOutcome   // BR-18: connection lost while saving
}

public sealed class OperationResult<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string Message { get; }
    public OperationFailure Failure { get; }

    private OperationResult(bool isSuccess, T? value, string message, OperationFailure failure)
    {
        IsSuccess = isSuccess;
        Value = value;
        Message = message;
        Failure = failure;
    }

    public static OperationResult<T> Success(T value, string message = "") =>
        new(true, value, message, OperationFailure.None);

    public static OperationResult<T> Fail(OperationFailure failure, string message) =>
        new(false, default, message, failure);

    /// <summary>Maps the exceptions Infrastructure raises into a result the UI can show.</summary>
    public static OperationResult<T> FromException(Exception exception) => exception switch
    {
        DataConflictException conflict => Fail(
            conflict.Kind is DataConflict.DuplicateStudentNumber or DataConflict.DuplicateEquipmentCode
                ? OperationFailure.Duplicate
                : OperationFailure.Conflict,
            conflict.Message),
        // Order matters: the "unknown outcome" type derives from DatabaseAccessException.
        CommitOutcomeUnknownException unknown => Fail(OperationFailure.UnknownOutcome, unknown.Message),
        DatabaseAccessException database => Fail(OperationFailure.DatabaseError, database.Message),
        _ => throw new ArgumentException("Not a database failure.", nameof(exception), exception)
    };
}