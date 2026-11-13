namespace EquipmentBorrowing.Application.Interfaces;

public interface IUnitOfWork
{
    /// <summary>
    /// One-shot save: every pending change commits together, or none does.
    /// Used by the simple single-row operations (register, edit, eligibility).
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts an explicit transaction (Serializable). Everything read and written until
    /// it is committed is one unit of work, which is how checkout and return recheck
    /// the current data at the transaction boundary (BR-15). Disposing the returned
    /// object WITHOUT committing rolls everything back.
    /// </summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}