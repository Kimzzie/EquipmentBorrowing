using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Data;

public sealed class DatabaseInitializer : IDatabaseInitializer
{
    private readonly EquipmentBorrowingDbContext _context;

    public DatabaseInitializer(EquipmentBorrowingDbContext context)
    {
        _context = context;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Applies only migrations the database does not have yet. On an up-to-date
            // database this does nothing.
            await _context.Database.MigrateAsync(cancellationToken);
            await DbSeeder.SeedAsync(_context, cancellationToken);
        }
        catch (DatabaseAccessException)
        {
            throw; // already safe to display
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new DatabaseAccessException(DatabaseErrorTranslator.ToUserMessage(ex), ex);
        }
    }
}