namespace EquipmentBorrowing.Application.Exceptions;

/// <summary>
/// The database could not be used (not reachable, not configured, rejected login...).
/// The message is ALWAYS safe to show to staff: it never contains the connection
/// string, password or server internals (BR-18). The original exception is kept
/// in InnerException for debugging.
/// </summary>
public class DatabaseAccessException : Exception
{
    public DatabaseAccessException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}