using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(
        EquipmentBorrowingDbContext context,
        CancellationToken cancellationToken = default)
    {
        // Guard: only seed on a truly empty database.
        // Without this check, every app startup would insert duplicates.
        if (await context.Students.AnyAsync(cancellationToken))
        {
            return;
        }

        context.Students.AddRange(
            new Student(id: 0, studentNumber: "2023-0001", name: "Bryl Lim", isAllowedToBorrow: true),
            new Student(id: 0, studentNumber: "2023-0002", name: "Boss Rod", isAllowedToBorrow: false),
            new Student(id: 0, studentNumber: "2023-0003", name: "Kim Cabatingan", isAllowedToBorrow: true));

        context.Equipment.AddRange(
            new Equipment(id: 0, name: "Crimping Tool", type: "Networking Tool", isAvailable: true),
            new Equipment(id: 0, name: "LAN Tester", type: "Networking Tool", isAvailable: true),
            new Equipment(id: 0, name: "Aircon Remote", type: "Remote Control", isAvailable: true),
            new Equipment(id: 0, name: "Projector", type: "AV Equipment", isAvailable: true));

        await context.SaveChangesAsync(cancellationToken);
    }
}