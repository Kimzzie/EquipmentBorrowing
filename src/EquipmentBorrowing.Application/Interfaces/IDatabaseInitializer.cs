namespace EquipmentBorrowing.Application.Interfaces;

/// <summary>
/// "Make sure the database is ready to use." The ViewModel calls this port;
/// how it is done (migrations, seeding) is an Infrastructure detail.
/// </summary>
public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}