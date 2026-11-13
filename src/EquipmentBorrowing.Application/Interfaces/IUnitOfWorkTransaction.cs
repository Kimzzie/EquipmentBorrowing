namespace EquipmentBorrowing.Application.Interfaces;

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    /// <summary>Sends the pending changes to the database inside the transaction. Nothing is final yet.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Makes everything final. If this throws CommitOutcomeUnknownException, nobody knows whether it was committed.</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);
}