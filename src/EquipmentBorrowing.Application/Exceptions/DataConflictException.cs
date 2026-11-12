namespace EquipmentBorrowing.Application.Exceptions;

public enum DataConflict
{
    DuplicateStudentNumber,
    DuplicateEquipmentCode,
    EquipmentAlreadyIssued,
    ReferenceMissing
}

/// <summary>
/// The database refused a write because it conflicts with existing data (a unique
/// index or a foreign key). Infrastructure raises it, so the Application layer
/// never has to know about PostgreSQL error codes. Known failure: nothing was saved.
/// </summary>
public class DataConflictException : Exception
{
    public DataConflict Kind { get; }

    public DataConflictException(DataConflict kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }
}