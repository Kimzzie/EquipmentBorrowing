namespace EquipmentBorrowing.Application.Exceptions;

/// <summary>
/// The connection was lost while saving, so nobody can say whether the change was
/// committed (BR-18). Callers must NOT report success, NOT claim a rollback and
/// NOT retry automatically; staff re-check the data first.
/// </summary>
public class CommitOutcomeUnknownException : DatabaseAccessException
{
    public CommitOutcomeUnknownException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}