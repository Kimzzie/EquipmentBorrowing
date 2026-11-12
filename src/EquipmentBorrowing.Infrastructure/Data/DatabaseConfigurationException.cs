using EquipmentBorrowing.Application.Exceptions;

namespace EquipmentBorrowing.Infrastructure.Data;

/// <summary>
/// Connection settings are missing or malformed. Derives from DatabaseAccessException
/// so the ViewModel can catch it without referencing Infrastructure.
/// </summary>
public sealed class DatabaseConfigurationException : DatabaseAccessException
{
    public DatabaseConfigurationException(string message) : base(message)
    {
    }
}