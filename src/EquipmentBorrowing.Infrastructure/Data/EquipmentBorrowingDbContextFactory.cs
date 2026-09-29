using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EquipmentBorrowing.Infrastructure.Data;

public class EquipmentBorrowingDbContextFactory : IDesignTimeDbContextFactory<EquipmentBorrowingDbContext>
{
    public EquipmentBorrowingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EquipmentBorrowingDbContext>();
        optionsBuilder.UseSqlite(DatabasePathProvider.GetConnectionString());

        return new EquipmentBorrowingDbContext(optionsBuilder.Options);
    }
}